import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { readFile, stat } from 'node:fs/promises';
import { extname, join, resolve, sep } from 'node:path';
import { FormulaProviderRegistry, ProviderRegistryError } from './formula-provider-registry.mjs';

const APP_VERSION = '0.5.0';
const args = process.argv.slice(2);
const option = (name, fallback) => {
  const index = args.indexOf(name);
  return index >= 0 && index + 1 < args.length ? args[index + 1] : fallback;
};

const requestedHost = option('--host', '127.0.0.1');
if (!['127.0.0.1', 'localhost', '::1'].includes(requestedHost)) {
  throw new Error('The formula editor may only listen on the local computer');
}

const host = requestedHost === 'localhost' ? '127.0.0.1' : requestedHost;
const port = Number.parseInt(option('--port', '0'), 10);
if (!Number.isInteger(port) || port < 0 || port > 65535) throw new Error('Invalid --port value');

const assetsRoot = resolve(option('--assets', join(import.meta.dirname, 'formula-editor')));
const parentPid = Number.parseInt(option('--parent-pid', '0'), 10);
const defaultLanguage = option('--lang', 'en').toLowerCase().startsWith('zh') ? 'zh-CN' : 'en';
const defaultProviderConfig = process.env.LOCALAPPDATA
  ? join(process.env.LOCALAPPDATA, 'ExcalidrawManager', 'formula-providers.json')
  : null;
const providerConfig = option('--provider-config', defaultProviderConfig);
const providers = new FormulaProviderRegistry(providerConfig);
const captureHelperPath = resolve(option('--capture-helper', join(import.meta.dirname, 'FormulaCapture.exe')));
const captureTimeoutMilliseconds = 120_000;
const maximumCaptureBytes = 12 * 1024 * 1024;
const maximumCaptureOutputBytes = 18 * 1024 * 1024;
const maximumCaptureErrorBytes = 64 * 1024;
let captureActive = false;
let captureChild = null;

const mimeTypes = {
  '.css': 'text/css; charset=utf-8',
  '.gif': 'image/gif',
  '.html': 'text/html; charset=utf-8',
  '.ico': 'image/x-icon',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.png': 'image/png',
  '.svg': 'image/svg+xml; charset=utf-8',
  '.ttf': 'font/ttf',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
};

const securityHeaders = {
  'Content-Security-Policy': "default-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors http://localhost:* http://127.0.0.1:*; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self' data:; connect-src 'self'; worker-src 'self' blob:",
  'Cross-Origin-Opener-Policy': 'same-origin',
  'Permissions-Policy': 'camera=(), geolocation=(), microphone=()',
  'Referrer-Policy': 'no-referrer',
  'X-Content-Type-Options': 'nosniff',
  'X-Formula-Editor-Version': APP_VERSION,
};

function loopbackUrl(value) {
  try {
    const url = new URL(value);
    return ['127.0.0.1', 'localhost', '[::1]'].includes(url.hostname) ? url : null;
  } catch {
    return null;
  }
}

function requestIsLocal(req) {
  return Boolean(loopbackUrl(`http://${req.headers.host || ''}`));
}

function originIsTrusted(req) {
  const origin = req.headers.origin;
  if (!origin) return true;
  const originUrl = loopbackUrl(origin);
  const address = server.address();
  return Boolean(originUrl && address && originUrl.protocol === 'http:' && originUrl.port === String(address.port));
}

function send(res, status, body, headers = {}) {
  res.writeHead(status, { ...securityHeaders, ...headers });
  res.end(body);
}

function sendJson(res, status, value) {
  send(res, status, JSON.stringify(value), {
    'Cache-Control': 'no-store',
    'Content-Type': mimeTypes['.json'],
  });
}

async function readJson(req, maximumBytes = 20 * 1024 * 1024) {
  const chunks = [];
  let size = 0;
  for await (const chunk of req) {
    size += chunk.length;
    if (size > maximumBytes) {
      const error = new Error('Request body exceeds the 20 MiB limit');
      error.statusCode = 413;
      throw error;
    }
    chunks.push(chunk);
  }
  const text = Buffer.concat(chunks).toString('utf8');
  return text ? JSON.parse(text) : {};
}

class CaptureError extends Error {
  constructor(code, message, statusCode) {
    super(message);
    this.name = 'CaptureError';
    this.code = code;
    this.statusCode = statusCode;
  }
}

