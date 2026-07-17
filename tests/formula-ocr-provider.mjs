import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { createServer } from 'node:net';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const projectRoot = resolve(import.meta.dirname, '..');
const python = process.env.EXCALIDRAW_MANAGER_PYTHON || (process.platform === 'win32' ? 'python' : 'python3');
const probe = spawnSync(python, ['--version'], { encoding: 'utf8' });
if (probe.error || probe.status !== 0) {
  console.log(`SKIP local OCR provider contract: Python is unavailable (${python})`);
  process.exit(0);
}

const modelRoot = await mkdtemp(join(tmpdir(), 'excalidraw-manager-ocr-test-'));
const tokenVariable = 'EXCALIDRAW_MANAGER_OCR_TEST_TOKEN';
const token = 'test-token-'.padEnd(64, '0');
const port = await new Promise((resolvePort, rejectPort) => {
  const server = createServer();
  server.once('error', rejectPort);
  server.listen(0, '127.0.0.1', () => {
    const address = server.address();
    server.close((error) => error ? rejectPort(error) : resolvePort(address.port));
  });
});

const child = spawn(python, [
  'runtime/formula-ocr-provider/server.py',
  '--host', '127.0.0.1',
  '--port', String(port),
  '--model-root', modelRoot,
  '--parent-pid', String(process.pid),
  '--token-env', tokenVariable,
], {
  cwd: projectRoot,
  env: { ...process.env, [tokenVariable]: token },
  stdio: ['ignore', 'pipe', 'pipe'],
});

async function stopChild() {
  if (child.exitCode !== null || child.signalCode !== null) return;
  await new Promise((resolveStopped) => {
    const timeout = setTimeout(() => {
      child.kill('SIGKILL');
      resolveStopped();
    }, 2_000);
    child.once('exit', () => {
      clearTimeout(timeout);
      resolveStopped();
    });
    child.kill('SIGTERM');
  });
}

try {
  await new Promise((resolveReady, rejectReady) => {
    let output = '';
    let errors = '';
    const timeout = setTimeout(() => rejectReady(new Error(`OCR provider startup timed out.\n${output}\n${errors}`)), 8_000);
    child.stdout.setEncoding('utf8');
    child.stderr.setEncoding('utf8');
    child.stdout.on('data', (chunk) => {
      output += chunk;
      if (output.includes('Formula OCR provider ready at')) {
        clearTimeout(timeout);
        resolveReady();
      }
    });
    child.stderr.on('data', (chunk) => { errors += chunk; });
    child.once('error', rejectReady);
    child.once('exit', (code, signal) => {
      clearTimeout(timeout);
      rejectReady(new Error(`OCR provider exited during startup (${code ?? signal}).\n${errors}`));
    });
  });

  const baseUrl = `http://127.0.0.1:${port}`;
  const unauthorized = await fetch(`${baseUrl}/v1/health`);
  assert.equal(unauthorized.status, 401, 'health endpoint requires the ephemeral local token');

  const headers = { Authorization: `Bearer ${token}` };
  const infoResponse = await fetch(`${baseUrl}/v1/info`, { headers });
  assert.equal(infoResponse.status, 200);
  const info = await infoResponse.json();
  assert.equal(info.provider.id, 'org.excalidraw-manager.rapid-latex-ocr');
  assert.equal(info.models[0].id, 'rapid-latex-ocr-onnx');

  const healthResponse = await fetch(`${baseUrl}/v1/health`, { headers });
  assert.equal(healthResponse.status, 200);
  const health = await healthResponse.json();
  assert.equal(health.status, 'unavailable', 'an incomplete model root is never reported as healthy');
  assert.equal(health.ready, false);
  assert.ok(health.warnings.length > 0);

  console.log('PASS local OCR provider loopback, token, metadata, and incomplete-install checks');
} finally {
  await stopChild();
  await rm(modelRoot, { recursive: true, force: true });
}
