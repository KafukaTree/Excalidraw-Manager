use crate::models::{AppSettings, FormulaOcrStatus};
use crate::processes::{find_free_port, runtime_directory};
use std::fs;
use std::io::{Read, Write};
use std::net::{SocketAddr, TcpStream};
use std::path::{Path, PathBuf};
use std::process::{Child, Command, Stdio};
use std::sync::Mutex;
use std::thread;
use std::time::{Duration, Instant};
use tauri::AppHandle;
use uuid::Uuid;

pub(crate) const TOKEN_VARIABLE: &str = "EXCALIDRAW_MANAGER_FORMULA_OCR_TOKEN";
const MODEL_FILES: [&str; 4] = [
    "image_resizer.onnx",
    "encoder.onnx",
    "decoder.onnx",
    "tokenizer.json",
];

#[derive(Clone)]
pub(crate) struct OcrSession {
    pub config_path: PathBuf,
    pub token: String,
}

struct ManagedOcrProvider {
    child: Child,
    root: PathBuf,
    port: u16,
    session: OcrSession,
}

#[derive(Default)]
struct OcrState {
    provider: Option<ManagedOcrProvider>,
    last_error: Option<String>,
}

#[derive(Default)]
pub struct OcrManager {
    state: Mutex<OcrState>,
}

struct Installation {
    root: PathBuf,
    python: PathBuf,
    model_root: PathBuf,
    server: PathBuf,
    installed: bool,
    message: String,
}

impl Drop for OcrManager {
    fn drop(&mut self) {
        if let Ok(state) = self.state.get_mut() {
            if let Some(provider) = state.provider.take() {
                stop_provider(provider);
            }
        }
    }
}

impl OcrManager {
    pub fn ensure_started(
        &self,
        app: &AppHandle,
        settings: &AppSettings,
    ) -> Result<Option<OcrSession>, String> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "The formula OCR registry is unavailable".to_string())?;

        if !settings.formula_ocr_enabled {
            if let Some(provider) = state.provider.take() {
                stop_provider(provider);
            }
            state.last_error = None;
            return Ok(None);
        }

        let installation = inspect_installation(app, &settings.formula_ocr_root)?;
        if !installation.installed {
            if let Some(provider) = state.provider.take() {
                stop_provider(provider);
            }
            state.last_error = None;
            return Ok(None);
        }

        if let Some(provider) = state.provider.as_mut() {
            let alive = provider.child.try_wait().ok().flatten().is_none();
            if alive
                && provider.root == installation.root
                && provider_is_ready(provider.port, &provider.session.token)
            {
                return Ok(Some(provider.session.clone()));
            }
        }
        if let Some(provider) = state.provider.take() {
            stop_provider(provider);
        }

        match start_provider(&installation) {
            Ok(provider) => {
                let session = provider.session.clone();
                state.provider = Some(provider);
                state.last_error = None;
                Ok(Some(session))
            }
            Err(error) => {
                state.last_error = Some(error);
                Ok(None)
            }
        }
    }

    pub fn status(
        &self,
        app: &AppHandle,
        settings: &AppSettings,
    ) -> Result<FormulaOcrStatus, String> {
        let installation = inspect_installation(app, &settings.formula_ocr_root)?;
        let configured = !settings.formula_ocr_root.trim().is_empty();
        let mut state = self
            .state
            .lock()
            .map_err(|_| "The formula OCR registry is unavailable".to_string())?;
        if let Some(provider) = state.provider.as_mut() {
            if provider.child.try_wait().ok().flatten().is_some() {
                state.provider = None;
            }
        }
        let running_provider = state
            .provider
            .as_ref()
            .filter(|provider| provider.root == installation.root);
        let running = running_provider.is_some();
        let port = running_provider.map(|provider| provider.port);

        let (status_state, message) = if !settings.formula_ocr_enabled {
            (
                "disabled",
                "Local formula OCR is disabled in Settings.".to_string(),
            )
        } else if !configured {
            (
                "notConfigured",
                "Choose the FormulaOCR installation folder first.".to_string(),
            )
        } else if !installation.installed {
            ("notInstalled", installation.message.clone())
        } else if running {
            (
                "running",
                format!(
                    "RapidLaTeXOCR is running locally on port {}.",
                    port.unwrap_or_default()
                ),
            )
        } else if let Some(error) = state.last_error.as_ref() {
            ("error", error.clone())
        } else {
            (
                "ready",
                "The local OCR environment is installed and will start with the formula editor."
                    .to_string(),
            )
        };

        Ok(FormulaOcrStatus {
            enabled: settings.formula_ocr_enabled,
            configured,
            installed: installation.installed,
            running,
            state: status_state.into(),
            message,
            root: path_text(&installation.root),
            port,
        })
    }

    pub fn stop(&self) -> Result<(), String> {
        let provider = self
            .state
            .lock()
            .map_err(|_| "The formula OCR registry is unavailable".to_string())?
            .provider
            .take();
        if let Some(provider) = provider {
            stop_provider(provider);
        }
        Ok(())
    }
}

