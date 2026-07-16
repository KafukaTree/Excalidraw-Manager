import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { extname, join, resolve, sep } from 'node:path';
import { FormulaProviderRegistry, ProviderRegistryError } from './formula-provider-registry.mjs';

const APP_VERSION = '0.3.0';
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
  'Content-Security-Policy': "default-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self' data:; connect-src 'self'; worker-src 'self' blob:",
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

async function readJson(req, maximumBytes = 16 * 1024 * 1024) {
  const chunks = [];
  let size = 0;
  for await (const chunk of req) {
    size += chunk.length;
    if (size > maximumBytes) {
      const error = new Error('Request body exceeds the 16 MiB limit');
      error.statusCode = 413;
      throw error;
    }
    chunks.push(chunk);
  }
  const text = Buffer.concat(chunks).toString('utf8');
  return text ? JSON.parse(text) : {};
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

    if (req.method === 'POST' && url.pathname === '/api/recognize') {
      const result = await providers.recognize(await readJson(req));
      sendJson(res, 200, result);
      return;
    }

    if (req.method === 'POST' && url.pathname === '/api/warmup') {
      const result = await providers.warmup(await readJson(req, 1024 * 1024));
      sendJson(res, 200, result);
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
    if (error?.code === 'ENOENT' || error?.code === 'ENOTDIR') {
      send(res, 404, 'Not found', { 'Content-Type': 'text/plain; charset=utf-8' });
      return;
    }
    const status = error instanceof SyntaxError ? 400 : (error.statusCode || 500);
    if (status >= 500) console.error(error);
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
