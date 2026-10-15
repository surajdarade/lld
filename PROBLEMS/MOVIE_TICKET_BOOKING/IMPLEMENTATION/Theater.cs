using System.Collections.ObjectModel;

namespace MovieTicketBooking;

public sealed class Theater
{
    private readonly ReadOnlyDictionary<string, Screen> _screens;

    public Theater(string id, string name, IEnumerable<Screen> screens)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Theater ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Theater name cannot be empty.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(screens);
        List<Screen> screenList = screens.ToList();

        if (screenList.Count == 0)
        {
            throw new ArgumentException("A theater must contain at least one screen.", nameof(screens));
        }

        if (screenList.Any(screen => screen is null))
        {
            throw new ArgumentException("Screen collection cannot contain null.", nameof(screens));
        }

        Dictionary<string, Screen> screenMap = new(StringComparer.Ordinal);
        foreach (Screen screen in screenList)
        {
            screenMap.Add(screen.Id, screen);
        }

        Id = id;
        Name = name;
        _screens = new ReadOnlyDictionary<string, Screen>(screenMap);
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyCollection<Screen> Screens => _screens.Values;

    public Screen GetScreen(string screenId)
    {
        if (string.IsNullOrWhiteSpace(screenId) || !_screens.TryGetValue(screenId, out Screen? screen))
        {
            throw new KeyNotFoundException("Screen not found in this theater.");
        }

        return screen;
    }

    internal bool ContainsScreen(Screen screen)
    {
        return _screens.TryGetValue(screen.Id, out Screen? registered) && ReferenceEquals(registered, screen);
    }
}
