```markdown
# Excalidraw-Manager Development Patterns

> Auto-generated skill from repository analysis

## Overview
This skill teaches you the development patterns and coding conventions used in the Excalidraw-Manager repository, a C# project with no detected framework. You'll learn how to structure files, write imports and exports, follow commit patterns, and implement and run tests. This guide is ideal for contributors aiming for consistency and maintainability in the codebase.

## Coding Conventions

### File Naming
- Use **camelCase** for file names.
  - Example: `excalidrawManager.cs`, `shapeHandler.cs`

### Import Style
- Use **relative imports**.
  - Example:
    ```csharp
    using ../utils/helperFunctions;
    ```

### Export Style
- Use **named exports**.
  - Example:
    ```csharp
    public class ShapeManager
    {
        // class implementation
    }
    ```

### Commit Patterns
- Commit messages are **freeform** with no strict prefixes.
- Average commit message length: ~50 characters.
  - Example: `Add support for new shape types`

## Workflows

### Adding a New Feature
**Trigger:** When you need to implement a new functionality.
**Command:** `/add-feature`

1. Create a new file using camelCase naming.
2. Implement the feature with named exports.
3. Use relative imports for dependencies.
4. Write or update corresponding test files (`*.test.*`).
5. Commit changes with a clear, concise message.

### Fixing a Bug
**Trigger:** When you identify and resolve a bug.
**Command:** `/fix-bug`

1. Locate the relevant file(s) using camelCase convention.
2. Apply the bug fix.
3. Update or add tests to cover the bug scenario.
4. Commit with a descriptive message explaining the fix.

### Writing and Running Tests
**Trigger:** When you add or modify code.
**Command:** `/run-tests`

1. Create or update test files following the `*.test.*` pattern.
2. Use the (unknown) test framework's conventions.
3. Run the test suite to ensure all tests pass.

## Testing Patterns

- Test files follow the `*.test.*` naming pattern.
  - Example: `shapeManager.test.cs`
- The specific testing framework is **unknown**; follow existing patterns in the repository.
- Place tests alongside or near the code they test.

## Commands
| Command      | Purpose                                 |
|--------------|-----------------------------------------|
| /add-feature | Start the workflow for adding features   |
| /fix-bug     | Begin the bug fixing workflow           |
| /run-tests   | Run all tests in the codebase           |
```