fn inspect_installation(app: &AppHandle, configured_root: &str) -> Result<Installation, String> {
    let root = normalize_root(configured_root);
    let python = venv_python(&root);
    let model_root = root.join("models").join("rapid-latex-ocr");
    let server = runtime_directory(app)?
        .join("formula-ocr-provider")
        .join("server.py");

    let missing = if configured_root.trim().is_empty() {
        Some("No FormulaOCR installation folder has been selected.".to_string())
    } else if !root.is_dir() {
        Some(format!(
            "The selected folder does not exist: {}",
            root.display()
        ))
    } else if !python.is_file() {
        Some(format!(
            "The isolated Python environment is missing: {}",
            python.display()
        ))
    } else if !root.join("install-complete.json").is_file() {
        Some("The OCR installation marker is missing; rerun the installer.".to_string())
    } else if !model_root.join("manifest.json").is_file() {
        Some("The OCR model manifest is missing; rerun the installer.".to_string())
    } else if let Some(name) = MODEL_FILES
        .iter()
        .find(|name| !model_root.join(name).is_file())
    {
        Some(format!(
            "The OCR model file {name} is missing; rerun the installer."
        ))
    } else if !server.is_file() {
        Some("The bundled OCR provider adapter is missing.".to_string())
    } else {
        None
    };

    Ok(Installation {
        root,
        python,
        model_root,
        server,
        installed: missing.is_none(),
        message: missing.unwrap_or_else(|| "The local OCR environment is installed.".into()),
    })
}

fn start_provider(installation: &Installation) -> Result<ManagedOcrProvider, String> {
    let run_directory = installation.root.join("run");
    let temp_directory = installation.root.join("temp");
    let cache_directory = installation.root.join("cache");
    for directory in [
        &run_directory,
        &temp_directory,
        &cache_directory.join("pip"),
        &cache_directory.join("torch"),
        &cache_directory.join("huggingface"),
    ] {
        fs::create_dir_all(directory)
            .map_err(|error| format!("Cannot create {}: {error}", directory.display()))?;
    }

    let port = find_free_port(17_861)?;
    let token = format!("{}{}", Uuid::new_v4().simple(), Uuid::new_v4().simple());
    let config_path = run_directory.join("formula-providers.active.json");
    let config = serde_json::json!({
        "providers": [{
            "id": "rapid-latex-ocr-local",
            "name": "RapidLaTeXOCR Local",
            "baseUrl": format!("http://127.0.0.1:{port}"),
            "enabled": true,
            "tokenEnv": TOKEN_VARIABLE
        }]
    });
    let config_text = serde_json::to_string_pretty(&config)
        .map_err(|error| format!("Cannot create the OCR provider configuration: {error}"))?;
    fs::write(&config_path, format!("{config_text}\n"))
        .map_err(|error| format!("Cannot save {}: {error}", config_path.display()))?;

    let provider_directory = installation
        .server
        .parent()
        .ok_or_else(|| "The OCR provider path has no parent directory".to_string())?;
    let mut command = Command::new(&installation.python);
    command
        .arg(&installation.server)
        .arg("--host")
        .arg("127.0.0.1")
        .arg("--port")
        .arg(port.to_string())
        .arg("--model-root")
        .arg(&installation.model_root)
        .arg("--parent-pid")
        .arg(std::process::id().to_string())
        .arg("--token-env")
        .arg(TOKEN_VARIABLE)
        .current_dir(provider_directory)
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .env(TOKEN_VARIABLE, &token)
        .env("PYTHONNOUSERSITE", "1")
        .env("PYTHONDONTWRITEBYTECODE", "1")
        .env("TEMP", &temp_directory)
        .env("TMP", &temp_directory)
        .env("TMPDIR", &temp_directory)
        .env("PIP_CACHE_DIR", cache_directory.join("pip"))
        .env("TORCH_HOME", cache_directory.join("torch"))
        .env("HF_HOME", cache_directory.join("huggingface"));
    for variable in ["PYTHONHOME", "PYTHONPATH", "PYTHONUSERBASE", "VIRTUAL_ENV"] {
        command.env_remove(variable);
    }
    let mut child = command.spawn().map_err(|error| {
        format!(
            "Cannot start local OCR with {}: {error}",
            installation.python.display()
        )
    })?;

    let deadline = Instant::now() + Duration::from_secs(12);
    loop {
        if provider_is_ready(port, &token) {
            break;
        }
        if let Some(status) = child
            .try_wait()
            .map_err(|error| format!("Cannot inspect the OCR provider process: {error}"))?
        {
            return Err(format!(
                "The local OCR provider exited during startup ({status})."
            ));
        }
        if Instant::now() >= deadline {
            let _ = child.kill();
            let _ = child.wait();
            return Err(
                "The local OCR provider did not become healthy within 12 seconds. Rerun the installer to verify its Python packages and model files."
                    .into(),
            );
        }
        thread::sleep(Duration::from_millis(100));
    }

    Ok(ManagedOcrProvider {
        child,
        root: installation.root.clone(),
        port,
        session: OcrSession { config_path, token },
    })
}

