import { useCallback, useEffect, useMemo, useState } from "react";
import { listen } from "@tauri-apps/api/event";
import { open } from "@tauri-apps/plugin-dialog";
import { register, unregister } from "@tauri-apps/plugin-global-shortcut";
import { openUrl } from "@tauri-apps/plugin-opener";
import {
  Activity,
  ChevronDown,
  ChevronRight,
  Diamond,
  ExternalLink,
  FilePlus,
  Folder,
  FolderPlus,
  Play,
  Plus,
  RefreshCw,
  Settings,
  Sigma,
  Square,
  X,
} from "lucide-react";
import appIcon from "../../src-tauri/icons/128x128.png";
import { api } from "./api";
import type {
  AppSettings,
  FileEntry,
  FormulaOcrStatus,
  RunningBoard,
  RunningFormulaEditor,
  ThemePreference,
  WorkspaceSnapshot,
} from "./types";

type Selection = FileEntry;
type CreateKind = "board" | "folder";

const fallbackSnapshot: WorkspaceSnapshot = {
  settings: {
    roots: [],
    startPort: 6417,
    theme: "system",
    language: "system",
    formulaOcrEnabled: false,
    formulaOcrRoot: "",
    recentFiles: [],
    aliases: {},
  },
  appInfo: {
    platform: "browser",
    architecture: "preview",
    version: "0.5.0",
    settingsPath: "在 Tauri 桌面应用中启动后可用",
    libraryPath: "在 Tauri 桌面应用中启动后可用",
  },
};

const previewRoot = "/Users/you/Documents/Excalidraw";
const previewSnapshot: WorkspaceSnapshot = {
  ...fallbackSnapshot,
  settings: { ...fallbackSnapshot.settings, roots: [previewRoot] },
};

function previewWorkspaceEnabled() {
  return !runningInTauri() && new URLSearchParams(window.location.search).get("preview") === "workspace";
}

function previewDirectoryEntries(path: string): FileEntry[] {
  if (path !== previewRoot) return [];
  return [
    { name: "产品", path: `${previewRoot}/产品`, kind: "directory", modifiedAt: Date.now() - 3_600_000 },
    { name: "研究笔记", path: `${previewRoot}/研究笔记`, kind: "directory", modifiedAt: Date.now() - 86_400_000 },
    { name: "路线图.excalidraw", path: `${previewRoot}/路线图.excalidraw`, kind: "board", modifiedAt: Date.now() - 720_000 },
    { name: "系统架构.excalidraw", path: `${previewRoot}/系统架构.excalidraw`, kind: "board", modifiedAt: Date.now() - 8_400_000 },
  ];
}

function runningInTauri() {
  return "__TAURI_INTERNALS__" in window;
}

function errorText(error: unknown) {
  return error instanceof Error ? error.message : String(error);
}

function fileName(path: string) {
  return path.split(/[\\/]/).filter(Boolean).at(-1) || path;
}

function directorySelection(path: string): Selection {
  return {
    name: fileName(path),
    path,
    kind: "directory",
    modifiedAt: null,
  };
}

interface DirectoryNodeProps {
  path: string;
  label: string;
  depth: number;
  initiallyOpen?: boolean;
  selectedPath: string | null;
  refreshToken: number;
  onSelect: (entry: Selection) => void;
  onError: (message: string) => void;
}

