use crate::formula::FormulaManager;
use crate::models::{
    AppInfo, AppSettings, FileEntry, FormulaOcrStatus, RunningBoard, RunningFormulaEditor,
    WorkspaceSnapshot,
};
use crate::ocr::OcrManager;
use crate::processes::BoardManager;
use crate::storage;
use std::fs;
use std::path::{Component, Path};
use std::time::UNIX_EPOCH;
use tauri::{AppHandle, State};

#[tauri::command]
pub fn bootstrap(app: AppHandle) -> Result<WorkspaceSnapshot, String> {
    Ok(WorkspaceSnapshot {
        settings: storage::load(&app)?,
        app_info: app_info(app)?,
    })
}

#[tauri::command]
pub fn app_info(app: AppHandle) -> Result<AppInfo, String> {
    Ok(AppInfo {
        platform: std::env::consts::OS.into(),
        architecture: std::env::consts::ARCH.into(),
        version: app.package_info().version.to_string(),
        settings_path: path_text(&storage::settings_path(&app)?),
        library_path: path_text(&storage::library_path(&app)?),
    })
}

#[tauri::command]
pub fn load_settings(app: AppHandle) -> Result<AppSettings, String> {
    storage::load(&app)
}

#[tauri::command]
pub fn save_settings(
    app: AppHandle,
    formula_manager: State<'_, FormulaManager>,
    ocr_manager: State<'_, OcrManager>,
    settings: AppSettings,
) -> Result<AppSettings, String> {
    let previous = storage::load(&app)?;
    let ocr_changed = previous.formula_ocr_enabled != settings.formula_ocr_enabled
        || previous.formula_ocr_root != settings.formula_ocr_root;
    let saved = storage::save(&app, settings)?;
    if ocr_changed {
        formula_manager.stop()?;
        ocr_manager.stop()?;
    }
    Ok(saved)
}

#[tauri::command]
pub fn add_workspace(app: AppHandle, path: String) -> Result<AppSettings, String> {
    let canonical = storage::canonical_directory(&path)?;
    let canonical = path_text(&canonical);
    let mut settings = storage::load(&app)?;
    if !settings
        .roots
        .iter()
        .any(|existing| storage::paths_equal(existing, &canonical))
    {
        settings.roots.push(canonical);
    }
    storage::save(&app, settings)
}

#[tauri::command]
pub fn remove_workspace(app: AppHandle, path: String) -> Result<AppSettings, String> {
    let mut settings = storage::load(&app)?;
    settings
        .roots
        .retain(|existing| !storage::paths_equal(existing, &path));
    storage::save(&app, settings)
}

#[tauri::command]
pub fn list_directory(app: AppHandle, path: String) -> Result<Vec<FileEntry>, String> {
    let directory = storage::ensure_allowed_directory(&app, path)?;
    let mut entries = Vec::new();
    let iterator = fs::read_dir(&directory)
        .map_err(|error| format!("Cannot read {}: {error}", directory.display()))?;

    for item in iterator {
        let item = item.map_err(|error| format!("Cannot read a directory entry: {error}"))?;
        let path = item.path();
        let name = item.file_name().to_string_lossy().to_string();
        if name.starts_with('.') {
            continue;
        }

        let file_type = item
            .file_type()
            .map_err(|error| format!("Cannot inspect {}: {error}", path.display()))?;
        let kind = if file_type.is_dir() {
            "directory"
        } else if file_type.is_file() && is_board(&path) {
            "board"
        } else {
            continue;
        };
        let modified_at = item
            .metadata()
            .ok()
            .and_then(|metadata| metadata.modified().ok())
            .and_then(|value| value.duration_since(UNIX_EPOCH).ok())
            .map(|value| value.as_millis() as u64);
        entries.push(FileEntry {
            name,
            path: path_text(&path),
            kind: kind.into(),
            modified_at,
        });
    }

    entries.sort_by(|left, right| {
        left.kind
            .cmp(&right.kind)
            .then_with(|| left.name.to_lowercase().cmp(&right.name.to_lowercase()))
    });
    Ok(entries)
}

#[tauri::command]
pub fn create_board(app: AppHandle, parent: String, name: String) -> Result<FileEntry, String> {
    let parent = storage::ensure_allowed_directory(&app, parent)?;
    let name = board_name(&name)?;
    let path = parent.join(&name);
    if path.exists() {
        return Err(format!("{} already exists", path.display()));
    }

    let empty_scene = serde_json::json!({
        "type": "excalidraw",
        "version": 2,
        "source": "https://excalidraw.com",
        "elements": [],
        "appState": {
            "gridSize": null,
            "viewBackgroundColor": "#ffffff"
        },
        "files": {}
    });
    let json = serde_json::to_string_pretty(&empty_scene)
        .map_err(|error| format!("Cannot create an empty board: {error}"))?;
    fs::write(&path, format!("{json}\n"))
        .map_err(|error| format!("Cannot create {}: {error}", path.display()))?;
    file_entry(path, "board")
}

#[tauri::command]
pub fn create_folder(app: AppHandle, parent: String, name: String) -> Result<FileEntry, String> {
    let parent = storage::ensure_allowed_directory(&app, parent)?;
    let name = simple_name(&name)?;
    let path = parent.join(name);
    fs::create_dir(&path).map_err(|error| format!("Cannot create {}: {error}", path.display()))?;
    file_entry(path, "directory")
}

