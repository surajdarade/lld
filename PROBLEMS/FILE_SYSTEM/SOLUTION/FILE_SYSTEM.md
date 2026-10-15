# RESOURCE(S)


[https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/file-system](https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/file-system)


# PROBLEM STATEMENT


Design an in-memory file system that supports creating files and directories, navigating paths and basic file operations.


**Primary Capabilities:**

**Error/Edge Case Handling:**

**Scope Boundaries:**


# STEP 1: REQUIREMENTS


1. Hierarchical file system with single root directory

2. Files store string content

3. Folders contains files and other folders

4. Create and delete files and folders

5. List contents of a folder

6. Navigate/resolve absolute paths (e.g., /home/user/docs)

7. Rename and move files and folders

8. Retrieve full path from any file/folder reference

9. Scale to tens of thousands of entries in memory


**Out of Scope:**

- Search Functionality
- Relative path resolution (../ or ./)
- Permissions, ownership and timestamps
- File type-specific behavior
- Persistence / disk storage
- Symbolic links
- UI Layer