function DirectoryNode({
  path,
  label,
  depth,
  initiallyOpen = false,
  selectedPath,
  refreshToken,
  onSelect,
  onError,
}: DirectoryNodeProps) {
  const [expanded, setExpanded] = useState(initiallyOpen);
  const [entries, setEntries] = useState<FileEntry[]>([]);
  const [loading, setLoading] = useState(false);

  const load = useCallback(async () => {
    if (!runningInTauri()) {
      if (previewWorkspaceEnabled()) setEntries(previewDirectoryEntries(path));
      return;
    }
    setLoading(true);
    try {
      setEntries(await api.listDirectory(path));
    } catch (error) {
      onError(errorText(error));
    } finally {
      setLoading(false);
    }
  }, [onError, path]);

  useEffect(() => {
    if (expanded) void load();
  }, [expanded, load, refreshToken]);

  return (
    <div className="tree-node">
      <div
        className={`tree-row directory-row ${selectedPath === path ? "selected" : ""}`}
        style={{ paddingLeft: 10 + depth * 16 }}
        onClick={() => onSelect(directorySelection(path))}
      >
        <button
          className="disclosure"
          aria-label={expanded ? "折叠文件夹" : "展开文件夹"}
          onClick={(event) => {
            event.stopPropagation();
            setExpanded((value) => !value);
          }}
        >
          {expanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
        </button>
        <Folder className="entry-icon folder-icon" size={16} aria-hidden="true" />
        <span className="entry-label" title={path}>{label}</span>
        {loading && <span className="tree-spinner" aria-label="加载中" />}
      </div>

      {expanded && (
        <div className="tree-children">
          {entries.map((entry) =>
            entry.kind === "directory" ? (
              <DirectoryNode
                key={entry.path}
                path={entry.path}
                label={entry.name}
                depth={depth + 1}
                selectedPath={selectedPath}
                refreshToken={refreshToken}
                onSelect={onSelect}
                onError={onError}
              />
            ) : (
              <button
                key={entry.path}
                className={`tree-row board-row ${selectedPath === entry.path ? "selected" : ""}`}
                style={{ paddingLeft: 34 + depth * 16 }}
                onClick={() => onSelect(entry)}
                title={entry.path}
              >
                <Diamond className="entry-icon board-icon" size={15} aria-hidden="true" />
                <span className="entry-label">{entry.name}</span>
              </button>
            ),
          )}
          {!loading && entries.length === 0 && (
            <div className="tree-empty" style={{ paddingLeft: 42 + depth * 16 }}>
              空文件夹
            </div>
          )}
        </div>
      )}
    </div>
  );
}

interface SettingsViewProps {
  settings: AppSettings;
  appInfo: WorkspaceSnapshot["appInfo"];
  onSave: (settings: AppSettings) => Promise<void>;
}

function SettingsView({ settings, appInfo, onSave }: SettingsViewProps) {
  const [draft, setDraft] = useState(settings);
  const [saving, setSaving] = useState(false);
  const [ocrStatus, setOcrStatus] = useState<FormulaOcrStatus | null>(null);
  const [checkingOcr, setCheckingOcr] = useState(false);

  useEffect(() => setDraft(settings), [settings]);

  const refreshOcrStatus = useCallback(async () => {
    if (!runningInTauri()) return;
    setCheckingOcr(true);
    try {
      setOcrStatus(await api.getFormulaOcrStatus());
    } finally {
      setCheckingOcr(false);
    }
  }, []);

  useEffect(() => {
    void refreshOcrStatus();
  }, [refreshOcrStatus, settings.formulaOcrEnabled, settings.formulaOcrRoot]);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setSaving(true);
    try {
      await onSave(draft);
      await refreshOcrStatus();
    } finally {
      setSaving(false);
    }
  }

  async function chooseOcrRoot() {
    if (!runningInTauri()) return;
    const selected = await open({
      directory: true,
      multiple: false,
      title: "选择 FormulaOCR 安装目录",
    });
    if (typeof selected === "string") {
      setDraft((current) => ({ ...current, formulaOcrRoot: selected }));
    }
  }

  const ocrCopy = (() => {
    if (!ocrStatus) return { title: "尚未检查", detail: "保存设置后检查本机 OCR 环境。" };
    switch (ocrStatus.state) {
      case "disabled":
        return { title: "未启用", detail: "公式编辑器仍可正常手动输入和渲染。" };
      case "notConfigured":
        return { title: "待选择目录", detail: "选择安装脚本生成的 FormulaOCR 根目录。" };
      case "notInstalled":
        return { title: "环境不完整", detail: "在该目录运行安装脚本后，再刷新状态。" };
      case "ready":
        return { title: "已安装", detail: "打开公式编辑器时会自动启动本地识别服务。" };
      case "running":
        return { title: "正在运行", detail: `本地识别服务端口 ${ocrStatus.port}，仅监听 127.0.0.1。` };
      case "error":
        return { title: "启动失败", detail: "重新运行安装脚本检查依赖和模型文件。" };
    }
  })();

  return (
    <div className="content-scroll">
      <div className="content-header">
        <p className="eyebrow">应用偏好</p>
        <h1>设置</h1>
        <p>这些设置保存在本机，不会上传到网络。</p>
      </div>

      <form className="settings-form" onSubmit={submit}>
        <section className="settings-card">
          <h2>外观与语言</h2>
          <label>
            <span>主题</span>
            <select
              value={draft.theme}
              onChange={(event) =>
                setDraft({ ...draft, theme: event.target.value as ThemePreference })
              }
            >
              <option value="system">跟随系统</option>
              <option value="light">浅色</option>
              <option value="dark">深色</option>
            </select>
          </label>
          <label>
            <span>界面语言</span>
            <select
              value={draft.language}
              onChange={(event) => setDraft({ ...draft, language: event.target.value })}
            >
              <option value="system">跟随系统</option>
              <option value="zh-CN">简体中文</option>
              <option value="en">English</option>
            </select>
          </label>
        </section>

        <section className="settings-card">
          <h2>画板服务</h2>
          <label>
            <span>起始端口</span>
            <input
              type="number"
              min={1024}
              max={65535}
              value={draft.startPort}
              onChange={(event) =>
                setDraft({ ...draft, startPort: Number(event.target.value) })
              }
            />
          </label>
          <p className="field-help">启动画板时从该端口开始查找可用的本机端口。</p>
        </section>

        <section className="settings-card ocr-settings-card">
          <div className="settings-section-heading">
            <div>
              <h2>本地公式 OCR</h2>
              <p>把公式截图识别成可编辑的 LaTeX；图片和识别过程不会离开本机。</p>
            </div>
            <label className="setting-toggle">
              <input
                type="checkbox"
                checked={draft.formulaOcrEnabled}
                onChange={(event) =>
                  setDraft({ ...draft, formulaOcrEnabled: event.target.checked })
                }
              />
              <span aria-hidden="true" />
              <b>{draft.formulaOcrEnabled ? "已启用" : "未启用"}</b>
            </label>
          </div>

          <label className="ocr-root-field">
            <span>安装目录</span>
            <div className="ocr-path-control">
              <input
                value={draft.formulaOcrRoot}
                onChange={(event) =>
                  setDraft({ ...draft, formulaOcrRoot: event.target.value })
                }
                placeholder="选择 FormulaOCR 根目录"
                spellCheck={false}
              />
              <button className="secondary-button" type="button" onClick={() => void chooseOcrRoot()}>
                选择…
              </button>
            </div>
          </label>

          <div className={`ocr-status-panel state-${ocrStatus?.state || "unknown"}`} title={ocrStatus?.message}>
            <span className="ocr-status-dot" aria-hidden="true" />
            <div>
              <strong>{checkingOcr ? "正在检查…" : ocrCopy.title}</strong>
              <p>{checkingOcr ? "正在读取隔离环境和模型清单。" : ocrCopy.detail}</p>
            </div>
            <button className="quiet-button" type="button" disabled={checkingOcr} onClick={() => void refreshOcrStatus()}>
              刷新状态
            </button>
          </div>

          <p className="field-help ocr-license-note">
            模型不会随应用静默下载。请先阅读上游 CC BY-NC-SA 权重许可，再运行项目中的
            <code> scripts/install-formula-ocr.sh </code>并显式确认许可。
          </p>
        </section>

        <section className="settings-card paths-card">
          <h2>本机数据</h2>
          <div>
            <span>设置文件</span>
            <code>{appInfo.settingsPath}</code>
          </div>
          <div>
            <span>共享素材库</span>
            <code>{appInfo.libraryPath}</code>
          </div>
        </section>

        <div className="form-actions">
          <button className="primary-button" type="submit" disabled={saving}>
            {saving ? "正在保存…" : "保存设置"}
          </button>
        </div>
      </form>
    </div>
  );
}

