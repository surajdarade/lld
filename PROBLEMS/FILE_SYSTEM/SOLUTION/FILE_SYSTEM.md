# RESOURCE(S)


[https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/file-system](https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/file-system)


# PROBLEM STATEMENT


Design an in-memory file system that supports creating files and directories, navigating paths and basic file operations.


**Primary Capabilities:**

- Maintain a single root directory and a hierarchy of files and directories.
- Store string content in files.
- Create, delete, list, rename and move entries.
- Resolve strict absolute paths and retrieve full paths from valid entry references.
- Support tens of thousands of in-memory entries.
- Preserve hierarchy consistency under concurrent operations.

**Error/Edge Case Handling:**

- Invalid paths, missing components and duplicate sibling names.
- File/directory type mismatches.
- Deleting the root or a non-empty directory.
- Invalid rename and move operations, including directory cycles.
- Stale or foreign entry references.
- Concurrent reads and mutations.

**Scope Boundaries:**

- Relative path resolution, search, permissions, ownership, timestamps, symbolic links, persistence, file type-specific behavior and UI are out of scope.
- File content is a string and supports reading and complete replacement only.
- The design supports concurrent calls within one process. Distributed coordination and persistence are out of scope.


# STEP 1: REQUIREMENTS


1. Hierarchical file system with single root directory

2. Files store string content

3. Folders contain files and other folders

4. Create and delete files and folders

5. List contents of a folder

6. Navigate/resolve absolute paths (e.g., /home/user/docs)

7. Rename and move files and folders

8. Retrieve full path from any file/folder reference

9. Scale to tens of thousands of entries in memory

Out of Scope:
- Search functionality
- Relative path resolution (../ or ./)
- Permissions, ownership, timestamps
- File type-specific behavior
- Persistence / disk storage
- Symbolic links
- UI layer


# STEP 2: ENTITIES


- `FileSystem`
  - Owns the root directory and global reader/writer lock.
  - Coordinates path resolution, file operations, directory operations, rename, move, deletion and full-path retrieval.
  - Enforces lifecycle validation and hierarchy invariants.
- `FileSystemEntry`
  - Abstract base class for shared entry state: name, parent directory and active/deleted lifecycle.
- `Directory`
  - Owns a dictionary of immediate child entries keyed by case-sensitive name.
  - Supports child lookup, snapshot listing, insertion, removal and empty-directory checks.
- `File`
  - Owns string content and supports complete content replacement.


**Relationships and State Ownership:**

- `FileSystem` owns the root directory and is the only public coordinator of shared-state operations.
- `File` and `Directory` derive from `FileSystemEntry`.
- Each non-root entry has one parent directory and appears exactly once in that parent's child dictionary.
- A directory owns its immediate children through a dictionary using ordinal, case-sensitive string comparison.
- Entry name, parent, lifecycle state, file content and child collections are internal state. Public operations route through `FileSystem` so state is accessed under its lock.
- Deleted entries are removed from their parent and marked inactive. Since non-empty directory deletion is rejected, no active descendants are orphaned by deletion.

**Unnecessary to Current Set of Requirements:**

- `PathParser` as a separate class: strict parsing is small and has no independent policy requirements.
- `FileSystemFactory`: construction is straightforward and has no factory-specific behavior.
- `SearchService`, `PermissionManager`, `MetadataStore`, `SymbolicLink` and persistence classes: these capabilities are out of scope.
- Separate lifecycle state classes: an active flag is sufficient for the current lifecycle.


# STEP 3: CLASS DESIGN


```text
class FileSystem:
    - root: Directory
    - syncLock: ReaderWriterLockSlim

    + FileSystem()
    + CreateDirectory(ParentPath, Name) -> Directory
    + CreateFile(ParentPath, Name, Content) -> File
    + ResolvePath(AbsolutePath) -> FileSystemEntry
    + ListDirectory(AbsolutePath) -> IReadOnlyList<FileSystemEntry>
    + GetEntryName(Entry) -> string
    + ReadFile(AbsolutePath) -> string
    + ReplaceFileContent(AbsolutePath, Content) -> void
    + Delete(Entry) -> void
    + Rename(Entry, NewName) -> void
    + Move(Entry, DestinationDirectory) -> void
    + GetFullPath(Entry) -> string
    - ResolvePathUnsafe(AbsolutePath) -> FileSystemEntry
    - ValidateEntryActive(Entry) -> void
    - ValidateName(Name) -> void
    - ValidateAbsolutePath(AbsolutePath) -> string[]
    - EnsureNameAvailable(Parent, Name) -> void
    - RequireDirectory(Entry) -> Directory
    - RequireFile(Entry) -> File
    - IsWithinSubtree(Candidate, Ancestor) -> boolean
```


