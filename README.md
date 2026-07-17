# Excalidraw Manager

[简体中文](README.zh-CN.md) | English

> **Long-lived cross-platform product line:** this branch is maintained
> independently from the Windows-only `main` branch. Do not merge either
> product-line branch into the other. Transfer reviewed fixes only by
> cherry-picking individual commits. See [BRANCH_POLICY.md](BRANCH_POLICY.md).

Excalidraw Manager 0.5 is being migrated into one desktop application for
macOS, Windows, and Linux. It uses Tauri 2, React, TypeScript, and Rust while
reusing the existing local board, formula editor, and shared-library runtimes.

The original Windows WinForms implementation remains in the repository while
advanced features are migrated.

The application is local-first: boards and settings stay on your computer.
Managed board servers listen only on `127.0.0.1`.

> This is an independent project and is not affiliated with or endorsed by
> Excalidraw.

## Cross-platform 0.5

- Native macOS `.app` / `.dmg` builds with a shared Windows and Linux code path.
- Multiple workspaces with local board and folder browsing and creation.
- Multiple isolated local board services with PID, port, open, and stop controls.
- One local Excalidraw library shared by every managed board.
- Local formula editor, in-board formula palette, tray, single instance, and a
  global shortcut.
- Local-first storage with services bound only to `127.0.0.1`.

See the [cross-platform development guide](docs/cross-platform-development.md)
for architecture, macOS setup, commands, and the current support matrix.

## Legacy Windows 0.4 features

- Manage multiple workspace roots with a lazy-loaded `.excalidraw` file tree.
- Create boards and folders without leaving the application.
- Start, open, restart, and stop several boards on separate ports.
- Select a free port automatically from 6417, or assign one manually.
- View PID, port, URL, source, theme, start time, path, and command line.
- Discover compatible `excalidraw-edit` processes started in other terminals.
- Open recent boards from the system tray.
- Forward board paths to the existing window through single-instance IPC.
- Persist one shared Excalidraw library across all managed boards and ports.
- Import, merge, replace, export, clear, and browse public libraries.
- Open the local formula editor from the toolbar or system tray.
- Type LaTeX with syntax highlighting, broad backslash-command completion, a
  compact hover symbol palette, and a live preview, or edit visually with MathLive.
- Toggle a floating formula palette over a managed board with `Ctrl+Alt+F`,
  then press `Ctrl+Enter` to insert the rendered SVG into Excalidraw.
- Paste a formula screenshot, or press `Ctrl+Alt+O` to select a screen region
  in memory and recognize it with optional, fully local OCR.
- Export formulas without an online rendering service; SVG is the default.
- Use `list` and `stop-all` from a terminal.
- Follow the Windows display language automatically or switch between English
  and Simplified Chinese from the application settings.

## Screenshots

### Manager interface

Manage local boards, ports, and running services from one Windows desktop
window.

<p align="center">
  <img src="assets/screenshots/manager.png" alt="Excalidraw Manager interface in Simplified Chinese" width="960">
</p>

### Excalidraw note editing

Open each managed board in the browser and edit it with the full Excalidraw
canvas.

<p align="center">
  <img src="assets/screenshots/excalidraw-note.gif" alt="Editing a local note in Excalidraw" width="1200">
</p>

## Legacy Windows environment

| Component | Requirement |
| --- | --- |
| Operating system | Windows 10 or Windows 11, 64-bit |
| PowerShell | Windows PowerShell 5.1 or newer |
| Node.js | 20.11.0 or newer; tested with 24.18.0 |
| `excalidraw-edit` | **Exactly 0.1.1** |
| Source builds | Windows .NET Framework 4.x C# compiler (`csc.exe`) |
| Browser | A current Chromium-, Firefox-, or WebView-compatible browser |
| Optional local formula OCR | 64-bit Python 3.10-3.12, Microsoft Visual C++ 2019+ x64 runtime, and about 450-650 MB on disk |

`excalidraw-edit` is pinned because the managed-library integration patches
specific client markers from version 0.1.1. A different version is rejected
instead of producing a subtly broken build.

Node.js and `excalidraw-edit` are external prerequisites; they are not bundled
in the executable or repository. Custom installation directories are supported
as long as `node.exe` and `excalidraw-edit.cmd` are on `PATH`.

