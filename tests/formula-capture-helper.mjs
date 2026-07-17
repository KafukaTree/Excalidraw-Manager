import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { access, readFile, stat } from 'node:fs/promises';
import { constants } from 'node:fs';
import { promisify } from 'node:util';
import { fileURLToPath } from 'node:url';

const execFileAsync = promisify(execFile);
const helper = fileURLToPath(new URL('../runtime/FormulaCapture', import.meta.url));

if (process.platform !== 'darwin') {
  console.log('formula capture helper tests skipped outside macOS');
  process.exit(0);
}

await access(helper, constants.X_OK);
const helperInfo = await stat(helper);
assert.equal(helperInfo.isFile(), true);

const { stdout, stderr } = await execFileAsync(helper, ['--describe'], {
  encoding: 'utf8',
  timeout: 5_000,
});
assert.equal(stderr, '');
const description = JSON.parse(stdout);
assert.deepEqual(description, {
  name: 'Excalidraw Manager macOS Capture',
  version: 1,
  mimeType: 'image/png',
  inMemory: false,
  maximumDesktopPixels: 64_000_000,
  maximumSelectionPixels: 40_000_000,
  maximumPngBytes: 12_582_912,
});

const source = await readFile(helper, 'utf8');
assert.match(source, /umask 077/);
assert.match(source, /\/usr\/sbin\/screencapture -i -s -x -d -t png/);
assert.match(source, /mktemp -d/);
assert.match(source, /trap cleanup EXIT/);
assert.doesNotMatch(source, /screencapture[^\n]* -c(?: |$)/);

console.log('formula capture helper tests passed');