```text
abstract class FileSystemEntry:
    # name: string
    # parent: Directory?
    # isActive: boolean

    # FileSystemEntry(Name, Parent)
    # SetName(Name) -> void
    # SetParent(Parent) -> void
    # MarkDeleted() -> void
```


```text
class Directory extends FileSystemEntry:
    - children: Dictionary<string, FileSystemEntry>

    + Directory(Name, Parent)
    - GetChild(Name) -> FileSystemEntry?
    - GetChildren() -> FileSystemEntry[]
    - AddChild(Entry) -> void
    - RemoveChild(Name) -> FileSystemEntry
    - IsEmpty: boolean
```


```text
class File extends FileSystemEntry:
    - content: string

    + File(Name, Parent, Content)
    - Content: string
    - ReplaceContent(Content) -> void
```


**Design Decisions:**

1. `FileSystem` coordinates operations that change multiple pieces of shared state.
2. `FileSystemEntry` centralizes name, parent and lifecycle state without exposing public mutable properties.
3. Each directory uses a dictionary with `StringComparer.Ordinal` for average O(1) child lookup and case-sensitive names.
4. Parent references are authoritative for full-path construction. Absolute paths are not cached, so moves and renames do not require cache invalidation.
5. `FileSystem` validates references by checking that each entry is active and reachable from its own root through consistent parent-child relationships.
6. One `ReaderWriterLockSlim` protects the entire hierarchy. Internal helpers never acquire the lock recursively.
7. Rename and move validate preconditions before mutation and restore the original mapping if a subsequent mutation fails.
8. Directory listing returns a snapshot instead of exposing the mutable dictionary.
9. `GetEntryName` provides safe access to an entry's name without exposing unsynchronized mutable state.
10. Each `FileSystem` instance owns an independent hierarchy; the design does not use a singleton.

**Principles Applied:**

- **Single Responsibility Principle:** `FileSystem` coordinates workflows; `Directory` owns child collection behavior; `File` owns content.
- **Encapsulation:** mutable state and mutation methods are internal. Public state access is coordinated by `FileSystem`.
- **Invariant preservation:** parent references, child dictionaries and lifecycle state are updated under the global write lock.
- **YAGNI:** no strategy hierarchy, factory, observer system or separate path-parser class is introduced without a requirement.

**Pattern Decision Log:**

| Pattern | Decision | Reason / trigger to reconsider |
|---|---|---|
| Composite | No separate pattern framework | The entry base class and directory-owned tree provide the needed hierarchy directly. Reconsider if entries gain uniform recursive operations. |
| Strategy | Not used | Only one path policy and one mutation model are required. Reconsider when alternative policies become requirements. |
| Factory | Not used | Entry construction is simple and coordinated by `FileSystem`. Reconsider if construction gains complex policy or dependencies. |
| State | Not used as a class hierarchy | An active flag is enough for active/deleted lifecycle behavior. Reconsider if lifecycle states gain distinct transitions or behavior. |
| Observer | Not used | No change notifications or subscribers are required. |
| Singleton | Not used | Independent file-system instances must be possible. |


# STEP 4: IMPLEMENTATION


