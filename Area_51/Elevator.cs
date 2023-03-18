using Area_51.Enums;
using Area_51.Exceptions;
using Area_51.Interfaces;

namespace Area_51;

public class Elevator
{
    private const string GROUND_FLOOR = "G";
    private const string SECRET_NUCLEAR_FLOOR = "S";
    private const string SECRET_EXPERIMENTAL_FLOOR = "T1";
    private const string SECRET_ALIEN_FLOOR = "T2";

    private const int TIME_ON_FLOOR = 1000;
    private int _currFloorIndex;
    private ElevatorStateEnum _state;
    private object _queueLock;
    private object _stateLock;

    private LinkedList<IElevatorCall?> _elevatorCallsQueue;
    private List<AgentElevatorCall> _agentsBeingProcessed;
    private CancellationTokenSource _cancellationTokenSource;
    public string[] Floors { get; }
    public CancellationToken CancellationToken => _cancellationTokenSource.Token;
    public Elevator()
    {
        this._currFloorIndex = 0;
        this._state = ElevatorStateEnum.Closed;
        this._queueLock = new object();
        this._stateLock = new object();
        this._elevatorCallsQueue = new LinkedList<IElevatorCall?>();
        this._agentsBeingProcessed = new List<AgentElevatorCall>();
        this._cancellationTokenSource = new CancellationTokenSource();
        Floors = new[]
        {
            GROUND_FLOOR,
            SECRET_NUCLEAR_FLOOR,
            SECRET_EXPERIMENTAL_FLOOR,
            SECRET_ALIEN_FLOOR
        };
    }
    private void SetState(ElevatorStateEnum toState)
    {
        lock (_stateLock)
        {
            _state = toState;
        }
    }
    private void SetState(ElevatorStateEnum toState, ElevatorStateEnum fromState)
    {
        lock (_stateLock)
        {
            if (_state == fromState)
            {
                _state = toState;
            }
        }
    }
    public Task<string> Call(string floor, Agent agent, Func<string> chooseFloorFunc)
    {
        var elevatorCall = new AgentElevatorCall(floor, agent, chooseFloorFunc);
        lock (_queueLock)
        {
            _elevatorCallsQueue.AddLast(elevatorCall);
        }
        return elevatorCall.TaskCompletionSource.Task;
    }
    public async void Start()
    {
        SetState(ElevatorStateEnum.Waiting);

        while (_state != ElevatorStateEnum.Closed)
        {
            IElevatorCall? currentCall = null;
            lock (_queueLock)
            {
                if (_elevatorCallsQueue.Count > 0)
                {
                    currentCall = _elevatorCallsQueue.First?.Value;
                    _elevatorCallsQueue.RemoveFirst();
                }
            }

            try
            {
                if (currentCall != null)
                {
                    await HandleCall(currentCall);
                }

                await Task.Delay(1, CancellationToken);
            }
            catch (TaskCanceledException) { }
        }


        var index = 0;
        while (index < _agentsBeingProcessed.Count)
        {
            var call = _agentsBeingProcessed[index];
            ExitAgentFromElevator(call.Agent);
            // TrySet in case an agent call has thrown an error once and it has already been handled.
            call.TaskCompletionSource.TrySetException(new ElevatorClosedException());
        }

        //Clear all unfinished elevator calls
        lock (_queueLock)
        {
            _elevatorCallsQueue.Clear();
        }
    }
    public void Stop()
    {
        SetState(ElevatorStateEnum.Closed);
        _cancellationTokenSource.Cancel();
    }

    private string EnterAgentIntoElevator(AgentElevatorCall call)
    {
        _agentsBeingProcessed.Add(call);

        var chosenFloor = call.DestinationFloor;
        return chosenFloor;
    }
    //Here we remove the agent from the others in the elevator
    private void ExitAgentFromElevator(Agent agent)
    {
        var index = _agentsBeingProcessed.FindIndex(aec => aec.Agent == agent);
        _agentsBeingProcessed.RemoveAt(index);
    }

