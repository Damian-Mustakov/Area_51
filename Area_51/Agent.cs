using System;
using Area_51.Enums;
using Area_51.Exceptions;

namespace Area_51;

public class Agent
{
    private readonly Elevator _elevator;
    private readonly Random _random;
    private string _currFloorLevel;
    public AccessLevelEnum AccessLevel { get; }
    public string AgentName { get; set; }
    public Agent(string agentName,Elevator elevator, Random random, AccessLevelEnum accessLevel )
    {
        _elevator = elevator;
        _random = random;
        _currFloorLevel = _elevator.Floors[0];
        AccessLevel = accessLevel;
        AgentName = agentName;
    }

    public async void GoAroundTheBase()
    {
        Task currentTask = null!;

        while (true)
        {
            if (currentTask != null)
            {
                try
                {
                    await currentTask;
                    currentTask = null!;
                }
                //If this exception is thrown, the agent will be removed from the elevator 
                catch (MissingRequiredAccessLevelException e)
                {
                    Console.WriteLine($"{this} was not allowed to leave the elevator because of missing required access level.");
                    Console.WriteLine($"{this} waits to get back to the floor they got on the elevator.");

                    currentTask = e.LeaveElevator.ContinueWith(_ => Console.WriteLine($"{this} left the elevator."));
                }
                catch (ElevatorClosedException)
                {
                    Console.WriteLine($"{this} was kicked out of the elevator, because it was closed.");
                    return;
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine($"{this} could not finish their job, as the base is closed.");
                    return;
                }
                
            }
            else
            {
                var nextTaskChance = _random.Next(5);
                if (nextTaskChance < 3)
                {
                    currentTask = RunAroundTheBase();
                }
                else
                {
                    currentTask = CallElevator();
                }
            }
        }
    }

    private Task RunAroundTheBase()
    {
        Console.WriteLine($"{this} is going around and doing their job in the base.");
        return Task.Delay(2000);
    }
    private string ChooseFloor()
    {
        Console.WriteLine($"{this} gets in the elevator.");

        // Choose which floor the agent wants to go to.
        string nextFloor;
        do
        {
            nextFloor = _elevator.Floors[_random.Next(_elevator.Floors.Length)];
        } while (nextFloor == _currFloorLevel);

        Console.WriteLine($"{this} presses the button for floor {nextFloor}.");
        return nextFloor;
    }
    private async Task CallElevator()
    {
        Console.WriteLine($"{this} calls the elevator.");

        string nextFloor = await _elevator.Call(_currFloorLevel, this, ChooseFloor);
        Console.WriteLine($"{this} reached floor {nextFloor} successfully.");
        _currFloorLevel = nextFloor;
    }


    public override string ToString()
    {
        return $"Agent {AgentName}";
    }
}