function createClientLifetime(req, res) {
  const controller = new AbortController();
  const abort = () => {
    if (!controller.signal.aborted) controller.abort();
  };
  const close = () => {
    if (!res.writableEnded) abort();
  };
  req.once('aborted', abort);
  res.once('close', close);
  if (req.aborted || res.destroyed) abort();
  return {
    signal: controller.signal,
    cleanup() {
      req.off('aborted', abort);
      res.off('close', close);
    },
  };
}

function clientAbortError() {
  return new CaptureError('REQUEST_ABORTED', 'The client disconnected', 499);
}

function validateCapturedImage(payload) {
  if (!payload || typeof payload !== 'object') {
    throw new CaptureError('CAPTURE_FAILED', 'Screen capture returned an invalid response', 502);
  }
  if (payload.cancelled === true) {
    throw new CaptureError('CAPTURE_CANCELLED', 'Screen capture was cancelled', 409);
  }
  if (payload.cancelled !== false || payload.mimeType !== 'image/png' || typeof payload.image !== 'string') {
    throw new CaptureError('CAPTURE_FAILED', 'Screen capture returned an invalid image', 502);
  }
  if (!Number.isInteger(payload.width) || !Number.isInteger(payload.height) ||
      payload.width < 2 || payload.height < 2 || payload.width > 32_768 || payload.height > 32_768 ||
      payload.width * payload.height > 40_000_000) {
    throw new CaptureError('CAPTURE_FAILED', 'Screen capture dimensions are invalid', 502);
  }
  if (payload.image.length === 0 || payload.image.length % 4 !== 0 ||
      !/^[A-Za-z0-9+/]*={0,2}$/.test(payload.image)) {
    throw new CaptureError('CAPTURE_FAILED', 'Screen capture returned invalid base64 data', 502);
  }

  const decoded = Buffer.from(payload.image, 'base64');
  if (decoded.length > maximumCaptureBytes) {
    throw new CaptureError('CAPTURE_TOO_LARGE', 'Captured image exceeds the 12 MiB OCR limit', 413);
  }
  if (decoded.toString('base64') !== payload.image || decoded.length < 24 ||
      !decoded.subarray(0, 8).equals(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])) ||
      decoded.readUInt32BE(16) !== payload.width || decoded.readUInt32BE(20) !== payload.height) {
    throw new CaptureError('CAPTURE_FAILED', 'Screen capture returned an invalid PNG image', 502);
  }

  return {
    image: {
      kind: 'image',
      mediaType: 'image/png',
      dataBase64: payload.image,
      width: payload.width,
      height: payload.height,
    },
  };
}

async function runScreenCapture(signal = null) {
  if (captureActive) {
    throw new CaptureError('CAPTURE_BUSY', 'Another screen capture is already in progress', 409);
  }
  if (signal?.aborted) throw clientAbortError();
  captureActive = true;

  try {
    try {
      const helperInfo = await stat(captureHelperPath);
      if (!helperInfo.isFile()) throw new Error('Capture helper is not a file');
    } catch {
      throw new CaptureError('CAPTURE_UNAVAILABLE', 'The screen capture helper is not installed', 503);
    }
    if (signal?.aborted) throw clientAbortError();

    return await new Promise((resolveCapture, rejectCapture) => {
      let settled = false;
      let terminationError = null;
      let timeout = null;
      let outputSize = 0;
      let errorSize = 0;
      const output = [];
      const errors = [];
      let child;

      const onAbort = () => requestStop(clientAbortError());

      const finish = (error, value) => {
        if (settled) return;
        settled = true;
        if (timeout) clearTimeout(timeout);
        signal?.removeEventListener('abort', onAbort);
        if (captureChild === child) captureChild = null;
        if (error) rejectCapture(error);
        else resolveCapture(value);
      };

      const requestStop = (error) => {
        if (settled) return;
        if (!terminationError) terminationError = error;
        // Do not release captureActive here. The close event proves the helper and
        // its stdout/stderr handles are gone before another picker may start.
        if (!child || child.exitCode !== null) return;
        try { child.kill(); } catch { /* The close/error events settle the request. */ }
      };

      try {
        child = spawn(captureHelperPath, ['--parent-pid', String(process.pid), '--lang', defaultLanguage], {
          cwd: import.meta.dirname,
          shell: false,
          stdio: ['ignore', 'pipe', 'pipe'],
          windowsHide: false,
        });
      } catch {
        finish(new CaptureError('CAPTURE_UNAVAILABLE', 'The screen capture helper could not be started', 503));
        return;
      }
      captureChild = child;

      child.stdout.on('data', (chunk) => {
        if (settled) return;
        outputSize += chunk.length;
        if (outputSize > maximumCaptureOutputBytes) {
          requestStop(new CaptureError('CAPTURE_TOO_LARGE', 'Screen capture output exceeds the allowed size', 413));
          return;
        }
        output.push(chunk);
      });

      child.stderr.on('data', (chunk) => {
        if (settled || errorSize >= maximumCaptureErrorBytes) return;
        const retained = chunk.subarray(0, maximumCaptureErrorBytes - errorSize);
        errorSize += retained.length;
        errors.push(retained);
      });

      child.once('error', () => {
        if (!terminationError) {
          terminationError = new CaptureError('CAPTURE_UNAVAILABLE', 'The screen capture helper could not be started', 503);
        }
      });

      child.once('close', (code) => {
        if (settled) return;
        if (terminationError) {
          finish(terminationError);
          return;
        }
        if (code !== 0) {
          const diagnostic = Buffer.concat(errors).toString('utf8').trim();
          if (diagnostic) console.error(`Formula capture helper failed: ${diagnostic}`);
          try {
            const payload = JSON.parse(Buffer.concat(output).toString('utf8'));
            if (['CAPTURE_DESKTOP_TOO_LARGE', 'CAPTURE_SELECTION_TOO_LARGE', 'CAPTURE_OUTPUT_TOO_LARGE'].includes(payload?.error)) {
              finish(new CaptureError('CAPTURE_TOO_LARGE', 'Captured image exceeds safe OCR limits', 413));
              return;
            }
          } catch { /* Fall through to the generic helper failure. */ }
          finish(new CaptureError('CAPTURE_FAILED', 'Screen capture failed', 500));
          return;
        }
        try {
          const payload = JSON.parse(Buffer.concat(output).toString('utf8'));
          finish(null, validateCapturedImage(payload));
        } catch (error) {
          finish(error instanceof CaptureError
            ? error
            : new CaptureError('CAPTURE_FAILED', 'Screen capture returned malformed output', 502));
        }
      });

      signal?.addEventListener('abort', onAbort, { once: true });
      if (signal?.aborted) onAbort();
      timeout = setTimeout(() => {
        requestStop(new CaptureError('CAPTURE_TIMEOUT', 'Screen capture timed out', 408));
      }, captureTimeoutMilliseconds);
      timeout.unref();
    });
  } finally {
    captureActive = false;
  }
}

