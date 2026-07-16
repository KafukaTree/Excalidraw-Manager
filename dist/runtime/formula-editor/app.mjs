import { MathfieldElement } from '/vendor/mathlive/mathlive.min.mjs';
import { quickGroups, templateCategories } from '/templates.mjs';

const translations = {
  'zh-CN': {
    appTitle: '本地公式编辑器', appSubtitle: 'LaTeX 编辑、实时预览与离线导出', offlineBadge: '完全本地', language: '语言',
    inputTitle: '输入区域', inputHint: '直接键入 LaTeX，或用可视化编辑器和模板快速构建公式。', keyboard: '数学键盘',
    quickTools: '快捷工具', formulaTemplates: '公式模板', imageRecognition: '图片识别', documentRecognition: '文档识别',
    modelNotInstalled: '尚未安装公式识别模型', modelOptional: '模型是可选组件。键盘编辑、渲染和所有导出功能已经可以离线使用。',
    interfaceReady: '接口已就绪', dropImage: '拖入、粘贴或选择公式截图', imageTypes: '支持 PNG、JPG、WebP；图片只在本机处理',
    chooseImage: '选择图片', remove: '移除', documentAdapter: '文档识别适配器已预留',
    documentOptional: '后续可接入页面分割、公式定位和批量识别模型，而不改变编辑器。', importPages: '导入页面', detectFormulas: '定位公式',
    replaceableModel: '可替换模型', reviewExport: '校对与导出', color: '颜色', font: '字体', defaultFont: '默认字体', environment: '环境',
    clear: '清空', sourceMode: '源码', visualMode: '可视化', latexSource: 'LaTeX 源码',
    sourceHelp: 'Ctrl + Enter 立即渲染 · 自动保存草稿', visualEditor: '可视化公式', visualHelp: '点击公式后，可使用方向键、Tab 和数学键盘编辑。',
    outputTitle: '输出区域', ready: '准备就绪', previewHint: '输入公式后将在这里实时渲染', scale: '倍率', background: '背景',
    transparent: '透明', white: '白色', dark: '深色', fileName: '文件名', copyPng: '复制 PNG', downloadSvg: '下载 SVG',
    copyOpenBoard: '复制并打开画板', copyCode: '复制代码', localPrivacy: '公式和图片不会离开这台电脑',
    rendering: '正在渲染…', rendered: '已在本机渲染', emptyFormula: '请先输入公式', copied: '已复制到剪贴板', copiedPng: 'PNG 已复制到剪贴板',
    downloaded: '文件已保存', renderFailed: '渲染失败', copyFailed: '剪贴板写入失败', imageReady: '图片已就绪，安装模型后即可识别',
    invalidImage: '请选择受支持的图片文件', confirmClear: '要清空当前公式吗？草稿也会被清除。', draftRestored: '已恢复上次的本地草稿',
    boardOpened: 'PNG 已复制，正在打开画板', modelUnavailable: '当前没有安装识别模型', themeSystem: '跟随系统', themeLight: '浅色', themeDark: '深色',
    outputUnavailable: '当前公式无法转换为这种格式', rasterTooLarge: '导出尺寸过大，请降低倍率', mathEngineFailed: '本地数学渲染器加载失败',
    modelProvider: '识别模型', recognizeFormula: '识别公式', modelReady: '发现 {0} 个可用的本地识别 Provider', recognizing: '正在本机识别…',
    recognized: '识别完成，请选择候选结果', candidates: '候选结果', confidence: '置信度', imageTooLarge: '图片不能超过 12 MiB', recognitionFailed: '公式识别失败',
  },
  en: {
    appTitle: 'Local Formula Editor', appSubtitle: 'LaTeX editing, live preview and offline export', offlineBadge: 'Fully local', language: 'Language',
    inputTitle: 'Input', inputHint: 'Type LaTeX directly, or build a formula with the visual editor and templates.', keyboard: 'Math keyboard',
    quickTools: 'Quick tools', formulaTemplates: 'Formula templates', imageRecognition: 'Image recognition', documentRecognition: 'Document recognition',
    modelNotInstalled: 'No formula recognition model installed', modelOptional: 'The model is optional. Keyboard editing, rendering and every export already work offline.',
    interfaceReady: 'Interface ready', dropImage: 'Drop, paste or choose a formula screenshot', imageTypes: 'PNG, JPG and WebP; images stay on this computer',
    chooseImage: 'Choose image', remove: 'Remove', documentAdapter: 'Document recognition adapter is ready',
    documentOptional: 'Page segmentation, formula detection and batch recognition can be added later without changing the editor.', importPages: 'Import pages', detectFormulas: 'Detect formulas',
    replaceableModel: 'Replaceable model', reviewExport: 'Review and export', color: 'Color', font: 'Font', defaultFont: 'Default font', environment: 'Environment',
    clear: 'Clear', sourceMode: 'Source', visualMode: 'Visual', latexSource: 'LaTeX source',
    sourceHelp: 'Ctrl + Enter to render now · Draft saved automatically', visualEditor: 'Visual formula', visualHelp: 'Use arrow keys, Tab and the math keyboard after selecting the formula.',
    outputTitle: 'Output', ready: 'Ready', previewHint: 'Your formula will render here as you type', scale: 'Scale', background: 'Background',
    transparent: 'Transparent', white: 'White', dark: 'Dark', fileName: 'File name', copyPng: 'Copy PNG', downloadSvg: 'Download SVG',
    copyOpenBoard: 'Copy and open board', copyCode: 'Copy code', localPrivacy: 'Formulas and images never leave this computer',
    rendering: 'Rendering…', rendered: 'Rendered locally', emptyFormula: 'Enter a formula first', copied: 'Copied to clipboard', copiedPng: 'PNG copied to clipboard',
    downloaded: 'File saved', renderFailed: 'Rendering failed', copyFailed: 'Could not write to the clipboard', imageReady: 'Image is ready; install a model to recognize it',
    invalidImage: 'Choose a supported image file', confirmClear: 'Clear this formula and its saved draft?', draftRestored: 'Restored your local draft',
    boardOpened: 'PNG copied; opening the board', modelUnavailable: 'No recognition model is installed', themeSystem: 'System theme', themeLight: 'Light theme', themeDark: 'Dark theme',
    outputUnavailable: 'This formula cannot be converted to that format', rasterTooLarge: 'Export is too large; choose a lower scale', mathEngineFailed: 'The local math renderer could not load',
    modelProvider: 'Recognition model', recognizeFormula: 'Recognize formula', modelReady: '{0} local recognition provider(s) available', recognizing: 'Recognizing locally…',
    recognized: 'Recognition complete; choose a candidate', candidates: 'Candidates', confidence: 'Confidence', imageTooLarge: 'Images must be 12 MiB or smaller', recognitionFailed: 'Formula recognition failed',
  },
};

