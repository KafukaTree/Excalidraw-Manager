import assert from 'node:assert/strict';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const testDirectory = fileURLToPath(new URL('.', import.meta.url));
const editorDirectory = path.resolve(process.argv[2] || path.join(testDirectory, '..', 'runtime', 'formula-editor'));
const { latexCommands, mathfieldInsertion, quickGroups } = await import(pathToFileURL(path.join(editorDirectory, 'templates.mjs')));

assert.deepEqual(
  mathfieldInsertion('\\frac{◊}{}'),
  { latex: '\\frac{#0}{#?}', selectionMode: 'placeholder' },
  'visual insertion should use the current MathLive selection and expose the denominator as the next placeholder',
);
assert.deepEqual(
  mathfieldInsertion('\\infty'),
  { latex: '\\infty', selectionMode: 'after' },
  'plain symbols should leave the visual caret after the inserted item',
);

const quickNthRoot = quickGroups.flatMap((group) => group.items).find((item) => item[3] === 'Nth root');
const commandNthRoot = latexCommands.find((item) => item[4] === 'Nth root');
assert.equal(quickNthRoot?.[0], '\\sqrt[◊]{}', 'quick nth root should start in the root index');
assert.equal(commandNthRoot?.[1], '\\sqrt[◊]{}', 'command nth root should start in the root index');
assert.deepEqual(
  mathfieldInsertion(quickNthRoot[0]),
  { latex: '\\sqrt[#0]{#?}', selectionMode: 'placeholder' },
  'visual nth root should offer the index first and the radicand second',
);

console.log('Formula template insertion tests passed (5 assertions).');
