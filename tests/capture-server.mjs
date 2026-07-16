import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { request as httpRequest } from 'node:http';
import { createServer } from 'node:net';
import { join, resolve } from 'node:path';
import { tmpdir } from 'node:os';

const runtimeDir = resolve(process.argv[2] || 'runtime');
const captureStub = resolve(process.argv[3] || 'FormulaCaptureStub.exe');
const formulaServer = join(runtimeDir, 'formula-server.mjs');
const assets = join(runtimeDir, 'formula-editor');

function delay(milliseconds) {
  return new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds));
}

async function reservePort() {
  const listener = createServer();
  listener.listen(0, '127.0.0.1');
  await once(listener, 'listening');
  const { port } = listener.address();
  listener.close();
  await once(listener, 'close');
  return port;
}

async function startFormulaServer(captureHelper, mode) {
  const port = await reservePort();
  const providerConfig = join(tmpdir(), `excalidraw-manager-capture-test-${process.pid}-${port}.json`);
  const child = spawn(process.execPath, [
    formulaServer,
    '--host', '127.0.0.1',
    '--port', String(port),
    '--assets', assets,
    '--provider-config', providerConfig,
    '--capture-helper', captureHelper,
  ], {
    env: { ...process.env, FORMULA_CAPTURE_STUB_MODE: mode },
    shell: false,
    stdio: ['ignore', 'pipe', 'pipe'],
    windowsHide: true,
  });
  let diagnostics = '';
  child.stderr.on('data', (chunk) => {
    if (diagnostics.length < 32_768) diagnostics += chunk.toString('utf8');
  });
  child.stdout.resume();

  const baseUrl = `http://127.0.0.1:${port}`;
  const deadline = Date.now() + 10_000;
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`Formula server exited early (${child.exitCode}): ${diagnostics}`);
    try {
      const response = await fetch(`${baseUrl}/api/status`);
      if (response.status === 200) return { child, baseUrl, diagnostics: () => diagnostics };
    } catch { /* Server has not started listening yet. */ }
    await delay(50);
  }
  child.kill();
  throw new Error(`Formula server startup timed out: ${diagnostics}`);
}

async function stopFormulaServer(instance) {
  if (instance.child.exitCode !== null) return;
  const closed = once(instance.child, 'close');
  instance.child.kill();
  await Promise.race([closed, delay(3000)]);
}

async function postCapture(baseUrl) {
  return fetch(`${baseUrl}/api/capture`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: '{}',
  });
}

function startCaptureRequest(baseUrl) {
  const url = new URL('/api/capture', baseUrl);
  const request = httpRequest(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
  }, (response) => response.resume());
  request.on('error', () => { /* Destroying the client socket is the behavior under test. */ });
  request.end('{}');
  return request;
}

async function withServer(helper, mode, action) {
  const instance = await startFormulaServer(helper, mode);
  try {
    await action(instance.baseUrl);
  } finally {
    await stopFormulaServer(instance);
  }
}

const missingHelper = join(tmpdir(), `missing-formula-capture-${process.pid}.exe`);
await withServer(missingHelper, 'cancel', async (baseUrl) => {
  const response = await postCapture(baseUrl);
  assert.equal(response.status, 503);
  assert.equal((await response.json()).error, 'CAPTURE_UNAVAILABLE');
});

await withServer(captureStub, 'cancel', async (baseUrl) => {
  const response = await postCapture(baseUrl);
  assert.equal(response.status, 409);
  assert.equal((await response.json()).error, 'CAPTURE_CANCELLED');
});

await withServer(captureStub, 'success', async (baseUrl) => {
  const response = await postCapture(baseUrl);
  assert.equal(response.status, 200);
  const body = await response.json();
  assert.deepEqual(
    {
      kind: body.image.kind,
      mediaType: body.image.mediaType,
      width: body.image.width,
      height: body.image.height,
    },
    { kind: 'image', mediaType: 'image/png', width: 2, height: 2 },
  );
  assert.match(body.image.dataBase64, /^[A-Za-z0-9+/]+={0,2}$/);
});

await withServer(captureStub, 'too-large', async (baseUrl) => {
  const response = await postCapture(baseUrl);
  assert.equal(response.status, 413);
  assert.equal((await response.json()).error, 'CAPTURE_TOO_LARGE');
});

await withServer(captureStub, 'wait', async (baseUrl) => {
  const firstRequest = postCapture(baseUrl);
  await delay(150);
  const busyResponse = await postCapture(baseUrl);
  assert.equal(busyResponse.status, 409);
  assert.equal((await busyResponse.json()).error, 'CAPTURE_BUSY');
  const firstResponse = await firstRequest;
  assert.equal(firstResponse.status, 409);
  assert.equal((await firstResponse.json()).error, 'CAPTURE_CANCELLED');
});

await withServer(captureStub, 'wait', async (baseUrl) => {
  const disconnectedRequest = startCaptureRequest(baseUrl);
  await delay(150);
  const busyBeforeDisconnect = await postCapture(baseUrl);
  assert.equal(busyBeforeDisconnect.status, 409);
  assert.equal((await busyBeforeDisconnect.json()).error, 'CAPTURE_BUSY');
  disconnectedRequest.destroy();
  await delay(200);
  const replacement = await postCapture(baseUrl);
  assert.equal(replacement.status, 409);
  assert.equal((await replacement.json()).error, 'CAPTURE_CANCELLED');
});

console.log('PASS capture endpoint missing-helper, cancellation, limits, PNG contract, concurrency, and client-disconnect tests');
