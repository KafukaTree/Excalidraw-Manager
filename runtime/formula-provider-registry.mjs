import { readFile } from 'node:fs/promises';

const LOOPBACK_HOSTS = new Set(['127.0.0.1', 'localhost', '[::1]']);
const MAX_PROVIDER_RESPONSE = 16 * 1024 * 1024;
const MAX_FORMULA_CANDIDATES = 10;
const MAX_LATEX_CHARACTERS = 16_384;

export class ProviderRegistryError extends Error {
  constructor(code, message, statusCode = 500, retryable = false, details = null) {
    super(message);
    this.name = 'ProviderRegistryError';
    this.code = code;
    this.statusCode = statusCode;
    this.retryable = retryable;
    this.details = details;
  }
}

function providerUrl(baseUrl, pathname) {
  let url;
  try {
    url = new URL(baseUrl);
  } catch {
    throw new ProviderRegistryError('PROVIDER_CONFIG_INVALID', 'Provider baseUrl is invalid');
  }
  if (url.protocol !== 'http:' || !LOOPBACK_HOSTS.has(url.hostname) || url.username || url.password) {
    throw new ProviderRegistryError('PROVIDER_CONFIG_INVALID', 'Providers must use an unauthenticated loopback HTTP URL');
  }
  url.pathname = `${url.pathname.replace(/\/$/, '')}${pathname}`;
  url.search = '';
  url.hash = '';
  return url;
}

async function responseJson(response) {
  const declaredLength = Number(response.headers.get('content-length') || 0);
  if (declaredLength > MAX_PROVIDER_RESPONSE) {
    throw new ProviderRegistryError('PROVIDER_RESPONSE_TOO_LARGE', 'Provider response exceeds 16 MiB', 502);
  }
  const reader = response.body?.getReader();
  if (!reader) return {};
  const chunks = [];
  let size = 0;
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    size += value.byteLength;
    if (size > MAX_PROVIDER_RESPONSE) {
      await reader.cancel();
      throw new ProviderRegistryError('PROVIDER_RESPONSE_TOO_LARGE', 'Provider response exceeds 16 MiB', 502);
    }
    chunks.push(value);
  }
  const bytes = new Uint8Array(size);
  let offset = 0;
  for (const chunk of chunks) {
    bytes.set(chunk, offset);
    offset += chunk.byteLength;
  }
  try {
    return JSON.parse(new TextDecoder().decode(bytes));
  } catch {
    throw new ProviderRegistryError('PROVIDER_RESPONSE_INVALID', 'Provider returned invalid JSON', 502);
  }
}

function providerHeaders(provider) {
  const headers = { Accept: 'application/json', 'Content-Type': 'application/json' };
  if (provider.tokenEnv) {
    const token = process.env[provider.tokenEnv];
    if (token) headers.Authorization = `Bearer ${token}`;
  }
  return headers;
}

async function requestProvider(provider, pathname, options = {}, timeoutMs = 5000) {
  const controller = new AbortController();
  const externalSignal = options.signal || null;
  const requestOptions = { ...options };
  delete requestOptions.signal;
  let timedOut = false;
  const abortFromClient = () => controller.abort();
  if (externalSignal?.aborted) abortFromClient();
  else externalSignal?.addEventListener('abort', abortFromClient, { once: true });
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, timeoutMs);
  timer.unref?.();
  try {
    const response = await fetch(providerUrl(provider.baseUrl, pathname), {
      ...requestOptions,
      headers: { ...providerHeaders(provider), ...(requestOptions.headers || {}) },
      redirect: 'error',
      signal: controller.signal,
    });
    const body = await responseJson(response);
    return { status: response.status, ok: response.ok, body };
  } catch (error) {
    if (error instanceof ProviderRegistryError) throw error;
    if (externalSignal?.aborted) {
      throw new ProviderRegistryError('REQUEST_ABORTED', 'The client disconnected', 499, false);
    }
    if (timedOut || error.name === 'AbortError') {
      throw new ProviderRegistryError('PROVIDER_TIMEOUT', 'Local provider did not respond in time', 504, true);
    }
    throw new ProviderRegistryError('PROVIDER_UNAVAILABLE', 'Local provider is unavailable', 503, true);
  } finally {
    clearTimeout(timer);
    externalSignal?.removeEventListener('abort', abortFromClient);
  }
}

function validateProvider(entry) {
  if (!entry || typeof entry !== 'object' || typeof entry.id !== 'string' || !entry.id.trim()) {
    throw new ProviderRegistryError('PROVIDER_CONFIG_INVALID', 'Every provider needs a stable id');
  }
  providerUrl(entry.baseUrl, '/v1/info');
  if (entry.tokenEnv !== undefined && !/^[A-Za-z_][A-Za-z0-9_]*$/.test(entry.tokenEnv)) {
    throw new ProviderRegistryError('PROVIDER_CONFIG_INVALID', `Provider ${entry.id} has an invalid tokenEnv`);
  }
  return {
    id: entry.id.trim(),
    name: typeof entry.name === 'string' ? entry.name.trim() : entry.id.trim(),
    baseUrl: entry.baseUrl,
    tokenEnv: entry.tokenEnv || null,
    enabled: entry.enabled !== false,
  };
}