const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => [...document.querySelectorAll(selector)];
const source = $('#latex-source');
const mathfield = $('#math-field');
const preview = $('#math-preview');
const placeholder = $('#preview-placeholder');
const measurementHost = $('#measurement-host');
const status = $('#render-status');
const codeValue = $('#code-value');
const colorInput = $('#color-input');
const backgroundSelect = $('#background-select');
const params = new URLSearchParams(location.search);

const state = {
  language: params.get('lang')?.toLowerCase().startsWith('zh') ? 'zh-CN' : (params.get('lang') === 'en' ? 'en' : (navigator.language.toLowerCase().startsWith('zh') ? 'zh-CN' : 'en')),
  themeChoice: localStorage.getItem('formula-editor.theme') || 'system',
  activeFormat: 'latex',
  activeTemplateCategory: templateCategories[0].id,
  activePanel: 'quick',
  editMode: 'source',
  zoom: Number(localStorage.getItem('formula-editor.zoom')) || 100,
  renderId: 0,
  renderTimer: 0,
  historyTimer: 0,
  history: [''],
  historyIndex: 0,
  svgElement: null,
  svgText: '',
  imageUrl: null,
  imageFile: null,
  availableProviders: [],
  boardUrl: null,
  toastTimer: 0,
};

function t(key) {
  return translations[state.language][key] || translations.en[key] || key;
}

function applyLanguage() {
  document.documentElement.lang = state.language;
  document.title = `${t('appTitle')} · Excalidraw Manager`;
  $$('#language-select option').forEach((option) => { option.selected = option.value === state.language; });
  $$('[data-i18n]').forEach((element) => {
    const value = t(element.dataset.i18n);
    if (value) element.textContent = value;
  });
  populateTools();
  populateTemplateCategories();
  populateTemplates();
  updateThemeButton();
  updateCodeOutput();
}

function systemIsDark() {
  return matchMedia('(prefers-color-scheme: dark)').matches;
}

function resolvedTheme() {
  return state.themeChoice === 'system' ? (systemIsDark() ? 'dark' : 'light') : state.themeChoice;
}

function applyTheme() {
  document.documentElement.dataset.theme = resolvedTheme();
  updateThemeButton();
}

