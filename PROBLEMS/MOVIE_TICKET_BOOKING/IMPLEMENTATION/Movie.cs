namespace MovieTicketBooking;

public sealed class Movie
{
    public Movie(string id, string title)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Movie ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Movie title cannot be empty.", nameof(title));
        }

        Id = id;
        Title = title;
    }

    public string Id { get; }

    public string Title { get; }
}
