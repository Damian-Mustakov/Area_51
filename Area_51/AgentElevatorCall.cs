using Area_51.Interfaces;

namespace Area_51;

public class AgentElevatorCall:IElevatorCall
{
    private string _destinationFloor;
    private readonly Func<string> _chooseFloorFunc;
    public AgentElevatorCall(string floor, Agent agent, Func<string> chooseFloorFunc)
    {
        InitialFloor = floor;
        Agent = agent;
        _chooseFloorFunc = chooseFloorFunc;
        TaskCompletionSource = new TaskCompletionSource<string>();
    }

    public string InitialFloor { get; }
    public string Floor => InitialFloor;
    public Agent Agent { get; }

    public string DestinationFloor
    {
        get
        {
            if (_destinationFloor == null)
            {
                _destinationFloor = _chooseFloorFunc();
            }

            return _destinationFloor;
        }
    }
    public TaskCompletionSource<string> TaskCompletionSource { get; set; }
}