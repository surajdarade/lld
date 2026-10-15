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
    public FileSystem():
        """
        Core Logic:
        1. Create the root directory with an empty internal name and no parent.
        2. Initialize the global reader/writer lock.

        Error/Edge Case(s):
        1. Construction failure prevents a usable instance from being returned.
        """

    public Directory CreateDirectory(parentPath, name):
        """
        Core Logic:
        1. Validate the name before taking the write lock.
        2. Acquire the write lock, resolve the parent path and require a directory.
        3. Reject a duplicate sibling name.
        4. Create the directory and register it in the parent.

        Error/Edge Case(s):
        1. Invalid path or name -> reject without mutation.
        2. Missing parent -> reject.
        3. Parent is a file or sibling name exists -> reject.
        """

    public File CreateFile(parentPath, name, content):
        """
        Core Logic:
        1. Validate the name and non-null content.
        2. Acquire the write lock, resolve the parent and require a directory.
        3. Reject a duplicate sibling name.
        4. Create the file and register it in the parent.

        Error/Edge Case(s):
        1. Invalid input, missing parent, wrong parent type or duplicate name -> reject without mutation.
        """

    public FileSystemEntry ResolvePath(absolutePath):
        """
        Core Logic:
        1. Acquire the read lock.
        2. Validate the strict absolute path and traverse from root.
        3. Return the resolved entry reference.

        Error/Edge Case(s):
        1. Malformed path or missing component -> reject.
        2. Traversal through a file -> reject.
        3. The reference may become stale after return and must be revalidated for later operations.
        """

    public IReadOnlyList<FileSystemEntry> ListDirectory(absolutePath):
        """
        Core Logic:
        1. Resolve the path under the read lock.
        2. Require a directory.
        3. Return a read-only snapshot of immediate children.

        Error/Edge Case(s):
        1. Missing path or file target -> reject.
        2. Later changes do not mutate the returned snapshot.
        """

    public string GetEntryName(entry):
        """
        Core Logic:
        1. Acquire the read lock and validate the entry reference.
        2. Return the entry name while the lock is held.

        Error/Edge Case(s):
        1. Null, deleted or foreign reference -> reject.
        """

    public string ReadFile(absolutePath):
        """
        Core Logic:
        1. Resolve the path under the read lock.
        2. Require a file and return its complete content.

        Error/Edge Case(s):
        1. Missing path or directory target -> reject.
        """

    public void ReplaceFileContent(absolutePath, content):
        """
        Core Logic:
        1. Reject null content.
        2. Resolve the target under the write lock and require a file.
        3. Replace the complete content with one assignment.

        Error/Edge Case(s):
        1. Missing path, directory target or invalid content -> reject without changing content.
        """

    public void Delete(entry):
        """
        Core Logic:
        1. Acquire the write lock and validate the reference.
        2. Reject the root and non-empty directories.
        3. Remove the entry from its parent and mark it inactive.

        Error/Edge Case(s):
        1. Null, stale or foreign reference -> reject.
        2. Root or non-empty directory -> reject without mutation.
        """

    public void Rename(entry, newName):
        """
        Core Logic:
        1. Validate the name and acquire the write lock.
        2. Validate the reference and reject the root.
        3. Treat the same name as a no-op and reject sibling collisions.
        4. Replace the old parent dictionary key and entry name.
        5. Restore the original key and name if insertion fails.

        Error/Edge Case(s):
        1. Invalid name, stale/foreign reference, root or duplicate name -> reject.
        2. Failure after removing the old key triggers rollback before releasing the lock.
        """

    public void Move(entry, destinationDirectory):
        """
        Core Logic:
        1. Acquire the write lock and validate both references.
        2. Reject the root, same-parent moves, name collisions and directory cycles.
        3. Remove the entry from the old parent.
        4. Update its parent and insert it into the destination.
        5. Restore the old parent and mapping if insertion fails.

        Error/Edge Case(s):
        1. Null, stale or foreign references -> reject.
        2. Invalid destination, root move, cycle or collision -> reject without mutation.
        3. Readers cannot observe the intermediate state because the write lock is held throughout.
        """

    public string GetFullPath(entry):
        """
        Core Logic:
        1. Validate the reference under the read lock.
        2. Return "/" for the root; otherwise follow parent references and collect names.
        3. Return the components in root-to-entry order.

        Error/Edge Case(s):
        1. Null, stale or foreign reference -> reject.
        2. The read lock prevents a concurrent move or rename from producing a mixed path.
        """

    private FileSystemEntry ResolvePathUnsafe(path):
        """
        Core Logic:
        1. Parse strict absolute path components.
        2. Traverse child dictionaries from root.
        3. Return the final entry.

        Error/Edge Case(s):
        1. Malformed path or missing child -> reject.
        2. An intermediate file -> reject.
        3. Caller must already hold the read or write lock.
        """

    private void ValidateEntryActive(entry):
        """
        Core Logic:
        1. Reject null and inactive entries.
        2. Follow parent references to this file system's root.
        3. Verify each parent contains the exact child under its current name.

        Error/Edge Case(s):
        1. Deleted or foreign reference -> reject.
        2. Broken parent-child invariant -> reject.
        3. Caller must already hold the file-system lock.
        """

    private void ValidateName(name):
        """
        Core Logic:
        1. Reject null names.
        2. Reject empty names, names containing '/' and the reserved names '.' and '..'.

        Error/Edge Case(s):
        1. Invalid name -> throw an argument exception before hierarchy mutation.
        """

    private string[] ValidateAbsolutePath(path):
        """
        Core Logic:
        1. Require an absolute path beginning with '/'.
        2. Resolve '/' to an empty component list.
        3. Reject trailing or repeated separators and '.' or '..' components.
        4. Return the validated components.

        Error/Edge Case(s):
        1. Null, relative or malformed path -> reject.
        """

    private void EnsureNameAvailable(parent, name):
        """
        Core Logic:
        1. Look up the name in the parent's child dictionary.
        2. Allow the name only when no child exists under it.

        Error/Edge Case(s):
        1. Existing sibling with the same case-sensitive name -> reject.
        """

    private Directory RequireDirectory(entry):
        """
        Core Logic:
        1. Return the entry when it is a Directory.

        Error/Edge Case(s):
        1. File supplied where a directory is required -> reject.
        """

    private File RequireFile(entry):
        """
        Core Logic:
        1. Return the entry when it is a File.

        Error/Edge Case(s):
        1. Directory supplied where a file is required -> reject.
        """

    private bool IsWithinSubtree(candidate, ancestor):
        """
        Core Logic:
        1. Walk from the candidate through its parent references.
        2. Return true if the ancestor is encountered; otherwise return false.

        Error/Edge Case(s):
        1. A candidate within the moved directory's subtree -> reject the move to prevent a cycle.
        """