#[tauri::command]
pub fn start_board(
    app: AppHandle,
    manager: State<'_, BoardManager>,
    file_path: String,
    requested_port: Option<u16>,
    theme: String,
    formula_url: Option<String>,
) -> Result<RunningBoard, String> {
    manager.start(&app, file_path, requested_port, theme, formula_url)
}

#[tauri::command]
pub fn list_running_boards(manager: State<'_, BoardManager>) -> Result<Vec<RunningBoard>, String> {
    manager.running()
}

#[tauri::command]
pub fn stop_board(manager: State<'_, BoardManager>, pid: u32) -> Result<(), String> {
    manager.stop(pid)
}

#[tauri::command]
pub fn stop_all_boards(manager: State<'_, BoardManager>) -> Result<usize, String> {
    manager.stop_all()
}

#[tauri::command]
pub fn start_formula_editor(
    app: AppHandle,
    manager: State<'_, FormulaManager>,
    ocr_manager: State<'_, OcrManager>,
    language: String,
) -> Result<RunningFormulaEditor, String> {
    let settings = storage::load(&app)?;
    let ocr_session = ocr_manager.ensure_started(&app, &settings)?;
    manager.start(&app, language, ocr_session.as_ref())
}

#[tauri::command]
pub fn get_formula_editor(
    manager: State<'_, FormulaManager>,
) -> Result<Option<RunningFormulaEditor>, String> {
    manager.running()
}

#[tauri::command]
pub fn stop_formula_editor(manager: State<'_, FormulaManager>) -> Result<(), String> {
    manager.stop()
}

#[tauri::command]
pub fn get_formula_ocr_status(
    app: AppHandle,
    manager: State<'_, OcrManager>,
) -> Result<FormulaOcrStatus, String> {
    let settings = storage::load(&app)?;
    manager.status(&app, &settings)
}

fn file_entry(path: impl AsRef<Path>, kind: &str) -> Result<FileEntry, String> {
    let path = path.as_ref();
    let metadata = fs::metadata(path)
        .map_err(|error| format!("Cannot inspect {}: {error}", path.display()))?;
    let modified_at = metadata
        .modified()
        .ok()
        .and_then(|value| value.duration_since(UNIX_EPOCH).ok())
        .map(|value| value.as_millis() as u64);
    Ok(FileEntry {
        name: path
            .file_name()
            .unwrap_or_default()
            .to_string_lossy()
            .to_string(),
        path: path_text(path),
        kind: kind.into(),
        modified_at,
    })
}

fn simple_name(value: &str) -> Result<String, String> {
    let value = value.trim();
    if value.is_empty() || value.len() > 120 {
        return Err("The name must contain between 1 and 120 characters".into());
    }
    if value.chars().any(|character| {
        matches!(
            character,
            '\0' | '<' | '>' | ':' | '"' | '/' | '\\' | '|' | '?' | '*'
        )
    }) || value.ends_with(['.', ' '])
    {
        return Err("The name contains a character that is not portable across platforms".into());
    }
    let windows_stem = value
        .split('.')
        .next()
        .unwrap_or_default()
        .to_ascii_uppercase();
    if matches!(windows_stem.as_str(), "CON" | "PRN" | "AUX" | "NUL")
        || windows_stem
            .strip_prefix("COM")
            .or_else(|| windows_stem.strip_prefix("LPT"))
            .and_then(|number| number.parse::<u8>().ok())
            .is_some_and(|number| (1..=9).contains(&number))
    {
        return Err("The name is reserved on Windows".into());
    }
    let path = Path::new(value);
    let mut components = path.components();
    if !matches!(components.next(), Some(Component::Normal(_))) || components.next().is_some() {
        return Err("The name may not contain a path".into());
    }
    Ok(value.to_string())
}

fn board_name(value: &str) -> Result<String, String> {
    let name = simple_name(value)?;
    if name.to_lowercase().ends_with(".excalidraw") {
        Ok(name)
    } else if Path::new(&name).extension().is_some() {
        Err("Board files must use the .excalidraw extension".into())
    } else {
        Ok(format!("{name}.excalidraw"))
    }
}

fn is_board(path: &Path) -> bool {
    path.extension()
        .and_then(|extension| extension.to_str())
        .map(|extension| extension.eq_ignore_ascii_case("excalidraw"))
        .unwrap_or(false)
}

fn path_text(path: &Path) -> String {
    path.to_string_lossy().to_string()
}

#[cfg(test)]
mod tests {
    use super::{board_name, simple_name};

    #[test]
    fn validates_cross_platform_file_names() {
        assert_eq!(simple_name("项目草图").unwrap(), "项目草图");
        assert!(simple_name("").is_err());
        assert!(simple_name("../outside").is_err());
        assert!(simple_name("folder/board").is_err());
        assert!(simple_name("folder\\board").is_err());
        assert!(simple_name("CON").is_err());
        assert!(simple_name("LPT1.txt").is_err());
        assert!(simple_name("trailing.").is_err());
    }

    #[test]
    fn normalizes_board_extension() {
        assert_eq!(board_name("idea").unwrap(), "idea.excalidraw");
        assert_eq!(board_name("idea.EXCALIDRAW").unwrap(), "idea.EXCALIDRAW");
        assert!(board_name("idea.json").is_err());
    }
}