The formula editor bundles MathLive 0.110.0 and MathJax 4.1.3 in the application
runtime. They do not need to be installed globally, and formula editing and
rendering do not require a network connection.

Formula OCR is optional. Its Python environment and model weights are not
bundled in the application, source repository, or release package.

## Legacy Windows quick start

1. Install Node.js, then open a **new** PowerShell window.
2. Install the compatible editor globally:

   ```powershell
   npm install --global excalidraw-edit@0.1.1
   ```

3. In the project directory, verify the environment:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\check-environment.ps1
   ```

4. Build and install for the current Windows user:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
   ```

5. Open a new terminal and start the manager:

   ```powershell
   excalidraw-manager
   ```

The default installation directory is:

```text
%LOCALAPPDATA%\Programs\ExcalidrawManager
```

`install.ps1` adds this directory to the current user's `PATH`. It does not
require administrator rights.

## Legacy Windows prebuilt package

If a package already contains the tested files and directories below, it can be
installed without recompiling:

```text
dist\ExcalidrawManager.exe
dist\ExcalidrawManager.Cli.exe
dist\runtime\FormulaCapture.exe
dist\runtime\main.js
dist\runtime\server.mjs
dist\runtime\formula-server.mjs
dist\runtime\formula-overlay.mjs
dist\runtime\formula-ocr-provider\
dist\runtime\formula-editor\
dist\runtime\formula-editor\completion.mjs
```

After installing the Node.js and `excalidraw-edit@0.1.1` prerequisites, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -SkipBuild
```

For portable use, keep the complete `dist` directory together and launch
`dist\ExcalidrawManager.exe`. The `runtime` directory must remain beside the
executable.

## Legacy Windows usage

The GUI can add one or more workspace roots. Double-click a board or use the
toolbar to start it, then open its local URL in your default browser. Use
**Environment** to see the resolved Node.js, `excalidraw-edit`, runtime,
library, and settings paths.

Terminal commands:

```powershell
excalidraw-manager                         # Open the GUI
excalidraw-manager .\board.excalidraw     # Open a board through the GUI
excalidraw-manager list                    # List compatible running instances
excalidraw-manager stop-all                # Stop every discovered compatible instance
excalidraw-manager --version
```

Be careful with `stop-all`: it also stops compatible `excalidraw-edit`
processes launched outside this manager.

## Local formula editor (0.5.0)

Choose **Formula editor** on the manager toolbar or in the system-tray menu.
The manager starts a service bound to `127.0.0.1` and opens the editor in your
default browser. The page starts in source mode with the LaTeX input focused,
so you can begin typing immediately. Switch to the visual editor when you want
MathLive-assisted editing; both modes update the preview.

MathJax renders the preview entirely from bundled local assets. Source mode
highlights LaTeX commands, braces, comments, numbers, operators, and errors.
Type a backslash to open suggestions from the expanded command catalog; for
example, `\lef` offers `\leftarrow`, `\leftrightarrow`, arrow tails, and
harpoons. Use the arrow keys to choose one, `Tab` or `Enter` to accept it, and
`Escape` to close the list. The previous large formula-template panel remains
removed. A compact category strip now opens small downward symbol menus on
hover, focus, or click, then inserts the selected item at the current caret.

The editor can:

- Copy LaTeX, inline or block Markdown, MathML, AsciiMath, Typst, or SVG code;
  SVG is selected by default.
- Export SVG, transparent or background-filled PNG, JPG, and `.tex` files.
- Render and copy the current formula as SVG with `Ctrl+Enter`.

Every managed board includes an `fx` launcher for a draggable formula palette.
Press `Ctrl+Alt+F` to show or hide it. Resize from any edge or corner; its size
and position persist. At narrow widths, categories and actions reflow, source
text wraps, and wide previews scale down without a horizontal scrollbar.
`Ctrl+Enter` sends sanitized SVG code directly to
Excalidraw and leaves the palette open. If direct insertion is unavailable, the
palette copies the SVG so it can be pasted with `Ctrl+V`. The embedded palette
stays inside the browser viewport by design; choose **Pop out** to obtain a
normal resizable window that can be moved beyond the board or onto another
monitor while still inserting into the original board.

With a compatible local OCR provider installed, paste a PNG, JPG, or WebP
formula screenshot into the compact palette. Recognition runs in the background
and applies the first LaTeX candidate to the input for review. The full editor's
**Image recognition** page also supports choosing, dropping, or pasting an image
and reviewing multiple candidates. While the board, embedded palette, or detached
formula window has focus, press `Ctrl+Alt+O` (or choose **Capture OCR**) to drag
around any screen region and recognize it immediately. This is intentionally a
window-level shortcut and does not take over an operating system global hotkey.
On Windows, the native picker transfers PNG data in memory. On macOS, Apple's
picker writes only to a private temporary directory; the helper streams that PNG
to the local formula service and removes it immediately. Neither platform writes
to the user's Screenshots folder. The first macOS capture may ask for **System
Settings → Privacy & Security → Screen Recording** access. Editing, rendering,
and export remain fully usable when OCR is not installed.

### Optional local RapidLaTeXOCR provider

Version 0.5.0 includes the provider adapter and an explicit installation script,
but it does **not** bundle a Python environment or model weights and never
downloads them merely by starting the manager. On macOS or Linux, install
64-bit Python 3.10-3.12, review the upstream model terms, and run:

```bash
./scripts/install-formula-ocr.sh \
  --install-root "$HOME/Library/Application Support/Excalidraw Manager/FormulaOCR" \
  --python "$(command -v python3)" \
  --accept-upstream-model-license
