use crate::models::RunningBoard;
use crate::storage;
use std::collections::BTreeMap;
use std::net::{TcpListener, TcpStream};
use std::path::{Path, PathBuf};
use std::process::{Child, Command, Stdio};
use std::sync::Mutex;
use std::thread;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};
use tauri::{AppHandle, Manager};

struct ManagedBoard {
    child: Child,
    info: RunningBoard,
}

#[derive(Default)]
pub struct BoardManager {
    boards: Mutex<BTreeMap<u32, ManagedBoard>>,
}

impl Drop for BoardManager {
    fn drop(&mut self) {
        if let Ok(boards) = self.boards.get_mut() {
            for board in boards.values_mut() {
                let _ = board.child.kill();
                let _ = board.child.wait();
            }
        }
    }
}

impl BoardManager {
    pub fn start(
        &self,
        app: &AppHandle,
        file_path: String,
        requested_port: Option<u16>,
        requested_theme: String,
        formula_url: Option<String>,
    ) -> Result<RunningBoard, String> {
        let file_path = storage::ensure_allowed_board(app, file_path)?;
        let theme = match requested_theme.as_str() {
            "light" | "dark" => requested_theme,
            _ => "system".into(),
        };

        if let Some(existing) = self
            .running()?
            .into_iter()
            .find(|board| Path::new(&board.file_path) == file_path)
        {
            return Ok(existing);
        }

        let settings = storage::load(app)?;
        let port = match requested_port {
            Some(port) if port >= 1024 => {
                if !port_is_available(port) {
                    return Err(format!("Port {port} is already in use"));
                }
                port
            }
            Some(port) => return Err(format!("Port {port} is reserved; choose 1024 or higher")),
            None => find_free_port(settings.start_port)?,
        };

        let node = find_node()?;
        let runtime_directory = runtime_directory(app)?;
        let server = runtime_directory.join("server.mjs");
        let patched_client = runtime_directory.join("main.js");
        let public_directory = public_directory(app)?;
        for required in [
            &server,
            &patched_client,
            &public_directory.join("index.html"),
        ] {
            if !required.exists() {
                return Err(format!(
                    "Required runtime file is missing: {}",
                    required.display()
                ));
            }
        }

        let library_path = storage::library_path(app)?;
        let mut command = Command::new(&node);
        command
            .arg(&server)
            .arg(&file_path)
            .arg("--port")
            .arg(port.to_string())
            .arg("--theme")
            .arg(&theme)
            .arg("--public-dir")
            .arg(&public_directory)
            .arg("--library")
            .arg(&library_path);
        if let Some(formula_url) = formula_url.filter(|url| loopback_url(url)) {
            command.arg("--formula-url").arg(formula_url);
        }
        let mut child = command
            .current_dir(&runtime_directory)
            .stdin(Stdio::null())
            .stdout(Stdio::null())
            .stderr(Stdio::null())
            .spawn()
            .map_err(|error| format!("Cannot start Node.js at {}: {error}", node.display()))?;

        let deadline = Instant::now() + Duration::from_secs(8);
        loop {
            if TcpStream::connect(("127.0.0.1", port)).is_ok() {
                break;
            }
            if let Some(status) = child
                .try_wait()
                .map_err(|error| format!("Cannot inspect the board process: {error}"))?
            {
                return Err(format!(
                    "The board service exited before startup ({status})"
                ));
            }
            if Instant::now() >= deadline {
                let _ = child.kill();
                let _ = child.wait();
                return Err(format!(
                    "The board service did not open port {port} within 8 seconds"
                ));
            }
            thread::sleep(Duration::from_millis(75));
        }

        let pid = child.id();
        let info = RunningBoard {
            pid,
            port,
            url: format!("http://127.0.0.1:{port}"),
            file_path: path_text(&file_path),
            name: file_path
                .file_name()
                .unwrap_or_default()
                .to_string_lossy()
                .to_string(),
            theme,
            started_at: SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap_or_default()
                .as_millis() as u64,
        };
        self.boards
            .lock()
            .map_err(|_| "The managed process registry is unavailable".to_string())?
            .insert(
                pid,
                ManagedBoard {
                    child,
                    info: info.clone(),
                },
            );
        Ok(info)
    }

    pub fn running(&self) -> Result<Vec<RunningBoard>, String> {
        let mut boards = self
            .boards
            .lock()
            .map_err(|_| "The managed process registry is unavailable".to_string())?;
        let mut running = Vec::new();
        boards.retain(|_, board| match board.child.try_wait() {
            Ok(None) => {
                running.push(board.info.clone());
                true
            }
            Ok(Some(_)) | Err(_) => false,
        });
        running.sort_by_key(|board| board.started_at);
        Ok(running)
    }

