using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("Excalidraw Manager")]
[assembly: AssemblyDescription("Manage multiple local Excalidraw boards on Windows")]
[assembly: AssemblyProduct("Excalidraw Manager")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Excalidraw Manager contributors")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

namespace ExcalidrawManager
{
    internal static class Program
    {
        private const string MutexName = "Local\\ExcalidrawManager.SingleInstance";
        private const string PipeName = "ExcalidrawManager.SingleInstance.Pipe";

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "stop-all", StringComparison.OrdinalIgnoreCase))
            {
                int count = ProcessService.StopAllDiscovered();
                Console.WriteLine("Stopped {0} excalidraw-edit process(es).", count);
                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in ProcessService.Discover())
                    Console.WriteLine("{0}\t{1}\t{2}\t{3}", item.Pid, item.Port, item.Url, item.FilePath);
                return;
            }

            string initialFile = args.Length > 0 && File.Exists(args[0]) ? Path.GetFullPath(args[0]) : null;
            bool ownsMutex;
            using (var mutex = new Mutex(true, MutexName, out ownsMutex))
            {
                if (!ownsMutex)
                {
                    SingleInstanceChannel.Send(PipeName, initialFile ?? "__SHOW__");
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var form = new MainForm(initialFile);
                using (var channel = new SingleInstanceChannel(PipeName, form))
                {
                    channel.Start();
                    Application.Run(form);
                }
            }
        }
    }

    public sealed class SingleInstanceChannel : IDisposable
    {
        private readonly string _pipeName;
        private readonly MainForm _form;
        private Thread _thread;
        private volatile bool _stopping;

        public SingleInstanceChannel(string pipeName, MainForm form) { _pipeName = pipeName; _form = form; }

        public void Start()
        {
            _thread = new Thread(ListenLoop) { IsBackground = true, Name = "ExcalidrawManager IPC" };
            _thread.Start();
        }

        private void ListenLoop()
        {
            while (!_stopping)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None))
                    {
                        pipe.WaitForConnection();
                        using (var reader = new StreamReader(pipe, Encoding.UTF8))
                        {
                            string message = reader.ReadLine();
                            if (_stopping) return;
                            if (!string.IsNullOrEmpty(message) && !_form.IsDisposed)
                                _form.BeginInvoke(new Action<string>(_form.HandleActivation), message);
                        }
                    }
                }
                catch { if (_stopping) return; Thread.Sleep(100); }
            }
        }

        public static bool Send(string pipeName, string message)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out))
                    {
                        pipe.Connect(250);
                        using (var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true }) writer.WriteLine(message);
                        return true;
                    }
                }
                catch { Thread.Sleep(100); }
            }
            return false;
        }

        public void Dispose()
        {
            _stopping = true;
            Send(_pipeName, "__STOP__");
            if (_thread != null) _thread.Join(1000);
        }
    }

    public sealed class AppSettings
    {
        public List<string> Roots { get; set; }
        public int StartPort { get; set; }
        public string Theme { get; set; }
        public bool TrayHintShown { get; set; }
        public List<string> RecentFiles { get; set; }
        public Dictionary<string, string> Aliases { get; set; }

        public AppSettings()
        {
            Roots = new List<string>();
            StartPort = 6417;
            Theme = "system";
            RecentFiles = new List<string>();
            Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static class SettingsStore
    {
        public static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExcalidrawManager");
        public static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");
        public static readonly string LibraryDirectory = Path.Combine(DirectoryPath, "libraries");
        public static readonly string SharedLibraryPath = Path.Combine(LibraryDirectory, "shared.excalidrawlib");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new AppSettings();
                var result = new JavaScriptSerializer().Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (result == null) return new AppSettings();
                if (result.Roots == null) result.Roots = new List<string>();
                if (result.RecentFiles == null) result.RecentFiles = new List<string>();
                if (result.Aliases == null) result.Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                return result;
            }
            catch { return new AppSettings(); }
        }

        public static void Save(AppSettings settings)
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(settings), Encoding.UTF8);
        }
    }

    public sealed class DrawingInstance
    {
        public int Pid { get; set; }
        public int Port { get; set; }
        public string Url { get { return Port > 0 ? "http://localhost:" + Port : ""; } }
        public string FilePath { get; set; }
        public string FileName { get { return string.IsNullOrEmpty(FilePath) ? "(unknown)" : Path.GetFileName(FilePath); } }
        public string Alias { get; set; }
        public string DisplayName { get { return string.IsNullOrWhiteSpace(Alias) ? FileName : Alias + " (" + FileName + ")"; } }
        public string Theme { get; set; }
        public string Source { get; set; }
        public string CommandLine { get; set; }
        public DateTime? StartedAt { get; set; }
    }

    public sealed class ManagedRecord
    {
        public int Pid { get; set; }
        public string FilePath { get; set; }
        public int Port { get; set; }
    }

    public static class ManagedRegistry
    {
        private static readonly object Sync = new object();
        private static readonly string RegistryPath = Path.Combine(SettingsStore.DirectoryPath, "managed-processes.json");

        public static void Add(DrawingInstance item)
        {
            lock (Sync)
            {
                var items = LoadInternal();
                items.RemoveAll(x => x.Pid == item.Pid);
                items.Add(new ManagedRecord { Pid = item.Pid, FilePath = item.FilePath, Port = item.Port });
                SaveInternal(items);
            }
        }

        public static void Remove(int pid)
        {
            lock (Sync)
            {
                var items = LoadInternal();
                items.RemoveAll(x => x.Pid == pid);
                SaveInternal(items);
            }
        }

        public static bool Contains(int pid, string commandLine)
        {
            lock (Sync)
            {
                var match = LoadInternal().FirstOrDefault(x => x.Pid == pid);
                if (match == null) return false;
                return string.IsNullOrEmpty(match.FilePath) || (commandLine ?? "").IndexOf(match.FilePath, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public static void Prune(IEnumerable<int> activePids)
        {
            lock (Sync)
            {
                var active = new HashSet<int>(activePids);
                var items = LoadInternal();
                if (items.RemoveAll(x => !active.Contains(x.Pid)) > 0) SaveInternal(items);
            }
        }

        private static List<ManagedRecord> LoadInternal()
        {
            try
            {
                if (!File.Exists(RegistryPath)) return new List<ManagedRecord>();
                return new JavaScriptSerializer().Deserialize<List<ManagedRecord>>(File.ReadAllText(RegistryPath)) ?? new List<ManagedRecord>();
            }
            catch { return new List<ManagedRecord>(); }
        }

        private static void SaveInternal(List<ManagedRecord> items)
        {
            Directory.CreateDirectory(SettingsStore.DirectoryPath);
            File.WriteAllText(RegistryPath, new JavaScriptSerializer().Serialize(items), Encoding.UTF8);
        }
    }

    public static class LibraryStore
    {
        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 512 };
        }

        public static List<object> Load(string path)
        {
            if (!File.Exists(path)) return new List<object>();
            var document = Serializer().DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
            object raw;
            if (document == null || !document.TryGetValue("libraryItems", out raw))
                throw new InvalidDataException("Not a valid .excalidrawlib file: libraryItems is missing.");
            var enumerable = raw as IEnumerable;
            if (enumerable == null) throw new InvalidDataException("Not a valid .excalidrawlib file: libraryItems is not an array.");
            var items = new List<object>();
            foreach (object item in enumerable) items.Add(item);
            return items;
        }

        public static void Save(string path, IEnumerable<object> items)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var document = new Dictionary<string, object>
            {
                { "type", "excalidrawlib" }, { "version", 2 }, { "source", "ExcalidrawManager" },
                { "libraryItems", items.ToList() }
            };
            File.WriteAllText(path, Serializer().Serialize(document), new UTF8Encoding(false));
        }

        public static int MergeFrom(string importPath)
        {
            var existing = Load(SettingsStore.SharedLibraryPath);
            var incoming = Load(importPath);
            var merged = new Dictionary<string, object>(StringComparer.Ordinal);
            int anonymous = 0;
            foreach (object item in existing.Concat(incoming))
            {
                var fields = item as Dictionary<string, object>;
                object id;
                string key = fields != null && fields.TryGetValue("id", out id) && id != null
                    ? Convert.ToString(id) : "__anonymous_" + anonymous++ + "_" + Serializer().Serialize(item).GetHashCode();
                merged[key] = item;
            }
            Save(SettingsStore.SharedLibraryPath, merged.Values);
            return merged.Count;
        }

        public static string Describe(object item, int index)
        {
            var fields = item as Dictionary<string, object>;
            if (fields == null) return "Item " + (index + 1);
            object id, status, elements;
            string itemId = fields.TryGetValue("id", out id) ? Convert.ToString(id) : "item-" + (index + 1);
            string itemStatus = fields.TryGetValue("status", out status) ? Convert.ToString(status) : "unknown";
            int elementCount = 0;
            var elementList = fields.TryGetValue("elements", out elements) ? elements as IEnumerable : null;
            if (elementList != null) foreach (object ignored in elementList) elementCount++;
            return itemId + "   [" + itemStatus + ", " + elementCount + " elements]";
        }
    }

    public static class ProcessService
    {
        private static string _nodePath;
        private static string _cliPath;
        private static string _cliVersion;
        private static string _publicDir;
        private static string _managedServerPath;

        public static string NodePath { get { EnsureEnvironment(); return _nodePath; } }
        public static string CliPath { get { EnsureEnvironment(); return _cliPath; } }
        public static string CliVersion { get { EnsureEnvironment(); return _cliVersion; } }
        public static string ManagedServerPath { get { EnsureEnvironment(); return _managedServerPath; } }

        private static void EnsureEnvironment()
        {
            if (!string.IsNullOrEmpty(_nodePath) && !string.IsNullOrEmpty(_cliPath)) return;
            _nodePath = FindOnPath("node.exe");
            string command = FindOnPath("excalidraw-edit.cmd");
            if (!string.IsNullOrEmpty(command))
            {
                string candidate = Path.Combine(Path.GetDirectoryName(command), "node_modules", "excalidraw-edit", "src", "cli.js");
                if (File.Exists(candidate)) _cliPath = candidate;
            }
            if (!string.IsNullOrEmpty(_cliPath)) _publicDir = Path.Combine(Path.GetDirectoryName(_cliPath), "public");
            _managedServerPath = Path.Combine(Path.GetDirectoryName(typeof(ProcessService).Assembly.Location), "runtime", "server.mjs");
            if (string.IsNullOrEmpty(_nodePath) || string.IsNullOrEmpty(_cliPath) || !Directory.Exists(_publicDir))
                throw new InvalidOperationException("Cannot find node.exe or the global excalidraw-edit installation. Run: npm i -g excalidraw-edit");
            string packageJson = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_cliPath)), "package.json");
            Match versionMatch = File.Exists(packageJson) ? Regex.Match(File.ReadAllText(packageJson), "\"version\"\\s*:\\s*\"([^\"]+)\"") : Match.Empty;
            _cliVersion = versionMatch.Success ? versionMatch.Groups[1].Value : "unknown";
            if (!string.Equals(_cliVersion, "0.1.1", StringComparison.Ordinal))
                throw new InvalidOperationException("This release requires excalidraw-edit 0.1.1, but found " + _cliVersion + ". Run: npm i -g excalidraw-edit@0.1.1");
            if (!File.Exists(_managedServerPath))
                throw new InvalidOperationException("Managed Excalidraw runtime is missing: " + _managedServerPath);
        }

        private static string FindOnPath(string name)
        {
            foreach (string raw in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                string dir = raw.Trim().Trim('"');
                if (dir.Length == 0) continue;
                try { string candidate = Path.Combine(dir, name); if (File.Exists(candidate)) return candidate; }
                catch { }
            }
            return null;
        }

        public static int FindFreePort(int start)
        {
            var used = new HashSet<int>(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(x => x.Port));
            for (int port = Math.Max(1, start); port <= 65535; port++) if (!used.Contains(port)) return port;
            throw new InvalidOperationException("No free TCP port is available.");
        }

        public static DrawingInstance Start(string filePath, int requestedPort, string theme)
        {
            EnsureEnvironment();
            filePath = Path.GetFullPath(filePath);
            if (!File.Exists(filePath)) CreateEmptyScene(filePath);
            int port = requestedPort > 0 ? requestedPort : FindFreePort(6417);
            if (FindFreePort(port) != port) throw new InvalidOperationException("Port " + port + " is already in use.");

            var psi = new ProcessStartInfo(_nodePath,
                Quote(_managedServerPath) + " " + Quote(filePath) + " --port " + port + " --theme " + theme +
                " --public-dir " + Quote(_publicDir) + " --library " + Quote(SettingsStore.SharedLibraryPath));
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.WorkingDirectory = Path.GetDirectoryName(filePath);
            var process = Process.Start(psi);
            if (process == null) throw new InvalidOperationException("Failed to start excalidraw-edit.");

            var error = new StringBuilder();
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) error.AppendLine(e.Data); };
            process.BeginErrorReadLine();
            for (int i = 0; i < 50; i++)
            {
                if (process.HasExited) throw new InvalidOperationException("excalidraw-edit exited: " + error.ToString().Trim());
                if (IsPortListening(port)) break;
                Thread.Sleep(100);
            }
            if (!IsPortListening(port))
            {
                try { process.Kill(); } catch { }
                throw new TimeoutException("excalidraw-edit did not listen on port " + port + " within 5 seconds.");
            }

            var instance = new DrawingInstance
            {
                Pid = process.Id, Port = port, FilePath = filePath, Theme = theme,
                Source = "Managed", CommandLine = psi.FileName + " " + psi.Arguments,
                StartedAt = SafeStartTime(process)
            };
            ManagedRegistry.Add(instance);
            return instance;
        }

        public static void CreateEmptyScene(string filePath)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            const string scene = "{\r\n  \"type\": \"excalidraw\",\r\n  \"version\": 2,\r\n  \"elements\": [],\r\n  \"appState\": {},\r\n  \"files\": {}\r\n}\r\n";
            using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(scene);
        }

        public static List<DrawingInstance> Discover()
        {
            var list = new List<DrawingInstance>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine, CreationDate FROM Win32_Process WHERE Name='node.exe'"))
                {
                    foreach (ManagementObject row in searcher.Get())
                    {
                        string command = Convert.ToString(row["CommandLine"]);
                        bool standardRuntime = command.IndexOf("excalidraw-edit", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool managedRuntime = command.IndexOf("ExcalidrawManager", StringComparison.OrdinalIgnoreCase) >= 0 && command.IndexOf("server.mjs", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!standardRuntime && !managedRuntime) continue;
                        int pid = Convert.ToInt32((uint)row["ProcessId"]);
                        var args = SplitCommandLine(command);
                        int cliIndex = args.FindIndex(x =>
                            (x.IndexOf("excalidraw-edit", StringComparison.OrdinalIgnoreCase) >= 0 && x.EndsWith("cli.js", StringComparison.OrdinalIgnoreCase)) ||
                            (x.IndexOf("ExcalidrawManager", StringComparison.OrdinalIgnoreCase) >= 0 && x.EndsWith("server.mjs", StringComparison.OrdinalIgnoreCase)));
                        string file = "";
                        int port = 6417;
                        string theme = "system";
                        for (int i = Math.Max(0, cliIndex + 1); i < args.Count; i++)
                        {
                            if ((args[i] == "--port" || args[i] == "-p") && i + 1 < args.Count) { int.TryParse(args[++i], out port); continue; }
                            if ((args[i] == "--theme" || args[i] == "-t") && i + 1 < args.Count) { theme = args[++i]; continue; }
                            if ((args[i] == "--public-dir" || args[i] == "--library") && i + 1 < args.Count) { i++; continue; }
                            if (args[i] == "--no-open") continue;
                            if (!args[i].StartsWith("-")) file = args[i];
                        }
                        if (!string.IsNullOrEmpty(file) && Path.IsPathRooted(file)) file = Path.GetFullPath(file);
                        Process p = null;
                        try { p = Process.GetProcessById(pid); } catch { }
                        list.Add(new DrawingInstance
                        {
                            Pid = pid, Port = port, FilePath = file, Theme = theme,
                            Source = ManagedRegistry.Contains(pid, command) ? "Managed" : "External",
                            CommandLine = command, StartedAt = p == null ? null : SafeStartTime(p)
                        });
                    }
                }
            }
            catch { }
            ManagedRegistry.Prune(list.Select(x => x.Pid));
            return list.OrderBy(x => x.Port).ToList();
        }

        public static bool Stop(int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                if (!string.Equals(p.ProcessName, "node", StringComparison.OrdinalIgnoreCase)) return false;
                Thread.Sleep(750);
                p.Kill();
                p.WaitForExit(3000);
                ManagedRegistry.Remove(pid);
                return true;
            }
            catch { return false; }
        }

        public static int StopAllDiscovered()
        {
            int count = 0;
            foreach (var item in Discover()) if (Stop(item.Pid)) count++;
            return count;
        }

        private static bool IsPortListening(int port)
        {
            return IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(x => x.Port == port);
        }

        private static DateTime? SafeStartTime(Process process)
        {
            try { return process.StartTime; } catch { return null; }
        }

        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }

        private static List<string> SplitCommandLine(string command)
        {
            var result = new List<string>();
            foreach (Match m in Regex.Matches(command ?? "", "(?:\\\"(?<q>[^\\\"]*)\\\"|(?<u>\\S+))"))
                result.Add(m.Groups["q"].Success ? m.Groups["q"].Value : m.Groups["u"].Value);
            return result;
        }
    }

    public sealed class MainForm : Form
    {
        private readonly AppSettings _settings;
        private readonly string _initialFile;
        private readonly TreeView _tree = new TreeView();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _status = new Label();
        private readonly NotifyIcon _tray = new NotifyIcon();
        private readonly System.Windows.Forms.Timer _refreshTimer = new System.Windows.Forms.Timer();
        private readonly ToolStripTextBox _search = new ToolStripTextBox();
        private List<DrawingInstance> _instances = new List<DrawingInstance>();
        private bool _reallyExit;

        public MainForm(string initialFile)
        {
            _initialFile = initialFile;
            _settings = SettingsStore.Load();
            Text = "Excalidraw Manager";
            MinimumSize = new Size(980, 600);
            Size = new Size(1180, 720);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            BuildUi();
            BuildTray();
            Load += OnLoaded;
            FormClosing += OnFormClosing;
        }

        private void BuildUi()
        {
            var tool = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6, 3, 6, 3) };
            tool.Items.Add(MakeButton("Add root", delegate { AddRoot(); }));
            tool.Items.Add(MakeButton("Open file", delegate { OpenFile(); }));
            tool.Items.Add(MakeButton("New board", delegate { NewBoard(); }));
            tool.Items.Add(MakeButton("Refresh", delegate { RefreshAll(); }));
            tool.Items.Add(new ToolStripSeparator());
            tool.Items.Add(new ToolStripLabel("Search:"));
            _search.AutoSize = false;
            _search.Width = 180;
            _search.ToolTipText = "Search all workspace roots; press Enter";
            _search.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { SearchFiles(_search.Text); e.SuppressKeyPress = true; } };
            tool.Items.Add(_search);
            tool.Items.Add(new ToolStripSeparator());
            tool.Items.Add(MakeButton("Settings", delegate { ShowSettings(); }));
            tool.Items.Add(MakeButton("Libraries", delegate { ShowLibraryManager(); }));
            tool.Items.Add(MakeButton("Environment", delegate { ShowEnvironment(); }));
            Controls.Add(tool);

            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 360, Panel1MinSize = 260 };
            Shown += delegate
            {
                int desired = Math.Max(280, split.ClientSize.Width / 3);
                split.SplitterDistance = Math.Min(desired, Math.Max(split.Panel1MinSize, split.ClientSize.Width - 560));
            };
            split.Panel1.Padding = new Padding(8);
            split.Panel2.Padding = new Padding(8);
            Controls.Add(split);
            split.BringToFront();

            var leftTitle = new Label { Text = "WORKSPACES", Dock = DockStyle.Top, Height = 26, ForeColor = Color.DimGray };
            _tree.Dock = DockStyle.Fill;
            _tree.HideSelection = false;
            _tree.ShowNodeToolTips = true;
            _tree.BeforeExpand += TreeBeforeExpand;
            _tree.NodeMouseDoubleClick += TreeDoubleClick;
            _tree.NodeMouseClick += TreeMouseClick;
            split.Panel1.Controls.Add(_tree);
            split.Panel1.Controls.Add(leftTitle);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 7, 0, 0) };
            actions.Controls.Add(MakeUiButton("Open", delegate { OpenSelected(); }));
            actions.Controls.Add(MakeUiButton("Copy URL", delegate { CopySelectedUrl(); }));
            actions.Controls.Add(MakeUiButton("Restart", delegate { RestartSelected(); }));
            actions.Controls.Add(MakeUiButton("Stop", delegate { StopSelected(); }));
            actions.Controls.Add(MakeUiButton("Stop all", delegate { StopAll(); }));

            _status.Dock = DockStyle.Bottom;
            _status.Height = 24;
            _status.ForeColor = Color.DimGray;
            _status.TextAlign = ContentAlignment.MiddleLeft;

            ConfigureGrid();
            var rightTitle = new Label { Text = "RUNNING INSTANCES", Dock = DockStyle.Top, Height = 26, ForeColor = Color.DimGray };
            split.Panel2.Controls.Add(_grid);
            split.Panel2.Controls.Add(actions);
            split.Panel2.Controls.Add(_status);
            split.Panel2.Controls.Add(rightTitle);
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoGenerateColumns = false;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = SystemColors.Window;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "File", DataPropertyName = "DisplayName", Width = 165 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PID", DataPropertyName = "Pid", Width = 65 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Port", DataPropertyName = "Port", Width = 60 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "URL", DataPropertyName = "Url", Width = 145 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source", DataPropertyName = "Source", Width = 75 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Theme", DataPropertyName = "Theme", Width = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Started", DataPropertyName = "StartedAt", Width = 125, DefaultCellStyle = new DataGridViewCellStyle { Format = "MM-dd HH:mm:ss" } });
            _grid.CellDoubleClick += delegate { OpenSelected(); };
            _grid.CellFormatting += delegate(object sender, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
                var row = _grid.Rows[e.RowIndex];
                var item = row.DataBoundItem as DrawingInstance;
                if (item == null || row.Cells.Count == 0) return;
                row.Cells[0].ToolTipText = item.FilePath + "\r\n" + item.CommandLine;
            };
        }

        private void BuildTray()
        {
            _tray.Icon = Icon ?? SystemIcons.Application;
            _tray.Text = "Excalidraw Manager";
            _tray.Visible = true;
            _tray.DoubleClick += delegate { RestoreWindow(); };
            var menu = new ContextMenuStrip();
            menu.Items.Add("Show", null, delegate { RestoreWindow(); });
            menu.Items.Add("Stop all services", null, delegate { StopAll(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit...", null, delegate { ExitFromTray(); });
            _tray.ContextMenuStrip = menu;
        }

        private void OnLoaded(object sender, EventArgs e)
        {
            LoadRoots();
            RefreshInstances();
            _refreshTimer.Interval = 5000;
            _refreshTimer.Tick += delegate { RefreshInstances(); };
            _refreshTimer.Start();
            if (!string.IsNullOrEmpty(_initialFile)) StartFileAsync(_initialFile, false, 0);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_reallyExit) return;
            e.Cancel = true;
            Hide();
            if (!_settings.TrayHintShown)
            {
                _tray.ShowBalloonTip(2500, "Excalidraw Manager", "Still running in the system tray. Use Exit there to close it.", ToolTipIcon.Info);
                _settings.TrayHintShown = true;
                SettingsStore.Save(_settings);
            }
        }

        private void ExitFromTray()
        {
            var answer = MessageBox.Show(
                "Yes: stop all excalidraw-edit services and exit.\r\nNo: keep services running and exit.\r\nCancel: return to the tray.",
                "Exit Excalidraw Manager", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) return;
            if (answer == DialogResult.Yes) ProcessService.StopAllDiscovered();
            _reallyExit = true;
            _tray.Visible = false;
            Application.Exit();
        }

        private void RestoreWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }

        public void HandleActivation(string message)
        {
            if (message == "__STOP__") return;
            RestoreWindow();
            if (!string.IsNullOrEmpty(message) && message != "__SHOW__" && File.Exists(message))
            {
                RememberRecent(message);
                StartFileAsync(Path.GetFullPath(message), true, 0);
            }
        }

        private void LoadRoots()
        {
            _tree.Nodes.Clear();
            var recent = new TreeNode("Recent boards") { Name = "__recent" };
            foreach (string file in _settings.RecentFiles.Where(File.Exists).Take(20)) recent.Nodes.Add(MakeFileNode(file));
            if (recent.Nodes.Count > 0) { _tree.Nodes.Add(recent); recent.Expand(); }
            foreach (string root in _settings.Roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase)) AddRootNode(root);
        }

        private void AddRootNode(string root)
        {
            var node = new TreeNode(Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar)) + "  [" + root + "]") { Tag = root, ToolTipText = root };
            node.Nodes.Add(new TreeNode("Loading..."));
            _tree.Nodes.Add(node);
        }

        private void PopulateDirectory(TreeNode node)
        {
            string path = node.Tag as string;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            node.Nodes.Clear();
            try
            {
                foreach (string dir in Directory.GetDirectories(path).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    var child = new TreeNode(Path.GetFileName(dir)) { Tag = dir, ToolTipText = dir };
                    child.Nodes.Add(new TreeNode("Loading..."));
                    node.Nodes.Add(child);
                }
                foreach (string file in Directory.GetFiles(path, "*.excalidraw").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    node.Nodes.Add(MakeFileNode(file));
                }
            }
            catch (Exception ex) { node.Nodes.Add(new TreeNode("Access failed: " + ex.Message)); }
        }

        private void TreeBeforeExpand(object sender, TreeViewCancelEventArgs e) { PopulateDirectory(e.Node); }

        private void TreeDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            string path = e.Node.Tag as string;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var existing = _instances.FirstOrDefault(x => SamePath(x.FilePath, path));
                if (existing != null) OpenUrl(existing.Url); else StartFileAsync(path, true, 0);
            }
        }

        private void TreeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            _tree.SelectedNode = e.Node;
            string path = e.Node.Tag as string;
            if (string.IsNullOrEmpty(path)) return;
            var menu = new ContextMenuStrip();
            if (Directory.Exists(path))
            {
                menu.Items.Add("New board here", null, delegate { NewBoard(path); });
                menu.Items.Add("New folder...", null, delegate { NewFolder(path); });
                menu.Items.Add("Refresh", null, delegate { PopulateDirectory(e.Node); });
                menu.Items.Add("Open in Explorer", null, delegate { OpenExplorer(path); });
                if (_settings.Roots.Any(x => SamePath(x, path))) menu.Items.Add("Remove root", null, delegate { RemoveRoot(path); });
            }
            else if (File.Exists(path))
            {
                menu.Items.Add("Start / Open", null, delegate { TreeDoubleClick(sender, e); });
                menu.Items.Add("Start with port...", null, delegate { StartWithPort(path); });
                menu.Items.Add("Force new instance", null, delegate { StartFileAsync(path, true, 0, true); });
                menu.Items.Add("Set alias...", null, delegate { SetAlias(path); });
                menu.Items.Add("Rename...", null, delegate { RenameBoard(path); });
                menu.Items.Add("Copy path", null, delegate { Clipboard.SetText(path); });
                menu.Items.Add("Show in Explorer", null, delegate { Process.Start("explorer.exe", "/select,\"" + path + "\""); });
            }
            menu.Show(_tree, e.Location);
        }

        private void AddRoot()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Choose a folder containing .excalidraw boards", ShowNewFolderButton = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!_settings.Roots.Any(x => SamePath(x, dialog.SelectedPath))) _settings.Roots.Add(dialog.SelectedPath);
                SettingsStore.Save(_settings);
                LoadRoots();
            }
        }

        private void OpenFile()
        {
            using (var dialog = new OpenFileDialog { Title = "Open Excalidraw board", Filter = "Excalidraw board (*.excalidraw)|*.excalidraw", Multiselect = false })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                RememberRecent(dialog.FileName);
                StartFileAsync(dialog.FileName, true, 0);
            }
        }

        private void RemoveRoot(string root)
        {
            _settings.Roots.RemoveAll(x => SamePath(x, root));
            SettingsStore.Save(_settings);
            LoadRoots();
        }

        private string SelectedDirectory()
        {
            string path = _tree.SelectedNode == null ? null : _tree.SelectedNode.Tag as string;
            if (File.Exists(path)) return Path.GetDirectoryName(path);
            if (Directory.Exists(path)) return path;
            return _settings.Roots.FirstOrDefault(Directory.Exists);
        }

        private void NewBoard() { NewBoard(SelectedDirectory()); }

        private void NewBoard(string directory)
        {
            using (var dialog = new SaveFileDialog
            {
                Title = "Create Excalidraw board", Filter = "Excalidraw board (*.excalidraw)|*.excalidraw",
                AddExtension = true, DefaultExt = "excalidraw", InitialDirectory = directory
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ProcessService.CreateEmptyScene(dialog.FileName);
                    RememberRecent(dialog.FileName);
                    RefreshAll();
                    if (MessageBox.Show("Board created. Start it now?", "New board", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        StartFileAsync(dialog.FileName, true, 0);
                }
                catch (IOException) { MessageBox.Show("The file already exists.", "New board", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                catch (Exception ex) { ShowError(ex); }
            }
        }

        private void NewFolder(string parent)
        {
            string name = Prompt.Show("Folder name:", "New folder", "New Folder");
            if (string.IsNullOrWhiteSpace(name)) return;
            try { Directory.CreateDirectory(Path.Combine(parent, name.Trim())); RefreshAll(); }
            catch (Exception ex) { ShowError(ex); }
        }

        private TreeNode MakeFileNode(string file)
        {
            bool running = _instances.Any(x => SamePath(x.FilePath, file));
            string alias;
            string label = _settings.Aliases.TryGetValue(Path.GetFullPath(file), out alias) && !string.IsNullOrWhiteSpace(alias)
                ? alias + "  (" + Path.GetFileName(file) + ")" : Path.GetFileName(file);
            var child = new TreeNode((running ? "● " : "○ ") + label) { Tag = file, ToolTipText = file };
            child.ForeColor = running ? Color.SeaGreen : SystemColors.WindowText;
            return child;
        }

        private void RememberRecent(string file)
        {
            string full = Path.GetFullPath(file);
            _settings.RecentFiles.RemoveAll(x => SamePath(x, full));
            _settings.RecentFiles.Insert(0, full);
            if (_settings.RecentFiles.Count > 30) _settings.RecentFiles.RemoveRange(30, _settings.RecentFiles.Count - 30);
            SettingsStore.Save(_settings);
        }

        private void SetAlias(string path)
        {
            string full = Path.GetFullPath(path);
            string existing;
            _settings.Aliases.TryGetValue(full, out existing);
            string alias = Prompt.Show("Display alias (leave blank to clear):", "Board alias", existing ?? "");
            if (alias == null) return;
            if (string.IsNullOrWhiteSpace(alias)) _settings.Aliases.Remove(full); else _settings.Aliases[full] = alias.Trim();
            SettingsStore.Save(_settings); RefreshAll();
        }

        private void RenameBoard(string path)
        {
            if (_instances.Any(x => SamePath(x.FilePath, path)))
            {
                MessageBox.Show("Stop this board before renaming it.", "Rename", MessageBoxButtons.OK, MessageBoxIcon.Information); return;
            }
            string name = Prompt.Show("New file name:", "Rename board", Path.GetFileName(path));
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!name.EndsWith(".excalidraw", StringComparison.OrdinalIgnoreCase)) name += ".excalidraw";
            string target = Path.Combine(Path.GetDirectoryName(path), name);
            try
            {
                File.Move(path, target);
                RememberRecent(target);
                _settings.RecentFiles.RemoveAll(x => SamePath(x, path));
                string alias;
                if (_settings.Aliases.TryGetValue(Path.GetFullPath(path), out alias)) { _settings.Aliases.Remove(Path.GetFullPath(path)); _settings.Aliases[Path.GetFullPath(target)] = alias; }
                SettingsStore.Save(_settings); RefreshAll();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void SearchFiles(string query)
        {
            query = (query ?? "").Trim();
            if (query.Length == 0) { LoadRoots(); return; }
            Cursor = Cursors.WaitCursor;
            try
            {
                var results = new List<string>();
                foreach (string root in _settings.Roots.Where(Directory.Exists)) FindFilesRecursive(root, query, results, 500);
                _tree.Nodes.Clear();
                var node = new TreeNode("Search results: " + query + "  (" + results.Count + ")");
                foreach (string file in results) node.Nodes.Add(MakeFileNode(file));
                _tree.Nodes.Add(node); node.Expand();
            }
            finally { Cursor = Cursors.Default; }
        }

        private static void FindFilesRecursive(string directory, string query, List<string> results, int limit)
        {
            if (results.Count >= limit) return;
            try
            {
                foreach (string file in Directory.GetFiles(directory, "*.excalidraw"))
                {
                    if (Path.GetFileName(file).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) results.Add(file);
                    if (results.Count >= limit) return;
                }
                foreach (string child in Directory.GetDirectories(directory)) { FindFilesRecursive(child, query, results, limit); if (results.Count >= limit) return; }
            }
            catch { }
        }

        private void StartWithPort(string path)
        {
            string text = Prompt.Show("TCP port:", "Start with port", ProcessService.FindFreePort(_settings.StartPort).ToString());
            int port;
            if (int.TryParse(text, out port) && port > 0 && port <= 65535) StartFileAsync(path, true, port, true);
        }

        private async void StartFileAsync(string path, bool open, int port, bool force)
        {
            try
            {
                var existing = _instances.FirstOrDefault(x => SamePath(x.FilePath, path));
                if (existing != null && !force) { if (open) OpenUrl(existing.Url); return; }
                _status.Text = "Starting " + Path.GetFileName(path) + "...";
                int target = port > 0 ? port : ProcessService.FindFreePort(_settings.StartPort);
                var instance = await Task.Run(() => ProcessService.Start(path, target, _settings.Theme));
                RememberRecent(path);
                RefreshInstances();
                _status.Text = "Started PID " + instance.Pid + " on port " + instance.Port;
                if (open) OpenUrl(instance.Url);
            }
            catch (Exception ex) { ShowError(ex); _status.Text = "Start failed"; }
        }

        private void StartFileAsync(string path, bool open, int port) { StartFileAsync(path, open, port, false); }

        private DrawingInstance SelectedInstance()
        {
            return _grid.CurrentRow == null ? null : _grid.CurrentRow.DataBoundItem as DrawingInstance;
        }

        private void OpenSelected() { var item = SelectedInstance(); if (item != null) OpenUrl(item.Url); }
        private void CopySelectedUrl() { var item = SelectedInstance(); if (item != null && !string.IsNullOrEmpty(item.Url)) Clipboard.SetText(item.Url); }

        private async void StopSelected()
        {
            var item = SelectedInstance();
            if (item == null) return;
            _status.Text = "Stopping PID " + item.Pid + " (brief save grace period)...";
            await Task.Run(() => ProcessService.Stop(item.Pid));
            RefreshAll();
        }

        private async void StopAll()
        {
            if (_instances.Count == 0) return;
            if (MessageBox.Show("Stop all discovered excalidraw-edit Node processes?", "Stop all", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            _status.Text = "Stopping all services...";
            int count = await Task.Run(() => ProcessService.StopAllDiscovered());
            _status.Text = "Stopped " + count + " service(s)";
            RefreshAll();
        }

        private async void RestartSelected()
        {
            var item = SelectedInstance();
            if (item == null || string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath)) return;
            string file = item.FilePath; int port = item.Port; string theme = item.Theme;
            _status.Text = "Restarting PID " + item.Pid + "...";
            await Task.Run(() => ProcessService.Stop(item.Pid));
            try { await Task.Run(() => ProcessService.Start(file, port, theme)); RefreshAll(); OpenUrl("http://localhost:" + port); }
            catch (Exception ex) { ShowError(ex); RefreshAll(); }
        }

        private void RefreshAll() { RefreshInstances(); RefreshExpandedNodes(_tree.Nodes); }

        private void RefreshExpandedNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes) if (node.IsExpanded) { PopulateDirectory(node); RefreshExpandedNodes(node.Nodes); }
        }

        private void RefreshInstances()
        {
            try
            {
                int selectedPid = SelectedInstance() == null ? -1 : SelectedInstance().Pid;
                _instances = ProcessService.Discover();
                ResolveExternalPaths();
                foreach (var item in _instances)
                {
                    string alias;
                    if (!string.IsNullOrEmpty(item.FilePath) && Path.IsPathRooted(item.FilePath) && _settings.Aliases.TryGetValue(Path.GetFullPath(item.FilePath), out alias)) item.Alias = alias;
                }
                _grid.DataSource = null;
                _grid.DataSource = _instances;
                foreach (DataGridViewRow row in _grid.Rows) if (((DrawingInstance)row.DataBoundItem).Pid == selectedPid) { row.Selected = true; _grid.CurrentCell = row.Cells[0]; }
                _status.Text = _instances.Count + " running instance(s)";
                RebuildTrayInstances();
            }
            catch (Exception ex) { _status.Text = "Refresh failed: " + ex.Message; }
        }

        private void ResolveExternalPaths()
        {
            foreach (var item in _instances.Where(x => !string.IsNullOrEmpty(x.FilePath) && !Path.IsPathRooted(x.FilePath)))
            {
                string name = Path.GetFileName(item.FilePath);
                var matches = new List<string>();
                foreach (string root in _settings.Roots.Where(Directory.Exists)) FindExactFilesRecursive(root, name, matches, 2);
                if (matches.Count == 1) item.FilePath = matches[0];
            }
        }

        private static void FindExactFilesRecursive(string directory, string name, List<string> results, int limit)
        {
            if (results.Count >= limit) return;
            try
            {
                string direct = Path.Combine(directory, name);
                if (File.Exists(direct)) results.Add(direct);
                if (results.Count >= limit) return;
                foreach (string child in Directory.GetDirectories(directory)) { FindExactFilesRecursive(child, name, results, limit); if (results.Count >= limit) return; }
            }
            catch { }
        }

        private void RebuildTrayInstances()
        {
            var menu = _tray.ContextMenuStrip;
            while (menu.Items.Count > 4) menu.Items.RemoveAt(1);
            int at = 1;
            foreach (var instance in _instances.Take(12))
            {
                var captured = instance;
                menu.Items.Insert(at++, new ToolStripMenuItem("Open " + instance.FileName + "  :" + instance.Port, null, delegate { OpenUrl(captured.Url); }));
            }
        }

        private void ShowEnvironment()
        {
            try
            {
                string nodeVersion = RunAndRead(ProcessService.NodePath, "--version").Trim();
                MessageBox.Show("node.exe\r\n" + ProcessService.NodePath + "\r\nVersion: " + nodeVersion + "\r\n\r\nexcalidraw-edit\r\n" + ProcessService.CliPath + "\r\nVersion: " + ProcessService.CliVersion + "\r\n\r\nManaged runtime\r\n" + ProcessService.ManagedServerPath + "\r\n\r\nShared library\r\n" + SettingsStore.SharedLibraryPath + "\r\n\r\nSettings\r\n" + SettingsStore.FilePath,
                    "Environment", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ShowLibraryManager()
        {
            using (var form = new Form { Width = 760, Height = 530, Text = "Excalidraw Libraries", StartPosition = FormStartPosition.CenterParent, MinimumSize = new Size(620, 420) })
            {
                try { form.Icon = Icon; } catch { }
                var title = new Label { Dock = DockStyle.Top, Height = 28, Text = "SHARED LOCAL LIBRARY", ForeColor = Color.DimGray };
                var path = new TextBox { Dock = DockStyle.Top, Height = 24, ReadOnly = true, Text = SettingsStore.SharedLibraryPath };
                var summary = new Label { Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft };
                var list = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 78, Padding = new Padding(0, 8, 0, 0), AutoScroll = true };
                DateTime lastLibraryWrite = DateTime.MinValue;

                Action reload = delegate
                {
                    try
                    {
                        var items = LibraryStore.Load(SettingsStore.SharedLibraryPath);
                        lastLibraryWrite = File.Exists(SettingsStore.SharedLibraryPath) ? File.GetLastWriteTimeUtc(SettingsStore.SharedLibraryPath) : DateTime.MinValue;
                        list.Items.Clear();
                        for (int i = 0; i < items.Count; i++) list.Items.Add(LibraryStore.Describe(items[i], i));
                        summary.Text = items.Count + " library item(s). Changes made in managed boards are saved here automatically.";
                    }
                    catch (Exception ex) { summary.Text = "Load failed: " + ex.Message; }
                };

                var import = MakeUiButton("Import / Merge...", delegate
                {
                    using (var dialog = new OpenFileDialog { Filter = "Excalidraw library (*.excalidrawlib)|*.excalidrawlib|JSON (*.json)|*.json", Multiselect = false })
                    {
                        if (dialog.ShowDialog(form) != DialogResult.OK) return;
                        try
                        {
                            int count = LibraryStore.MergeFrom(dialog.FileName); reload();
                            MessageBox.Show(count + " item(s) are now stored locally. Refresh already-open board tabs to load the updated library.", "Library imported", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex) { ShowError(ex); }
                    }
                });
                var replace = MakeUiButton("Replace...", delegate
                {
                    using (var dialog = new OpenFileDialog { Filter = "Excalidraw library (*.excalidrawlib)|*.excalidrawlib|JSON (*.json)|*.json", Multiselect = false })
                    {
                        if (dialog.ShowDialog(form) != DialogResult.OK) return;
                        if (MessageBox.Show("Replace the complete shared local library?", "Replace library", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                        try { LibraryStore.Save(SettingsStore.SharedLibraryPath, LibraryStore.Load(dialog.FileName)); reload(); }
                        catch (Exception ex) { ShowError(ex); }
                    }
                });
                var export = MakeUiButton("Export...", delegate
                {
                    using (var dialog = new SaveFileDialog { Filter = "Excalidraw library (*.excalidrawlib)|*.excalidrawlib", AddExtension = true, DefaultExt = "excalidrawlib", FileName = "shared.excalidrawlib" })
                    {
                        if (dialog.ShowDialog(form) != DialogResult.OK) return;
                        try
                        {
                            if (!File.Exists(SettingsStore.SharedLibraryPath)) LibraryStore.Save(SettingsStore.SharedLibraryPath, new object[0]);
                            File.Copy(SettingsStore.SharedLibraryPath, dialog.FileName, true);
                        }
                        catch (Exception ex) { ShowError(ex); }
                    }
                });
                var browse = MakeUiButton("Browse via active board", delegate
                {
                    var active = SelectedInstance() ?? _instances.FirstOrDefault();
                    if (active == null || string.IsNullOrEmpty(active.Url))
                    {
                        MessageBox.Show("Start a board first. Public libraries must be opened from a running board so Excalidraw can generate the secure return token.", "Browse libraries", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    OpenUrl(active.Url);
                    MessageBox.Show("In the board, open the Library panel and click 'Browse libraries'.\r\nChoose a library on the website, then click 'Add to Excalidraw'. It will return to this local board and be saved automatically.", "Install a public library", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
                var folder = MakeUiButton("Open local folder", delegate { Directory.CreateDirectory(SettingsStore.LibraryDirectory); OpenExplorer(SettingsStore.LibraryDirectory); });
                var clear = MakeUiButton("Clear", delegate
                {
                    if (MessageBox.Show("Clear every item from the shared local library?\r\nOpen board tabs must be refreshed afterward.", "Clear library", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    try { LibraryStore.Save(SettingsStore.SharedLibraryPath, new object[0]); reload(); }
                    catch (Exception ex) { ShowError(ex); }
                });
                buttons.Controls.AddRange(new Control[] { import, replace, export, browse, folder, clear });
                form.Controls.Add(list); form.Controls.Add(summary); form.Controls.Add(path); form.Controls.Add(title); form.Controls.Add(buttons);
                var watcher = new System.Windows.Forms.Timer { Interval = 1000 };
                watcher.Tick += delegate
                {
                    DateTime current = File.Exists(SettingsStore.SharedLibraryPath) ? File.GetLastWriteTimeUtc(SettingsStore.SharedLibraryPath) : DateTime.MinValue;
                    if (current != lastLibraryWrite) reload();
                };
                form.FormClosed += delegate { watcher.Stop(); watcher.Dispose(); };
                reload(); watcher.Start(); form.ShowDialog(this);
            }
        }

        private void ShowSettings()
        {
            using (var form = new Form { Width = 390, Height = 220, Text = "Settings", StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false })
            {
                var portLabel = new Label { Left = 18, Top = 22, Width = 130, Text = "Starting port:" };
                var port = new NumericUpDown { Left = 155, Top = 18, Width = 190, Minimum = 1, Maximum = 65535, Value = Math.Max(1, Math.Min(65535, _settings.StartPort)) };
                var themeLabel = new Label { Left = 18, Top = 62, Width = 130, Text = "Board theme:" };
                var theme = new ComboBox { Left = 155, Top = 58, Width = 190, DropDownStyle = ComboBoxStyle.DropDownList };
                theme.Items.AddRange(new object[] { "system", "dark", "light" }); theme.SelectedItem = _settings.Theme;
                var ok = new Button { Text = "Save", Left = 190, Top = 115, Width = 75, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", Left = 272, Top = 115, Width = 75, DialogResult = DialogResult.Cancel };
                form.Controls.AddRange(new Control[] { portLabel, port, themeLabel, theme, ok, cancel }); form.AcceptButton = ok; form.CancelButton = cancel;
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    _settings.StartPort = (int)port.Value; _settings.Theme = Convert.ToString(theme.SelectedItem) ?? "system"; SettingsStore.Save(_settings);
                }
            }
        }

        private static string RunAndRead(string file, string args)
        {
            var p = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
            string output = p.StandardOutput.ReadToEnd(); p.WaitForExit(); return output;
        }

        private static void OpenUrl(string url) { if (!string.IsNullOrEmpty(url)) Process.Start(url); }
        private static void OpenExplorer(string path) { Process.Start("explorer.exe", "\"" + path + "\""); }
        private static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }
        private static void ShowError(Exception ex) { MessageBox.Show(ex.Message, "Excalidraw Manager", MessageBoxButtons.OK, MessageBoxIcon.Error); }

        private static ToolStripButton MakeButton(string text, EventHandler click) { var b = new ToolStripButton(text); b.Click += click; return b; }
        private static Button MakeUiButton(string text, EventHandler click) { var b = new Button { Text = text, AutoSize = true, Height = 28 }; b.Click += click; return b; }
    }

    public static class Prompt
    {
        public static string Show(string text, string caption, string value)
        {
            using (var form = new Form { Width = 440, Height = 155, Text = caption, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false })
            {
                var label = new Label { Left = 12, Top = 12, Width = 395, Text = text };
                var input = new TextBox { Left = 12, Top = 36, Width = 395, Text = value };
                var ok = new Button { Text = "OK", Left = 250, Width = 75, Top = 72, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", Left = 332, Width = 75, Top = 72, DialogResult = DialogResult.Cancel };
                form.Controls.AddRange(new Control[] { label, input, ok, cancel });
                form.AcceptButton = ok; form.CancelButton = cancel;
                return form.ShowDialog() == DialogResult.OK ? input.Text : null;
            }
        }
    }
}
