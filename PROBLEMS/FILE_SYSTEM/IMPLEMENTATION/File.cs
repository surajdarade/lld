namespace FileSystemDesign;

public sealed class File : FileSystemEntry
{
    private string _content;

    internal File(string name, Directory parent, string content)
        : base(name, parent)
    {
        ArgumentNullException.ThrowIfNull(content);
        _content = content;
    }

    internal string Content => _content;

    internal void ReplaceContent(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _content = content;
    }
}
