namespace MovieTicketBooking;

public sealed class MovieTicketBookingSystem
{
    private readonly Dictionary<string, Movie> _movies;
    private readonly Dictionary<string, Theater> _theaters;
    private readonly Dictionary<string, Show> _shows;
    private readonly Dictionary<string, Reservation> _reservations;
    private readonly object _syncLock = new();

    public MovieTicketBookingSystem(
        IEnumerable<Movie> movies,
        IEnumerable<Theater> theaters,
        IEnumerable<Show> shows)
    {
        ArgumentNullException.ThrowIfNull(movies);
        ArgumentNullException.ThrowIfNull(theaters);
        ArgumentNullException.ThrowIfNull(shows);

        _movies = ToUniqueDictionary(movies, movie => movie.Id, nameof(movies));
        _theaters = ToUniqueDictionary(theaters, theater => theater.Id, nameof(theaters));
        _shows = ToUniqueDictionary(shows, show => show.Id, nameof(shows));
        _reservations = new Dictionary<string, Reservation>(StringComparer.Ordinal);

        foreach (Show show in _shows.Values)
        {
            if (!_movies.TryGetValue(show.Movie.Id, out Movie? registeredMovie) ||
                !ReferenceEquals(registeredMovie, show.Movie))
            {
                throw new ArgumentException($"Show '{show.Id}' references a movie not registered in the system.", nameof(shows));
            }

            if (!_theaters.TryGetValue(show.Theater.Id, out Theater? registeredTheater) ||
                !ReferenceEquals(registeredTheater, show.Theater))
            {
                throw new ArgumentException($"Show '{show.Id}' references a theater not registered in the system.", nameof(shows));
            }
        }
    }

    public List<Movie> SearchMovies(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Movie title is required.", nameof(title));
        }

        lock (_syncLock)
        {
            return _movies.Values
                .Where(movie => movie.Title.Contains(title, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public List<Movie> GetMoviesAtTheater(string theaterId)
    {
        lock (_syncLock)
        {
            _ = GetTheaterOrThrow(theaterId);

            return _shows.Values
                .Where(show => string.Equals(show.Theater.Id, theaterId, StringComparison.Ordinal))
                .Select(show => show.Movie)
                .GroupBy(movie => movie.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }
    }

    public List<Show> GetShowsAtTheater(string theaterId)
    {
        lock (_syncLock)
        {
            _ = GetTheaterOrThrow(theaterId);

            return _shows.Values
                .Where(show => string.Equals(show.Theater.Id, theaterId, StringComparison.Ordinal))
                .ToList();
        }
    }

    public List<string> GetAvailableSeats(string showId)
    {
        lock (_syncLock)
        {
            Show show = GetShowOrThrow(showId);
            return show.GetAvailableSeatsUnsafe();
        }
    }

    public string BookSeats(string showId, IReadOnlyList<string> seatIds)
    {
        if (seatIds is null || seatIds.Count == 0)
        {
            throw new ArgumentException("Select at least one seat.", nameof(seatIds));
        }

        lock (_syncLock)
        {
            Show show = GetShowOrThrow(showId);
            ValidateSeatSelection(show, seatIds);
            show.ValidateSeatsAvailable(seatIds);

            string confirmationId = GenerateConfirmationId();
            show.BookSeatsUnsafe(seatIds);

            try
            {
                Reservation reservation = new(confirmationId, show, seatIds);
                _reservations.Add(confirmationId, reservation);
            }
            catch
            {
                show.ReleaseSeatsUnsafe(seatIds);
                throw;
            }

            return confirmationId;
        }
    }

    public void CancelReservation(string confirmationId)
    {
        if (string.IsNullOrWhiteSpace(confirmationId))
        {
            throw new ArgumentException("Confirmation ID cannot be empty.", nameof(confirmationId));
        }

        lock (_syncLock)
        {
            Reservation reservation = GetReservationOrThrow(confirmationId);

            if (reservation.Status != ReservationStatus.CONFIRMED)
            {
                throw new InvalidOperationException("Reservation has already been cancelled.");
            }

            reservation.Show.ValidateSeatsBooked(reservation.SeatIds);
            reservation.Show.ReleaseSeatsUnsafe(reservation.SeatIds);
            reservation.MarkCancelled();
        }
    }

    private void ValidateSeatSelection(Show show, IReadOnlyList<string> seatIds)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (string? seatId in seatIds)
        {
            if (string.IsNullOrWhiteSpace(seatId))
            {
                throw new ArgumentException("Seat IDs cannot be empty.", nameof(seatIds));
            }

            if (!seen.Add(seatId))
            {
                throw new ArgumentException($"Duplicate seat '{seatId}' was selected.", nameof(seatIds));
            }

            if (!show.ContainsSeat(seatId))
            {
                throw new ArgumentException($"Seat '{seatId}' does not belong to this show.", nameof(seatIds));
            }
        }
    }

    private Theater GetTheaterOrThrow(string theaterId)
    {
        if (string.IsNullOrWhiteSpace(theaterId) || !_theaters.TryGetValue(theaterId, out Theater? theater))
        {
            throw new KeyNotFoundException("Theater not found.");
        }

        return theater;
    }

    private Show GetShowOrThrow(string showId)
    {
        if (string.IsNullOrWhiteSpace(showId) || !_shows.TryGetValue(showId, out Show? show))
        {
            throw new KeyNotFoundException("Show not found.");
        }

        return show;
    }

    private Reservation GetReservationOrThrow(string confirmationId)
    {
        if (!_reservations.TryGetValue(confirmationId, out Reservation? reservation))
        {
            throw new KeyNotFoundException("Reservation not found.");
        }

        return reservation;
    }

    private string GenerateConfirmationId()
    {
        string confirmationId;
        do
        {
            confirmationId = Guid.NewGuid().ToString("N");
        }
        while (_reservations.ContainsKey(confirmationId));

        return confirmationId;
    }

    private static Dictionary<string, T> ToUniqueDictionary<T>(
        IEnumerable<T> items,
        Func<T, string> keySelector,
        string parameterName)
        where T : class
    {
        List<T> itemList = items.ToList();
        if (itemList.Any(item => item is null))
        {
            throw new ArgumentException("Collection cannot contain null.", parameterName);
        }

        Dictionary<string, T> result = new(StringComparer.Ordinal);
        foreach (T item in itemList)
        {
            result.Add(keySelector(item), item);
        }

        return result;
    }
}
