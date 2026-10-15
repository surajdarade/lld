using System.Collections.ObjectModel;

namespace MovieTicketBooking;

public sealed class Screen
{
    private readonly ReadOnlyDictionary<string, Seat> _seats;

    public Screen(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Screen ID cannot be empty.", nameof(id));
        }

        Id = id;
        Dictionary<string, Seat> seats = new(StringComparer.Ordinal);

        for (char row = 'A'; row <= 'Z'; row++)
        {
            for (int number = 0; number <= 20; number++)
            {
                Seat seat = new(row, number);
                seats.Add(seat.Id, seat);
            }
        }

        _seats = new ReadOnlyDictionary<string, Seat>(seats);
    }

    public string Id { get; }

    public IReadOnlyDictionary<string, Seat> Seats => _seats;
}
