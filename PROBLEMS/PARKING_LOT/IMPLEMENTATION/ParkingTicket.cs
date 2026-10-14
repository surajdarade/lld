using System.Threading;

namespace ParkingLotSystem;

public sealed class ParkingTicket
{
    private int _status;

    internal ParkingTicket(
        string id,
        Vehicle vehicle,
        ParkingSpot assignedSpot,
        DateTimeOffset entryTimeUtc)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Ticket ID cannot be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(assignedSpot);

        if (entryTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Entry time must be UTC.", nameof(entryTimeUtc));
        }

        Id = id;
        Vehicle = vehicle;
        AssignedSpot = assignedSpot;
        EntryTimeUtc = entryTimeUtc;
        _status = (int)TicketStatus.Active;
    }

    public string Id { get; }

    public Vehicle Vehicle { get; }

    public string SpotId => AssignedSpot.Id;

    public DateTimeOffset EntryTimeUtc { get; }

    public TicketStatus Status => (TicketStatus)Volatile.Read(ref _status);

    internal ParkingSpot AssignedSpot { get; }

    internal void MarkCompleted()
    {
        int previous = Interlocked.CompareExchange(
            ref _status,
            (int)TicketStatus.COMPLETED,
            (int)TicketStatus.ACTIVE);

        if (previous != (int)TicketStatus.ACTIVE)
        {
            throw new InvalidOperationException($"Ticket '{Id}' has already been completed.");
        }
    }
}