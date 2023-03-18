using Area_51.Interfaces;

namespace Area_51;

public class Button : IElevatorCall
{
    public Button(string floor)
    {
        Floor = floor;
    }

    public string Floor { get; }
}