function safeStaticPath(pathname) {
  let decoded;
  try {
    decoded = decodeURIComponent(pathname);
  } catch {
    return null;
  }
  if (decoded.includes('\\') || decoded.includes('\0')) return null;
  const relative = decoded === '/' ? 'index.html' : decoded.replace(/^\/+/, '');
  const candidate = resolve(assetsRoot, relative);
  return candidate === assetsRoot || candidate.startsWith(assetsRoot + sep) ? candidate : null;
}

async function serveStatic(url, res) {
  const filePath = safeStaticPath(url.pathname);
  if (!filePath) {
    send(res, 400, 'Invalid path', { 'Content-Type': 'text/plain; charset=utf-8' });
    return;
  }
  const info = await stat(filePath);
  const resolvedFile = info.isDirectory() ? join(filePath, 'index.html') : filePath;
  const data = await readFile(resolvedFile);
  const isVendor = resolvedFile.startsWith(join(assetsRoot, 'vendor') + sep);
  send(res, 200, data, {
    'Cache-Control': isVendor ? 'public, max-age=31536000, immutable' : 'no-cache',
    'Content-Type': mimeTypes[extname(resolvedFile).toLowerCase()] || 'application/octet-stream',
  });
}

const server = createServer(async (req, res) => {
  const url = new URL(req.url || '/', `http://${req.headers.host || 'localhost'}`);
  try {
    if (!requestIsLocal(req)) {
      sendJson(res, 421, { error: 'HOST_NOT_ALLOWED', message: 'Formula editor only accepts loopback host names' });
      return;
    }
    if (req.method === 'POST' && !originIsTrusted(req)) {
      sendJson(res, 403, { error: 'ORIGIN_NOT_ALLOWED', message: 'Cross-origin requests are not allowed' });
      return;
    }
    if (req.method === 'POST' && url.pathname.startsWith('/api/') && !String(req.headers['content-type'] || '').toLowerCase().startsWith('application/json')) {
      sendJson(res, 415, { error: 'UNSUPPORTED_MEDIA_TYPE', message: 'API requests must use application/json' });
      return;
    }
    if (req.method === 'GET' && url.pathname === '/api/status') {
      await providers.load();
      sendJson(res, 200, {
        app: 'Excalidraw Manager Formula Editor',
        version: APP_VERSION,
        language: defaultLanguage,
        offline: true,
        ocrAvailable: providers.providers.length > 0,
        providers: providers.providers.map((provider) => provider.id),
      });
      return;
    }

    if (req.method === 'GET' && url.pathname === '/api/models') {
      const availableProviders = await providers.describe(url.searchParams.get('refresh') === '1');
      sendJson(res, 200, {
        activeProvider: null,
        providers: availableProviders,
        contract: 'formula-model-api-v1',
        message: defaultLanguage === 'zh-CN'
          ? '尚未安装识别模型；键盘编辑、渲染与导出不受影响。'
          : 'No recognition model is installed. Editing, rendering and export still work.',
      });
      return;
    }

    if (req.method === 'POST' && url.pathname === '/api/capture') {
      const lifetime = createClientLifetime(req, res);
      try {
        await readJson(req, 64 * 1024);
        if (lifetime.signal.aborted) throw clientAbortError();
        const result = await runScreenCapture(lifetime.signal);
        if (lifetime.signal.aborted) throw clientAbortError();
        sendJson(res, 200, result);
      } finally {
        lifetime.cleanup();
      }
      return;
    }

    if (req.method === 'POST' && url.pathname === '/api/recognize') {
      const lifetime = createClientLifetime(req, res);
      try {
        const request = await readJson(req);
        if (lifetime.signal.aborted) throw new ProviderRegistryError('REQUEST_ABORTED', 'The client disconnected', 499);
        const result = await providers.recognize(request, lifetime.signal);
        if (lifetime.signal.aborted) throw new ProviderRegistryError('REQUEST_ABORTED', 'The client disconnected', 499);
        sendJson(res, 200, result);
      } finally {
        lifetime.cleanup();
      }
      return;
    }

    if (req.method === 'POST' && url.pathname === '/api/warmup') {
      const lifetime = createClientLifetime(req, res);
      try {
        const request = await readJson(req, 1024 * 1024);
        if (lifetime.signal.aborted) throw new ProviderRegistryError('REQUEST_ABORTED', 'The client disconnected', 499);
        const result = await providers.warmup(request, lifetime.signal);
        if (lifetime.signal.aborted) throw new ProviderRegistryError('REQUEST_ABORTED', 'The client disconnected', 499);
        sendJson(res, 200, result);
      } finally {
        lifetime.cleanup();
      }
      return;
    }

    if (req.method === 'GET') {
      await serveStatic(url, res);
      return;
    }

    send(res, 405, 'Method not allowed', {
      Allow: 'GET, POST',
      'Content-Type': 'text/plain; charset=utf-8',
    });
  } catch (error) {
    if (error?.code === 'REQUEST_ABORTED' || res.destroyed || res.writableEnded) return;
    if (error?.code === 'ENOENT' || error?.code === 'ENOTDIR') {
      send(res, 404, 'Not found', { 'Content-Type': 'text/plain; charset=utf-8' });
      return;
    }
    const status = error instanceof SyntaxError ? 400 : (error.statusCode || 500);
    if (status >= 500) console.error(error);
    if (error instanceof CaptureError) {
      sendJson(res, status, {
        error: error.code,
        message: error.message,
      });
      return;
    }
    if (error instanceof ProviderRegistryError) {
      sendJson(res, status, {
        error: error.code,
        message: defaultLanguage === 'zh-CN' && error.code === 'MODEL_UNAVAILABLE'
          ? '尚未配置本地识别模型。请先安装并选择兼容的模型提供器。'
          : error.message,
        retryable: error.retryable,
        details: error.details,
      });
      return;
    }
    sendJson(res, status, {
      error: status === 400 ? 'INVALID_REQUEST' : 'INTERNAL_ERROR',
      message: status === 500 ? 'Formula editor server error' : error.message,
    });
  }
});

server.on('error', (error) => {
  console.error(error);
  process.exitCode = 1;
});

server.on('close', () => {
  if (captureChild) {
    try { captureChild.kill(); } catch { /* Process may already be closing. */ }
  }
});

server.listen(port, host, () => {
  const address = server.address();
  const displayHost = address.family === 'IPv6' ? `[${address.address}]` : address.address;
  console.log(`Formula editor ready at http://${displayHost}:${address.port}/`);
});

function closeForParentExit() {
  if (!Number.isInteger(parentPid) || parentPid <= 0 || parentPid === process.pid) return;
  const timer = setInterval(() => {
    try {
      process.kill(parentPid, 0);
    } catch {
      clearInterval(timer);
      server.close(() => process.exit(0));
      setTimeout(() => process.exit(0), 1500).unref();
    }
  }, 3000);
  timer.unref();
}

closeForParentExit();
