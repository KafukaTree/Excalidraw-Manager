import assert from 'node:assert/strict';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const testDirectory = fileURLToPath(new URL('.', import.meta.url));
const editorDirectory = path.resolve(process.argv[2] || path.join(testDirectory, '..', 'runtime', 'formula-editor'));
const { isCommandTypingInput } = await import(pathToFileURL(path.join(editorDirectory, 'completion.mjs')));

assert.equal(isCommandTypingInput({ inputType: 'insertText', data: '\\' }), true, 'typing a backslash arms completion');
assert.equal(isCommandTypingInput({ inputType: 'insertText', data: 'l' }), true, 'typing an ASCII command letter keeps completion armed');
assert.equal(isCommandTypingInput({ inputType: 'insertFromPaste', data: '\\left' }), false, 'pasting a command does not interrupt selection with suggestions');
assert.equal(isCommandTypingInput({ inputType: 'deleteContentBackward', data: null }), false, 'deleting near an existing command does not reopen suggestions');
assert.equal(isCommandTypingInput({ inputType: 'insertText', data: '{' }), false, 'typing a non-command character closes suggestions');
assert.equal(isCommandTypingInput(null), false, 'non-input caret movement never arms completion');

console.log('Formula completion trigger tests passed (6 assertions).');
