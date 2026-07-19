import { MathfieldElement } from '/vendor/mathlive/mathlive.min.mjs';
import { isCommandTypingInput } from '/completion.mjs';
import { latexCommands, mathfieldInsertion, quickGroups } from '/templates.mjs';

const translations = {
  'zh-CN': {
    appTitle: '本地公式编辑器', paletteTitle: '公式悬浮窗', appSubtitle: 'LaTeX 编辑、实时预览与离线导出', offlineBadge: '完全本地', language: '语言',
    inputTitle: '输入区域', inputHint: '直接键入 LaTeX，或用可视化编辑器快速构建公式。', keyboard: '数学键盘',
    quickTools: '快捷工具', quickPalette: '常用公式与符号', closeQuickPalette: '关闭符号面板', imageRecognition: '图片识别', documentRecognition: '文档识别',
    modelNotInstalled: '尚未安装公式识别模型', modelOptional: '模型是可选组件。键盘编辑、渲染和所有导出功能已经可以离线使用。',
    interfaceReady: '接口已就绪', dropImage: '拖入、粘贴或选择公式截图', imageTypes: '支持 PNG、JPG、WebP；图片只在本机处理',
    chooseImage: '选择图片', remove: '移除', documentAdapter: '文档识别适配器已预留',
    documentOptional: '后续可接入页面分割、公式定位和批量识别模型，而不改变编辑器。', importPages: '导入页面', detectFormulas: '定位公式',
    replaceableModel: '可替换模型', reviewExport: '校对与导出', color: '颜色', font: '字体', defaultFont: '默认字体', environment: '环境',
    clear: '清空', sourceMode: '源码', visualMode: '可视化', latexSource: 'LaTeX 源码',
    sourceHelp: 'Ctrl + Enter 复制所选格式 · 输入 \\ 查看命令', visualEditor: '可视化公式', visualHelp: '点击公式后，可使用方向键、Tab 和数学键盘编辑。',
    outputTitle: '输出区域', ready: '准备就绪', previewHint: '输入公式后将在这里实时渲染', scale: '倍率', background: '背景',
    transparent: '透明', white: '白色', dark: '深色', fileName: '文件名', copySvg: '复制 SVG', copyPng: '复制 PNG', copyLatex: '复制 LaTeX',
    copyMarkdownInline: '复制 Markdown 行内', copyMarkdownBlock: '复制 Markdown 块', copyMathml: '复制 MathML', copyMenu: '选择复制格式',
    formatSvg: 'SVG', formatPng: 'PNG', formatLatex: 'LaTeX', formatMarkdownInline: 'Markdown 行内', formatMarkdownBlock: 'Markdown 块', formatMathml: 'MathML', downloadSvg: '下载 SVG',
    copyOpenBoard: '复制并打开画板', copyCode: '复制代码', localPrivacy: '公式和图片不会离开这台电脑',
    rendering: '正在渲染…', rendered: '已在本机渲染', emptyFormula: '请先输入公式', copied: '已复制到剪贴板', copiedSvg: 'SVG 已复制到剪贴板', copiedPng: 'PNG 已复制到剪贴板', svgSent: 'SVG 已发送到画板',
    downloaded: '文件已保存', renderFailed: '渲染失败', copyFailed: '剪贴板写入失败', imageReady: '图片已就绪，安装模型后即可识别',
    invalidImage: '请选择受支持的图片文件', confirmClear: '要清空当前公式吗？草稿也会被清除。', draftRestored: '已恢复上次的本地草稿',
    boardOpened: 'SVG 已复制，正在打开画板', modelUnavailable: '当前没有安装识别模型', themeSystem: '跟随系统', themeLight: '浅色', themeDark: '深色',
    outputUnavailable: '当前公式无法转换为这种格式', rasterTooLarge: '导出尺寸过大，请降低倍率', imageClipboardUnavailable: '当前浏览器不支持复制 PNG 图片', mathEngineFailed: '本地数学渲染器加载失败',
    modelProvider: '识别模型', recognizeFormula: '识别公式', modelReady: '发现 {0} 个可用的本地识别 Provider', recognizing: '正在本机识别…',
    recognized: '识别完成，请选择候选结果', recognizedApplied: '识别完成，已填入公式', candidates: '候选结果', confidence: '置信度', imageTooLarge: '图片不能超过 12 MiB', recognitionFailed: '公式识别失败',
    captureOcr: '截屏 OCR', captureSelecting: '请拖动选择公式区域；Esc 或右键取消', captureCancelled: '已取消截图',
    captureBusy: '已有一个截图选区正在进行', captureTimeout: '截图选择超时', captureUnavailable: '本机截图组件不可用', captureFailed: '截图失败',
  },
  en: {
    appTitle: 'Local Formula Editor', paletteTitle: 'Formula palette', appSubtitle: 'LaTeX editing, live preview and offline export', offlineBadge: 'Fully local', language: 'Language',
    inputTitle: 'Input', inputHint: 'Type LaTeX directly, or build a formula with the visual editor.', keyboard: 'Math keyboard',
    quickTools: 'Quick tools', quickPalette: 'Common formulas and symbols', closeQuickPalette: 'Close symbol palette', imageRecognition: 'Image recognition', documentRecognition: 'Document recognition',
    modelNotInstalled: 'No formula recognition model installed', modelOptional: 'The model is optional. Keyboard editing, rendering and every export already work offline.',
    interfaceReady: 'Interface ready', dropImage: 'Drop, paste or choose a formula screenshot', imageTypes: 'PNG, JPG and WebP; images stay on this computer',
    chooseImage: 'Choose image', remove: 'Remove', documentAdapter: 'Document recognition adapter is ready',
    documentOptional: 'Page segmentation, formula detection and batch recognition can be added later without changing the editor.', importPages: 'Import pages', detectFormulas: 'Detect formulas',
    replaceableModel: 'Replaceable model', reviewExport: 'Review and export', color: 'Color', font: 'Font', defaultFont: 'Default font', environment: 'Environment',
    clear: 'Clear', sourceMode: 'Source', visualMode: 'Visual', latexSource: 'LaTeX source',
    sourceHelp: 'Ctrl + Enter copies the selected format · Type \\ for commands', visualEditor: 'Visual formula', visualHelp: 'Use arrow keys, Tab and the math keyboard after selecting the formula.',
    outputTitle: 'Output', ready: 'Ready', previewHint: 'Your formula will render here as you type', scale: 'Scale', background: 'Background',
    transparent: 'Transparent', white: 'White', dark: 'Dark', fileName: 'File name', copySvg: 'Copy SVG', copyPng: 'Copy PNG', copyLatex: 'Copy LaTeX',
    copyMarkdownInline: 'Copy inline Markdown', copyMarkdownBlock: 'Copy block Markdown', copyMathml: 'Copy MathML', copyMenu: 'Choose copy format',
    formatSvg: 'SVG', formatPng: 'PNG', formatLatex: 'LaTeX', formatMarkdownInline: 'Inline Markdown', formatMarkdownBlock: 'Block Markdown', formatMathml: 'MathML', downloadSvg: 'Download SVG',
    copyOpenBoard: 'Copy and open board', copyCode: 'Copy code', localPrivacy: 'Formulas and images never leave this computer',
    rendering: 'Rendering…', rendered: 'Rendered locally', emptyFormula: 'Enter a formula first', copied: 'Copied to clipboard', copiedSvg: 'SVG copied to clipboard', copiedPng: 'PNG copied to clipboard', svgSent: 'SVG sent to the board',
    downloaded: 'File saved', renderFailed: 'Rendering failed', copyFailed: 'Could not write to the clipboard', imageReady: 'Image is ready; install a model to recognize it',
    invalidImage: 'Choose a supported image file', confirmClear: 'Clear this formula and its saved draft?', draftRestored: 'Restored your local draft',
    boardOpened: 'SVG copied; opening the board', modelUnavailable: 'No recognition model is installed', themeSystem: 'System theme', themeLight: 'Light theme', themeDark: 'Dark theme',
    outputUnavailable: 'This formula cannot be converted to that format', rasterTooLarge: 'Export is too large; choose a lower scale', imageClipboardUnavailable: 'This browser cannot copy PNG images', mathEngineFailed: 'The local math renderer could not load',
    modelProvider: 'Recognition model', recognizeFormula: 'Recognize formula', modelReady: '{0} local recognition provider(s) available', recognizing: 'Recognizing locally…',
    recognized: 'Recognition complete; choose a candidate', recognizedApplied: 'Recognition complete; formula inserted', candidates: 'Candidates', confidence: 'Confidence', imageTooLarge: 'Images must be 12 MiB or smaller', recognitionFailed: 'Formula recognition failed',
    captureOcr: 'Capture OCR', captureSelecting: 'Drag around a formula; press Esc or right-click to cancel', captureCancelled: 'Screen capture cancelled',
    captureBusy: 'A screen capture is already in progress', captureTimeout: 'Screen capture timed out', captureUnavailable: 'The local screen capture component is unavailable', captureFailed: 'Screen capture failed',
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
const compactMode = params.get('compact') === '1' || params.get('embed') === '1';
const supportedImageTypes = new Set(['image/png', 'image/jpeg', 'image/webp']);
const copyFormats = new Set(['svg', 'png', 'latex', 'markdown-inline', 'markdown-block', 'mathml']);
const storedCopyFormat = localStorage.getItem('formula-editor.copy-format');

const state = {
  language: params.get('lang')?.toLowerCase().startsWith('zh') ? 'zh-CN' : (params.get('lang') === 'en' ? 'en' : (navigator.language.toLowerCase().startsWith('zh') ? 'zh-CN' : 'en')),
  themeChoice: localStorage.getItem('formula-editor.theme') || 'system',
  activeFormat: 'svg',
  copyFormat: copyFormats.has(storedCopyFormat) ? storedCopyFormat : 'svg',
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
  compact: compactMode,
  commandMatches: [],
  commandIndex: 0,
  commandRange: null,
  composing: false,
  ocrGeneration: 0,
  ocrController: null,
  quickGroupId: null,
  quickGroupPinned: false,
  quickGroupTrigger: null,
  quickCloseTimer: 0,
  quickIgnoreFocus: false,
  captureInProgress: false,
  captureController: null,
};

function escapeMarkup(value) {
  return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}

function highlightedLatex(value) {
  const matcher = /\\(?:[A-Za-z@]+|.)|%[^\n]*|\d+(?:\.\d+)?|[{}\[\]()]|[_^&=+\-*/,:;|]+/g;
  let result = '';
  let cursor = 0;
  for (const match of value.matchAll(matcher)) {
    result += escapeMarkup(value.slice(cursor, match.index));
    const token = match[0];
    let kind = 'operator';
    if (token.startsWith('\\')) kind = 'command';
    else if (token.startsWith('%')) kind = 'comment';
    else if (/^\d/.test(token)) kind = 'number';
    else if (/^[{}\[\]()]$/.test(token)) kind = 'bracket';
    result += `<span class="syntax-${kind}">${escapeMarkup(token)}</span>`;
    cursor = match.index + token.length;
  }
  result += escapeMarkup(value.slice(cursor));
  return result + (value.endsWith('\n') ? ' ' : '');
}

function updateSourceHighlight() {
  $('#source-highlight code').innerHTML = highlightedLatex(source.value);
  const highlight = $('#source-highlight');
  highlight.scrollTop = source.scrollTop;
  highlight.scrollLeft = source.scrollLeft;
}

function caretCoordinates(position) {
  const style = getComputedStyle(source);
  const mirror = document.createElement('div');
  const copiedProperties = [
    'boxSizing', 'width', 'height', 'paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft',
    'borderTopWidth', 'borderRightWidth', 'borderBottomWidth', 'borderLeftWidth', 'fontFamily',
    'fontSize', 'fontWeight', 'fontStyle', 'letterSpacing', 'lineHeight', 'textTransform',
    'textIndent', 'tabSize', 'wordSpacing', 'overflowWrap', 'wordBreak', 'whiteSpace',
  ];
  for (const property of copiedProperties) mirror.style[property] = style[property];
  mirror.style.position = 'fixed';
  mirror.style.left = '-100000px';
  mirror.style.top = '0';
  mirror.style.overflow = 'hidden';
  mirror.style.visibility = 'hidden';
  mirror.textContent = source.value.slice(0, position);
  const marker = document.createElement('span');
  marker.textContent = source.value.slice(position, position + 1) || '\u200b';
  mirror.append(marker);
  document.body.append(mirror);
  const lineHeight = Number.parseFloat(style.lineHeight) || Number.parseFloat(style.fontSize) * 1.65;
  const result = {
    left: marker.offsetLeft - source.scrollLeft,
    top: marker.offsetTop - source.scrollTop + lineHeight,
    lineHeight,
  };
  mirror.remove();
  return result;
}

function hideCommandSuggestions() {
  state.commandMatches = [];
  state.commandRange = null;
  const menu = $('#command-suggestions');
  menu.classList.add('hidden');
  menu.replaceChildren();
  source.setAttribute('aria-expanded', 'false');
  source.removeAttribute('aria-activedescendant');
}

function positionCommandSuggestions() {
  const menu = $('#command-suggestions');
  if (menu.classList.contains('hidden')) return;
  const editor = $('#source-code-editor');
  const caret = caretCoordinates(source.selectionStart);
  const margin = 8;
  const left = Math.max(margin, Math.min(caret.left, editor.clientWidth - menu.offsetWidth - margin));
  const below = caret.top + 5;
  const top = below + menu.offsetHeight <= source.clientHeight - margin
    ? below
    : Math.max(margin, caret.top - caret.lineHeight - menu.offsetHeight - 5);
  menu.style.left = `${left}px`;
  menu.style.top = `${top}px`;
}

function renderCommandSuggestions() {
  const menu = $('#command-suggestions');
  if (!state.commandMatches.length) {
    menu.classList.add('hidden');
    source.setAttribute('aria-expanded', 'false');
    source.removeAttribute('aria-activedescendant');
    return;
  }
  menu.replaceChildren();
  state.commandMatches.forEach((item, index) => {
    const button = document.createElement('button');
    button.type = 'button';
    button.id = `command-suggestion-${index}`;
    button.className = 'command-suggestion';
    button.setAttribute('role', 'option');
    button.setAttribute('aria-selected', String(index === state.commandIndex));
    const command = document.createElement('code');
    command.textContent = item[2];
    const description = document.createElement('span');
    description.textContent = state.language === 'zh-CN' ? item[3] : item[4];
    button.append(command, description);
    button.addEventListener('mousedown', (event) => {
      event.preventDefault();
      applyCommandSuggestion(item);
    });
    menu.append(button);
  });
  menu.classList.remove('hidden');
  source.setAttribute('aria-expanded', 'true');
  source.setAttribute('aria-activedescendant', `command-suggestion-${state.commandIndex}`);
  menu.children[state.commandIndex]?.scrollIntoView({ block: 'nearest' });
  requestAnimationFrame(positionCommandSuggestions);
}

function updateCommandSuggestions() {
  if (state.composing || source.selectionStart !== source.selectionEnd) return hideCommandSuggestions();
  const beforeCursor = source.value.slice(0, source.selectionStart);
  const match = beforeCursor.match(/\\([A-Za-z]*)$/);
  if (!match) return hideCommandSuggestions();
  const prefix = match[1].toLowerCase();
  state.commandMatches = latexCommands
    .filter((item) => item[0].toLowerCase().startsWith(prefix))
    .sort((a, b) => Number(b[0].toLowerCase() === prefix) - Number(a[0].toLowerCase() === prefix))
    .slice(0, 12);
  state.commandIndex = 0;
  state.commandRange = { start: source.selectionStart - match[0].length, end: source.selectionStart };
  renderCommandSuggestions();
}

function moveCommandSelection(delta) {
  if (!state.commandMatches.length) return;
  state.commandIndex = (state.commandIndex + delta + state.commandMatches.length) % state.commandMatches.length;
  renderCommandSuggestions();
}

function applyCommandSuggestion(item = state.commandMatches[state.commandIndex]) {
  if (!item || !state.commandRange) return;
  const { start, end } = state.commandRange;
  const marker = item[1].indexOf('◊');
  const insertion = item[1].replace('◊', '');
  source.setRangeText(insertion, start, end, 'end');
  if (marker >= 0) source.selectionStart = source.selectionEnd = start + marker;
  hideCommandSuggestions();
  recordHistory(source.value);
  syncMathfield();
  saveDraft();
  scheduleRender(0);
  source.focus();
}

function t(key) {
  return translations[state.language][key] || translations.en[key] || key;
}

function copyFormatTranslationKey(format) {
  return {
    svg: 'copySvg',
    png: 'copyPng',
    latex: 'copyLatex',
    'markdown-inline': 'copyMarkdownInline',
    'markdown-block': 'copyMarkdownBlock',
    mathml: 'copyMathml',
  }[format] || 'copySvg';
}

function updateCopyAction() {
  const label = $('#copy-main-label');
  const main = $('#copy-main');
  const text = t(copyFormatTranslationKey(state.copyFormat));
  if (label) label.textContent = text;
  if (main) {
    main.title = `Ctrl + Enter · ${text}`;
    main.setAttribute('aria-label', text);
  }
  $$('#copy-menu [data-copy-format]').forEach((button) => {
    button.setAttribute('aria-checked', String(button.dataset.copyFormat === state.copyFormat));
  });
}

function closeCopyMenu() {
  $('#copy-menu').classList.add('hidden');
  $('#copy-menu-button').setAttribute('aria-expanded', 'false');
}

function setCopyFormat(format) {
  if (!copyFormats.has(format)) return;
  state.copyFormat = format;
  localStorage.setItem('formula-editor.copy-format', format);
  updateCopyAction();
  closeCopyMenu();
}

function applyLanguage() {
  document.documentElement.lang = state.language;
  document.title = params.get('native') === '1' ? t('paletteTitle') : `${t('appTitle')} · Excalidraw Manager`;
  $$('#language-select option').forEach((option) => { option.selected = option.value === state.language; });
  $$('[data-i18n]').forEach((element) => {
    const value = t(element.dataset.i18n);
    if (value) element.textContent = value;
  });
  $$('[data-i18n-aria-label]').forEach((element) => {
    const value = t(element.dataset.i18nAriaLabel);
    if (value) element.setAttribute('aria-label', value);
  });
  updateCopyAction();
  populateTools();
  renderCommandSuggestions();
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

function quickGroupLabel(group) {
  return state.language === 'zh-CN' ? group.zh : group.en;
}

function clearQuickCloseTimer() {
  clearTimeout(state.quickCloseTimer);
  state.quickCloseTimer = 0;
}

function positionQuickPopover() {
  const panel = $('#quick-panel');
  const groups = $('#quick-groups');
  const popover = $('#quick-popover');
  const trigger = state.quickGroupTrigger;
  if (!trigger || popover.classList.contains('hidden')) return;
  const panelRect = panel.getBoundingClientRect();
  const triggerRect = trigger.getBoundingClientRect();
  const groupsRect = groups.getBoundingClientRect();
  const margin = 8;
  const preferredLeft = triggerRect.left - panelRect.left;
  const maxLeft = Math.max(margin, panel.clientWidth - popover.offsetWidth - margin);
  popover.style.left = `${Math.max(margin, Math.min(preferredLeft, maxLeft))}px`;
  // Anchor below the complete category strip. Compact mode deliberately keeps
  // all ten triggers on this single row so the pointer path stays unobstructed.
  popover.style.top = `${groupsRect.bottom - panelRect.top + 2}px`;
}

function closeQuickGroup({ returnFocus = false } = {}) {
  clearQuickCloseTimer();
  const trigger = state.quickGroupTrigger;
  $('#quick-popover').classList.add('hidden');
  $$('.tool-trigger').forEach((button) => {
    button.classList.remove('active');
    button.setAttribute('aria-expanded', 'false');
  });
  state.quickGroupId = null;
  state.quickGroupPinned = false;
  state.quickGroupTrigger = null;
  if (returnFocus && trigger) {
    state.quickIgnoreFocus = true;
    trigger.focus({ preventScroll: true });
    queueMicrotask(() => { state.quickIgnoreFocus = false; });
  }
}

function openQuickGroup(group, trigger, { pinned = false, focusFirst = false } = {}) {
  clearQuickCloseTimer();
  state.quickGroupId = group.id;
  state.quickGroupPinned = pinned;
  state.quickGroupTrigger = trigger;
  $$('.tool-trigger').forEach((button) => {
    const active = button === trigger;
    button.classList.toggle('active', active);
    button.setAttribute('aria-expanded', String(active));
  });

  const popover = $('#quick-popover');
  const title = $('#quick-popover-title');
  const grid = $('#quick-symbol-grid');
  title.textContent = quickGroupLabel(group);
  grid.replaceChildren();
  for (const [latex, symbol, zhLabel, enLabel = zhLabel] of group.items) {
    const label = state.language === 'zh-CN' ? zhLabel : enLabel;
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'symbol-button';
    button.setAttribute('role', 'menuitem');
    button.textContent = symbol;
    button.title = `${label} · ${latex.replace('◊', '□')}`;
    button.setAttribute('aria-label', `${label}: ${latex.replace('◊', '')}`);
    button.addEventListener('click', () => {
      insertLatex(latex);
      closeQuickGroup();
    });
    grid.append(button);
  }
  popover.classList.remove('hidden');
  requestAnimationFrame(() => {
    positionQuickPopover();
    if (focusFirst) grid.querySelector('button')?.focus();
  });
}

function scheduleQuickGroupClose() {
  clearQuickCloseTimer();
  if (state.quickGroupPinned) return;
  state.quickCloseTimer = setTimeout(() => closeQuickGroup(), 280);
}

function populateTools() {
  closeQuickGroup();
  const host = $('#quick-groups');
  host.setAttribute('aria-label', t('quickPalette'));
  host.replaceChildren();
  for (const group of quickGroups) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'tool-trigger';
    button.dataset.quickGroup = group.id;
    button.setAttribute('aria-haspopup', 'menu');
    button.setAttribute('aria-expanded', 'false');
    button.setAttribute('aria-controls', 'quick-popover');
    button.setAttribute('aria-label', quickGroupLabel(group));
    button.title = quickGroupLabel(group);

    const icon = document.createElement('span');
    icon.className = 'tool-trigger-icon';
    icon.setAttribute('aria-hidden', 'true');
    icon.textContent = group.icon;
    const name = document.createElement('span');
    name.className = 'tool-trigger-label';
    name.textContent = quickGroupLabel(group);
    const caret = document.createElement('span');
    caret.className = 'tool-trigger-caret';
    caret.setAttribute('aria-hidden', 'true');
    caret.textContent = '▾';
    button.append(icon, name, caret);

    button.addEventListener('pointerenter', () => {
      clearQuickCloseTimer();
      if (state.quickGroupId === group.id && state.quickGroupPinned) return;
      openQuickGroup(group, button);
    });
    button.addEventListener('pointerleave', scheduleQuickGroupClose);
    button.addEventListener('focus', () => {
      if (!state.quickIgnoreFocus) openQuickGroup(group, button);
    });
    button.addEventListener('click', () => {
      if (state.quickGroupId === group.id && state.quickGroupPinned) closeQuickGroup({ returnFocus: true });
      else openQuickGroup(group, button, { pinned: true });
    });
    button.addEventListener('keydown', (event) => {
      const triggers = $$('.tool-trigger');
      const index = triggers.indexOf(button);
      if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
        event.preventDefault();
        const delta = event.key === 'ArrowRight' ? 1 : -1;
        triggers[(index + delta + triggers.length) % triggers.length]?.focus();
      } else if (event.key === 'ArrowDown') {
        event.preventDefault();
        openQuickGroup(group, button, { pinned: true, focusFirst: true });
      } else if (event.key === 'Escape') {
        event.preventDefault();
        closeQuickGroup({ returnFocus: true });
      }
    });
    host.append(button);
  }
}

function setActivePanel(panel) {
  if (panel !== 'quick') closeQuickGroup();
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
  updateSourceHighlight();
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
  if (state.editMode === 'visual') {
    const insertion = mathfieldInsertion(template);
    const inserted = mathfield.insert(insertion.latex, {
      format: 'latex',
      insertionMode: 'replaceSelection',
      selectionMode: insertion.selectionMode,
    });
    if (inserted) syncSourceFromMathfield(true);
    mathfield.focus();
    return inserted;
  }

  const start = source.selectionStart;
  const end = source.selectionEnd;
  const selected = source.value.slice(start, end);
  const marker = template.indexOf('◊');
  let insertion = template.replace('◊', selected);
  let cursor = insertion.length;
  if (marker >= 0) cursor = marker + selected.length;
  commitSource(insertion, false, cursor);
  return true;
}

function syncSourceFromMathfield(recordImmediately = false) {
  source.value = mathfield.getValue('latex');
  $('#character-count').textContent = String(source.value.length);
  updateSourceHighlight();
  saveDraft();
  if (recordImmediately) {
    clearTimeout(state.historyTimer);
    recordHistory(source.value);
  } else {
    scheduleHistory();
  }
  if (recordImmediately) scheduleRender(0);
  else scheduleRender();
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
  // Start directly with <svg>. Excalidraw's paste importer intentionally
  // recognizes SVG text only when the trimmed payload starts with that tag.
  return new XMLSerializer().serializeToString(clone);
}

function scheduleRender(delay = 220) {
  clearTimeout(state.renderTimer);
  const id = ++state.renderId;
  state.renderTimer = setTimeout(() => renderFormula(id), delay);
}

function updatePreviewScale() {
  const svg = preview.querySelector('svg');
  if (!svg || !state.svgElement) return;
  const stage = $('#preview-stage');
  const style = getComputedStyle(stage);
  const horizontalPadding = (Number.parseFloat(style.paddingLeft) || 0) + (Number.parseFloat(style.paddingRight) || 0);
  const availableWidth = Math.max(1, stage.clientWidth - horizontalPadding - 2);
  const naturalWidth = dimensionsFromSvg(state.svgElement).width;
  const displayedWidth = Math.min(naturalWidth * (state.zoom / 100), availableWidth);
  const width = `${displayedWidth.toFixed(2)}px`;
  preview.style.width = width;
  preview.style.transform = 'none';
  svg.style.width = width;
  svg.style.height = 'auto';
}

async function renderFormula(id = ++state.renderId) {
  const latex = cleanLatex(source.value);
  if (!latex) {
    state.svgElement = null;
    state.svgText = '';
    preview.replaceChildren();
    preview.style.removeProperty('width');
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
    preview.classList.remove('hidden');
    updatePreviewScale();
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
    preview.style.removeProperty('width');
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
  updatePreviewScale();
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

async function copySvg() {
  try {
    if (!cleanLatex(source.value)) return showToast(t('emptyFormula'), true), false;
    clearTimeout(state.renderTimer);
    await renderFormula();
    if (!requireSvg()) return false;
    const svg = serializeSvg(state.svgElement);
    if (state.compact && window.parent !== window && state.boardUrl) {
      // Keep the SVG on the clipboard as a fallback even when the parent board
      // handles the seamless paste message successfully.
      try { await writeText(svg); } catch (error) { console.warn('SVG clipboard fallback unavailable:', error); }
      window.parent.postMessage({ type: 'excalidraw-manager:insert-svg', svg }, new URL(state.boardUrl).origin);
      showToast(t('svgSent'));
    } else {
      await writeText(svg);
      showToast(t('copiedSvg'));
    }
    return true;
  } catch (error) {
    console.error(error);
    showToast(error.message || t('copyFailed'), true);
    return false;
  }
}

async function copyPng() {
  try {
    if (!cleanLatex(source.value)) return showToast(t('emptyFormula'), true), false;
    if (!navigator.clipboard?.write || typeof ClipboardItem === 'undefined') {
      throw new Error(t('imageClipboardUnavailable'));
    }
    clearTimeout(state.renderTimer);
    await renderFormula();
    if (!requireSvg()) return false;
    const blob = await svgToRasterBlob('image/png');
    if (!blob) return false;
    await navigator.clipboard.write([new ClipboardItem({ 'image/png': blob })]);
    showToast(t('copiedPng'));
    return true;
  } catch (error) {
    console.error(error);
    showToast(error.message || t('copyFailed'), true);
    return false;
  }
}

async function copySelectedFormat() {
  if (state.copyFormat === 'svg') return copySvg();
  if (state.copyFormat === 'png') return copyPng();
  try {
    if (!cleanLatex(source.value)) return showToast(t('emptyFormula'), true), false;
    await writeText(formatValue(state.copyFormat));
    showToast(t('copied'));
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
    if (url.protocol !== 'http:' || url.username || url.password
      || !['localhost', '127.0.0.1', '[::1]'].includes(url.hostname)) return null;
    return url.href;
  } catch {
    return null;
  }
}

async function copyAndOpenBoard() {
  if (await copySvg()) {
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
    if (!available && !state.compact) status.textContent = t('modelUnavailable');
  } catch (error) {
    console.warn('Model status unavailable:', error);
    state.availableProviders = [];
    $('#recognition-actions').classList.add('hidden');
  }
}

function stageImage(file) {
  if (!file || !supportedImageTypes.has(file.type)) return showToast(t('invalidImage'), true);
  if (file.size > 12 * 1024 * 1024) return showToast(t('imageTooLarge'), true);
  state.ocrGeneration += 1;
  state.ocrController?.abort();
  state.ocrController = null;
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
  state.ocrGeneration += 1;
  state.ocrController?.abort();
  state.ocrController = null;
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

async function recognizeImage({ autoApplyFirst = false } = {}) {
  if (!state.imageFile || !state.availableProviders.length) return showToast(t('modelUnavailable'), true);
  const imageFile = state.imageFile;
  const generation = ++state.ocrGeneration;
  state.ocrController?.abort();
  const controller = new AbortController();
  state.ocrController = controller;
  const button = $('#recognize-button');
  button.disabled = true;
  status.textContent = t('recognizing');
  try {
    const requestId = crypto.randomUUID ? crypto.randomUUID() : `formula-${Date.now()}`;
    const response = await fetch('/api/recognize', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      signal: controller.signal,
      body: JSON.stringify({
        requestId,
        providerId: $('#provider-select').value,
        mode: 'formula',
        input: {
          kind: 'image',
          mediaType: imageFile.type,
          dataBase64: await fileBase64(imageFile),
        },
        languageHints: [state.language, 'en'],
        options: { maxCandidates: 3, outputFormats: ['latex'], preprocessing: 'auto', timeoutMs: 60000, debug: false },
      }),
    });
    const result = await response.json();
    if (!response.ok) throw new Error(result.message || result.error?.message || t('recognitionFailed'));
    if (generation !== state.ocrGeneration || state.imageFile !== imageFile) return false;
    const candidates = (result.candidates || []).filter((candidate) => candidate && typeof candidate.latex === 'string');
    if (!candidates.length) throw new Error(t('recognitionFailed'));
    if (autoApplyFirst) {
      commitSource(candidates[0].latex, true);
      $('#candidate-list').classList.add('hidden');
    } else {
      showCandidates(candidates);
    }
    status.textContent = t(autoApplyFirst ? 'recognizedApplied' : 'recognized');
    showToast(t(autoApplyFirst ? 'recognizedApplied' : 'recognized'));
    return true;
  } catch (error) {
    if (error.name === 'AbortError' || generation !== state.ocrGeneration) return false;
    console.error(error);
    status.textContent = t('recognitionFailed');
    showToast(error.message || t('recognitionFailed'), true);
  } finally {
    if (state.ocrController === controller) state.ocrController = null;
    button.disabled = !state.imageFile || !state.availableProviders.length;
  }
}

async function recognizeCompactImage(file) {
  stageImage(file);
  if (state.imageFile !== file) return;
  try {
    if (!state.availableProviders.length) await refreshModelStatus();
    if (state.imageFile === file && state.availableProviders.length) {
      showToast(t('recognizing'));
      await recognizeImage({ autoApplyFirst: true });
    } else {
      showToast(t('modelUnavailable'), true);
    }
  } finally {
    // Compact mode has no visible image staging controls. Release the in-memory
    // screenshot and its object URL after each attempt instead of retaining an
    // inaccessible image until the palette closes.
    if (state.compact && state.imageFile === file) removeImage();
  }
}

function capturedImageFile(payload) {
  const image = payload?.image;
  if (!image || image.kind !== 'image' || image.mediaType !== 'image/png'
    || typeof image.dataBase64 !== 'string' || !image.dataBase64) {
    throw new Error(t('captureFailed'));
  }
  let binary;
  try {
    binary = atob(image.dataBase64);
  } catch {
    throw new Error(t('captureFailed'));
  }
  if (binary.length > 12 * 1024 * 1024) throw new Error(t('imageTooLarge'));
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) bytes[index] = binary.charCodeAt(index);
  return new File([bytes], `formula-capture-${Date.now()}.png`, { type: 'image/png' });
}

function captureErrorMessage(code, fallback) {
  if (code === 'CAPTURE_CANCELLED') return t('captureCancelled');
  if (code === 'CAPTURE_BUSY') return t('captureBusy');
  if (code === 'CAPTURE_TIMEOUT') return t('captureTimeout');
  if (code === 'CAPTURE_UNAVAILABLE') return t('captureUnavailable');
  if (code === 'CAPTURE_TOO_LARGE') return t('imageTooLarge');
  return fallback || t('captureFailed');
}

async function captureAndRecognize() {
  if (state.captureInProgress) return false;
  if (!state.availableProviders.length) await refreshModelStatus();
  if (!state.availableProviders.length) {
    showToast(t('modelUnavailable'), true);
    return false;
  }
  const button = $('#capture-ocr');
  const controller = new AbortController();
  state.captureInProgress = true;
  state.captureController = controller;
  button.disabled = true;
  showToast(t('captureSelecting'));
  try {
    const response = await fetch('/api/capture', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      cache: 'no-store',
      signal: controller.signal,
      body: '{}',
    });
    const result = await response.json();
    if (!response.ok) {
      const error = new Error(captureErrorMessage(result.error, result.message));
      error.code = result.error;
      throw error;
    }
    const file = capturedImageFile(result);
    await recognizeCompactImage(file);
    return true;
  } catch (error) {
    if (error.name === 'AbortError') return false;
    const cancelled = error.code === 'CAPTURE_CANCELLED';
    if (!cancelled) console.error('Formula screen capture failed:', error);
    showToast(captureErrorMessage(error.code, error.message), !cancelled);
    return false;
  } finally {
    if (state.captureController === controller) state.captureController = null;
    state.captureInProgress = false;
    button.disabled = false;
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
  $('#quick-popover').addEventListener('pointerenter', clearQuickCloseTimer);
  $('#quick-popover').addEventListener('pointerleave', scheduleQuickGroupClose);
  $('#quick-popover-close').addEventListener('click', () => closeQuickGroup({ returnFocus: true }));
  $('#quick-panel').addEventListener('focusout', () => setTimeout(() => {
    if (!$('#quick-panel').contains(document.activeElement)) closeQuickGroup();
  }, 0));
  $('#quick-popover').addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      closeQuickGroup({ returnFocus: true });
      return;
    }
    const buttons = [...$('#quick-symbol-grid').querySelectorAll('button')];
    const index = buttons.indexOf(document.activeElement);
    if (index < 0 || !['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    const firstTop = buttons[0]?.offsetTop;
    const nextRowIndex = buttons.findIndex((button) => button.offsetTop > firstTop);
    const columns = nextRowIndex < 0 ? buttons.length : nextRowIndex;
    let nextIndex = index;
    if (event.key === 'ArrowLeft') nextIndex = index - 1;
    else if (event.key === 'ArrowRight') nextIndex = index + 1;
    else if (event.key === 'ArrowUp') nextIndex = index - columns;
    else if (event.key === 'ArrowDown') nextIndex = index + columns;
    else if (event.key === 'Home') nextIndex = 0;
    else if (event.key === 'End') nextIndex = buttons.length - 1;
    buttons[Math.max(0, Math.min(nextIndex, buttons.length - 1))]?.focus();
  });
  $('#quick-groups').addEventListener('scroll', positionQuickPopover, { passive: true });
  window.addEventListener('resize', () => {
    positionQuickPopover();
    updatePreviewScale();
  }, { passive: true });
  document.addEventListener('pointerdown', (event) => {
    if (state.quickGroupPinned && !$('#quick-panel').contains(event.target)) closeQuickGroup();
  });
  $$('.feature-tab').forEach((button) => button.addEventListener('click', () => setActivePanel(button.dataset.panel)));
  $$('[data-edit-mode]').forEach((button) => button.addEventListener('click', () => setEditMode(button.dataset.editMode)));
  source.addEventListener('input', (event) => {
    syncMathfield();
    saveDraft();
    scheduleHistory();
    scheduleRender();
    if (isCommandTypingInput(event)) updateCommandSuggestions();
    else hideCommandSuggestions();
  });
  source.addEventListener('keydown', (event) => {
    if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
      event.preventDefault();
      event.stopPropagation();
      hideCommandSuggestions();
      copySelectedFormat();
      return;
    }
    const plainSuggestionKey = !event.shiftKey && !event.ctrlKey && !event.metaKey && !event.altKey;
    if (state.commandMatches.length && plainSuggestionKey && (event.key === 'ArrowDown' || event.key === 'ArrowUp')) {
      event.preventDefault();
      moveCommandSelection(event.key === 'ArrowDown' ? 1 : -1);
      return;
    }
    if (state.commandMatches.length && plainSuggestionKey && (event.key === 'Enter' || event.key === 'Tab')) {
      event.preventDefault();
      applyCommandSuggestion();
      return;
    }
    if (state.commandMatches.length && event.key === 'Escape') {
      event.preventDefault();
      hideCommandSuggestions();
      return;
    }
    if (event.key === 'Tab' && plainSuggestionKey) {
      event.preventDefault();
      commitSource('  ');
      return;
    }
    if (event.key === 'Tab') hideCommandSuggestions();
    if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End', 'PageUp', 'PageDown'].includes(event.key)) {
      hideCommandSuggestions();
    }
  });
  source.addEventListener('scroll', () => {
    updateSourceHighlight();
    if (!$('#command-suggestions').classList.contains('hidden')) positionCommandSuggestions();
  });
  source.addEventListener('pointerdown', hideCommandSuggestions);
  source.addEventListener('select', hideCommandSuggestions);
  document.addEventListener('selectionchange', () => {
    if (document.activeElement === source && source.selectionStart !== source.selectionEnd) hideCommandSuggestions();
  });
  source.addEventListener('compositionstart', () => { state.composing = true; hideCommandSuggestions(); });
  source.addEventListener('compositionend', () => { state.composing = false; hideCommandSuggestions(); });
  source.addEventListener('blur', () => setTimeout(() => {
    if (!$('#command-suggestions').matches(':hover')) hideCommandSuggestions();
  }, 100));
  mathfield.addEventListener('input', () => syncSourceFromMathfield());
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
  $('#copy-main').addEventListener('click', copySelectedFormat);
  $('#copy-menu-button').addEventListener('click', (event) => {
    event.stopPropagation();
    $('#download-menu').classList.add('hidden');
    $('#download-menu-button').setAttribute('aria-expanded', 'false');
    const menu = $('#copy-menu');
    const open = menu.classList.contains('hidden');
    menu.classList.toggle('hidden', !open);
    event.currentTarget.setAttribute('aria-expanded', String(open));
  });
  $$('[data-copy-format]').forEach((button) => button.addEventListener('click', () => setCopyFormat(button.dataset.copyFormat)));
  $('#capture-ocr').addEventListener('click', captureAndRecognize);
  $('#download-main').addEventListener('click', () => download('svg'));
  $('#download-menu-button').addEventListener('click', (event) => {
    event.stopPropagation();
    closeCopyMenu();
    const menu = $('#download-menu');
    menu.classList.toggle('hidden');
    event.currentTarget.setAttribute('aria-expanded', String(!menu.classList.contains('hidden')));
  });
  $$('[data-download]').forEach((button) => button.addEventListener('click', () => { $('#download-menu').classList.add('hidden'); download(button.dataset.download); }));
  document.addEventListener('click', () => {
    closeCopyMenu();
    $('#download-menu').classList.add('hidden');
    $('#download-menu-button').setAttribute('aria-expanded', 'false');
  });
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
  dropzone.addEventListener('drop', (event) => stageImage([...event.dataTransfer.files].find((file) => supportedImageTypes.has(file.type))));
  document.addEventListener('paste', (event) => {
    const clipboard = event.clipboardData;
    const files = [...(clipboard?.files || [])];
    for (const item of [...(clipboard?.items || [])]) {
      const file = item.kind === 'file' ? item.getAsFile() : null;
      if (file && !files.includes(file)) files.push(file);
    }
    const file = files.find((item) => supportedImageTypes.has(item.type));
    if (!file) return;
    if (state.compact) {
      event.preventDefault();
      recognizeCompactImage(file);
      return;
    }
    if (state.activePanel !== 'image' || event.target === source || event.target.closest?.('math-field')) return;
    event.preventDefault();
    stageImage(file);
  });
  document.addEventListener('keydown', (event) => {
    if (event.defaultPrevented) return;
    if ((event.ctrlKey || event.metaKey) && event.altKey && event.key.toLowerCase() === 'o') {
      event.preventDefault();
      captureAndRecognize();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && event.key === 'Enter' && state.compact) { event.preventDefault(); copySelectedFormat(); }
    if ((event.ctrlKey || event.metaKey) && event.shiftKey && event.key.toLowerCase() === 'c') { event.preventDefault(); copySelectedFormat(); }
  });
  addEventListener('message', (event) => {
    if (!state.compact || !state.boardUrl || window.parent === window
      || event.source !== window.parent || event.origin !== new URL(state.boardUrl).origin
      || event.data?.type !== 'excalidraw-manager:capture-formula') return;
    captureAndRecognize();
  });
  addEventListener('beforeunload', () => {
    state.captureController?.abort();
    removeImage();
  });
}

async function initialize() {
  const storedLanguage = localStorage.getItem('formula-editor.language');
  if (!params.has('lang') && storedLanguage) state.language = storedLanguage === 'zh-CN' ? 'zh-CN' : 'en';
  document.body.classList.toggle('compact-mode', state.compact);
  document.documentElement.classList.toggle('compact-mode', state.compact);
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
  if (state.compact) {
    refreshModelStatus();
    requestAnimationFrame(() => source.focus());
  }
  loadMathJax().catch((error) => { console.error(error); showToast(t('mathEngineFailed'), true); });
}

initialize();
