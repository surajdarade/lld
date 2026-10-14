namespace ParkingLotSystem;

public sealed class Vehicle
{
    public Vehicle(string id, VehicleType type)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Vehicle ID cannot be empty.", nameof(id));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Unsupported vehicle type.");
        }

        Id = id;
        Type = type;
    }

    public string Id { get; }

    public VehicleType Type { get; }
}