interface CreateDialogProps {
  kind: CreateKind;
  parent: string;
  onCancel: () => void;
  onCreate: (name: string) => Promise<void>;
}

function CreateDialog({ kind, parent, onCancel, onCreate }: CreateDialogProps) {
  const [name, setName] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!name.trim()) return;
    setSubmitting(true);
    try {
      await onCreate(name);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={onCancel}>
      <form
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="create-dialog-title"
        onSubmit={submit}
        onMouseDown={(event) => event.stopPropagation()}
        onKeyDown={(event) => {
          if (event.key === "Escape") onCancel();
        }}
      >
        <p className="eyebrow">{kind === "board" ? "新建画板" : "新建文件夹"}</p>
        <h2 id="create-dialog-title">{kind === "board" ? "创建 Excalidraw 画板" : "创建文件夹"}</h2>
        <p className="modal-path">位置：{parent}</p>
        <label className="modal-field">
          <span>名称</span>
          <input
            autoFocus
            value={name}
            maxLength={120}
            placeholder={kind === "board" ? "例如：产品构思" : "例如：项目草图"}
            onChange={(event) => setName(event.target.value)}
          />
        </label>
        <div className="modal-actions">
          <button type="button" className="secondary-button" onClick={onCancel}>取消</button>
          <button type="submit" className="primary-button" disabled={submitting || !name.trim()}>
            {submitting ? "正在创建…" : "创建"}
          </button>
        </div>
      </form>
    </div>
  );
}

