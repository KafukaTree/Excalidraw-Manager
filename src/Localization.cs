using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace ExcalidrawManager
{
    public static class Localization
    {
        private static readonly Dictionary<string, string> Chinese = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Excalidraw Manager", "Excalidraw 管理器" },
            { "Add root", "添加工作区" },
            { "Open file", "打开文件" },
            { "New board", "新建画板" },
            { "Formula Editor", "公式编辑器" },
            { "Refresh", "刷新" },
            { "Search:", "搜索：" },
            { "Search all workspace roots; press Enter", "搜索所有工作区；按 Enter 执行" },
            { "Settings", "设置" },
            { "Libraries", "素材库" },
            { "Environment", "环境" },
            { "WORKSPACES", "工作区" },
            { "Recent boards", "最近画板" },
            { "Loading...", "正在加载..." },
            { "Access failed: {0}", "访问失败：{0}" },
            { "RUNNING INSTANCES", "运行中的实例" },
            { "Open", "打开" },
            { "Copy URL", "复制 URL" },
            { "Restart", "重启" },
            { "Stop", "停止" },
            { "Stop all", "全部停止" },
            { "File", "文件" },
            { "Port", "端口" },
            { "Source", "来源" },
            { "Theme", "主题" },
            { "Started", "启动时间" },
            { "Managed", "受管理" },
            { "External", "外部" },
            { "system", "跟随系统" },
            { "dark", "深色" },
            { "light", "浅色" },
            { "Show", "显示" },
            { "Stop all services", "停止所有服务" },
            { "Exit...", "退出..." },
            { "Still running in the system tray. Use Exit there to close it.", "软件仍在系统托盘中运行。请从托盘菜单选择“退出”以关闭。" },
            { "Yes: stop all excalidraw-edit services and exit.\r\nNo: keep services running and exit.\r\nCancel: return to the tray.", "是：停止所有 excalidraw-edit 服务并退出。\r\n否：保留服务运行并退出。\r\n取消：返回系统托盘。" },
            { "Exit Excalidraw Manager", "退出 Excalidraw 管理器" },
            { "New board here", "在此新建画板" },
            { "New folder...", "新建文件夹..." },
            { "Open in Explorer", "在资源管理器中打开" },
            { "Remove root", "移除工作区" },
            { "Start / Open", "启动 / 打开" },
            { "Start with port...", "指定端口启动..." },
            { "Force new instance", "强制新建实例" },
            { "Set alias...", "设置别名..." },
            { "Rename...", "重命名..." },
            { "Copy path", "复制路径" },
            { "Show in Explorer", "在资源管理器中显示" },
            { "Choose a folder containing .excalidraw boards", "选择包含 .excalidraw 画板的文件夹" },
            { "Open Excalidraw board", "打开 Excalidraw 画板" },
            { "Create Excalidraw board", "创建 Excalidraw 画板" },
            { "Excalidraw board (*.excalidraw)|*.excalidraw", "Excalidraw 画板 (*.excalidraw)|*.excalidraw" },
            { "Board created. Start it now?", "画板已创建。现在启动吗？" },
            { "The file already exists.", "该文件已存在。" },
            { "Folder name:", "文件夹名称：" },
            { "New folder", "新建文件夹" },
            { "New Folder", "新建文件夹" },
            { "Display alias (leave blank to clear):", "显示别名（留空可清除）：" },
            { "Board alias", "画板别名" },
            { "Stop this board before renaming it.", "请先停止该画板，再进行重命名。" },
            { "Rename", "重命名" },
            { "New file name:", "新文件名：" },
            { "Rename board", "重命名画板" },
            { "Search results: {0}  ({1})", "搜索结果：{0}  ({1})" },
            { "TCP port:", "TCP 端口：" },
            { "Start with port", "指定端口启动" },
            { "Starting {0}...", "正在启动 {0}..." },
            { "Starting formula editor...", "正在启动公式编辑器..." },
            { "Formula editor opened", "公式编辑器已打开" },
            { "Started PID {0} on port {1}", "已启动 PID {0}，端口 {1}" },
            { "Start failed", "启动失败" },
            { "Stopping PID {0} (brief save grace period)...", "正在停止 PID {0}（短暂等待保存）..." },
            { "Stop all discovered excalidraw-edit Node processes?", "停止所有已发现的 excalidraw-edit Node 进程吗？" },
            { "Stopping all services...", "正在停止所有服务..." },
            { "Stopped {0} service(s)", "已停止 {0} 个服务" },
            { "Restarting PID {0}...", "正在重启 PID {0}..." },
            { "{0} running instance(s)", "{0} 个实例正在运行" },
            { "Refresh failed: {0}", "刷新失败：{0}" },
            { "Open {0}  :{1}", "打开 {0}  :{1}" },
            { "Version:", "版本：" },
            { "Managed runtime", "受管理运行时" },
            { "Shared library", "共享素材库" },
            { "Excalidraw Libraries", "Excalidraw 素材库" },
            { "SHARED LOCAL LIBRARY", "共享本地素材库" },
            { "{0} library item(s). Changes made in managed boards are saved here automatically.", "共 {0} 个素材项。受管理画板中的更改会自动保存到这里。" },
            { "Load failed: {0}", "加载失败：{0}" },
            { "Import / Merge...", "导入 / 合并..." },
            { "Replace...", "替换..." },
            { "Export...", "导出..." },
            { "Browse via active board", "通过活动画板浏览" },
            { "Open local folder", "打开本地文件夹" },
            { "Clear", "清空" },
            { "Excalidraw library (*.excalidrawlib)|*.excalidrawlib|JSON (*.json)|*.json", "Excalidraw 素材库 (*.excalidrawlib)|*.excalidrawlib|JSON (*.json)|*.json" },
            { "Excalidraw library (*.excalidrawlib)|*.excalidrawlib", "Excalidraw 素材库 (*.excalidrawlib)|*.excalidrawlib" },
            { "{0} item(s) are now stored locally. Refresh already-open board tabs to load the updated library.", "现已在本地存储 {0} 个素材项。请刷新已打开的画板标签页以加载更新后的素材库。" },
            { "Library imported", "素材库已导入" },
            { "Replace the complete shared local library?", "替换整个共享本地素材库吗？" },
            { "Replace library", "替换素材库" },
            { "Start a board first. Public libraries must be opened from a running board so Excalidraw can generate the secure return token.", "请先启动一个画板。公共素材库必须从运行中的画板打开，以便 Excalidraw 生成安全返回令牌。" },
            { "Browse libraries", "浏览素材库" },
            { "In the board, open the Library panel and click 'Browse libraries'.\r\nChoose a library on the website, then click 'Add to Excalidraw'. It will return to this local board and be saved automatically.", "请在画板中打开素材库面板并点击“浏览素材库”。\r\n在网站上选择素材库，然后点击“添加到 Excalidraw”。页面会返回此本地画板并自动保存。" },
            { "Install a public library", "安装公共素材库" },
            { "Clear every item from the shared local library?\r\nOpen board tabs must be refreshed afterward.", "清空共享本地素材库中的所有素材项吗？\r\n完成后需要刷新已打开的画板标签页。" },
            { "Clear library", "清空素材库" },
            { "Starting port:", "起始端口：" },
            { "Board theme:", "画板主题：" },
            { "Interface language:", "界面语言：" },
            { "Follow system", "跟随系统" },
            { "Simplified Chinese", "简体中文" },
            { "English", "English" },
            { "Save", "保存" },
            { "Cancel", "取消" },
            { "OK", "确定" },
            { "The interface will restart to apply the new language. Running board services will stay open.", "界面将重新启动以应用新语言。正在运行的画板服务会保持开启。" },
            { "Language changed", "语言已更改" },
            { "(unknown)", "（未知）" },
            { "Item {0}", "素材项 {0}" },
            { "unknown", "未知" },
            { "published", "已发布" },
            { "unpublished", "未发布" },
            { "{0} elements", "{0} 个元素" },
            { "Not a valid .excalidrawlib file: libraryItems is missing.", "不是有效的 .excalidrawlib 文件：缺少 libraryItems。" },
            { "Not a valid .excalidrawlib file: libraryItems is not an array.", "不是有效的 .excalidrawlib 文件：libraryItems 不是数组。" },
            { "Cannot find node.exe or the global excalidraw-edit installation. Run: npm i -g excalidraw-edit", "找不到 node.exe 或全局 excalidraw-edit。请运行：npm i -g excalidraw-edit" },
            { "This release requires excalidraw-edit 0.1.1, but found {0}. Run: npm i -g excalidraw-edit@0.1.1", "此版本需要 excalidraw-edit 0.1.1，但检测到 {0}。请运行：npm i -g excalidraw-edit@0.1.1" },
            { "Managed Excalidraw runtime is missing: {0}", "缺少受管理的 Excalidraw 运行时：{0}" },
            { "Formula editor runtime is missing: {0}", "缺少公式编辑器运行时：{0}" },
            { "Formula editor assets are missing: {0}", "缺少公式编辑器资源：{0}" },
            { "Failed to start formula editor.", "无法启动公式编辑器。" },
            { "Refusing to open a non-local formula editor URL.", "已拒绝打开非本机的公式编辑器地址。" },
            { "Formula editor exited: {0}", "公式编辑器已退出：{0}" },
            { "Formula editor did not start within 8 seconds.", "公式编辑器未能在 8 秒内启动。" },
            { "No free TCP port is available.", "没有可用的 TCP 端口。" },
            { "Port {0} is already in use.", "端口 {0} 已被占用。" },
            { "Failed to start excalidraw-edit.", "无法启动 excalidraw-edit。" },
            { "excalidraw-edit exited: {0}", "excalidraw-edit 已退出：{0}" },
            { "excalidraw-edit did not listen on port {0} within 5 seconds.", "excalidraw-edit 未能在 5 秒内监听端口 {0}。" },
            { "Usage:", "用法：" },
            { "Open the GUI", "打开图形界面" },
            { "Open a board through the GUI", "通过图形界面打开画板" },
            { "List running instances", "列出运行中的实例" },
            { "Stop all excalidraw-edit Node processes", "停止所有 excalidraw-edit Node 进程" },
            { "{0} instance(s)", "{0} 个实例" },
            { "Stopped {0} instance(s)", "已停止 {0} 个实例" },
            { "Unknown command: {0}", "未知命令：{0}" },
            { "Stopped {0} excalidraw-edit process(es).", "已停止 {0} 个 excalidraw-edit 进程。" }
        };

        private static readonly string SystemLanguage = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en";
        private static string _language = SystemLanguage;

        public static string Language { get { return _language; } }
        public static bool IsChinese { get { return string.Equals(_language, "zh-CN", StringComparison.OrdinalIgnoreCase); } }

        public static void Configure(string preference)
        {
            _language = Resolve(preference);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(_language);
        }

        public static string NormalizePreference(string preference)
        {
            if (string.Equals(preference, "zh", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(preference, "zh-CN", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
            if (string.Equals(preference, "en", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(preference, "en-US", StringComparison.OrdinalIgnoreCase)) return "en";
            return "system";
        }

        private static string Resolve(string preference)
        {
            string normalized = NormalizePreference(preference);
            if (normalized == "zh-CN") return "zh-CN";
            if (normalized == "en") return "en";
            return SystemLanguage;
        }

        public static string T(string english)
        {
            if (!IsChinese || string.IsNullOrEmpty(english)) return english;
            string translated;
            return Chinese.TryGetValue(english, out translated) ? translated : english;
        }

        public static string F(string english, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, T(english), args);
        }
    }

    public sealed class LocalizedChoice
    {
        public string Value { get; private set; }
        public string Text { get; private set; }

        public LocalizedChoice(string value, string text)
        {
            Value = value;
            Text = text;
        }

        public override string ToString() { return Text; }
    }
}
