namespace ParkingLotSystem;

public sealed class ParkingLot
{
    private readonly List<ParkingSpot> _parkingSpots;
    private readonly Dictionary<string, ParkingTicket> _tickets;
    private readonly IClock _clock;
    private readonly ParkingFeeCalculator _feeCalculator;
    private readonly decimal _hourlyRate;
    private readonly object _syncLock = new();

    public ParkingLot(
        IEnumerable<ParkingSpot> parkingSpots,
        decimal hourlyRate,
        IClock clock,
        ParkingFeeCalculator? feeCalculator = null)
    {
        ArgumentNullException.ThrowIfNull(parkingSpots);
        ArgumentNullException.ThrowIfNull(clock);

        if (hourlyRate < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hourlyRate),
                "Hourly rate cannot be negative.");
        }

        _parkingSpots = parkingSpots.ToList();

        if (_parkingSpots.Any(spot => spot is null))
        {
            throw new ArgumentException("Spot collection cannot contain null.", nameof(parkingSpots));
        }

        if (_parkingSpots
            .Select(spot => spot.Id)
            .Distinct(StringComparer.Ordinal)
            .Count() != _parkingSpots.Count)
        {
            throw new ArgumentException("Spot IDs must be unique.", nameof(parkingSpots));
        }

        _tickets = new Dictionary<string, ParkingTicket>(StringComparer.Ordinal);
        _clock = clock;
        _feeCalculator = feeCalculator ?? new ParkingFeeCalculator();
        _hourlyRate = hourlyRate;
    }

    public ParkingTicket EnterVehicle(Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        lock (_syncLock)
        {
            ParkingSpot? spot = FindAvailableSpot(vehicle.Type);

            if (spot is null)
            {
                throw new InvalidOperationException("No compatible parking spot is available.");
            }

            DateTimeOffset entryTimeUtc = _clock.UtcNow.ToUniversalTime();
            ParkingTicket ticket = new(
                GenerateTicketId(),
                vehicle,
                spot,
                entryTimeUtc);

            spot.Occupy(vehicle.Type);

            try
            {
                _tickets.Add(ticket.Id, ticket);
            }
            catch
            {
                spot.Release();
                throw;
            }

            return ticket;
        }
    }

    public decimal ExitVehicle(string ticketId)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            throw new ArgumentException("Ticket ID cannot be empty.", nameof(ticketId));
        }

        lock (_syncLock)
        {
            if (!_tickets.TryGetValue(ticketId, out ParkingTicket? ticket))
            {
                throw new ArgumentException("Unknown ticket ID.", nameof(ticketId));
            }

            if (ticket.Status != TicketStatus.ACTIVE)
            {
                throw new InvalidOperationException("Ticket has already been used.");
            }

            DateTimeOffset exitTimeUtc = _clock.UtcNow.ToUniversalTime();
            decimal fee = _feeCalculator.CalculateFee(
                ticket.EntryTimeUtc,
                exitTimeUtc,
                _hourlyRate);

            if (!ticket.AssignedSpot.IsOccupied)
            {
                throw new InvalidOperationException(
                    "Ticket and parking spot state are inconsistent.");
            }

            ticket.MarkCompleted();
            ticket.AssignedSpot.Release();

            return fee;
        }
    }

    private ParkingSpot? FindAvailableSpot(VehicleType vehicleType)
    {
        foreach (ParkingSpot spot in _parkingSpots)
        {
            if (spot.IsAvailable && spot.CanFit(vehicleType))
            {
                return spot;
            }
        }

        return null;
    }

    private string GenerateTicketId()
    {
        string ticketId;

        do
        {
            ticketId = Guid.NewGuid().ToString("N");
        }
        while (_tickets.ContainsKey(ticketId));

        return ticketId;
    }
}