export class FormulaProviderRegistry {
  constructor(configPath = null) {
    this.configPath = configPath;
    this.providers = [];
    this.loaded = false;
  }

  async load(force = false) {
    if (this.loaded && !force) return;
    this.loaded = true;
    this.providers = [];
    if (!this.configPath) return;
    try {
      const parsed = JSON.parse(await readFile(this.configPath, 'utf8'));
      const entries = Array.isArray(parsed) ? parsed : parsed.providers;
      if (!Array.isArray(entries)) throw new Error('providers must be an array');
      const ids = new Set();
      this.providers = entries.filter((entry) => entry?.enabled !== false).map(validateProvider).filter((provider) => {
        if (ids.has(provider.id)) throw new Error(`duplicate provider id: ${provider.id}`);
        ids.add(provider.id);
        return true;
      });
    } catch (error) {
      if (error.code === 'ENOENT') return;
      console.error(`Formula provider config ignored: ${error.message}`);
      this.providers = [];
    }
  }

  async describe(refresh = false) {
    await this.load(refresh);
    return Promise.all(this.providers.map(async (provider) => {
      try {
        const result = await requestProvider(provider, '/v1/info', { method: 'GET' }, 1500);
        if (!result.ok || !String(result.body.apiVersion || '').startsWith('1.')) {
          throw new ProviderRegistryError('API_VERSION_UNSUPPORTED', 'Provider does not expose a compatible v1 API', 502);
        }
        const health = await requestProvider(provider, '/v1/health', { method: 'GET' }, 1500);
        if (!health.ok || health.body?.status !== 'ok') {
          throw new ProviderRegistryError(
            'MODEL_UNAVAILABLE',
            health.body?.warnings?.join(' ') || 'The local provider is not ready',
            503,
          );
        }
        return { id: provider.id, name: provider.name, available: true, info: result.body, health: health.body };
      } catch (error) {
        return { id: provider.id, name: provider.name, available: false, error: { code: error.code || 'PROVIDER_UNAVAILABLE', message: error.message } };
      }
    }));
  }

  async recognize(request, signal = null) {
    await this.load();
    const provider = this.select(request.providerId);
    const payload = { ...request };
    delete payload.providerId;
    const timeout = Math.min(180000, Math.max(1000, Number(payload.options?.timeoutMs) || 60000));
    const result = await requestProvider(provider, '/v1/recognize', {
      method: 'POST',
      body: JSON.stringify(payload),
      signal,
    }, timeout + 1000);
    if (!result.ok) {
      const remoteError = result.body?.error;
      throw new ProviderRegistryError(
        remoteError?.code || 'RECOGNITION_FAILED',
        remoteError?.message || 'Recognition provider rejected the request',
        result.status,
        Boolean(remoteError?.retryable),
        remoteError?.details || null,
      );
    }
    if (payload.mode === 'formula') {
      const candidates = Array.isArray(result.body.candidates)
        ? result.body.candidates.filter((item) => typeof item?.latex === 'string' && item.latex.trim())
        : [];
      if (!candidates.length) {
        throw new ProviderRegistryError('PROVIDER_RESPONSE_INVALID', 'Provider returned no LaTeX candidate', 502);
      }
      if (candidates.some((item) => item.latex.length > MAX_LATEX_CHARACTERS)) {
        throw new ProviderRegistryError('PROVIDER_RESPONSE_TOO_LARGE', 'A LaTeX candidate exceeds 16384 characters', 502);
      }
      const requestedLimit = Number(payload.options?.maxCandidates);
      const candidateLimit = Math.min(
        MAX_FORMULA_CANDIDATES,
        Math.max(1, Number.isFinite(requestedLimit) ? Math.floor(requestedLimit) : MAX_FORMULA_CANDIDATES),
      );
      result.body = { ...result.body, candidates: candidates.slice(0, candidateLimit) };
    }
    return result.body;
  }

  async warmup(request, signal = null) {
    await this.load();
    const provider = this.select(request.providerId);
    const payload = { ...request };
    delete payload.providerId;
    const result = await requestProvider(provider, '/v1/warmup', {
      method: 'POST',
      body: JSON.stringify(payload),
      signal,
    }, Math.min(300000, Math.max(1000, Number(payload.timeoutMs) || 120000)) + 1000);
    if (!result.ok) {
      const remoteError = result.body?.error;
      throw new ProviderRegistryError(remoteError?.code || 'WARMUP_FAILED', remoteError?.message || 'Provider warmup failed', result.status, Boolean(remoteError?.retryable));
    }
    return result.body;
  }

  select(providerId) {
    if (!this.providers.length) {
      throw new ProviderRegistryError('MODEL_UNAVAILABLE', 'No local recognition provider is configured', 503, false);
    }
    if (!providerId && this.providers.length === 1) return this.providers[0];
    const provider = this.providers.find((item) => item.id === providerId);
    if (!provider) throw new ProviderRegistryError('PROVIDER_NOT_FOUND', 'Select a configured recognition provider', 404, false);
    return provider;
  }
}
