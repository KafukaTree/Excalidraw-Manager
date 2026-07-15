```markdown
# Excalidraw-Manager Development Patterns

> Auto-generated skill from repository analysis

## Overview
This skill teaches the core development patterns and conventions used in the Excalidraw-Manager repository, a C# project with no specific framework detected. You'll learn about file naming, import/export styles, commit message habits, and how to write and run tests. This guide also provides suggested commands for common workflows.

## Coding Conventions

### File Naming
- Use **PascalCase** for all file names.
  - Example: `DrawingManager.cs`, `ShapeUtils.cs`

### Imports
- Use **relative import paths** for referencing other files within the project.
  - Example:
    ```csharp
    using ExcalidrawManager.Models;
    using ExcalidrawManager.Utils;
    ```

### Exports
- Use **named exports** (public classes, methods, etc.).
  - Example:
    ```csharp
    public class DrawingManager
    {
        // ...
    }
    ```

### Commit Messages
- Freeform, with no strict prefixing.
- Average commit message length: ~34 characters.
  - Example: `Add support for new shape types`

## Workflows

### Adding a New Feature
**Trigger:** When implementing a new capability or module  
**Command:** `/add-feature`

1. Create a new file using PascalCase (e.g., `NewFeature.cs`).
2. Implement the feature using named exports.
3. Import any dependencies using relative paths.
4. Write corresponding tests in a `*.test.*` file.
5. Commit changes with a clear, concise message.

### Fixing a Bug
**Trigger:** When resolving a defect or issue  
**Command:** `/fix-bug`

1. Identify the problematic code section.
2. Modify the relevant file(s), following coding conventions.
3. Update or add tests to cover the bug fix.
4. Commit with a message describing the fix.

### Writing and Running Tests
**Trigger:** When validating code correctness  
**Command:** `/run-tests`

1. Create or update test files with the pattern `*.test.*` (e.g., `DrawingManager.test.cs`).
2. Write test cases for new or changed functionality.
3. Use the project's preferred method to run tests (framework unknown; consult project docs or maintainers).
4. Review test results and address any failures.

## Testing Patterns

- Test files are named using the pattern `*.test.*` (e.g., `ShapeUtils.test.cs`).
- The specific testing framework is unknown; check with maintainers or project documentation.
- Place tests alongside or in a dedicated test directory as per project structure.

## Commands
| Command       | Purpose                                 |
|---------------|-----------------------------------------|
| /add-feature  | Scaffold and implement a new feature    |
| /fix-bug      | Apply and commit a bug fix              |
| /run-tests    | Run all test suites                     |
```
