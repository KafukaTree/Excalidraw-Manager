import { createServer } from 'node:http';
import { mkdir, rm, writeFile } from 'node:fs/promises';
import { dirname, join, resolve, sep } from 'node:path';
import { pathToFileURL } from 'node:url';

const runtimeDirectory = resolve(process.argv[2] || join(import.meta.dirname, '..', 'runtime'));
const registryPath = join(runtimeDirectory, 'formula-provider-registry.mjs');
const { FormulaProviderRegistry } = await import(pathToFileURL(registryPath));
const root = resolve(join(import.meta.dirname, '..'));
const tempDirectory = resolve(join(root, '.tmp', `provider-registry-${process.pid}`));
if (!tempDirectory.startsWith(root + sep)) throw new Error('Unsafe provider test directory');

function assert(condition, message) {
  if (!condition) throw new Error(`Assertion failed: ${message}`);
}

async function readBody(request) {
  const chunks = [];
  for await (const chunk of request) chunks.push(chunk);
  return JSON.parse(Buffer.concat(chunks).toString('utf8') || '{}');
}

const mock = createServer(async (request, response) => {
  const url = new URL(request.url, 'http://localhost');
  response.setHeader('Content-Type', 'application/json');
  if (request.method === 'GET' && url.pathname === '/v1/info') {
    response.end(JSON.stringify({
      apiVersion: '1.0',
      provider: { id: 'mock-provider', name: 'Mock Provider', version: '1.0.0' },
      models: [{ id: 'mock-formula', name: 'Mock Formula', version: '1.0.0', modes: ['formula'], devices: ['cpu'] }],
      capabilities: { modes: ['formula'], inputMediaTypes: ['image/png'], outputFormats: ['latex'], multipleCandidates: true },
    }));
    return;
  }
  if (request.method === 'POST' && url.pathname === '/v1/warmup') {
    const body = await readBody(request);
    response.end(JSON.stringify({ requestId: body.requestId, status: 'ready', elapsedMs: 1 }));
    return;
  }
  if (request.method === 'POST' && url.pathname === '/v1/recognize') {
    const body = await readBody(request);
    response.end(JSON.stringify({
      requestId: body.requestId,
      mode: body.mode,
      candidates: [{ id: 'candidate-0', latex: 'E=mc^2', confidence: 0.99, formats: {}, warnings: [] }],
      provider: { id: 'mock-provider', version: '1.0.0' },
      model: { id: 'mock-formula', version: '1.0.0', revision: 'test' },
      timing: { totalMs: 1 },
    }));
    return;
  }
  response.statusCode = 404;
  response.end(JSON.stringify({ error: { code: 'NOT_FOUND', message: 'Not found' } }));
});

try {
  await mkdir(tempDirectory, { recursive: true });
  await new Promise((resolve, reject) => {
    mock.once('error', reject);
    mock.listen(0, '127.0.0.1', resolve);
  });
  const port = mock.address().port;
  const configPath = join(tempDirectory, 'providers.json');
  await writeFile(configPath, JSON.stringify({ providers: [{ id: 'mock', baseUrl: `http://127.0.0.1:${port}`, enabled: true }] }), 'utf8');

  const registry = new FormulaProviderRegistry(configPath);
  const providers = await registry.describe();
  assert(providers.length === 1 && providers[0].available, 'mock provider is discovered');
  assert(providers[0].info.provider.id === 'mock-provider', 'provider identity is preserved');

  const recognition = await registry.recognize({
    requestId: 'recognize-test',
    providerId: 'mock',
    mode: 'formula',
    input: { kind: 'image', mediaType: 'image/png', dataBase64: 'AA==' },
    options: { timeoutMs: 2000 },
  });
  assert(recognition.candidates[0].latex === 'E=mc^2', 'recognition result is proxied');
  assert(recognition.candidates[0].confidence === 0.99, 'candidate metadata is preserved');

  const warmup = await registry.warmup({ providerId: 'mock', requestId: 'warmup-test', timeoutMs: 2000 });
  assert(warmup.status === 'ready', 'warmup result is proxied');

  const unsafePath = join(tempDirectory, 'unsafe.json');
  await writeFile(unsafePath, JSON.stringify([{ id: 'unsafe', baseUrl: 'http://example.com:80' }]), 'utf8');
  const unsafeRegistry = new FormulaProviderRegistry(unsafePath);
  await unsafeRegistry.load();
  assert(unsafeRegistry.providers.length === 0, 'non-loopback provider is rejected');

  console.log('PASS provider discovery, recognition, warmup, and loopback restriction');
} finally {
  await new Promise((resolve) => mock.close(resolve));
  await rm(tempDirectory, { recursive: true, force: true });
}