function updateThemeButton() {
  const labels = { system: t('themeSystem'), light: t('themeLight'), dark: t('themeDark') };
  const button = $('#theme-button');
  button.title = labels[state.themeChoice];
  button.setAttribute('aria-label', labels[state.themeChoice]);
  button.textContent = state.themeChoice === 'system' ? '◐' : (state.themeChoice === 'light' ? '☀' : '☾');
}

function cycleTheme() {
  const choices = ['system', 'light', 'dark'];
  state.themeChoice = choices[(choices.indexOf(state.themeChoice) + 1) % choices.length];
  localStorage.setItem('formula-editor.theme', state.themeChoice);
  applyTheme();
}

function showToast(message, isError = false) {
  const toast = $('#toast');
  clearTimeout(state.toastTimer);
  toast.textContent = message;
  toast.classList.toggle('error', isError);
  toast.classList.add('visible');
  state.toastTimer = setTimeout(() => toast.classList.remove('visible'), 2600);
}

function populateTools() {
  const host = $('#quick-groups');
  host.replaceChildren();
  for (const group of quickGroups) {
    const section = document.createElement('section');
    section.className = 'tool-group';
    const title = document.createElement('h3');
    title.textContent = state.language === 'zh-CN' ? group.zh : group.en;
    const grid = document.createElement('div');
    grid.className = 'symbol-grid';
    for (const [latex, symbol, label] of group.items) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'symbol-button';
      button.textContent = symbol;
      button.title = label;
      button.setAttribute('aria-label', label);
      button.addEventListener('click', () => insertLatex(latex));
      grid.append(button);
    }
    section.append(title, grid);
    host.append(section);
  }
}

function populateTemplateCategories() {
  const host = $('#template-categories');
  host.replaceChildren();
  for (const category of templateCategories) {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = state.language === 'zh-CN' ? category.zh : category.en;
    button.classList.toggle('active', category.id === state.activeTemplateCategory);
    button.addEventListener('click', () => {
      state.activeTemplateCategory = category.id;
      populateTemplateCategories();
      populateTemplates();
    });
    host.append(button);
  }
}

function populateTemplates() {
  const host = $('#template-grid');
  host.replaceChildren();
  const category = templateCategories.find((item) => item.id === state.activeTemplateCategory) || templateCategories[0];
  for (const [zh, en, latex] of category.templates) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'template-button';
    const name = document.createElement('strong');
    name.textContent = state.language === 'zh-CN' ? zh : en;
    const code = document.createElement('code');
    code.textContent = latex;
    button.append(name, code);
    button.addEventListener('click', () => commitSource(latex, true));
    host.append(button);
  }
}

function setActivePanel(panel) {
  state.activePanel = panel;
  $$('.feature-tab').forEach((button) => {
    const active = button.dataset.panel === panel;
    button.classList.toggle('active', active);
    button.setAttribute('aria-selected', String(active));
  });
  $$('.feature-panel').forEach((element) => element.classList.add('hidden'));
  $(`#${panel}-panel`).classList.remove('hidden');
  if (panel === 'image' || panel === 'document') refreshModelStatus();
}

function cleanLatex(value) {
  let result = value.trim();
  const wrappers = [['$$', '$$'], ['\\[', '\\]'], ['\\(', '\\)']];
  for (const [start, end] of wrappers) {
    if (result.startsWith(start) && result.endsWith(end)) {
      result = result.slice(start.length, -end.length).trim();
      break;
    }
  }
  return result;
}

function syncMathfield() {
  if (mathfield.value !== source.value) mathfield.value = source.value;
  $('#character-count').textContent = String(source.value.length);
}

function scheduleHistory() {
  clearTimeout(state.historyTimer);
  state.historyTimer = setTimeout(() => recordHistory(source.value), 450);
}

function recordHistory(value) {
  if (state.history[state.historyIndex] === value) return;
  state.history = state.history.slice(0, state.historyIndex + 1);
  state.history.push(value);
  if (state.history.length > 100) state.history.shift();
  state.historyIndex = state.history.length - 1;
}

function applyHistory(index) {
  if (index < 0 || index >= state.history.length) return;
  state.historyIndex = index;
  source.value = state.history[index];
  syncMathfield();
  saveDraft();
  scheduleRender(0);
}

function commitSource(value, replaceAll = false, cursorOffset = null) {
  if (replaceAll) {
    source.value = value;
    source.selectionStart = source.selectionEnd = value.length;
  } else {
    const start = source.selectionStart;
    const end = source.selectionEnd;
    source.setRangeText(value, start, end, 'end');
    if (cursorOffset !== null) source.selectionStart = source.selectionEnd = start + cursorOffset;
  }
  recordHistory(source.value);
  syncMathfield();
  saveDraft();
  scheduleRender(0);
  if (state.editMode === 'source') source.focus(); else mathfield.focus();
}

