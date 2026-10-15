namespace FileSystemDesign;

public sealed class Directory : FileSystemEntry
{
    private readonly Dictionary<string, FileSystemEntry> _children = new(StringComparer.Ordinal);

    internal Directory(string name, Directory? parent)
        : base(name, parent)
    {
    }

    internal FileSystemEntry? GetChild(string name)
    {
        return _children.TryGetValue(name, out FileSystemEntry? child) ? child : null;
    }

    internal FileSystemEntry[] GetChildren()
    {
        return _children.Values.ToArray();
    }

    internal bool IsEmpty => _children.Count == 0;

    internal void AddChild(FileSystemEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!entry.IsActive || !ReferenceEquals(entry.Parent, this))
        {
            throw new InvalidOperationException("The entry is inactive or has an inconsistent parent.");
        }

        _children.Add(entry.Name, entry);
    }

    internal FileSystemEntry RemoveChild(string name)
    {
        if (!_children.Remove(name, out FileSystemEntry? child))
        {
            throw new KeyNotFoundException($"No child named '{name}' exists in this directory.");
        }

        return child;
    }
}
