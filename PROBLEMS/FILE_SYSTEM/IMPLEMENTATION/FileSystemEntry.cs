namespace FileSystemDesign;

public abstract class FileSystemEntry
{
    private string _name;
    private Directory? _parent;
    private bool _isActive;

    internal FileSystemEntry(string name, Directory? parent)
    {
        _name = name;
        _parent = parent;
        _isActive = true;
    }

    internal string Name => _name;

    internal Directory? Parent => _parent;

    internal bool IsActive => _isActive;

    internal void SetName(string name)
    {
        _name = name;
    }

    internal void SetParent(Directory parent)
    {
        _parent = parent;
    }

    internal void MarkDeleted()
    {
        _isActive = false;
    }
}