    pub fn stop(&self, pid: u32) -> Result<(), String> {
        let board = self
            .boards
            .lock()
            .map_err(|_| "The managed process registry is unavailable".to_string())?
            .remove(&pid);
        let Some(mut board) = board else {
            return Err(format!("No managed board process has PID {pid}"));
        };
        if board.child.try_wait().ok().flatten().is_none() {
            board
                .child
                .kill()
                .map_err(|error| format!("Cannot stop PID {pid}: {error}"))?;
        }
        let _ = board.child.wait();
        Ok(())
    }

    pub fn stop_all(&self) -> Result<usize, String> {
        let boards = {
            let mut registry = self
                .boards
                .lock()
                .map_err(|_| "The managed process registry is unavailable".to_string())?;
            std::mem::take(&mut *registry)
        };
        let count = boards.len();
        for (_, mut board) in boards {
            if board.child.try_wait().ok().flatten().is_none() {
                let _ = board.child.kill();
            }
            let _ = board.child.wait();
        }
        Ok(count)
    }
}

pub(crate) fn find_free_port(start: u16) -> Result<u16, String> {
    for port in start.max(1024)..=u16::MAX {
        if port_is_available(port) {
            return Ok(port);
        }
    }
    Err(format!("No free TCP port is available from {start}"))
}

fn port_is_available(port: u16) -> bool {
    TcpListener::bind(("127.0.0.1", port)).is_ok()
}

pub(crate) fn find_node() -> Result<PathBuf, String> {
    if let Some(path) = std::env::var_os("EXCALIDRAW_MANAGER_NODE") {
        let path = PathBuf::from(path);
        if path.is_file() {
            return Ok(path);
        }
    }
    if let Ok(path) = which::which(if cfg!(target_os = "windows") {
        "node.exe"
    } else {
        "node"
    }) {
        return Ok(path);
    }

    let candidates = if cfg!(target_os = "macos") {
        vec![
            "/opt/homebrew/bin/node",
            "/usr/local/bin/node",
            "/usr/bin/node",
        ]
    } else if cfg!(target_os = "linux") {
        vec!["/usr/local/bin/node", "/usr/bin/node"]
    } else {
        Vec::new()
    };
    candidates
        .into_iter()
        .map(PathBuf::from)
        .find(|path| path.is_file())
        .ok_or_else(|| {
            "Cannot find Node.js. Install Node.js 20 or newer, or set EXCALIDRAW_MANAGER_NODE."
                .to_string()
        })
}

fn development_root() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .parent()
        .expect("src-tauri must be inside the project root")
        .to_path_buf()
}

pub(crate) fn runtime_directory(app: &AppHandle) -> Result<PathBuf, String> {
    if cfg!(debug_assertions) {
        return Ok(development_root().join("runtime"));
    }
    app.path()
        .resource_dir()
        .map(|path| path.join("runtime"))
        .map_err(|error| format!("Cannot resolve bundled runtime files: {error}"))
}

fn public_directory(app: &AppHandle) -> Result<PathBuf, String> {
    if cfg!(debug_assertions) {
        return Ok(development_root()
            .join("node_modules")
            .join("excalidraw-edit")
            .join("src")
            .join("public"));
    }
    app.path()
        .resource_dir()
        .map(|path| path.join("excalidraw-public"))
        .map_err(|error| format!("Cannot resolve bundled Excalidraw files: {error}"))
}

fn path_text(path: &Path) -> String {
    path.to_string_lossy().to_string()
}

fn loopback_url(url: &str) -> bool {
    url::Url::parse(url).ok().is_some_and(|url| {
        let host_is_loopback = match url.host() {
            Some(url::Host::Domain(host)) => host.eq_ignore_ascii_case("localhost"),
            Some(url::Host::Ipv4(address)) => address == std::net::Ipv4Addr::LOCALHOST,
            Some(url::Host::Ipv6(address)) => address == std::net::Ipv6Addr::LOCALHOST,
            None => false,
        };
        url.scheme() == "http"
            && url.username().is_empty()
            && url.password().is_none()
            && host_is_loopback
            && url.port().is_some()
    })
}

#[cfg(test)]
mod tests {
    use super::loopback_url;

    #[test]
    fn accepts_only_local_http_formula_urls() {
        assert!(loopback_url("http://127.0.0.1:17840/"));
        assert!(loopback_url("http://localhost:17840/editor"));
        assert!(loopback_url("http://[::1]:17840/"));
        assert!(!loopback_url("https://127.0.0.1:17840/"));
        assert!(!loopback_url("http://localhost.example:17840/"));
        assert!(!loopback_url("http://user@localhost:17840/"));
        assert!(!loopback_url("http://127.0.0.1/"));
    }
}
