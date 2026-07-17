import { invoke } from "@tauri-apps/api/core";
import type {
  AppInfo,
  AppSettings,
  FileEntry,
  FormulaOcrStatus,
  RunningBoard,
  RunningFormulaEditor,
  WorkspaceSnapshot,
} from "./types";

export const api = {
  bootstrap: () => invoke<WorkspaceSnapshot>("bootstrap"),
  appInfo: () => invoke<AppInfo>("app_info"),
  loadSettings: () => invoke<AppSettings>("load_settings"),
  saveSettings: (settings: AppSettings) =>
    invoke<AppSettings>("save_settings", { settings }),
  addWorkspace: (path: string) =>
    invoke<AppSettings>("add_workspace", { path }),
  removeWorkspace: (path: string) =>
    invoke<AppSettings>("remove_workspace", { path }),
  listDirectory: (path: string) =>
    invoke<FileEntry[]>("list_directory", { path }),
  createBoard: (parent: string, name: string) =>
    invoke<FileEntry>("create_board", { parent, name }),
  createFolder: (parent: string, name: string) =>
    invoke<FileEntry>("create_folder", { parent, name }),
  startBoard: (
    filePath: string,
    requestedPort: number | null,
    theme: string,
    formulaUrl: string | null,
  ) => invoke<RunningBoard>("start_board", { filePath, requestedPort, theme, formulaUrl }),
  listRunningBoards: () => invoke<RunningBoard[]>("list_running_boards"),
  stopBoard: (pid: number) => invoke<void>("stop_board", { pid }),
  stopAllBoards: () => invoke<number>("stop_all_boards"),
  startFormulaEditor: (language: string) =>
    invoke<RunningFormulaEditor>("start_formula_editor", { language }),
  getFormulaEditor: () =>
    invoke<RunningFormulaEditor | null>("get_formula_editor"),
  stopFormulaEditor: () => invoke<void>("stop_formula_editor"),
  getFormulaOcrStatus: () =>
    invoke<FormulaOcrStatus>("get_formula_ocr_status"),
};
