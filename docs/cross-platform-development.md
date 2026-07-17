# Excalidraw Manager 0.5 跨平台开发指南

0.5 版在保留原 Windows WinForms 实现的同时，新增一套 macOS、Windows、Linux
共用的桌面架构。当前 macOS Apple Silicon 已完成本机编译和运行验证；Windows 与
Linux 共用同一份前端和 Rust 代码，后续需要分别在对应系统完成安装包回归。

## 技术栈

| 层 | 技术 | 职责 |
| --- | --- | --- |
| 桌面壳 | Tauri 2 | 原生窗口、托盘、单实例、权限和安装包 |
| 界面 | React 19 + TypeScript + Vite | 工作区、文件树、运行状态、设置和操作反馈 |
| 本机后端 | Rust | 文件安全边界、设置、端口、子进程与生命周期管理 |
| 画板运行时 | Node.js + `excalidraw-edit@0.1.1` | 本地画板读写、共享素材库和公式面板注入 |
| 公式运行时 | Node.js + MathLive + MathJax | 离线公式编辑、预览、导出与可选 OCR Provider |
| 公式 OCR | Python + RapidLaTeXOCR + ONNX Runtime | 可选的本地 CPU 公式图片识别 |

桌面进程只允许访问用户主动添加的工作区。画板和公式服务绑定到
`127.0.0.1`，公式地址还会在 Rust 和 Node 两层校验为本机 HTTP 地址。

## macOS 开发

需要 Node.js 20 或更高版本、Rust stable、Xcode Command Line Tools。

```bash
xcode-select --install
brew install node rustup
export PATH="/opt/homebrew/opt/rustup/bin:$PATH"
rustup default stable
npm install
npm run desktop:dev
```

Intel Mac 使用 Homebrew 时，Rustup 通常位于 `/usr/local/opt/rustup/bin`。
也可以通过 `EXCALIDRAW_MANAGER_NODE=/绝对路径/node` 指定 Node.js。

### macOS 可选公式 OCR

本地 OCR 不随应用或安装包分发。先安装 64 位 Python 3.10-3.12，并阅读 pix2tex
上游权重的 CC BY-NC-SA 条款；确认许可适合你的用途后，在仓库根目录显式运行：

```bash
./scripts/install-formula-ocr.sh \
  --install-root "$HOME/Library/Application Support/Excalidraw Manager/FormulaOCR" \
  --python "$(command -v python3)" \
  --accept-upstream-model-license
```

脚本会创建隔离的 Python 环境、下载固定版本和 SHA-256 的四个模型文件，并把缓存
限制在所选目录。完成后在应用的**设置 → 本地公式 OCR**中启用功能并选择同一目录。
应用只在打开公式编辑器时启动 OCR，服务仅监听 `127.0.0.1`，使用每次随机生成的
Bearer token，并随应用退出。

## 构建与测试

```bash
npm run check:runtime
npm run test:runtime
npm run build
cargo test --manifest-path src-tauri/Cargo.toml
npm run desktop:build
```

macOS 构建产物位于 `src-tauri/target/release/bundle/macos/` 和
`src-tauri/target/release/bundle/dmg/`。Windows 与 Linux 应在各自操作系统上运行
同一条 `npm run desktop:build`，分别生成 MSI/NSIS 和 DEB/AppImage 等本机包。

## 当前支持情况

| 能力 | macOS | Windows | Linux |
| --- | --- | --- | --- |
| 工作区、文件树、新建画板/目录 | 已实现 | 共用实现，待回归 | 共用实现，待回归 |
| 多画板端口和进程管理 | 已实现 | 共用实现，待回归 | 共用实现，待回归 |
| 共享素材库与官方库导入 | 已实现 | 共用实现，待回归 | 共用实现，待回归 |
| 本地公式编辑器与画板公式面板 | 已实现 | 共用实现，待回归 | 共用实现，待回归 |
| 托盘、单实例、全局公式快捷键 | 已实现 | 共用实现，待回归 | 共用实现，待回归 |
| 框选屏幕公式 OCR | 已实现（系统框选器，私有临时 PNG 随即清理） | 保留旧版内存 helper | 待新增 portal helper |
| 粘贴/选择图片公式 OCR | 已实现 | 共用实现，待回归 | 共用实现，待回归 |
| 可选 RapidLaTeXOCR 检测与自动管理 | 已实现 | 共用实现，待回归 | 共用实现，待回归 |

全局打开公式编辑器的快捷键是 `Command/Ctrl + Shift + F`；画板内部公式面板继续
使用 `Ctrl/Command + Alt + F`，两者不会争用同一组合键。

## 目录结构

```text
desktop/          React/TypeScript 桌面界面
src-tauri/        Rust/Tauri 本机后端与打包配置
runtime/          画板、公式编辑器和共享素材库运行时
src/              原 Windows WinForms/C# 实现（迁移参考与兼容保留）
tests/            Node 运行时与服务集成测试
scripts/          Windows 与 macOS/Linux 可选 OCR 安装脚本
```

发布版目前仍需要系统中存在 Node.js 20+。将 Node 运行时改为按目标平台打包的 Tauri
sidecar 是后续发布完善项；在此之前应用会优先使用
`EXCALIDRAW_MANAGER_NODE`，随后查找 `PATH`、Homebrew 和常见系统路径。