```

On Windows, ensure the Microsoft Visual C++ 2019-or-newer x64 runtime is
present, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-formula-ocr.ps1 `
  -InstallRoot 'D:\DevTools\ExcalidrawManager\FormulaOCR' `
  -Python 'D:\DevTools\Python\Master\Py312\python.exe' `
  -AcceptUpstreamModelLicense
```

Adjust the Python path to match your installation. The script creates an
isolated environment and keeps its pip, temporary, Hugging Face, and Torch cache
directories under the selected root. It refuses the Windows system drive by
default. Expect approximately **260-290 MB of downloads** and **450-650 MB of
disk usage** after installation. It does not install the Microsoft Visual C++
runtime; if that prerequisite is missing, install it separately only after
reviewing and approving Microsoft's installer.

After installation, open **Settings**, enable automatic local formula OCR, and
select the same `FormulaOCR` root. The manager then starts the provider only on
`127.0.0.1`, creates an ephemeral Bearer token, and stops the provider with the
application. This RapidLaTeXOCR adapter intentionally uses CPU inference.

The model files are not committed to this repository or included in public
releases. The referenced weights originate from pix2tex and are marked upstream
as **CC BY-NC-SA**; review those non-commercial/share-alike terms before
downloading or redistributing them. See
[Third-party notices](THIRD_PARTY_NOTICES.md) for the upstream links and caveat.

Providers can be added or replaced behind the documented model interface; see
[Formula recognition provider API](docs/formula-model-api.md). This separation
allows later OCR experiments without changing the editor UI.

To use a different local experimental provider, create
`%LOCALAPPDATA%\ExcalidrawManager\formula-providers.json`:

```json
{
  "providers": [
    {
      "id": "my-formula-model",
      "baseUrl": "http://127.0.0.1:17861",
      "enabled": true
    }
  ]
}
```

The adapter only accepts loopback HTTP addresses. A provider that needs a
Bearer token can name an inherited environment variable with `tokenEnv`; do not
put the token itself in this file. The image-recognition page discovers the
provider through `/v1/info` and enables recognition without changing the UI.

## Interface language

The default **Follow system** setting uses Simplified Chinese when the Windows
display language is Chinese and English otherwise. To choose explicitly, open
**Settings → Interface language** and select **Simplified Chinese** or
**English**. The manager restarts its interface to apply the change; running
board services remain open.

The selected value is stored as `Language` in
`%LOCALAPPDATA%\ExcalidrawManager\settings.json`. The command-line companion
follows the Windows display language.

## Local data and backups

User data is stored outside the installation directory:

| Data | Location |
| --- | --- |
| Settings and workspace roots | `%LOCALAPPDATA%\ExcalidrawManager\settings.json` |
| Shared library | `%LOCALAPPDATA%\ExcalidrawManager\libraries\shared.excalidrawlib` |
| Managed-process registry | `%LOCALAPPDATA%\ExcalidrawManager\managed-processes.json` |
| Boards | Wherever the user creates the `.excalidraw` files |

Back up the `.excalidraw` files and `shared.excalidrawlib` to preserve all
local content. Reinstalling the program does not intentionally remove these
files.

Library changes made in one managed board are written to the shared library.
Refresh already-open board tabs after importing, replacing, or clearing the
library from the manager. Public library imports accept HTTPS URLs from
`libraries.excalidraw.com` only.

## Build and test

No .NET SDK or NuGet restore is required. `build.ps1` uses the 64-bit .NET
Framework C# compiler included with supported Windows installations and
generates the managed browser client from the installed `excalidraw-edit`
package.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\smoke.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\localization.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\formula-editor.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\formula-ocr-provider.ps1
```

