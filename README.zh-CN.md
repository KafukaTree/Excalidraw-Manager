# Excalidraw Manager

简体中文 | [English](README.md)

Excalidraw Manager 是一个面向 Windows 10/11 的轻量桌面软件，用于集中管理并
运行多个本地 `.excalidraw` 画板。它在
[`excalidraw-edit`](https://github.com/wh1le/excalidraw-edit) 的基础上提供
WinForms 图形界面、进程控制、系统托盘以及跨画板共享的本地素材库。

软件以本地使用为主：画板和设置保存在你的电脑上，受管理的画板服务仅监听
`127.0.0.1`。

> 本项目是独立项目，与 Excalidraw 官方不存在隶属或背书关系。

## 功能

- 添加多个工作区，并按需加载其中的 `.excalidraw` 文件树。
- 直接在软件中创建画板和文件夹。
- 在不同端口启动、打开、重启或停止多个画板。
- 从 6417 开始自动选择空闲端口，也可以手动指定端口。
- 查看 PID、端口、URL、来源、主题、启动时间、路径及命令行。
- 识别从其他终端启动的兼容 `excalidraw-edit` 进程。
- 通过系统托盘快速打开最近使用的画板。
- 单实例运行；再次启动时会激活原窗口并转交画板路径。
- 所有受管理画板和端口共用一个本地 Excalidraw 素材库。
- 支持素材库合并导入、替换、导出、清空及浏览官方公共素材库。
- 提供 `list` 和 `stop-all` 命令行操作。

## 支持的环境

| 组件 | 要求 |
| --- | --- |
| 操作系统 | 64 位 Windows 10 或 Windows 11 |
| PowerShell | Windows PowerShell 5.1 或更高版本 |
| Node.js | 20.11.0 或更高版本；已在 24.18.0 上验证 |
| `excalidraw-edit` | **必须为 0.1.1** |
| 从源码构建 | Windows .NET Framework 4.x C# 编译器（`csc.exe`） |
| 浏览器 | 当前版本的 Chromium、Firefox 或兼容 WebView 的浏览器 |

之所以固定 `excalidraw-edit` 版本，是因为共享素材库功能会对 0.1.1 的客户端
代码标记进行补丁处理。环境检查会直接拒绝其他版本，避免构建成功后出现难以察觉
的功能异常。

Node.js 和 `excalidraw-edit` 属于外部前置依赖，不会打包进 EXE 或代码仓库。
它们可以安装在自定义位置，但 `node.exe` 和 `excalidraw-edit.cmd` 必须能从
`PATH` 中找到。

## 从源码快速安装

1. 安装 Node.js，然后重新打开一个 **新的** PowerShell 窗口。
2. 全局安装兼容版本：

   ```powershell
   npm install --global excalidraw-edit@0.1.1
   ```

3. 在项目根目录检查环境：

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\check-environment.ps1
   ```

4. 为当前 Windows 用户构建并安装：

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
   ```

5. 重新打开终端并启动：

   ```powershell
   excalidraw-manager
   ```

默认安装目录为：

```text
%LOCALAPPDATA%\Programs\ExcalidrawManager
```

`install.ps1` 会将该目录加入当前用户的 `PATH`，不需要管理员权限。

## 安装预编译包

如果下载的发布包已经包含下列四个经过测试的文件，可以跳过本地编译：

```text
dist\ExcalidrawManager.exe
dist\ExcalidrawManager.Cli.exe
dist\runtime\main.js
dist\runtime\server.mjs
```

先安装 Node.js 和 `excalidraw-edit@0.1.1`，然后运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -SkipBuild
```

如果要便携运行，请完整保留 `dist` 目录并启动
`dist\ExcalidrawManager.exe`；`runtime` 文件夹必须与 EXE 放在一起。

## 使用方法

在图形界面中添加一个或多个工作区。双击画板或使用工具栏启动，然后在默认浏览器
中打开它的本地 URL。点击 **Environment** 可以查看软件实际找到的 Node.js、
`excalidraw-edit`、运行时、共享素材库和设置文件路径。

命令行用法：

```powershell
excalidraw-manager                         # 打开图形界面
excalidraw-manager .\board.excalidraw     # 通过图形界面打开画板
excalidraw-manager list                    # 列出兼容的运行实例
excalidraw-manager stop-all                # 停止所有识别到的兼容实例
excalidraw-manager --version
```

请谨慎使用 `stop-all`：它也会停止不是由本软件启动、但能被识别的
`excalidraw-edit` 进程。

## 本地数据与备份

用户数据不会放在安装目录中：

| 数据 | 路径 |
| --- | --- |
| 设置和工作区列表 | `%LOCALAPPDATA%\ExcalidrawManager\settings.json` |
| 共享素材库 | `%LOCALAPPDATA%\ExcalidrawManager\libraries\shared.excalidrawlib` |
| 受管理进程记录 | `%LOCALAPPDATA%\ExcalidrawManager\managed-processes.json` |
| 画板 | 用户创建 `.excalidraw` 文件的位置 |

备份全部 `.excalidraw` 文件和 `shared.excalidrawlib` 即可保留主要本地内容。
重新安装软件不会主动删除这些文件。

任一受管理画板中的素材库变化都会写入共享素材库。如果在管理器中导入、替换或
清空了素材库，请刷新已经打开的画板标签页。公共素材库导入只接受
`libraries.excalidraw.com` 的 HTTPS 地址。

## 构建与测试

本项目不需要 .NET SDK，也不需要 NuGet 还原。`build.ps1` 使用受支持 Windows
系统内置的 64 位 .NET Framework C# 编译器，并根据已安装的
`excalidraw-edit` 生成受管理的浏览器客户端。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\smoke.ps1
```

冒烟测试会创建一个临时画板，启动一个本地 Node.js 进程，检查画板、素材库和
客户端端点，最后只停止它自己创建的 PID。

项目结构：

```text
assets/                  应用图标源文件
dist/                    已测试的 Windows 运行文件
runtime/                 本地 HTTP 运行时及浏览器客户端补丁器
src/                     WinForms 图形界面和命令行启动器
tests/                   PowerShell 冒烟测试
build.ps1                本地可复现构建脚本
check-environment.ps1    前置依赖诊断脚本
install.ps1              当前用户安装脚本
```

## 常见问题

### 找不到 `node.exe` 或 `excalidraw-edit`

安装 Node.js 后重新打开终端，再检查实际命令路径：

```powershell
Get-Command node.exe
Get-Command excalidraw-edit.cmd
node --version
excalidraw-edit --version
npm root --global
```

如果 `excalidraw-edit` 没有安装或版本不对：

```powershell
npm uninstall --global excalidraw-edit
npm install --global excalidraw-edit@0.1.1
```

如果使用了自定义 npm 全局目录，请把包含 `excalidraw-edit.cmd` 的目录加入用户
`PATH`，然后重新打开终端。

### PowerShell 阻止脚本运行

本文命令中的 `-ExecutionPolicy Bypass` 只影响当次 PowerShell 进程，不会永久
修改电脑的执行策略。

### 缺少 C# 编译器

运行 `check-environment.ps1` 可查看软件期待的路径：

```text
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

可以使用完整预编译包配合 `install.ps1 -SkipBuild`，或者修复/启用 Windows 的
.NET Framework 组件后再从源码构建。

### 画板无法启动

- 检查指定端口是否空闲，或改用自动选择端口。
- 运行 `check-environment.ps1` 并处理每一条 `[FAIL]`。
- 确认 `dist\runtime\server.mjs` 和 `dist\runtime\main.js` 位于 EXE 旁边的
  `runtime` 目录中。
- 在图形界面中打开 **Environment**，核对软件实际解析到的路径。

## 安全与网络行为

受管理的服务只绑定 `127.0.0.1`，且没有为远程访问设计身份验证。除非同时增加
身份认证、权限控制、TLS 和请求防护，否则不要把运行时改成监听公网或局域网地址。

正常编辑本地画板不依赖在线服务。安装 npm 前置依赖，以及用户主动导入官方公共
素材库时，需要访问网络。

## 参与贡献

请保持对 Windows PowerShell 5.1 和系统内置 .NET Framework 编译器的兼容性。
提交拉取请求前运行 `build.ps1` 和 `tests\smoke.ps1`，不要提交个人画板或本地
设置文件。

## 许可证

Excalidraw Manager 使用 [MIT License](LICENSE) 开源。所包含或引用的第三方
组件仍遵循各自的版权和许可条款，详见
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
