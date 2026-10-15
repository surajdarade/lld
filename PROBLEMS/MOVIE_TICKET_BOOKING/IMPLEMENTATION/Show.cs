namespace MovieTicketBooking;

public sealed class Show
{
    private readonly Dictionary<string, SeatStatus> _seatStatuses;

    public Show(
        string id,
        Movie movie,
        Theater theater,
        string screenId,
        DateTimeOffset startTime)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Show ID cannot be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(movie);
        ArgumentNullException.ThrowIfNull(theater);

        Id = id;
        Movie = movie;
        Theater = theater;
        Screen = theater.GetScreen(screenId);
        StartTime = startTime;
        _seatStatuses = Screen.Seats.Keys.ToDictionary(
            seatId => seatId,
            _ => SeatStatus.AVAILABLE,
            StringComparer.Ordinal);
    }

    public string Id { get; }

    public Movie Movie { get; }

    public Theater Theater { get; }

    public Screen Screen { get; }

    public DateTimeOffset StartTime { get; }

    internal List<string> GetAvailableSeatsUnsafe()
    {
        return _seatStatuses
            .Where(pair => pair.Value == SeatStatus.AVAILABLE)
            .Select(pair => pair.Key)
            .ToList();
    }

    internal bool ContainsSeat(string seatId)
    {
        return _seatStatuses.ContainsKey(seatId);
    }

    internal void ValidateSeatsAvailable(IReadOnlyCollection<string> seatIds)
    {
        foreach (string seatId in seatIds)
        {
            if (!_seatStatuses.TryGetValue(seatId, out SeatStatus status))
            {
                throw new ArgumentException($"Seat '{seatId}' does not belong to this show.", nameof(seatIds));
            }

            if (status != SeatStatus.AVAILABLE)
            {
                throw new InvalidOperationException($"Seat '{seatId}' is unavailable.");
            }
        }
    }

    internal void BookSeatsUnsafe(IReadOnlyCollection<string> seatIds)
    {
        // Caller must validate the complete selection and hold the coordinator lock.
        foreach (string seatId in seatIds)
        {
            _seatStatuses[seatId] = SeatStatus.BOOKED;
        }
    }

    internal void ValidateSeatsBooked(IReadOnlyCollection<string> seatIds)
    {
        foreach (string seatId in seatIds)
        {
            if (!_seatStatuses.TryGetValue(seatId, out SeatStatus status) || status != SeatStatus.BOOKED)
            {
                throw new InvalidOperationException($"Seat '{seatId}' is not booked.");
            }
        }
    }

    internal void ReleaseSeatsUnsafe(IReadOnlyCollection<string> seatIds)
    {
        // Validate every seat before changing any state.
        ValidateSeatsBooked(seatIds);

        foreach (string seatId in seatIds)
        {
            _seatStatuses[seatId] = SeatStatus.AVAILABLE;
        }
    }
}
