namespace ParkingLotSystem;

public sealed class ParkingSpot
{
    private bool _isOccupied;

    public ParkingSpot(string id, SpotSize size)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Spot ID cannot be empty.", nameof(id));
        }

        if (!Enum.IsDefined(size))
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Unsupported spot size.");
        }

        Id = id;
        Size = size;
    }

    public string Id { get; }

    public SpotSize Size { get; }

    public bool IsAvailable => !_isOccupied;

    internal bool IsOccupied => _isOccupied;

    public bool CanFit(VehicleType vehicleType)
    {
        return vehicleType switch
        {
            VehicleType.MOTORCYCLE => Size is SpotSize.MOTORCYCLE or SpotSize.CAR or SpotSize.LARGE,
            VehicleType.CAR => Size is SpotSize.CAR or SpotSize.LARGE,
            VehicleType.LARGE_VEHICLE => Size == SpotSize.LARGE,
            _ => false
        };
    }

    internal void Occupy(VehicleType vehicleType)
    {
        if (_isOccupied)
        {
            throw new InvalidOperationException($"Spot '{Id}' is already occupied.");
        }

        if (!CanFit(vehicleType))
        {
            throw new InvalidOperationException(
                $"Spot '{Id}' cannot accommodate vehicle type '{vehicleType}'.");
        }

        _isOccupied = true;
    }

    internal void Release()
    {
        if (!_isOccupied)
        {
            throw new InvalidOperationException($"Spot '{Id}' is already available.");
        }

        _isOccupied = false;
    }
}