function insertLatex(template) {
  const start = source.selectionStart;
  const end = source.selectionEnd;
  const selected = source.value.slice(start, end);
  const marker = template.indexOf('◊');
  let insertion = template.replace('◊', selected);
  let cursor = insertion.length;
  if (marker >= 0) cursor = marker + selected.length;
  commitSource(insertion, false, cursor);
}

function setEditMode(mode) {
  state.editMode = mode;
  $$('[data-edit-mode]').forEach((button) => button.classList.toggle('active', button.dataset.editMode === mode));
  $('#source-editor-wrap').classList.toggle('hidden', mode !== 'source');
  $('#visual-editor-wrap').classList.toggle('hidden', mode !== 'visual');
  syncMathfield();
  requestAnimationFrame(() => (mode === 'source' ? source : mathfield).focus());
}

function saveDraft() {
  if (source.value) localStorage.setItem('formula-editor.draft', source.value);
  else localStorage.removeItem('formula-editor.draft');
}

let mathJaxPromise;
function loadMathJax() {
  if (mathJaxPromise) return mathJaxPromise;
  window.MathJax = {
    loader: { paths: { 'mathjax-newcm': '/vendor/mathjax' } },
    startup: { typeset: false },
    options: {
      enableMenu: false,
      enableEnrichment: false,
      enableExplorer: false,
      enableSpeech: false,
      enableBraille: false,
    },
    svg: { fontCache: 'local' },
  };
  mathJaxPromise = new Promise((resolve, reject) => {
    const script = document.createElement('script');
    script.src = '/vendor/mathjax/tex-mml-svg.js';
    script.onload = () => window.MathJax.startup.promise.then(() => resolve(window.MathJax), reject);
    script.onerror = () => reject(new Error('MathJax script failed to load'));
    document.head.append(script);
  });
  return mathJaxPromise;
}

function sanitizeSvg(svg) {
  svg.querySelectorAll('script, foreignObject').forEach((element) => element.remove());
  for (const element of svg.querySelectorAll('*')) {
    for (const attribute of [...element.attributes]) {
      const name = attribute.name.toLowerCase();
      if (name.startsWith('on')) element.removeAttribute(attribute.name);
      if ((name === 'href' || name === 'xlink:href') && !attribute.value.startsWith('#')) element.removeAttribute(attribute.name);
    }
  }
  svg.removeAttribute('aria-hidden');
  svg.removeAttribute('focusable');
  svg.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
  return svg;
}

function serializeSvg(svg, includeBackground = true) {
  const clone = sanitizeSvg(svg.cloneNode(true));
  clone.style.color = colorInput.value;
  const background = backgroundSelect.value;
  if (includeBackground && background !== 'transparent') {
    const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    rect.setAttribute('x', '0');
    rect.setAttribute('y', '0');
    rect.setAttribute('width', '100%');
    rect.setAttribute('height', '100%');
    rect.setAttribute('fill', background);
    clone.insertBefore(rect, clone.firstChild);
  }
  return `<?xml version="1.0" encoding="UTF-8"?>\n${new XMLSerializer().serializeToString(clone)}`;
}

function scheduleRender(delay = 220) {
  clearTimeout(state.renderTimer);
  const id = ++state.renderId;
  state.renderTimer = setTimeout(() => renderFormula(id), delay);
}