```text
class FileSystem:
    public Directory CreateDirectory(parentPath, name):
        """
        Core Logic:
        1. Validate the name before acquiring the write lock.
        2. Acquire the write lock and resolve the parent directory.
        3. Reject an existing child with the same name.
        4. Create the directory and add it to the parent.

        Error/Edge Case(s):
        1. Invalid path or name -> throw error.
        2. Parent does not exist or is a file -> throw error.
        3. Duplicate name -> reject without mutation.
        """

        ValidateName(name)

        acquire writeLock

        try:
            parent = RequireDirectory(ResolvePathUnsafe(parentPath))

            EnsureNameAvailable(parent, name)

            directory = Directory(name, parent)

            parent.AddChild(directory)

            return directory
        finally:
            release writeLock


    public File CreateFile(parentPath, name, content):
        """
        Core Logic:
        1. Validate the name and content.
        2. Acquire the write lock and resolve the parent directory.
        3. Reject an existing child with the same name.
        4. Create the file and add it to the parent.

        Error/Edge Case(s):
        1. Invalid input or missing parent -> throw error.
        2. Parent is a file or name already exists -> reject.
        3. Failed creation must not leave a partial entry.
        """

        ValidateName(name)

        if (content == null)
            throw Error("Content Cannot Be Null")

        acquire writeLock

        try:
            parent = RequireDirectory(ResolvePathUnsafe(parentPath))

            EnsureNameAvailable(parent, name)

            file = File(name, parent, content)

            parent.AddChild(file)

            return file
        finally:
            release writeLock


    public FileSystemEntry ResolvePath(absolutePath):
        """
        Core Logic:
        1. Acquire the read lock.
        2. Validate the absolute path.
        3. Traverse the hierarchy from the root.
        4. Return the resolved entry.

        Error/Edge Case(s):
        1. Invalid path or missing component -> throw error.
        2. Traversal through a file -> throw error.
        3. The returned reference may become stale after the lock is released.
        """

        acquire readLock

        try:
            return ResolvePathUnsafe(absolutePath)
        finally:
            release readLock


    public IReadOnlyList<FileSystemEntry> ListDirectory(absolutePath):
        """
        Core Logic:
        1. Acquire the read lock and resolve the path.
        2. Require the resolved entry to be a directory.
        3. Return a snapshot of its immediate children.

        Error/Edge Case(s):
        1. Missing path or file target -> throw error.
        2. Later mutations must not change the returned snapshot.
        """

        acquire readLock

        try:
            directory = RequireDirectory(ResolvePathUnsafe(absolutePath))

            return ReadOnlySnapshot(directory.GetChildren())
        finally:
            release readLock


    public string ReadFile(absolutePath):
        """
        Core Logic:
        1. Acquire the read lock.
        2. Resolve the path and require a file.
        3. Return the complete file content.

        Error/Edge Case(s):
        1. Missing path or directory target -> throw error.
        """

        acquire readLock

        try:
            file = RequireFile(ResolvePathUnsafe(absolutePath))

            return file.Content
        finally:
            release readLock


    public void ReplaceFileContent(absolutePath, content):
        """
        Core Logic:
        1. Reject null content.
        2. Acquire the write lock.
        3. Resolve the path and require a file.
        4. Replace the complete content.

        Error/Edge Case(s):
        1. Null content, missing path or directory target -> reject.
        2. Failed validation must leave the original content unchanged.
        """

        if (content == null)
            throw Error("Content Cannot Be Null")

        acquire writeLock

        try:
            file = RequireFile(ResolvePathUnsafe(absolutePath))

            file.ReplaceContent(content)
        finally:
            release writeLock


    public void Delete(entry):
        """
        Core Logic:
        1. Acquire the write lock and validate the entry reference.
        2. Reject the root and non-empty directories.
        3. Remove the entry from its parent.
        4. Mark the entry inactive.

        Error/Edge Case(s):
        1. Null, stale or foreign reference -> throw error.
        2. Root or non-empty directory -> reject without mutation.
        """

        acquire writeLock

        try:
            ValidateEntryActive(entry)

            if (entry == root)
                throw Error("Cannot Delete Root")

            if (entry is Directory && !entry.IsEmpty())
                throw Error("Directory Is Not Empty")

            parent = entry.Parent

            parent.RemoveChild(entry.Name)

            entry.MarkDeleted()
        finally:
            release writeLock


    public void Rename(entry, newName):
        """
        Core Logic:
        1. Validate the new name and acquire the write lock.
        2. Validate the entry and reject the root.
        3. Treat the same name as a no-op.
        4. Reject a sibling name collision.
        5. Replace the old dictionary key and update the entry name.
        6. Roll back if inserting the new key fails.

        Error/Edge Case(s):
        1. Invalid name, stale reference or root -> reject.
        2. Duplicate sibling name -> reject without mutation.
        3. Failed insertion -> restore the original name and mapping.
        """

        ValidateName(newName)

        acquire writeLock

        try:
            ValidateEntryActive(entry)

            if (entry == root)
                throw Error("Cannot Rename Root")

            if (entry.Name == newName)
                return

            parent = entry.Parent

            EnsureNameAvailable(parent, newName)

            oldName = entry.Name

            parent.RemoveChild(oldName)

            try:
                entry.SetName(newName)

                parent.AddChild(entry)
            catch:
                entry.SetName(oldName)

                parent.AddChild(entry)

                throw
        finally:
            release writeLock


    public void Move(entry, destinationDirectory):
        """
        Core Logic:
        1. Acquire the write lock and validate both references.
        2. Reject the root, same-parent moves and directory cycles.
        3. Reject a destination name collision.
        4. Remove the entry from its old parent.
        5. Update its parent and add it to the destination.
        6. Roll back if the destination insertion fails.

        Error/Edge Case(s):
        1. Null, stale or foreign reference -> reject.
        2. Invalid destination, root move, cycle or collision -> reject.
        3. Rollback restores the original parent and mapping.
        4. The write lock prevents readers from observing an intermediate state.
        """

        acquire writeLock

        try:
            ValidateEntryActive(entry)
            ValidateEntryActive(destinationDirectory)

            destination = RequireDirectory(destinationDirectory)

            if (entry == root)
                throw Error("Cannot Move Root")

            if (entry.Parent == destination)
                throw Error("Entry Already Has This Parent")

            if (entry is Directory &&
                IsWithinSubtree(destination, entry))
                throw Error("Cannot Move Directory Into Its Subtree")

            EnsureNameAvailable(destination, entry.Name)

            oldParent = entry.Parent
            oldName = entry.Name

            oldParent.RemoveChild(oldName)

            try:
                entry.SetParent(destination)

                destination.AddChild(entry)
            catch:
                entry.SetParent(oldParent)

                oldParent.AddChild(entry)

                throw
        finally:
            release writeLock


    public string GetFullPath(entry):
        """
        Core Logic:
        1. Acquire the read lock and validate the reference.
        2. Return "/" for the root.
        3. Follow parent references and collect entry names.
        4. Reverse the names and construct the absolute path.

        Error/Edge Case(s):
        1. Null, stale or foreign reference -> throw error.
        2. The read lock prevents concurrent rename or move from producing a mixed path.
        """

        acquire readLock

        try:
            ValidateEntryActive(entry)

            if (entry == root)
                return "/"

            names = []

            current = entry

            while (current != root):
                names.Add(current.Name)

                current = current.Parent

            Reverse(names)

            return "/" + Join(names, "/")
        finally:
            release readLock


    private FileSystemEntry ResolvePathUnsafe(path):
        """
        Core Logic:
        1. Validate and split the absolute path into components.
        2. Start from the root.
        3. Traverse each component through the current directory.
        4. Return the final entry.

        Error/Edge Case(s):
        1. Malformed path or missing child -> throw error.
        2. Intermediate entry is a file -> throw error.
        3. Caller must already hold the read or write lock.
        """

        components = ValidateAbsolutePath(path)

        current = root

        for component in components:
            directory = RequireDirectory(current)

            current = directory.GetChild(component)

            if (current == null)
                throw Error("Path Does Not Exist")

        return current


    private void ValidateEntryActive(entry):
        """
        Core Logic:
        1. Reject null or inactive entries.
        2. Follow parent references toward the root.
        3. Verify each parent contains the exact child under its current name.
        4. Accept only references belonging to this file system.

        Error/Edge Case(s):
        1. Deleted or foreign reference -> reject.
        2. Broken parent-child relationship -> reject.
        3. Caller must already hold the file-system lock.
        """

        if (entry == null || !entry.IsActive)
            throw Error("Invalid Entry Reference")

        current = entry

        while (current != root):
            parent = current.Parent

            if (parent == null ||
                parent.GetChild(current.Name) != current)
                throw Error("Invalid Entry Reference")

            current = parent


    private string[] ValidateAbsolutePath(path):
        """
        Core Logic:
        1. Require a path beginning with "/".
        2. Treat "/" as the root.
        3. Split the path into components.
        4. Reject empty components, "." and "..".
        5. Return the validated components.

        Error/Edge Case(s):
        1. Null, relative, repeated-separator or trailing-separator path -> reject.
        2. Root "/" is valid.
        """

        if (path == null || !path.StartsWith("/"))
            throw Error("Absolute Path Required")

        if (path == "/")
            return []

        if (path.EndsWith("/") || path.Contains("//"))
            throw Error("Malformed Path")

        components = Split(path.Substring(1), "/")

        for component in components:
            if (component == "." || component == "..")
                throw Error("Invalid Path Component")

        return components
```


