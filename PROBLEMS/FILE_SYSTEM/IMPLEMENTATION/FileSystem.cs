using System.Threading;

namespace FileSystemDesign;

public sealed class FileSystem
{
    private readonly Directory _root;
    private readonly ReaderWriterLockSlim _syncLock = new(LockRecursionPolicy.NoRecursion);

    public FileSystem()
    {
        _root = new Directory(string.Empty, null);
    }

    public Directory CreateDirectory(string parentPath, string name)
    {
        ValidateName(name);

        _syncLock.EnterWriteLock();
        try
        {
            Directory parent = RequireDirectory(ResolvePathUnsafe(parentPath));
            EnsureNameAvailable(parent, name);

            Directory directory = new(name, parent);
            parent.AddChild(directory);
            return directory;
        }
        finally
        {
            _syncLock.ExitWriteLock();
        }
    }

    public File CreateFile(string parentPath, string name, string content)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(content);

        _syncLock.EnterWriteLock();
        try
        {
            Directory parent = RequireDirectory(ResolvePathUnsafe(parentPath));
            EnsureNameAvailable(parent, name);

            File file = new(name, parent, content);
            parent.AddChild(file);
            return file;
        }
        finally
        {
            _syncLock.ExitWriteLock();
        }
    }

    public FileSystemEntry ResolvePath(string absolutePath)
    {
        _syncLock.EnterReadLock();
        try
        {
            return ResolvePathUnsafe(absolutePath);
        }
        finally
        {
            _syncLock.ExitReadLock();
        }
    }

    public IReadOnlyList<FileSystemEntry> ListDirectory(string absolutePath)
    {
        _syncLock.EnterReadLock();
        try
        {
            Directory directory = RequireDirectory(ResolvePathUnsafe(absolutePath));
            return Array.AsReadOnly(directory.GetChildren());
        }
        finally
        {
            _syncLock.ExitReadLock();
        }
    }

    public string GetEntryName(FileSystemEntry entry)
    {
        _syncLock.EnterReadLock();
        try
        {
            ValidateEntryActive(entry);
            return entry.Name;
        }
        finally
        {
            _syncLock.ExitReadLock();
        }
    }

    public string ReadFile(string absolutePath)
    {
        _syncLock.EnterReadLock();
        try
        {
            File file = RequireFile(ResolvePathUnsafe(absolutePath));
            return file.Content;
        }
        finally
        {
            _syncLock.ExitReadLock();
        }
    }

    public void ReplaceFileContent(string absolutePath, string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        _syncLock.EnterWriteLock();
        try
        {
            File file = RequireFile(ResolvePathUnsafe(absolutePath));
            file.ReplaceContent(content);
        }
        finally
        {
            _syncLock.ExitWriteLock();
        }
    }

    public void Delete(FileSystemEntry entry)
    {
        _syncLock.EnterWriteLock();
        try
        {
            ValidateEntryActive(entry);

            if (ReferenceEquals(entry, _root))
            {
                throw new InvalidOperationException("The root directory cannot be deleted.");
            }

            if (entry is Directory directory && !directory.IsEmpty)
            {
                throw new InvalidOperationException("A non-empty directory cannot be deleted.");
            }

            Directory parent = entry.Parent!;
            parent.RemoveChild(entry.Name);
            entry.MarkDeleted();
        }
        finally
        {
            _syncLock.ExitWriteLock();
        }
    }

    public void Rename(FileSystemEntry entry, string newName)
    {
        ValidateName(newName);

        _syncLock.EnterWriteLock();
        try
        {
            ValidateEntryActive(entry);

            if (ReferenceEquals(entry, _root))
            {
                throw new InvalidOperationException("The root directory cannot be renamed.");
            }

            if (string.Equals(entry.Name, newName, StringComparison.Ordinal))
            {
                return;
            }

            Directory parent = entry.Parent!;
            EnsureNameAvailable(parent, newName);

            string oldName = entry.Name;
            parent.RemoveChild(oldName);

            try
            {
                entry.SetName(newName);
                parent.AddChild(entry);
            }
            catch
            {
                entry.SetName(oldName);
                parent.AddChild(entry);
                throw;
            }
        }
        finally
        {
            _syncLock.ExitWriteLock();
        }
    }

    public void Move(FileSystemEntry entry, Directory destinationDirectory)
    {
        ArgumentNullException.ThrowIfNull(destinationDirectory);

        _syncLock.EnterWriteLock();
        try
        {
            ValidateEntryActive(entry);
            ValidateEntryActive(destinationDirectory);

            if (ReferenceEquals(entry, _root))
            {
                throw new InvalidOperationException("The root directory cannot be moved.");
            }

            Directory oldParent = entry.Parent!;

            if (ReferenceEquals(destinationDirectory, oldParent))
            {
                throw new InvalidOperationException("The entry is already in the destination directory.");
            }

            if (entry is Directory directory && IsWithinSubtree(destinationDirectory, directory))
            {
                throw new InvalidOperationException("A directory cannot be moved into itself or a descendant.");
            }

            EnsureNameAvailable(destinationDirectory, entry.Name);
            oldParent.RemoveChild(entry.Name);

            try
            {
                entry.SetParent(destinationDirectory);
                destinationDirectory.AddChild(entry);
            }
            catch
            {
                entry.SetParent(oldParent);
                oldParent.AddChild(entry);
                throw;
            }
        }
        finally
        {
            _syncLock.ExitWriteLock();
        }
    }

    public string GetFullPath(FileSystemEntry entry)
    {
        _syncLock.EnterReadLock();
        try
        {
            ValidateEntryActive(entry);

            if (ReferenceEquals(entry, _root))
            {
                return "/";
            }

            Stack<string> components = new();
            FileSystemEntry current = entry;

            while (!ReferenceEquals(current, _root))
            {
                components.Push(current.Name);
                current = current.Parent
                    ?? throw new InvalidOperationException("The entry is disconnected from the root.");
            }

            return "/" + string.Join("/", components);
        }
        finally
        {
            _syncLock.ExitReadLock();
        }
    }

    private FileSystemEntry ResolvePathUnsafe(string absolutePath)
    {
        string[] components = ValidateAbsolutePath(absolutePath);
        FileSystemEntry current = _root;

        foreach (string component in components)
        {
            if (current is not Directory directory)
            {
                throw new InvalidOperationException("A path component before the end of the path is a file.");
            }

            current = directory.GetChild(component)
                ?? throw new KeyNotFoundException($"Path component '{component}' was not found.");
        }

        return current;
    }

    private void ValidateEntryActive(FileSystemEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!entry.IsActive)
        {
            throw new InvalidOperationException("The entry has been deleted.");
        }

        FileSystemEntry current = entry;

        while (!ReferenceEquals(current, _root))
        {
            Directory parent = current.Parent
                ?? throw new InvalidOperationException("The entry does not belong to this file system.");

            if (!parent.IsActive || !ReferenceEquals(parent.GetChild(current.Name), current))
            {
                throw new InvalidOperationException("The entry is not part of the active file-system hierarchy.");
            }

            current = parent;
        }
    }

    private static void ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (name.Length == 0 || name.Contains('/') || name is "." or "..")
        {
            throw new ArgumentException("Name must be a non-empty path component and cannot contain '/'.", nameof(name));
        }
    }

    private static string[] ValidateAbsolutePath(string absolutePath)
    {
        ArgumentNullException.ThrowIfNull(absolutePath);

        if (absolutePath.Length == 0 || absolutePath[0] != '/')
        {
            throw new ArgumentException("Path must be absolute.", nameof(absolutePath));
        }

        if (absolutePath == "/")
        {
            return Array.Empty<string>();
        }

        if (absolutePath[^1] == '/' || absolutePath.Contains("//", StringComparison.Ordinal))
        {
            throw new ArgumentException("Path cannot contain repeated or trailing separators.", nameof(absolutePath));
        }

        string[] components = absolutePath[1..].Split('/');

        if (components.Any(component => component.Length == 0 || component is "." or ".."))
        {
            throw new ArgumentException("Path contains an invalid component.", nameof(absolutePath));
        }

        return components;
    }

    private static void EnsureNameAvailable(Directory parent, string name)
    {
        if (parent.GetChild(name) is not null)
        {
            throw new InvalidOperationException($"An entry named '{name}' already exists in the directory.");
        }
    }

    private static Directory RequireDirectory(FileSystemEntry entry)
    {
        return entry as Directory
            ?? throw new InvalidOperationException("The path does not identify a directory.");
    }

    private static File RequireFile(FileSystemEntry entry)
    {
        return entry as File
            ?? throw new InvalidOperationException("The path does not identify a file.");
    }

    private static bool IsWithinSubtree(Directory candidate, Directory ancestor)
    {
        FileSystemEntry? current = candidate;

        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }
}