async function renderFormula(id = ++state.renderId) {
  const latex = cleanLatex(source.value);
  if (!latex) {
    state.svgElement = null;
    state.svgText = '';
    preview.replaceChildren();
    preview.classList.add('hidden');
    placeholder.classList.remove('hidden');
    status.textContent = t('ready');
    updateCodeOutput();
    return;
  }
  status.textContent = t('rendering');
  try {
    const MathJax = await loadMathJax();
    const wrapper = await MathJax.tex2svgPromise(latex, { display: true });
    if (id !== state.renderId) return;
    const svg = wrapper.querySelector('svg');
    if (!svg) throw new Error('MathJax returned no SVG');
    measurementHost.replaceChildren(wrapper);
    const rect = svg.getBoundingClientRect();
    const width = Math.max(1, rect.width || 320);
    const height = Math.max(1, rect.height || 80);
    const standalone = sanitizeSvg(svg.cloneNode(true));
    standalone.setAttribute('width', `${width.toFixed(2)}px`);
    standalone.setAttribute('height', `${height.toFixed(2)}px`);
    standalone.setAttribute('preserveAspectRatio', 'xMidYMid meet');
    standalone.style.color = colorInput.value;
    measurementHost.replaceChildren();
    state.svgElement = standalone;
    state.svgText = serializeSvg(standalone, false);
    const visible = standalone.cloneNode(true);
    preview.replaceChildren(visible);
    preview.style.transform = `scale(${state.zoom / 100})`;
    preview.classList.remove('hidden');
    placeholder.classList.add('hidden');
    status.textContent = t('rendered');
    updatePreviewBackground();
    updateCodeOutput();
  } catch (error) {
    if (id !== state.renderId) return;
    console.error(error);
    state.svgElement = null;
    state.svgText = '';
    preview.replaceChildren();
    preview.classList.add('hidden');
    placeholder.classList.remove('hidden');
    status.textContent = `${t('renderFailed')}: ${error.message}`;
    showToast(t(error.message.includes('load') ? 'mathEngineFailed' : 'renderFailed'), true);
    updateCodeOutput();
  }
}

function mathfieldValue(format) {
  try {
    syncMathfield();
    return mathfield.getValue(format);
  } catch {
    return '';
  }
}

function formatValue(format) {
  const latex = cleanLatex(source.value);
  if (!latex) return '';
  switch (format) {
    case 'latex': return latex;
    case 'markdown-inline': return `$${latex}$`;
    case 'markdown-block': return `$$\n${latex}\n$$`;
    case 'mathml': return mathfieldValue('math-ml');
    case 'asciimath': return mathfieldValue('ascii-math');
    case 'typst': return mathfieldValue('typst');
    case 'svg': return state.svgElement ? serializeSvg(state.svgElement) : '';
    default: return latex;
  }
}

function updateCodeOutput() {
  codeValue.value = formatValue(state.activeFormat);
}

function setActiveFormat(format) {
  state.activeFormat = format;
  $$('.code-tabs button').forEach((button) => button.classList.toggle('active', button.dataset.format === format));
  updateCodeOutput();
}

function setZoom(value) {
  state.zoom = Math.min(250, Math.max(50, value));
  localStorage.setItem('formula-editor.zoom', String(state.zoom));
  $('#zoom-value').textContent = `${state.zoom}%`;
  preview.style.transform = `scale(${state.zoom / 100})`;
}

function updatePreviewBackground() {
  const value = backgroundSelect.value;
  $('#preview-stage').style.setProperty('--preview-background', value === 'transparent' ? 'transparent' : value);
}

function requireSvg() {
  if (!state.svgElement) {
    showToast(t('emptyFormula'), true);
    return false;
  }
  return true;
}

function dimensionsFromSvg(svg) {
  return {
    width: Math.max(1, Number.parseFloat(svg.getAttribute('width')) || 320),
    height: Math.max(1, Number.parseFloat(svg.getAttribute('height')) || 80),
  };
}

async function svgToRasterBlob(type = 'image/png') {
  if (!requireSvg()) return null;
  const scale = Number($('#export-scale').value) || 2;
  const { width, height } = dimensionsFromSvg(state.svgElement);
  const pixelWidth = Math.ceil(width * scale);
  const pixelHeight = Math.ceil(height * scale);
  if (pixelWidth > 16384 || pixelHeight > 16384 || pixelWidth * pixelHeight > 64_000_000) throw new Error(t('rasterTooLarge'));
  const svgText = serializeSvg(state.svgElement, false);
  const url = URL.createObjectURL(new Blob([svgText], { type: 'image/svg+xml;charset=utf-8' }));
  try {
    const image = new Image();
    await new Promise((resolve, reject) => {
      image.onload = resolve;
      image.onerror = () => reject(new Error('SVG image decode failed'));
      image.src = url;
    });
    const canvas = document.createElement('canvas');
    canvas.width = pixelWidth;
    canvas.height = pixelHeight;
    const context = canvas.getContext('2d');
    const selectedBackground = backgroundSelect.value;
    const background = type === 'image/jpeg' && selectedBackground === 'transparent' ? '#ffffff' : selectedBackground;
    if (background !== 'transparent') {
      context.fillStyle = background;
      context.fillRect(0, 0, pixelWidth, pixelHeight);
    }
    context.drawImage(image, 0, 0, pixelWidth, pixelHeight);
    return await new Promise((resolve, reject) => canvas.toBlob((blob) => blob ? resolve(blob) : reject(new Error('Canvas export failed')), type, .94));
  } finally {
    URL.revokeObjectURL(url);
  }
}

