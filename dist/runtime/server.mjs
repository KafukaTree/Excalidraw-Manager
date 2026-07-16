import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { basename, dirname, extname, join, resolve } from 'node:path';

const args = process.argv.slice(2);
const filePath = resolve(args[0]);
const option = (name, fallback) => {
  const index = args.indexOf(name);
  return index >= 0 && index + 1 < args.length ? args[index + 1] : fallback;
};
const port = Number.parseInt(option('--port', '6417'), 10);
const theme = option('--theme', 'system');
const publicDir = resolve(option('--public-dir', ''));
const libraryPath = resolve(option('--library', 'shared.excalidrawlib'));
const requestedFormulaUrl = option('--formula-url', '');
const patchedMainPath = join(import.meta.dirname, 'main.js');
const formulaOverlayPath = join(import.meta.dirname, 'formula-overlay.mjs');
const emptyLibrary = { type: 'excalidrawlib', version: 2, source: 'ExcalidrawManager', libraryItems: [] };

const mimeTypes = {
  '.html': 'text/html; charset=utf-8', '.js': 'application/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8', '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml', '.woff2': 'font/woff2', '.woff': 'font/woff',
  '.ttf': 'font/ttf', '.png': 'image/png', '.ico': 'image/x-icon',
};

function loopbackFormulaUrl(value) {
  if (!value) return null;
  try {
    const url = new URL(value);
    if (url.protocol !== 'http:' || !['localhost', '127.0.0.1', '[::1]'].includes(url.hostname) || url.username || url.password)
      return null;
    return url.href;
  } catch {
    return null;
  }
}

const formulaEditorUrl = loopbackFormulaUrl(requestedFormulaUrl);

async function readLibrary() {
  try {
    const parsed = JSON.parse(await readFile(libraryPath, 'utf8'));
    return { ...emptyLibrary, ...parsed, libraryItems: Array.isArray(parsed.libraryItems) ? parsed.libraryItems : [] };
  } catch (error) {
    if (error.code !== 'ENOENT') console.error('Library read failed:', error.message);
    return emptyLibrary;
  }
}

async function saveLibrary(items) {
  await mkdir(dirname(libraryPath), { recursive: true });
  await writeFile(libraryPath, JSON.stringify({ ...emptyLibrary, libraryItems: items }, null, 2));
}

async function importOfficialLibrary(urlText) {
  const sourceUrl = new URL(urlText);
  if (sourceUrl.protocol !== 'https:' || sourceUrl.hostname !== 'libraries.excalidraw.com')
    throw new Error('Only official libraries.excalidraw.com library URLs are allowed');
  const response = await fetch(sourceUrl, { redirect: 'follow' });
  if (!response.ok) throw new Error(`Library download failed: HTTP ${response.status}`);
  const text = await response.text();
  if (text.length > 64 * 1024 * 1024) throw new Error('Library download exceeds 64 MiB');
  const incoming = JSON.parse(text);
  if (incoming.type !== 'excalidrawlib' || !Array.isArray(incoming.libraryItems))
    throw new Error('Downloaded file is not a valid Excalidraw library');
  const current = await readLibrary();
  const merged = new Map();
  let anonymous = 0;
  for (const item of [...current.libraryItems, ...incoming.libraryItems])
    merged.set(item?.id || `anonymous-${anonymous++}-${JSON.stringify(item).length}`, item);
  await saveLibrary([...merged.values()]);
  return { imported: incoming.libraryItems.length, total: merged.size };
}

async function readBody(req) {
  const chunks = [];
  let size = 0;
  for await (const chunk of req) {
    size += chunk.length;
    if (size > 64 * 1024 * 1024) throw new Error('Request body exceeds 64 MiB');
    chunks.push(chunk);
  }
  return Buffer.concat(chunks).toString('utf8');
}

const server = createServer(async (req, res) => {
  const url = new URL(req.url, 'http://localhost');
  try {
    if (req.method === 'GET' && url.pathname === '/') {
      const html = await readFile(join(publicDir, 'index.html'));
      res.writeHead(200, { 'Content-Type': mimeTypes['.html'] }); res.end(html); return;
    }
    if (req.method === 'GET' && url.pathname === '/favicon.ico') {
      const data = await readFile(join(publicDir, 'favicon.ico'));
      res.writeHead(200, { 'Content-Type': mimeTypes['.ico'] }); res.end(data); return;
    }
    if (req.method === 'GET' && url.pathname === '/meta') {
      res.writeHead(200, { 'Content-Type': mimeTypes['.json'] });
      res.end(JSON.stringify({ fileName: basename(filePath), theme, libraryPersistence: true, formulaEditorUrl })); return;
    }
    if (req.method === 'GET' && url.pathname === '/data') {
      const data = await readFile(filePath);
      res.writeHead(200, { 'Content-Type': mimeTypes['.json'] }); res.end(data); return;
    }
    if (req.method === 'POST' && url.pathname === '/save') {
      const body = await readBody(req); JSON.parse(body); await writeFile(filePath, body);
      res.writeHead(200, { 'Content-Type': mimeTypes['.json'] }); res.end('{"ok":true}'); return;
    }
    if (req.method === 'GET' && url.pathname === '/library') {
      res.writeHead(200, { 'Content-Type': mimeTypes['.json'], 'Cache-Control': 'no-store' });
      res.end(JSON.stringify(await readLibrary())); return;
    }
    if (req.method === 'POST' && url.pathname === '/library') {
      const parsed = JSON.parse(await readBody(req));
      if (!Array.isArray(parsed.libraryItems)) throw new Error('Invalid Excalidraw library payload');
      await saveLibrary(parsed.libraryItems);
      res.writeHead(200, { 'Content-Type': mimeTypes['.json'] }); res.end('{"ok":true}'); return;
    }
    if (req.method === 'POST' && url.pathname === '/library/import') {
      const parsed = JSON.parse(await readBody(req));
      const result = await importOfficialLibrary(parsed.url);
      res.writeHead(200, { 'Content-Type': mimeTypes['.json'] }); res.end(JSON.stringify({ ok: true, ...result })); return;
    }
    if (req.method === 'GET' && url.pathname.startsWith('/assets/')) {
      const relative = url.pathname.slice('/assets/'.length).replaceAll('..', '');
      const path = relative === 'main.js' ? patchedMainPath : join(publicDir, 'assets', relative);
      const data = await readFile(path);
      res.writeHead(200, { 'Content-Type': mimeTypes[extname(path)] || 'application/octet-stream' }); res.end(data); return;
    }
    if (req.method === 'GET' && url.pathname === '/formula-overlay.mjs') {
      const data = await readFile(formulaOverlayPath);
      res.writeHead(200, {
        'Content-Type': 'text/javascript; charset=utf-8',
        'Cache-Control': 'no-cache',
        'X-Content-Type-Options': 'nosniff',
      });
      res.end(data); return;
    }
    res.writeHead(404); res.end('Not found');
  } catch (error) {
    console.error(error);
    res.writeHead(500, { 'Content-Type': 'text/plain; charset=utf-8' }); res.end(error.message);
  }
});

server.on('error', (error) => { console.error(error); process.exit(1); });
server.listen(port, '127.0.0.1', () => {
  const address = server.address();
  console.log('Excalidraw Manager runtime');
  console.log(`Editing ${basename(filePath)}`);
  console.log(`http://localhost:${address.port}`);
  console.log(`Library ${libraryPath}`);
});
