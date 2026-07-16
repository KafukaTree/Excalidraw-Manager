```markdown
# Excalidraw-Manager Development Patterns

> Auto-generated skill from repository analysis

## Overview
This skill covers the development patterns and conventions used in the Excalidraw-Manager JavaScript codebase. It documents file structure, code style, commit habits, and testing approaches, providing clear examples and step-by-step workflows for contributors. This guide is designed to help you quickly understand and contribute effectively to Excalidraw-Manager.

## Coding Conventions

### File Naming
- **Style:** camelCase
- **Example:**  
  ```
  drawManager.js
  userSettings.js
  ```

### Import Style
- **Relative imports are used.**
- **Example:**  
  ```js
  import { getUserSettings } from './userSettings';
  ```

### Export Style
- **Named exports are preferred.**
- **Example:**  
  ```js
  // In userSettings.js
  export function getUserSettings() { ... }
  export const DEFAULT_SETTINGS = { ... };
  ```

### Commit Patterns
- **Type:** Freeform, no strict prefixes.
- **Average length:** ~55 characters.
- **Example:**  
  ```
  Fix bug in settings panel when loading defaults
  Add export functionality for drawings
  ```

## Workflows

### Running Tests
**Trigger:** When you want to verify code changes.
**Command:** `/run-tests`

1. Ensure you have all dependencies installed.
2. Run the test suite (framework is unknown; typically use `npm test` or similar).
3. Review output for passing and failing tests.

### Adding a New Module
**Trigger:** When implementing a new feature or utility.
**Command:** `/add-module`

1. Create a new file using camelCase naming (e.g., `featureName.js`).
2. Use relative imports to include dependencies.
3. Export your functions or constants using named exports.
4. If applicable, add a corresponding test file (e.g., `featureName.test.ts`).

### Writing a Commit
**Trigger:** When saving changes to the repository.
**Command:** `/commit`

1. Write a clear, descriptive commit message (freeform, ~55 chars).
2. Avoid strict prefixes; focus on clarity.
3. Example:  
   ```
   Refactor export logic for better performance
   ```

## Testing Patterns

- **Test File Pattern:** All test files are named with the `.test.ts` suffix.
- **Testing Framework:** Not explicitly detected; likely uses a standard JS/TS testing tool (e.g., Jest, Mocha).
- **Example Test File:**  
  ```
  drawManager.test.ts
  ```
- **Test Example:**  
  ```ts
  import { drawShape } from './drawManager';

  test('drawShape creates a rectangle', () => {
    const shape = drawShape('rectangle', { width: 10, height: 20 });
    expect(shape.type).toBe('rectangle');
    expect(shape.width).toBe(10);
    expect(shape.height).toBe(20);
  });
  ```

## Commands
| Command      | Purpose                                   |
|--------------|-------------------------------------------|
| /run-tests   | Run the test suite                        |
| /add-module  | Add a new module following conventions    |
| /commit      | Commit changes with a clear message       |
```
