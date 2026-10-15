namespace MovieTicketBooking;

public sealed class Seat
{
    internal Seat(char row, int number)
    {
        if (row is < 'A' or > 'Z')
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        if (number is < 0 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(number));
        }

        Row = row;
        Number = number;
        Id = $"{row}{number}";
    }

    public string Id { get; }

    public char Row { get; }

    public int Number { get; }
}