    private bool HasRequiredAccessLevel(Agent agent, string floor)
    {
        switch (agent.AccessLevel)
        {
            case AccessLevelEnum.Confidential:
                return floor == GROUND_FLOOR;
            case AccessLevelEnum.Secret:
                return floor is GROUND_FLOOR or SECRET_NUCLEAR_FLOOR;
            case AccessLevelEnum.TopSecret:
                return true;
            default:
                throw new InvalidOperationException(
                    $"Unexpected confidentiality level: {agent.AccessLevel}");
        }
    }
    private async Task GoTo(string floor)
    {
        //We get the position of the requested floor
        int nextFloorIndex = Array.IndexOf(Floors, floor);

        //After that we calculate the distance between floors
        int distance = Math.Abs(_currFloorIndex - nextFloorIndex);

        if (distance == 0)
        {
            // We are already at the requested floor.
            return;
        }

        Console.WriteLine($"The elevator is traveling to floor {floor}.");
        SetState(ElevatorStateEnum.Moving, ElevatorStateEnum.Waiting);
        for (int i = 0; i < distance; i++)
        {
            await Task.Delay(TIME_ON_FLOOR, CancellationToken);
            if (_currFloorIndex < nextFloorIndex)
            {
                _currFloorIndex++;
            }
            else
            {
                _currFloorIndex--;
            }
        }

        SetState(ElevatorStateEnum.Waiting, ElevatorStateEnum.Moving);
    }

    private async Task HandleCall(IElevatorCall call)
    {
        var nextFloor = call.Floor;
        // We go to the next requested floor.
        await GoTo(nextFloor);

        //Here we check if the agents have the required access level
        foreach (var caller in _agentsBeingProcessed)
        {
            if (!HasRequiredAccessLevel(caller.Agent, nextFloor))
            {
                // There is an agent with insufficient access for this floor.
                // We have to drop them off back at their initial floor.

                var getOffElevatorCompletionSource = new TaskCompletionSource<string>();
                var exception = new MissingRequiredAccessLevelException(getOffElevatorCompletionSource.Task);
                caller.TaskCompletionSource.SetException(exception);

                // Despite modifying the collection in the loop, we can get away
                // with it because we return before any more iterations can occur.

                // Set the caller's new completion source, in case the elevator gets closed
                // while returning the agent to their initial floor.

                caller.TaskCompletionSource = getOffElevatorCompletionSource;
                await GoTo(caller.InitialFloor);
                ExitAgentFromElevator(caller.Agent);
                getOffElevatorCompletionSource.SetResult(Floors[_currFloorIndex]);

                var retryFloor = new Button(caller.DestinationFloor);
                lock (_queueLock)
                {
                    _elevatorCallsQueue.AddFirst(retryFloor);
                }

                return;
            }
        }

        Console.WriteLine($"The elevator arrived at floor {nextFloor}.");
        // Here all agents have sufficient levels of access.
        // We check if any of them want to get off here.
        var index = 0;
        while (index < _agentsBeingProcessed.Count)
        {
            var caller = _agentsBeingProcessed[index];
            if (nextFloor == caller.DestinationFloor)
            {
                ExitAgentFromElevator(caller.Agent);
                caller.TaskCompletionSource.SetResult(nextFloor);
            }
            else
            {
                index++;
            }
        }

        var pressedButtons = new HashSet<string>();
        string pressedButton;
        if (call is AgentElevatorCall agentCall)
        {
            pressedButton = EnterAgentIntoElevator(agentCall);
            pressedButtons.Add(pressedButton);
        }

        // We check if there are other callers waiting on floor we are currently at.
        lock (_queueLock)
        {
            var currentNode = _elevatorCallsQueue.First;
            while (currentNode != null)
            {
                if (currentNode.Value is AgentElevatorCall caller && currentNode.Value.Floor == nextFloor)
                {
                    // We have found a matching call. Remove the caller from the queue
                    // and put them in the elevator. That is why we use linked list instead of the structure queue.
                    // Because we can remove a node from the middle of the queue not only from the end or the beginning

                    pressedButton = EnterAgentIntoElevator(caller);
                    pressedButtons.Add(pressedButton);

                    var next = currentNode.Next;
                    // Remove the caller from the task queue, as they already have been dealt with.
                    _elevatorCallsQueue.Remove(currentNode);
                    currentNode = next;
                }
                else
                {
                    currentNode = currentNode.Next;
                }
            }
        }

        // All agents are handled on the current floor, and we now press their buttons.
        foreach (var button in pressedButtons)
        {
            var newCall = new Button(button);
            lock (_queueLock)
            {
                _elevatorCallsQueue.AddLast(newCall);
            }
        }

        Console.WriteLine("Elevator doors are closing.");
    }


}