```text
class Directory extends FileSystemEntry:
    internal FileSystemEntry? GetChild(name):
        """
        Core Logic:
        1. Look up the child by its case-sensitive name.
        2. Return the child or null.

        Error/Edge Case(s):
        1. Missing child -> return null.
        2. Caller must hold the file-system lock.
        """

        return children.GetValueOrDefault(name)


    internal void AddChild(entry):
        """
        Core Logic:
        1. Verify the entry's parent is this directory.
        2. Add the entry under its name without overwriting another child.

        Error/Edge Case(s):
        1. Duplicate name or inconsistent parent -> reject.
        2. Caller must hold the write lock.
        """

        if (entry.Parent != this)
            throw Error("Invalid Parent")

        children.Add(entry.Name, entry)


    internal FileSystemEntry RemoveChild(name):
        """
        Core Logic:
        1. Remove and return the child associated with the name.

        Error/Edge Case(s):
        1. Missing child -> throw error.
        2. Caller must hold the write lock.
        """

        if (!children.ContainsKey(name))
            throw Error("Child Does Not Exist")

        return children.RemoveAndReturn(name)
```


```text
abstract class FileSystemEntry:
    internal void SetName(name):
        """
        Core Logic:
        1. Update the entry name during a validated rename.

        Error/Edge Case(s):
        1. Caller must hold the write lock and coordinate the parent mapping.
        """

        this.name = name


    internal void SetParent(parent):
        """
        Core Logic:
        1. Update the parent reference during a move.

        Error/Edge Case(s):
        1. Caller must hold the write lock and coordinate both parent mappings.
        """

        this.parent = parent


    internal void MarkDeleted():
        """
        Core Logic:
        1. Mark the entry inactive after removing it from its parent.

        Error/Edge Case(s):
        1. Caller must validate and remove the entry under the write lock.
        """

        this.isActive = false
```


