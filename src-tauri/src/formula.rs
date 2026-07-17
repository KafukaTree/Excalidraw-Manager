use crate::models::RunningFormulaEditor;
use crate::ocr::{OcrSession, TOKEN_VARIABLE};
use crate::processes::{find_free_port, find_node, runtime_directory};
use crate::storage;
use std::net::TcpStream;
use std::path::{Path, PathBuf};
use std::process::{Child, Command, Stdio};
use std::sync::Mutex;
use std::thread;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};
use tauri::AppHandle;

struct ManagedFormulaEditor {
    child: Child,
    info: RunningFormulaEditor,
    provider_config: PathBuf,
    ocr_token: Option<String>,
}

#[derive(Default)]
pub struct FormulaManager {
    editor: Mutex<Option<ManagedFormulaEditor>>,
}

impl Drop for FormulaManager {
    fn drop(&mut self) {
        if let Ok(editor) = self.editor.get_mut() {
            if let Some(editor) = editor.as_mut() {
                let _ = editor.child.kill();
                let _ = editor.child.wait();
            }
        }
    }
}

impl FormulaManager {
    pub fn start(
        &self,
        app: &AppHandle,
        language: String,
        ocr_session: Option<&OcrSession>,
    ) -> Result<RunningFormulaEditor, String> {
        let runtime_directory = runtime_directory(app)?;
        let server = runtime_directory.join("formula-server.mjs");
        let assets = runtime_directory.join("formula-editor");
        if !server.is_file() || !assets.join("index.html").is_file() {
            return Err(format!(
                "Formula editor runtime is missing from {}",
                runtime_directory.display()
            ));
        }

        let app_directory = storage::app_directory(app)?;
        std::fs::create_dir_all(&app_directory)
            .map_err(|error| format!("Cannot create {}: {error}", app_directory.display()))?;
        let provider_config = ocr_session
            .map(|session| session.config_path.clone())
            .unwrap_or_else(|| app_directory.join("formula-providers.json"));
        let ocr_token = ocr_session.map(|session| session.token.as_str());
        if let Some(existing) = self.running_for(&provider_config, ocr_token)? {
            return Ok(existing);
        }
        let capture_helper = if cfg!(target_os = "windows") {
            runtime_directory.join("FormulaCapture.exe")
        } else if cfg!(target_os = "macos") {
            runtime_directory.join("FormulaCapture")
        } else {
            // Linux capture needs a portal-aware helper. Do not accidentally
            // expose the macOS helper as available on another Unix platform.
            runtime_directory.join("FormulaCapture-linux")
        };
        let port = find_free_port(17840)?;
        let node = find_node()?;
        let language = if language.to_lowercase().starts_with("zh") {
            "zh-CN"
        } else {
            "en"
        };

        let mut command = Command::new(&node);
        command
            .arg(&server)
            .arg("--host")
            .arg("127.0.0.1")
            .arg("--port")
            .arg(port.to_string())
            .arg("--assets")
            .arg(&assets)
            .arg("--provider-config")
            .arg(&provider_config)
            .arg("--parent-pid")
            .arg(std::process::id().to_string())
            .arg("--lang")
            .arg(language);
        if capture_helper.is_file() {
            command.arg("--capture-helper").arg(capture_helper);
        }
        if let Some(session) = ocr_session {
            command.env(TOKEN_VARIABLE, &session.token);
        }
        let mut child = command
            .current_dir(&runtime_directory)
            .stdin(Stdio::null())
            .stdout(Stdio::null())
            .stderr(Stdio::null())
            .spawn()
            .map_err(|error| {
                format!(
                    "Cannot start the formula editor with {}: {error}",
                    node.display()
                )
            })?;

        let deadline = Instant::now() + Duration::from_secs(8);
        loop {
            if TcpStream::connect(("127.0.0.1", port)).is_ok() {
                break;
            }
            if let Some(status) = child
                .try_wait()
                .map_err(|error| format!("Cannot inspect the formula editor process: {error}"))?
            {
                return Err(format!(
                    "The formula editor exited before startup ({status})"
                ));
            }
            if Instant::now() >= deadline {
                let _ = child.kill();
                let _ = child.wait();
                return Err("The formula editor did not start within 8 seconds".into());
            }
            thread::sleep(Duration::from_millis(75));
        }

        let info = RunningFormulaEditor {
            pid: child.id(),
            port,
            url: format!("http://127.0.0.1:{port}"),
            started_at: SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap_or_default()
                .as_millis() as u64,
        };
        *self
            .editor
            .lock()
            .map_err(|_| "The formula editor registry is unavailable".to_string())? =
            Some(ManagedFormulaEditor {
                child,
                info: info.clone(),
                provider_config,
                ocr_token: ocr_session.map(|session| session.token.clone()),
            });
        Ok(info)
    }

    fn running_for(
        &self,
        provider_config: &Path,
        ocr_token: Option<&str>,
    ) -> Result<Option<RunningFormulaEditor>, String> {
        let mut editor = self
            .editor
            .lock()
            .map_err(|_| "The formula editor registry is unavailable".to_string())?;
        if let Some(managed) = editor.as_mut() {
            match managed.child.try_wait() {
                Ok(None)
                    if managed.provider_config == provider_config
                        && managed.ocr_token.as_deref() == ocr_token =>
                {
                    return Ok(Some(managed.info.clone()));
                }
                Ok(None) => {}
                Ok(Some(_)) | Err(_) => {
                    *editor = None;
                    return Ok(None);
                }
            }
        }
        let stale = editor.take();
        drop(editor);
        if let Some(mut stale) = stale {
            let _ = stale.child.kill();
            let _ = stale.child.wait();
        }
        Ok(None)
    }

    pub fn running(&self) -> Result<Option<RunningFormulaEditor>, String> {
        let mut editor = self
            .editor
            .lock()
            .map_err(|_| "The formula editor registry is unavailable".to_string())?;
        if let Some(managed) = editor.as_mut() {
            match managed.child.try_wait() {
                Ok(None) => return Ok(Some(managed.info.clone())),
                Ok(Some(_)) | Err(_) => *editor = None,
            }
        }
        Ok(None)
    }

    pub fn stop(&self) -> Result<(), String> {
        let editor = self
            .editor
            .lock()
            .map_err(|_| "The formula editor registry is unavailable".to_string())?
            .take();
        if let Some(mut editor) = editor {
            if editor.child.try_wait().ok().flatten().is_none() {
                let _ = editor.child.kill();
            }
            let _ = editor.child.wait();
        }
        Ok(())
    }
}
