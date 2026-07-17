import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const projectRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const temporaryRoot = await mkdtemp(join(tmpdir(), 'excalidraw-manager-test-'));
const children = [];

function startService(arguments_, readyPattern) {
  const child = spawn(process.execPath, arguments_, {
    cwd: projectRoot,
    env: process.env,
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  children.push(child);

  return new Promise((resolveReady, rejectReady) => {
    let output = '';
    let errors = '';
    const timeout = setTimeout(() => {
      rejectReady(new Error(`Service startup timed out.\n${output}\n${errors}`));
    }, 10_000);

    const inspect = () => {
      const match = output.match(readyPattern);
      if (!match) return;
      clearTimeout(timeout);
      resolveReady({ child, url: match[1] });
    };

    child.stdout.setEncoding('utf8');
    child.stderr.setEncoding('utf8');
    child.stdout.on('data', (chunk) => {
      output += chunk;
      inspect();
    });
    child.stderr.on('data', (chunk) => {
      errors += chunk;
    });
    child.once('exit', (code, signal) => {
      clearTimeout(timeout);
      rejectReady(new Error(`Service exited during startup (${code ?? signal}).\n${output}\n${errors}`));
    });
    child.once('error', rejectReady);
  });
}

async function stopChildren() {
  await Promise.all(children.map((child) => new Promise((resolveStopped) => {
    if (child.exitCode !== null || child.signalCode !== null) {
      resolveStopped();
      return;
    }
    const timeout = setTimeout(() => {
      child.kill('SIGKILL');
      resolveStopped();
    }, 2_000);
    child.once('exit', () => {
      clearTimeout(timeout);
      resolveStopped();
    });
    child.kill('SIGTERM');
  })));
}

try {
  const boardPath = join(temporaryRoot, 'cross-platform.excalidraw');
  const libraryPath = join(temporaryRoot, 'shared.excalidrawlib');
  const providerPath = join(temporaryRoot, 'formula-providers.json');
  const initialBoard = {
    type: 'excalidraw',
    version: 2,
    source: 'https://excalidraw.com',
    elements: [],
    appState: { gridSize: null, viewBackgroundColor: '#ffffff' },
    files: {},
  };
  await writeFile(boardPath, `${JSON.stringify(initialBoard)}\n`);

  const formula = await startService([
    'runtime/formula-server.mjs',
    '--host', '127.0.0.1',
    '--port', '0',
    '--assets', 'runtime/formula-editor',
    '--provider-config', providerPath,
    '--lang', 'zh-CN',
  ], /Formula editor ready at (http:\/\/[^\s]+\/)/);

  const formulaStatus = await fetch(new URL('/api/status', formula.url)).then((response) => {
    assert.equal(response.status, 200);
    return response.json();
  });
  assert.equal(formulaStatus.version, '0.5.0');
  assert.equal(formulaStatus.language, 'zh-CN');
  assert.equal(formulaStatus.offline, true);

  const editorHtml = await fetch(formula.url).then((response) => {
    assert.equal(response.status, 200);
    return response.text();
  });
  assert.match(editorHtml, /Excalidraw Manager/);

  const board = await startService([
    'runtime/server.mjs', boardPath,
    '--port', '0',
    '--theme', 'dark',
    '--public-dir', 'node_modules/excalidraw-edit/src/public',
    '--library', libraryPath,
    '--formula-url', formula.url,
  ], /(http:\/\/localhost:\d+)/);

  const metadata = await fetch(new URL('/meta', board.url)).then((response) => {
    assert.equal(response.status, 200);
    return response.json();
  });
  assert.equal(metadata.fileName, 'cross-platform.excalidraw');
  assert.equal(metadata.theme, 'dark');
  assert.equal(metadata.formulaEditorUrl, formula.url);

  const loadedBoard = await fetch(new URL('/data', board.url)).then((response) => response.json());
  assert.deepEqual(loadedBoard.elements, []);

  const changedBoard = { ...initialBoard, appState: { ...initialBoard.appState, zoom: 1.25 } };
  const saveResponse = await fetch(new URL('/save', board.url), {
    method: 'POST',
    body: JSON.stringify(changedBoard),
  });
  assert.equal(saveResponse.status, 200);
  assert.deepEqual(JSON.parse(await readFile(boardPath, 'utf8')), changedBoard);

  const library = await fetch(new URL('/library', board.url)).then((response) => response.json());
  assert.equal(library.type, 'excalidrawlib');
  assert.deepEqual(library.libraryItems, []);

  console.log('Runtime services: formula editor, board load/save, metadata, and shared library passed');
} finally {
  await stopChildren();
  await rm(temporaryRoot, { recursive: true, force: true });
}