```text
class File extends FileSystemEntry:
    internal void ReplaceContent(content):
        """
        Core Logic:
        1. Replace the entire stored content.

        Error/Edge Case(s):
        1. Null content -> reject.
        2. Caller must hold the write lock.
        """

        if (content == null)
            throw Error("Content Cannot Be Null")

        this.content = content
```


# STEP 5: EXTENSIBILITY


**1. How would you support relative paths such as `./` and `../`?**

- I'd introduce a path resolver that accepts a starting directory and resolves the path from there.
- `.` would keep the current directory, while `..` would move to its parent.
- I'd keep the existing absolute-path resolution unchanged.

```
ResolveRelativePath(startDirectory, relativePath):
    current = startDirectory

    for component in relativePath:
        if component == ".":
            continue

        if component == "..":
            current = current.Parent

        else:
            current = current.GetChild(component)

    return current
```


**2. How would you add permissions and ownership?**

- I'd add owner and permission information to `FileSystemEntry`.
- Before reading, writing, deleting or moving an entry, `FileSystem` would check whether the user has permission.
- I'd keep these checks in `FileSystem` instead of repeating them in `File` and `Directory`.

```
class FileSystemEntry:
    - owner: User
    - permissions: PermissionSet

class FileSystem:
    public ReadFile(path, user):
        entry = ResolvePath(path)
        CheckPermission(user, entry, READ)
        return entry.Content
```


**3. How would you support persistence?**

- I'd introduce a storage component to save entries and file contents.
- `FileSystem` would continue managing file operations, while the storage component would handle saving and loading data.
- I'd use transactions so a failed operation cannot leave the stored hierarchy inconsistent.

```
interface IFileSystemStorage:
    + Load() -> Directory
    + SaveEntry(Entry) -> void
    + DeleteEntry(Entry) -> void
```


**4. How would you improve concurrency if the global reader/writer lock became a bottleneck?**

- I'd first check whether the global lock is actually slowing down the system.
- If it is, I'd introduce finer-grained locks, such as one lock per directory.
- When moving entries between directories, I'd acquire locks in a consistent order to prevent deadlocks and preserve atomic moves.

```
Move(entry, destination):
    locks = AcquireLocksInConsistentOrder(
        entry.Parent,
        destination
    )

    try:
        ValidateMove(entry, destination)
        MoveAtomically(entry, destination)
    finally:
        ReleaseLocks(locks)
```