fn provider_is_ready(port: u16, token: &str) -> bool {
    let address: SocketAddr = match format!("127.0.0.1:{port}").parse() {
        Ok(address) => address,
        Err(_) => return false,
    };
    let mut stream = match TcpStream::connect_timeout(&address, Duration::from_millis(300)) {
        Ok(stream) => stream,
        Err(_) => return false,
    };
    let _ = stream.set_read_timeout(Some(Duration::from_millis(500)));
    let _ = stream.set_write_timeout(Some(Duration::from_millis(500)));
    let request = format!(
        "GET /v1/health HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nAuthorization: Bearer {token}\r\nConnection: close\r\n\r\n"
    );
    if stream.write_all(request.as_bytes()).is_err() {
        return false;
    }
    let mut response = String::new();
    if stream.read_to_string(&mut response).is_err() {
        return false;
    }
    (response.starts_with("HTTP/1.0 200") || response.starts_with("HTTP/1.1 200"))
        && response.contains("\"status\":\"ok\"")
}

fn stop_provider(mut provider: ManagedOcrProvider) {
    if provider.child.try_wait().ok().flatten().is_none() {
        let _ = provider.child.kill();
    }
    let _ = provider.child.wait();
}

fn normalize_root(value: &str) -> PathBuf {
    let value = value.trim();
    if let Some(remainder) = value.strip_prefix("~/") {
        if let Some(home) = std::env::var_os("HOME") {
            return PathBuf::from(home).join(remainder);
        }
    }
    PathBuf::from(value)
}

fn venv_python(root: &Path) -> PathBuf {
    if cfg!(target_os = "windows") {
        return root.join("venv").join("Scripts").join("python.exe");
    }
    let python3 = root.join("venv").join("bin").join("python3");
    if python3.is_file() {
        python3
    } else {
        root.join("venv").join("bin").join("python")
    }
}

fn path_text(path: &Path) -> String {
    path.to_string_lossy().to_string()
}

#[cfg(test)]
mod tests {
    use super::{normalize_root, venv_python};
    use std::path::Path;

    #[test]
    fn uses_platform_virtual_environment_layout() {
        let path = venv_python(Path::new("/tmp/formula-ocr"));
        if cfg!(target_os = "windows") {
            assert!(path.ends_with("venv/Scripts/python.exe"));
        } else {
            assert!(path.ends_with("venv/bin/python"));
        }
    }

    #[test]
    fn preserves_absolute_install_roots() {
        assert_eq!(
            normalize_root("/opt/formula-ocr"),
            PathBuf::from("/opt/formula-ocr")
        );
    }

    use std::path::PathBuf;
}