function safeFilename(extension) {
  const base = ($('#filename-input').value || 'formula').trim().replace(/[<>:"/\\|?*\x00-\x1f]/g, '-').replace(/[. ]+$/g, '').slice(0, 80) || 'formula';
  return `${base}.${extension}`;
}

function downloadBlob(blob, filename) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
  showToast(t('downloaded'));
}

async function download(format) {
  try {
    if (format === 'svg') {
      if (!requireSvg()) return;
      downloadBlob(new Blob([serializeSvg(state.svgElement)], { type: 'image/svg+xml;charset=utf-8' }), safeFilename('svg'));
    } else if (format === 'png' || format === 'jpg') {
      const blob = await svgToRasterBlob(format === 'jpg' ? 'image/jpeg' : 'image/png');
      if (blob) downloadBlob(blob, safeFilename(format));
    } else if (format === 'tex') {
      const latex = cleanLatex(source.value);
      if (!latex) return showToast(t('emptyFormula'), true);
      downloadBlob(new Blob([latex], { type: 'application/x-tex;charset=utf-8' }), safeFilename('tex'));
    }
  } catch (error) {
    console.error(error);
    showToast(error.message || t('renderFailed'), true);
  }
}

async function writeText(text) {
  if (!text) throw new Error(t('outputUnavailable'));
  if (navigator.clipboard?.writeText) return navigator.clipboard.writeText(text);
  const helper = document.createElement('textarea');
  helper.value = text;
  helper.style.position = 'fixed';
  helper.style.opacity = '0';
  document.body.append(helper);
  helper.select();
  const ok = document.execCommand('copy');
  helper.remove();
  if (!ok) throw new Error(t('copyFailed'));
}

async function copyPng() {
  try {
    const blob = await svgToRasterBlob('image/png');
    if (!blob) return false;
    if (!navigator.clipboard?.write || !window.ClipboardItem) throw new Error(t('copyFailed'));
    await navigator.clipboard.write([new ClipboardItem({ 'image/png': blob })]);
    showToast(t('copiedPng'));
    return true;
  } catch (error) {
    console.error(error);
    showToast(error.message || t('copyFailed'), true);
    return false;
  }
}

function validBoardUrl(raw) {
  if (!raw) return null;
  try {
    const url = new URL(raw);
    if (url.protocol !== 'http:' || !['localhost', '127.0.0.1', '[::1]'].includes(url.hostname)) return null;
    return url.href;
  } catch {
    return null;
  }
}

async function copyAndOpenBoard() {
  if (await copyPng()) {
    window.open(state.boardUrl, '_blank', 'noopener');
    showToast(t('boardOpened'));
  }
}

async function refreshModelStatus() {
  try {
    const response = await fetch('/api/models?refresh=1', { cache: 'no-store' });
    const result = await response.json();
    state.availableProviders = (result.providers || []).filter((provider) => provider.available);
    const select = $('#provider-select');
    select.replaceChildren();
    for (const provider of state.availableProviders) {
      const option = document.createElement('option');
      option.value = provider.id;
      option.textContent = provider.info?.provider?.name || provider.name || provider.id;
      select.append(option);
    }
    const available = state.availableProviders.length > 0;
    $('#recognition-actions').classList.toggle('hidden', !available);
    $('#recognize-button').disabled = !available || !state.imageFile;
    const notice = $('#image-panel .model-notice strong');
    notice.textContent = available ? t('modelReady').replace('{0}', state.availableProviders.length) : t('modelNotInstalled');
    if (!available) status.textContent = t('modelUnavailable');
  } catch (error) {
    console.warn('Model status unavailable:', error);
    state.availableProviders = [];
    $('#recognition-actions').classList.add('hidden');
  }
}

function stageImage(file) {
  if (!file || !file.type.startsWith('image/')) return showToast(t('invalidImage'), true);
  if (file.size > 12 * 1024 * 1024) return showToast(t('imageTooLarge'), true);
  if (state.imageUrl) URL.revokeObjectURL(state.imageUrl);
  state.imageFile = file;
  state.imageUrl = URL.createObjectURL(file);
  $('#image-preview').src = state.imageUrl;
  $('#image-name').textContent = file.name || 'clipboard-image.png';
  $('#image-meta').textContent = `${file.type || 'image'} · ${(file.size / 1024).toFixed(1)} KiB`;
  $('#image-staging').classList.remove('hidden');
  $('#recognize-button').disabled = state.availableProviders.length === 0;
  $('#candidate-list').classList.add('hidden');
  showToast(t('imageReady'));
}

function removeImage() {
  if (state.imageUrl) URL.revokeObjectURL(state.imageUrl);
  state.imageUrl = null;
  state.imageFile = null;
  $('#image-preview').removeAttribute('src');
  $('#image-staging').classList.add('hidden');
  $('#recognize-button').disabled = true;
  $('#candidate-list').classList.add('hidden');
  $('#candidate-list').replaceChildren();
  $('#image-input').value = '';
}

function fileBase64(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result).split(',', 2)[1] || '');
    reader.onerror = () => reject(reader.error || new Error('Image read failed'));
    reader.readAsDataURL(file);
  });
}

