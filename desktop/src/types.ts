export type ThemePreference = "system" | "light" | "dark";

export interface AppSettings {
  roots: string[];
  startPort: number;
  theme: ThemePreference;
  language: string;
  formulaOcrEnabled: boolean;
  formulaOcrRoot: string;
  recentFiles: string[];
  aliases: Record<string, string>;
}

export interface AppInfo {
  platform: string;
  architecture: string;
  version: string;
  settingsPath: string;
  libraryPath: string;
}

export type EntryKind = "directory" | "board";

export interface FileEntry {
  name: string;
  path: string;
  kind: EntryKind;
  modifiedAt: number | null;
}

export interface WorkspaceSnapshot {
  settings: AppSettings;
  appInfo: AppInfo;
}

export interface RunningBoard {
  pid: number;
  port: number;
  url: string;
  filePath: string;
  name: string;
  theme: ThemePreference;
  startedAt: number;
}

export interface RunningFormulaEditor {
  pid: number;
  port: number;
  url: string;
  startedAt: number;
}

export type FormulaOcrState =
  | "disabled"
  | "notConfigured"
  | "notInstalled"
  | "ready"
  | "running"
  | "error";

export interface FormulaOcrStatus {
  enabled: boolean;
  configured: boolean;
  installed: boolean;
  running: boolean;
  state: FormulaOcrState;
  message: string;
  root: string;
  port: number | null;
}
