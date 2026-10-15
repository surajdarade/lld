using System.Threading;

namespace MovieTicketBooking;

public sealed class Reservation
{
    private readonly string[] _seatIds;
    private int _status;

    internal Reservation(string confirmationId, Show show, IEnumerable<string> seatIds)
    {
        if (string.IsNullOrWhiteSpace(confirmationId))
        {
            throw new ArgumentException("Confirmation ID cannot be empty.", nameof(confirmationId));
        }

        ArgumentNullException.ThrowIfNull(show);
        ArgumentNullException.ThrowIfNull(seatIds);

        _seatIds = seatIds.ToArray();
        if (_seatIds.Length == 0)
        {
            throw new ArgumentException("A reservation must contain at least one seat.", nameof(seatIds));
        }

        ConfirmationId = confirmationId;
        Show = show;
        _status = (int)ReservationStatus.CONFIRMED;
    }

    public string ConfirmationId { get; }

    public Show Show { get; }

    public IReadOnlyList<string> SeatIds => Array.AsReadOnly(_seatIds);

    public ReservationStatus Status => (ReservationStatus)Volatile.Read(ref _status);

    internal void MarkCancelled()
    {
        int previous = Interlocked.CompareExchange(
            ref _status,
            (int)ReservationStatus.CANCELLED,
            (int)ReservationStatus.CONFIRMED);

        if (previous != (int)ReservationStatus.CONFIRMED)
        {
            throw new InvalidOperationException("Reservation has already been cancelled.");
        }
    }
}
