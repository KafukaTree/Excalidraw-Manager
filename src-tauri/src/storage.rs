use crate::models::AppSettings;
use std::fs;
use std::path::{Path, PathBuf};
use tauri::{AppHandle, Manager};

pub fn app_directory(app: &AppHandle) -> Result<PathBuf, String> {
    app.path()
        .app_config_dir()
        .map_err(|error| format!("Cannot resolve the application data directory: {error}"))
}

pub fn settings_path(app: &AppHandle) -> Result<PathBuf, String> {
    Ok(app_directory(app)?.join("settings.json"))
}

pub fn library_path(app: &AppHandle) -> Result<PathBuf, String> {
    Ok(app_directory(app)?
        .join("libraries")
        .join("shared.excalidrawlib"))
}

pub fn load(app: &AppHandle) -> Result<AppSettings, String> {
    let path = settings_path(app)?;
    if !path.exists() {
        return Ok(AppSettings::default());
    }

    let text = fs::read_to_string(&path)
        .map_err(|error| format!("Cannot read {}: {error}", path.display()))?;
    let mut settings: AppSettings = serde_json::from_str(&text)
        .map_err(|error| format!("Cannot parse {}: {error}", path.display()))?;
    normalize(&mut settings);
    Ok(settings)
}

pub fn save(app: &AppHandle, mut settings: AppSettings) -> Result<AppSettings, String> {
    normalize(&mut settings);
    let path = settings_path(app)?;
    let parent = path
        .parent()
        .ok_or_else(|| "The settings path has no parent directory".to_string())?;
    fs::create_dir_all(parent)
        .map_err(|error| format!("Cannot create {}: {error}", parent.display()))?;
    let json = serde_json::to_string_pretty(&settings)
        .map_err(|error| format!("Cannot serialize settings: {error}"))?;
    fs::write(&path, format!("{json}\n"))
        .map_err(|error| format!("Cannot save {}: {error}", path.display()))?;
    Ok(settings)
}

pub fn canonical_directory(path: impl AsRef<Path>) -> Result<PathBuf, String> {
    let path = path.as_ref();
    let canonical = fs::canonicalize(path)
        .map_err(|error| format!("Cannot access {}: {error}", path.display()))?;
    if !canonical.is_dir() {
        return Err(format!("{} is not a directory", canonical.display()));
    }
    Ok(canonical)
}

pub fn ensure_allowed_directory(
    app: &AppHandle,
    path: impl AsRef<Path>,
) -> Result<PathBuf, String> {
    let canonical = canonical_directory(path)?;
    let settings = load(app)?;
    let allowed = settings.roots.iter().any(|root| {
        fs::canonicalize(root)
            .map(|root| canonical.starts_with(root))
            .unwrap_or(false)
    });
    if !allowed {
        return Err("The requested path is outside the configured workspaces".into());
    }
    Ok(canonical)
}

pub fn ensure_allowed_board(app: &AppHandle, path: impl AsRef<Path>) -> Result<PathBuf, String> {
    let path = path.as_ref();
    let canonical = fs::canonicalize(path)
        .map_err(|error| format!("Cannot access {}: {error}", path.display()))?;
    if !canonical.is_file()
        || !canonical
            .extension()
            .and_then(|extension| extension.to_str())
            .map(|extension| extension.eq_ignore_ascii_case("excalidraw"))
            .unwrap_or(false)
    {
        return Err(format!(
            "{} is not an Excalidraw board",
            canonical.display()
        ));
    }

    let settings = load(app)?;
    let allowed = settings.roots.iter().any(|root| {
        fs::canonicalize(root)
            .map(|root| canonical.starts_with(root))
            .unwrap_or(false)
    });
    if !allowed {
        return Err("The requested board is outside the configured workspaces".into());
    }
    Ok(canonical)
}

fn normalize(settings: &mut AppSettings) {
    if settings.start_port == 0 {
        settings.start_port = 6417;
    }
    if !matches!(settings.theme.as_str(), "system" | "light" | "dark") {
        settings.theme = "system".into();
    }
    if settings.language.trim().is_empty() {
        settings.language = "system".into();
    }

    settings.roots.sort_by_key(|path| path.to_lowercase());
    settings
        .roots
        .dedup_by(|left, right| paths_equal(left, right));
    settings.recent_files.truncate(20);
}

pub fn paths_equal(left: &str, right: &str) -> bool {
    if cfg!(target_os = "windows") {
        left.eq_ignore_ascii_case(right)
    } else {
        left == right
    }
}