The smoke test creates a temporary board, starts one local Node.js process,
checks the scene/library/client endpoints, and stops only the PID that it
created. The localization test validates Chinese and English strings, language
preference handling, and version metadata without modifying user settings. The
formula-editor test checks the local editor server, bundled browser assets,
security boundaries, and formula-editor API without installing an OCR model.
Its capture tests use a tiny fake helper and never take a real screenshot.
The provider-contract test uses a small fake Python runtime and does not download
or install RapidLaTeXOCR or its weights.

Project layout:

```text
assets/                         Application icon sources
docs/formula-model-api.md       Replaceable OCR provider contract
dist/                           Tested Windows payload
runtime/                        Local HTTP runtimes and browser-client patcher
runtime/formula-overlay.mjs     Floating formula palette for managed boards
runtime/formula-ocr-provider/   Loopback-only optional OCR provider adapter
runtime/formula-editor/         Formula editor UI and bundled math assets
scripts/install-formula-ocr.ps1 Explicit optional OCR installer
src/                            WinForms GUI and command-line launcher
src/FormulaCapture.cs           In-memory Windows region picker used by capture OCR
runtime/FormulaCapture          Private-temporary-file macOS region picker
tests/                          PowerShell integration tests
build.ps1                       Reproducible local build
check-environment.ps1           Prerequisite diagnostics
install.ps1                     Per-user installer
```

## Troubleshooting

### `node.exe` or `excalidraw-edit` cannot be found

Open a new terminal after installing Node.js, then inspect command resolution:

```powershell
Get-Command node.exe
Get-Command excalidraw-edit.cmd
node --version
excalidraw-edit --version
npm root --global
```

If `excalidraw-edit` is missing or has another version:

```powershell
npm uninstall --global excalidraw-edit
npm install --global excalidraw-edit@0.1.1
```

If you use a custom npm prefix, add the directory containing
`excalidraw-edit.cmd` to your user `PATH`, then open a new terminal.

### PowerShell blocks a script

The documented commands use `-ExecutionPolicy Bypass` for that process only;
they do not permanently change the machine's execution policy.

### The C# compiler is missing

Run `check-environment.ps1` to see the expected path. Source builds require:

```text
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

Use `install.ps1 -SkipBuild` with a complete prebuilt package, or repair/enable
the Windows .NET Framework components before building from source.

### A board does not start

- Check that the requested port is free, or use automatic port selection.
- Run `check-environment.ps1` and correct every `[FAIL]` entry.
- Confirm that `dist\runtime\server.mjs` and `dist\runtime\main.js` are beside
  the executable in the `runtime` directory.
- Use the GUI's **Environment** dialog to confirm the paths actually selected.

## Security and network behavior

Managed services bind to `127.0.0.1` and have no remote-access authentication.
Do not modify the runtime to listen on a public interface unless you also add
appropriate authentication, authorization, TLS, and request protections.

Normal local editing does not require an online service. Network access is
used when installing npm prerequisites and when the user explicitly imports
an official public library. The bundled formula editor renders locally and does
not upload formulas or images. The optional managed RapidLaTeXOCR provider also
runs locally; only its explicit installation step downloads Python packages and
model files. A separately configured third-party provider may have different
network and privacy behavior, so review it before use.

## Contributing

Keep changes compatible with Windows PowerShell 5.1 and the built-in .NET
Framework compiler. Before opening a pull request, run `build.ps1`,
`tests\smoke.ps1`, `tests\localization.ps1`, `tests\formula-editor.ps1`, and
`tests\formula-ocr-provider.ps1`. Avoid committing personal board files or local
settings.

## License

Excalidraw Manager is available under the [MIT License](LICENSE). Bundled or
referenced third-party components retain their own copyright and license
terms; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