function showCandidates(candidates) {
  const host = $('#candidate-list');
  host.replaceChildren();
  const title = document.createElement('p');
  title.textContent = t('candidates');
  host.append(title);
  for (const candidate of candidates) {
    if (!candidate || typeof candidate.latex !== 'string') continue;
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'candidate-button';
    const code = document.createElement('code');
    code.textContent = candidate.latex;
    const confidence = document.createElement('span');
    confidence.textContent = Number.isFinite(candidate.confidence)
      ? `${t('confidence')} ${(candidate.confidence * 100).toFixed(1)}%`
      : '';
    button.append(code, confidence);
    button.addEventListener('click', () => {
      commitSource(candidate.latex, true);
      setActivePanel('quick');
    });
    host.append(button);
  }
  host.classList.toggle('hidden', host.children.length <= 1);
}

async function recognizeImage() {
  if (!state.imageFile || !state.availableProviders.length) return showToast(t('modelUnavailable'), true);
  const button = $('#recognize-button');
  button.disabled = true;
  status.textContent = t('recognizing');
  try {
    const requestId = crypto.randomUUID ? crypto.randomUUID() : `formula-${Date.now()}`;
    const response = await fetch('/api/recognize', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        requestId,
        providerId: $('#provider-select').value,
        mode: 'formula',
        input: {
          kind: 'image',
          mediaType: state.imageFile.type,
          dataBase64: await fileBase64(state.imageFile),
        },
        languageHints: [state.language, 'en'],
        options: { maxCandidates: 3, outputFormats: ['latex'], preprocessing: 'auto', timeoutMs: 60000, debug: false },
      }),
    });
    const result = await response.json();
    if (!response.ok) throw new Error(result.message || result.error?.message || t('recognitionFailed'));
    showCandidates(result.candidates || []);
    if (!result.candidates?.length) throw new Error(t('recognitionFailed'));
    status.textContent = t('recognized');
    showToast(t('recognized'));
  } catch (error) {
    console.error(error);
    status.textContent = t('recognitionFailed');
    showToast(error.message || t('recognitionFailed'), true);
  } finally {
    button.disabled = !state.imageFile || !state.availableProviders.length;
  }
}

