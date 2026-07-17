use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AppSettings {
    pub roots: Vec<String>,
    pub start_port: u16,
    pub theme: String,
    pub language: String,
    pub formula_ocr_enabled: bool,
    pub formula_ocr_root: String,
    pub recent_files: Vec<String>,
    pub aliases: BTreeMap<String, String>,
}

impl Default for AppSettings {
    fn default() -> Self {
        Self {
            roots: Vec::new(),
            start_port: 6417,
            theme: "system".into(),
            language: "system".into(),
            formula_ocr_enabled: false,
            formula_ocr_root: String::new(),
            recent_files: Vec::new(),
            aliases: BTreeMap::new(),
        }
    }
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AppInfo {
    pub platform: String,
    pub architecture: String,
    pub version: String,
    pub settings_path: String,
    pub library_path: String,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FileEntry {
    pub name: String,
    pub path: String,
    pub kind: String,
    pub modified_at: Option<u64>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct WorkspaceSnapshot {
    pub settings: AppSettings,
    pub app_info: AppInfo,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RunningBoard {
    pub pid: u32,
    pub port: u16,
    pub url: String,
    pub file_path: String,
    pub name: String,
    pub theme: String,
    pub started_at: u64,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RunningFormulaEditor {
    pub pid: u32,
    pub port: u16,
    pub url: String,
    pub started_at: u64,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FormulaOcrStatus {
    pub enabled: bool,
    pub configured: bool,
    pub installed: bool,
    pub running: bool,
    pub state: String,
    pub message: String,
    pub root: String,
    pub port: Option<u16>,
}
