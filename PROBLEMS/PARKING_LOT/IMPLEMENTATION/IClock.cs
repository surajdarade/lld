namespace ParkingLotSystem;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