function bindEvents() {
  $('#language-select').addEventListener('change', (event) => {
    state.language = event.target.value;
    localStorage.setItem('formula-editor.language', state.language);
    applyLanguage();
  });
  $('#theme-button').addEventListener('click', cycleTheme);
  matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => { if (state.themeChoice === 'system') applyTheme(); });
  $$('.feature-tab').forEach((button) => button.addEventListener('click', () => setActivePanel(button.dataset.panel)));
  $$('[data-edit-mode]').forEach((button) => button.addEventListener('click', () => setEditMode(button.dataset.editMode)));
  source.addEventListener('input', () => {
    syncMathfield();
    saveDraft();
    scheduleHistory();
    scheduleRender();
  });
  source.addEventListener('keydown', (event) => {
    if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') { event.preventDefault(); scheduleRender(0); }
    if (event.key === 'Tab') {
      event.preventDefault();
      commitSource('  ');
    }
  });
  mathfield.addEventListener('input', () => {
    source.value = mathfield.getValue('latex');
    $('#character-count').textContent = String(source.value.length);
    saveDraft();
    scheduleHistory();
    scheduleRender();
  });
  $('#keyboard-button').addEventListener('click', () => {
    if (state.editMode !== 'visual') setEditMode('visual');
    mathfield.focus();
    mathfield.executeCommand('toggleVirtualKeyboard');
  });
  $('#undo-button').addEventListener('click', () => applyHistory(state.historyIndex - 1));
  $('#redo-button').addEventListener('click', () => applyHistory(state.historyIndex + 1));
  $('#clear-button').addEventListener('click', () => {
    if (source.value && !confirm(t('confirmClear'))) return;
    commitSource('', true);
  });
  $('#font-select').addEventListener('change', (event) => {
    const command = event.target.value;
    if (command) insertLatex(`\\${command}{◊}`);
    event.target.value = '';
  });
  $('#environment-select').addEventListener('change', (event) => {
    const environment = event.target.value;
    if (environment) insertLatex(`\\begin{${environment}}\n◊\n\\end{${environment}}`);
    event.target.value = '';
  });
  colorInput.addEventListener('input', () => {
    document.documentElement.style.setProperty('--formula-color', colorInput.value);
    scheduleRender(0);
  });
  backgroundSelect.addEventListener('change', () => { updatePreviewBackground(); updateCodeOutput(); });
  $('#zoom-out').addEventListener('click', () => setZoom(state.zoom - 10));
  $('#zoom-in').addEventListener('click', () => setZoom(state.zoom + 10));
  $$('.code-tabs button').forEach((button) => button.addEventListener('click', () => setActiveFormat(button.dataset.format)));
  $('#copy-code').addEventListener('click', async () => {
    try { await writeText(codeValue.value); showToast(t('copied')); } catch (error) { showToast(error.message, true); }
  });
  $('#copy-png').addEventListener('click', copyPng);
  $('#download-main').addEventListener('click', () => download('svg'));
  $('#download-menu-button').addEventListener('click', (event) => {
    event.stopPropagation();
    const menu = $('#download-menu');
    menu.classList.toggle('hidden');
    event.currentTarget.setAttribute('aria-expanded', String(!menu.classList.contains('hidden')));
  });
  $$('[data-download]').forEach((button) => button.addEventListener('click', () => { $('#download-menu').classList.add('hidden'); download(button.dataset.download); }));
  document.addEventListener('click', () => $('#download-menu').classList.add('hidden'));
  $('#board-button').addEventListener('click', copyAndOpenBoard);
  $('[data-file-trigger="image"]').addEventListener('click', (event) => { event.stopPropagation(); $('#image-input').click(); });
  $('#image-input').addEventListener('change', (event) => stageImage(event.target.files[0]));
  $('#remove-image').addEventListener('click', removeImage);
  $('#recognize-button').addEventListener('click', recognizeImage);
  const dropzone = $('#image-dropzone');
  dropzone.addEventListener('click', () => $('#image-input').click());
  dropzone.addEventListener('keydown', (event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); $('#image-input').click(); } });
  for (const eventName of ['dragenter', 'dragover']) dropzone.addEventListener(eventName, (event) => { event.preventDefault(); dropzone.classList.add('dragging'); });
  for (const eventName of ['dragleave', 'drop']) dropzone.addEventListener(eventName, (event) => { event.preventDefault(); dropzone.classList.remove('dragging'); });
  dropzone.addEventListener('drop', (event) => stageImage([...event.dataTransfer.files].find((file) => file.type.startsWith('image/'))));
  document.addEventListener('paste', (event) => {
    if (state.activePanel !== 'image' || event.target === source || event.target.closest?.('math-field')) return;
    const file = [...event.clipboardData.files].find((item) => item.type.startsWith('image/'));
    if (file) { event.preventDefault(); stageImage(file); }
  });
  document.addEventListener('keydown', (event) => {
    if ((event.ctrlKey || event.metaKey) && event.shiftKey && event.key.toLowerCase() === 'c') { event.preventDefault(); copyPng(); }
  });
  addEventListener('beforeunload', removeImage);
}

async function initialize() {
  const storedLanguage = localStorage.getItem('formula-editor.language');
  if (!params.has('lang') && storedLanguage) state.language = storedLanguage === 'zh-CN' ? 'zh-CN' : 'en';
  MathfieldElement.fontsDirectory = '/vendor/mathlive/fonts/';
  MathfieldElement.soundsDirectory = null;
  applyTheme();
  applyLanguage();
  bindEvents();
  setZoom(state.zoom);
  updatePreviewBackground();
  state.boardUrl = validBoardUrl(params.get('board'));
  $('#board-button').classList.toggle('hidden', !state.boardUrl);
  const linkedFormula = params.get('latex');
  const draft = linkedFormula !== null ? linkedFormula : (localStorage.getItem('formula-editor.draft') || '');
  source.value = draft;
  state.history = [draft];
  syncMathfield();
  if (draft) {
    if (linkedFormula === null) showToast(t('draftRestored'));
    scheduleRender(0);
  } else {
    updateCodeOutput();
    requestAnimationFrame(() => source.focus());
  }
  loadMathJax().catch((error) => { console.error(error); showToast(t('mathEngineFailed'), true); });
}

initialize();