```


```text
class Directory extends FileSystemEntry:
    internal Directory(name, parent):
        """
        Core Logic:
        1. Initialize the shared entry state.
        2. Initialize an empty child dictionary using ordinal, case-sensitive name comparison.

        Error/Edge Case(s):
        1. Construction with an invalid name or parent relationship -> reject.
        """

    internal FileSystemEntry? GetChild(name):
        """
        Core Logic:
        1. Perform a case-sensitive dictionary lookup.
        2. Return the child or null.

        Error/Edge Case(s):
        1. Missing child -> return null.
        2. Caller must hold the file-system lock.
        """

    internal FileSystemEntry[] GetChildren():
        """
        Core Logic:
        1. Copy the immediate child values into an array snapshot.
        2. Return the snapshot without exposing the mutable dictionary.

        Error/Edge Case(s):
        1. Empty directory -> return an empty array.
        2. Caller must hold the file-system lock while creating the snapshot.
        """

    internal void AddChild(entry):
        """
        Core Logic:
        1. Verify the entry is active and its parent is this directory.
        2. Add it under its name without overwriting an existing child.

        Error/Edge Case(s):
        1. Duplicate name or inconsistent parent -> reject.
        2. Caller must hold the write lock.
        """

    internal FileSystemEntry RemoveChild(name):
        """
        Core Logic:
        1. Remove and return the child associated with the name.

        Error/Edge Case(s):
        1. Missing child -> reject.
        2. Caller must hold the write lock.
        """
```


```text
abstract class FileSystemEntry:
    internal FileSystemEntry(name, parent):
        """
        Core Logic:
        1. Store the name and parent reference.
        2. Initialize the entry as active.

        Error/Edge Case(s):
        1. Invalid construction arguments -> reject.
        2. Only the root directory may have a null parent.
        """

    internal void SetName(name):
        """
        Core Logic:
        1. Update the internal name after coordinator validation.

        Error/Edge Case(s):
        1. Caller must hold the write lock and coordinate the parent dictionary.
        """

    internal void SetParent(parent):
        """
        Core Logic:
        1. Update the parent reference during a coordinated move.

        Error/Edge Case(s):
        1. Caller must hold the write lock and update the matching child dictionaries.
        """

    internal void MarkDeleted():
        """
        Core Logic:
        1. Mark the removed entry inactive.

        Error/Edge Case(s):
        1. The coordinator must first validate and remove the entry under the write lock.
        """
```


```text
class File extends FileSystemEntry:
    internal File(name, parent, content):
        """
        Core Logic:
        1. Initialize the shared entry state.
        2. Store the initial string content.

        Error/Edge Case(s):
        1. Null content -> reject.
        """

    internal void ReplaceContent(content):
        """
        Core Logic:
        1. Replace the entire stored string.

        Error/Edge Case(s):
        1. Null content -> reject.
        2. Caller must hold the write lock and validate the entry's lifecycle.
        """
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