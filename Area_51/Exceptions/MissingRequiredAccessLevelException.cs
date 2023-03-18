namespace Area_51.Exceptions;

public class MissingRequiredAccessLevelException : Exception
{
    public Task LeaveElevator { get; }
    public MissingRequiredAccessLevelException(Task leaveElevatorTask)
    {
        LeaveElevator = leaveElevatorTask;
    }
}