interface DirectoryContentsProps {
  path: string;
  refreshToken: number;
  onSelect: (entry: Selection) => void;
  onError: (message: string) => void;
}

function DirectoryContents({ path, refreshToken, onSelect, onError }: DirectoryContentsProps) {
  const [entries, setEntries] = useState<FileEntry[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    setLoading(true);
    if (!runningInTauri()) {
      setEntries(previewWorkspaceEnabled() ? previewDirectoryEntries(path) : []);
      setLoading(false);
      return () => {
        active = false;
      };
    }
    api.listDirectory(path)
      .then((items) => {
        if (active) setEntries(items);
      })
      .catch((loadError) => {
        if (active) onError(errorText(loadError));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [onError, path, refreshToken]);

  if (loading) {
    return (
      <div className="directory-loading" role="status">
        <span className="tree-spinner" />
        正在读取文件夹
      </div>
    );
  }

  return (
    <section className="directory-browser" aria-label="文件夹内容">
      <div className="file-list-header" aria-hidden="true">
        <span>名称</span>
        <span>类型</span>
        <span>修改时间</span>
      </div>
      <div className="file-list">
        {entries.map((entry) => (
          <button
            key={entry.path}
            className="file-list-row"
            onClick={() => onSelect(entry)}
            title={entry.path}
          >
            <span className="file-list-name">
              {entry.kind === "directory"
                ? <Folder size={16} aria-hidden="true" />
                : <Diamond size={15} aria-hidden="true" />}
              <span>{entry.name}</span>
            </span>
            <span>{entry.kind === "directory" ? "文件夹" : "画板"}</span>
            <span>{entry.modifiedAt ? new Date(entry.modifiedAt).toLocaleString() : "—"}</span>
          </button>
        ))}
        {entries.length === 0 && (
          <div className="directory-empty">
            <Folder size={22} aria-hidden="true" />
            <span>这个文件夹还是空的</span>
            <small>可以在上方新建画板或文件夹。</small>
          </div>
        )}
      </div>
    </section>
  );
}

export default function App() {
  const [snapshot, setSnapshot] = useState<WorkspaceSnapshot | null>(null);
  const [selection, setSelection] = useState<Selection | null>(null);
  const [showSettings, setShowSettings] = useState(false);
  const [createKind, setCreateKind] = useState<CreateKind | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);
  const [runningBoards, setRunningBoards] = useState<RunningBoard[]>([]);
  const [formulaEditor, setFormulaEditor] = useState<RunningFormulaEditor | null>(null);
  const [processBusy, setProcessBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const reportError = useCallback((message: string) => {
    setError(message);
    setNotice(null);
  }, []);

  useEffect(() => {
    let active = true;
    async function load() {
      try {
        const value = runningInTauri()
          ? await api.bootstrap()
          : previewWorkspaceEnabled() ? previewSnapshot : fallbackSnapshot;
        if (active) {
          setSnapshot(value);
          if (previewWorkspaceEnabled()) setSelection(directorySelection(previewRoot));
        }
      } catch (loadError) {
        if (active) reportError(errorText(loadError));
      }
    }
    void load();
    return () => {
      active = false;
    };
  }, [reportError]);

  const refreshRunningBoards = useCallback(async () => {
    if (!runningInTauri()) return;
    try {
      setRunningBoards(await api.listRunningBoards());
    } catch (refreshError) {
      reportError(errorText(refreshError));
    }
  }, [reportError]);

  const refreshFormulaEditor = useCallback(async () => {
    if (!runningInTauri()) return;
    try {
      setFormulaEditor(await api.getFormulaEditor());
    } catch (refreshError) {
      reportError(errorText(refreshError));
    }
  }, [reportError]);

  useEffect(() => {
    if (!snapshot || !runningInTauri()) return;
    void refreshRunningBoards();
    void refreshFormulaEditor();
    const timer = window.setInterval(() => {
      void refreshRunningBoards();
      void refreshFormulaEditor();
    }, 2000);
    return () => window.clearInterval(timer);
  }, [refreshFormulaEditor, refreshRunningBoards, snapshot]);

  useEffect(() => {
    const theme = snapshot?.settings.theme || "system";
    document.documentElement.dataset.theme = theme;
  }, [snapshot?.settings.theme]);

  const targetDirectory = useMemo(() => {
    if (!selection) return snapshot?.settings.roots[0] || null;
    if (selection.kind === "directory") return selection.path;
    const separator = Math.max(selection.path.lastIndexOf("/"), selection.path.lastIndexOf("\\"));
    return separator >= 0 ? selection.path.slice(0, separator) : null;
  }, [selection, snapshot?.settings.roots]);

  const selectedRunningBoard = useMemo(
    () => runningBoards.find((board) => board.filePath === selection?.path) || null,
    [runningBoards, selection?.path],
  );

  async function addWorkspace() {
    if (!runningInTauri()) {
      setNotice("浏览器预览模式不能选择本机文件夹，请运行 Tauri 桌面应用。");
      return;
    }
    const selected = await open({ directory: true, multiple: false, title: "选择 Excalidraw 工作区" });
    if (typeof selected !== "string") return;
    try {
      const settings = await api.addWorkspace(selected);
      setSnapshot((current) => current && { ...current, settings });
      setSelection(directorySelection(selected));
      setNotice("工作区已添加");
      setError(null);
    } catch (addError) {
      reportError(errorText(addError));
    }
  }

  async function removeWorkspace(root: string) {
    if (!window.confirm(`从管理器移除工作区？\n\n${root}\n\n磁盘中的文件不会删除。`)) return;
    try {
      const settings = await api.removeWorkspace(root);
      setSnapshot((current) => current && { ...current, settings });
      if (selection?.path === root || selection?.path.startsWith(`${root}/`)) setSelection(null);
      setNotice("工作区已移除，原文件保持不变");
      setError(null);
    } catch (removeError) {
      reportError(errorText(removeError));
    }
  }

  async function saveSettings(settings: AppSettings) {
    try {
      const saved = runningInTauri() ? await api.saveSettings(settings) : settings;
      setSnapshot((current) => current && { ...current, settings: saved });
      setNotice("设置已保存");
      setError(null);
    } catch (saveError) {
      reportError(errorText(saveError));
    }
  }

  async function createItem(name: string) {
    if (!targetDirectory || !createKind) return;
    try {
      const created = createKind === "board"
        ? await api.createBoard(targetDirectory, name)
        : await api.createFolder(targetDirectory, name);
      setCreateKind(null);
      setSelection(created);
      setRefreshToken((value) => value + 1);
      setNotice(createKind === "board" ? "画板已创建" : "文件夹已创建");
      setError(null);
    } catch (createError) {
      reportError(errorText(createError));
    }
  }

  const openFormulaEditor = useCallback(async () => {
    if (!snapshot || !runningInTauri()) return;
    try {
      const editor = await api.startFormulaEditor(snapshot.settings.language);
      setFormulaEditor(editor);
      await openUrl(editor.url);
      setNotice("公式编辑器已打开");
      setError(null);
    } catch (formulaError) {
      reportError(errorText(formulaError));
    }
  }, [reportError, snapshot]);

  useEffect(() => {
    if (!snapshot || !runningInTauri()) return;
    let disposed = false;
    let removeEventListener: (() => void) | undefined;
    const shortcut = "CommandOrControl+Shift+F";

    void listen("open-formula-editor", () => void openFormulaEditor()).then((remove) => {
      if (disposed) remove();
      else removeEventListener = remove;
    });
    void unregister(shortcut)
      .catch(() => undefined)
      .then(() => {
        if (!disposed) return register(shortcut, () => void openFormulaEditor());
        return undefined;
      })
      .catch(() => setNotice("公式编辑器快捷键未能注册，可以继续使用工具栏按钮。"));

    return () => {
      disposed = true;
      removeEventListener?.();
      void unregister(shortcut).catch(() => undefined);
    };
  }, [openFormulaEditor, snapshot]);

  async function startSelectedBoard() {
    if (!snapshot || !selection || selection.kind !== "board") return;
    setProcessBusy(true);
    try {
      const editor = await api.startFormulaEditor(snapshot.settings.language);
      setFormulaEditor(editor);
      const board = await api.startBoard(
        selection.path,
        null,
        snapshot.settings.theme,
        editor.url,
      );
      await refreshRunningBoards();
      await openUrl(board.url);
      setNotice(`画板已在端口 ${board.port} 启动`);
      setError(null);
    } catch (startError) {
      reportError(errorText(startError));
    } finally {
      setProcessBusy(false);
    }
  }

  async function openRunningBoard(board: RunningBoard) {
    try {
      await openUrl(board.url);
      setNotice(`已打开 ${board.name}`);
      setError(null);
    } catch (openError) {
      reportError(errorText(openError));
    }
  }

  async function stopRunningBoard(board: RunningBoard) {
    setProcessBusy(true);
    try {
      await api.stopBoard(board.pid);
      await refreshRunningBoards();
      setNotice(`${board.name} 已停止`);
      setError(null);
    } catch (stopError) {
      reportError(errorText(stopError));
    } finally {
      setProcessBusy(false);
    }
  }

  if (!snapshot) {
    return (
      <main className="loading-screen">
        <div className="app-mark"><img className="app-logo" src={appIcon} alt="" /></div>
        <p>{error || "正在载入 Excalidraw Manager…"}</p>
      </main>
    );
  }

  return (
    <main className="app-shell">
      <a className="skip-link" href="#main-content">跳到主要内容</a>
      <aside className="sidebar">
        <div className="sidebar-title">
          <div className="app-mark small"><img className="app-logo" src={appIcon} alt="" /></div>
          <div>
            <strong>Excalidraw</strong>
            <span>Manager</span>
          </div>
        </div>

        <div className="sidebar-heading">
          <span>工作区</span>
          <button className="icon-button" onClick={() => void addWorkspace()} title="添加工作区" aria-label="添加工作区">
            <Plus size={15} />
          </button>
        </div>

        <div className="workspace-list">
          {snapshot.settings.roots.map((root) => (
            <div className="workspace" key={root}>
              <DirectoryNode
                path={root}
                label={fileName(root)}
                depth={0}
                initiallyOpen
                selectedPath={selection?.path || null}
                refreshToken={refreshToken}
                onSelect={(entry) => {
                  setSelection(entry);
                  setShowSettings(false);
                }}
                onError={reportError}
              />
              <button className="remove-workspace" onClick={() => void removeWorkspace(root)} title="移除工作区" aria-label={`移除工作区 ${fileName(root)}`}>
                <X size={13} />
              </button>
            </div>
          ))}
          {snapshot.settings.roots.length === 0 && (
            <button className="empty-workspaces" onClick={() => void addWorkspace()}>
              <Plus size={15} />
              添加第一个工作区
            </button>
          )}
        </div>

        {(runningBoards.length > 0 || formulaEditor) && (
          <div className="running-panel">
            <div className="running-heading">
              <span>运行中</span>
              <span className="running-count">{runningBoards.length + (formulaEditor ? 1 : 0)}</span>
            </div>
            {formulaEditor && (
              <button className="running-item" onClick={() => void openFormulaEditor()}>
                <Sigma className="running-icon formula-icon" size={14} aria-hidden="true" />
                <span>公式编辑器</span>
                <small>:{formulaEditor.port}</small>
              </button>
            )}
            {runningBoards.map((board) => (
              <button
                key={board.pid}
                className="running-item"
                onClick={() => {
                  setSelection({
                    name: board.name,
                    path: board.filePath,
                    kind: "board",
                    modifiedAt: null,
                  });
                  setShowSettings(false);
                }}
              >
                <Activity className="running-icon" size={14} aria-hidden="true" />
                <span>{board.name}</span>
                <small>:{board.port}</small>
              </button>
            ))}
          </div>
        )}

        <button
          className={`sidebar-settings ${showSettings ? "active" : ""}`}
          onClick={() => setShowSettings(true)}
        >
          <Settings size={15} /> 设置
        </button>
      </aside>

      <section className="main-pane" id="main-content" tabIndex={-1}>
        <header className="toolbar">
          <div className="toolbar-group">
            <button className="toolbar-button" onClick={() => void addWorkspace()}>
              <Plus size={15} /> 工作区
            </button>
            <span className="toolbar-divider" />
            <button
              className="toolbar-button"
              disabled={!targetDirectory || !runningInTauri()}
              onClick={() => setCreateKind("board")}
            >
              <FilePlus size={15} /> 新建画板
            </button>
            <button
              className="toolbar-button"
              disabled={!targetDirectory || !runningInTauri()}
              onClick={() => setCreateKind("folder")}
            >
              <FolderPlus size={15} /> 新建文件夹
            </button>
            <button className="toolbar-button" onClick={() => void openFormulaEditor()} title="公式编辑器（Command/Ctrl + Shift + F）">
              <Sigma size={15} /> 公式编辑器
            </button>
          </div>
          <button className="toolbar-button compact" onClick={() => setRefreshToken((value) => value + 1)}>
            <RefreshCw size={14} /> 刷新
          </button>
        </header>

        {showSettings ? (
          <SettingsView settings={snapshot.settings} appInfo={snapshot.appInfo} onSave={saveSettings} />
        ) : selection ? (
          <div className="content-scroll">
            <header className="selection-header">
              <div className={`selection-icon ${selection.kind}`}>
                {selection.kind === "board"
                  ? <Diamond size={20} aria-hidden="true" />
                  : <Folder size={20} aria-hidden="true" />}
              </div>
              <div className="selection-copy">
                <p className="eyebrow">{selection.kind === "board" ? "Excalidraw 画板" : "文件夹"}</p>
                <h1>{selection.name}</h1>
                <p className="path-text">{selection.path}</p>
              </div>
              <div className="selection-actions">
                {selection.kind === "board" ? (
                  selectedRunningBoard ? (
                    <>
                      <button
                        className="primary-button"
                        disabled={processBusy}
                        onClick={() => void openRunningBoard(selectedRunningBoard)}
                      >
                        <ExternalLink size={14} /> 打开
                      </button>
                      <button
                        className="secondary-button danger-button"
                        disabled={processBusy}
                        onClick={() => void stopRunningBoard(selectedRunningBoard)}
                      >
                        <Square size={12} fill="currentColor" /> 停止
                      </button>
                    </>
                  ) : (
                    <button
                      className="primary-button"
                      disabled={processBusy || !runningInTauri()}
                      onClick={() => void startSelectedBoard()}
                    >
                      <Play size={14} fill="currentColor" />
                      {processBusy ? "正在启动" : "启动画板"}
                    </button>
                  )
                ) : (
                  <>
                    <button className="primary-button" onClick={() => setCreateKind("board")}>
                      <FilePlus size={14} /> 新建画板
                    </button>
                    <button className="secondary-button" onClick={() => setCreateKind("folder")}>
                      <FolderPlus size={14} /> 新建文件夹
                    </button>
                  </>
                )}
              </div>
            </header>

            {selection.kind === "directory" ? (
              <DirectoryContents
                path={selection.path}
                refreshToken={refreshToken}
                onSelect={(entry) => setSelection(entry)}
                onError={reportError}
              />
            ) : (
              <section className="board-details" aria-label="画板信息">
                <div className={`service-summary ${selectedRunningBoard ? "online" : ""}`}>
                  <Activity size={17} aria-hidden="true" />
                  <div>
                    <strong>{selectedRunningBoard ? "画板服务正在运行" : "画板尚未启动"}</strong>
                    <span>
                      {selectedRunningBoard
                        ? `本机端口 ${selectedRunningBoard.port} · PID ${selectedRunningBoard.pid}`
                        : "启动时会自动选择空闲端口，并接入共享素材库与公式工具。"}
                    </span>
                  </div>
                </div>
                <dl className="metadata-list">
                  <div><dt>文件类型</dt><dd>.excalidraw</dd></div>
                  <div><dt>修改时间</dt><dd>{selection.modifiedAt ? new Date(selection.modifiedAt).toLocaleString() : "—"}</dd></div>
                  <div><dt>工作区状态</dt><dd>已纳入管理</dd></div>
                  <div><dt>公式工具</dt><dd>{formulaEditor ? `运行于端口 ${formulaEditor.port}` : "启动画板时自动准备"}</dd></div>
                </dl>
              </section>
            )}
          </div>
        ) : (
          <div className="welcome-view">
            <div className="welcome-mark" aria-hidden="true"><img className="app-logo" src={appIcon} alt="" /></div>
            <h1>{snapshot.settings.roots.length ? "选择一个画板或文件夹" : "添加第一个工作区"}</h1>
            <p>
              {snapshot.settings.roots.length
                ? "从左侧开始浏览；画板、素材库和设置始终留在本机。"
                : "选择存放 .excalidraw 文件的本机文件夹。"}
            </p>
            <button
              className="primary-button large"
              onClick={() => {
                const firstRoot = snapshot.settings.roots[0];
                if (firstRoot) setSelection(directorySelection(firstRoot));
                else void addWorkspace();
              }}
            >
              {snapshot.settings.roots.length ? <Folder size={15} /> : <Plus size={15} />}
              {snapshot.settings.roots.length ? "浏览工作区" : "选择工作区"}
            </button>
          </div>
        )}

        <footer className="statusbar">
          <span className="platform-pill">{snapshot.appInfo.platform} · {snapshot.appInfo.architecture}</span>
          <span className={error ? "status-error" : ""} role={error ? "alert" : "status"} aria-live="polite">
            {error || notice || "就绪"}
          </span>
          <span>v{snapshot.appInfo.version}</span>
        </footer>
      </section>

      {createKind && targetDirectory && (
        <CreateDialog
          kind={createKind}
          parent={targetDirectory}
          onCancel={() => setCreateKind(null)}
          onCreate={createItem}
        />
      )}
    </main>
  );
}
