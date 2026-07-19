using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Management;
using Microsoft.Win32;
using System.Net;
using System.Net.NetworkInformation;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
[assembly: AssemblyVersion("0.4.0.0")]
[assembly: AssemblyFileVersion("0.4.0.0")]

namespace ExcalidrawManager
{
    internal static class Program
    {
        private const string MutexName = "Local\\ExcalidrawManager.SingleInstance";
        private const string PipeName = "ExcalidrawManager.SingleInstance.Pipe";

        [STAThread]
        private static void Main(string[] args)
        {
            Localization.Configure("system");
            if (args.Length > 0 && string.Equals(args[0], "stop-all", StringComparison.OrdinalIgnoreCase))
            {
                int count = ProcessService.StopAllDiscovered();
                Console.WriteLine(Localization.F("Stopped {0} excalidraw-edit process(es).", count));
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
            bool restartRequested = false;
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
                restartRequested = form.RestartRequested;
            }
            if (restartRequested) Process.Start(Application.ExecutablePath);
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

    public sealed class WorkspaceGroup
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ParentId { get; set; }

        public WorkspaceGroup()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "New group";
        }
    }

    public sealed class AppSettings
    {
        public List<string> Roots { get; set; }
        public int StartPort { get; set; }
        public string Theme { get; set; }
        public string Language { get; set; }
        public bool TrayHintShown { get; set; }
        public bool FormulaOcrEnabled { get; set; }
        public string FormulaOcrRoot { get; set; }
        public List<string> RecentFiles { get; set; }
        public Dictionary<string, string> Aliases { get; set; }
        public List<WorkspaceGroup> WorkspaceGroups { get; set; }
        public Dictionary<string, string> WorkspaceRootGroups { get; set; }
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public bool WindowMaximized { get; set; }

        public AppSettings()
        {
            Roots = new List<string>();
            StartPort = 6417;
            Theme = "system";
            Language = "system";
            FormulaOcrEnabled = true;
            FormulaOcrRoot = Environment.GetEnvironmentVariable("EXCALIDRAW_MANAGER_FORMULA_OCR_ROOT") ?? "";
            RecentFiles = new List<string>();
            Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            WorkspaceGroups = new List<WorkspaceGroup>();
            WorkspaceRootGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
                result.Aliases = CopyCaseInsensitive(result.Aliases);
                if (result.WorkspaceGroups == null) result.WorkspaceGroups = new List<WorkspaceGroup>();
                result.WorkspaceRootGroups = CopyCaseInsensitive(result.WorkspaceRootGroups);
                if (result.FormulaOcrRoot == null) result.FormulaOcrRoot = "";
                result.Language = Localization.NormalizePreference(result.Language);
                NormalizeWorkspaceGroups(result);
                return result;
            }
            catch { return new AppSettings(); }
        }

        public static void Save(AppSettings settings)
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(settings), Encoding.UTF8);
        }

        private static Dictionary<string, string> CopyCaseInsensitive(Dictionary<string, string> source)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (source == null) return result;
            foreach (var item in source)
                if (!string.IsNullOrWhiteSpace(item.Key)) result[item.Key] = item.Value;
            return result;
        }

        private static void NormalizeWorkspaceGroups(AppSettings settings)
        {
            var valid = new Dictionary<string, WorkspaceGroup>(StringComparer.OrdinalIgnoreCase);
            var normalized = new List<WorkspaceGroup>();
            foreach (var group in settings.WorkspaceGroups.Where(x => x != null))
            {
                group.Id = (group.Id ?? "").Trim();
                group.Name = (group.Name ?? "").Trim();
                group.ParentId = string.IsNullOrWhiteSpace(group.ParentId) ? null : group.ParentId.Trim();
                if (group.Id.Length == 0 || valid.ContainsKey(group.Id)) group.Id = Guid.NewGuid().ToString("N");
                if (group.Name.Length == 0) group.Name = Localization.T("New group");
                valid[group.Id] = group;
                normalized.Add(group);
            }

            // Repair direct orphan/self references first. Cycle detection must be
            // a separate pass so a valid child is not promoted merely because
            // its parent (visited later) still points at a missing grandparent.
            foreach (var group in valid.Values)
                if (group.ParentId != null && (!valid.ContainsKey(group.ParentId) ||
                    string.Equals(group.Id, group.ParentId, StringComparison.OrdinalIgnoreCase))) group.ParentId = null;

            foreach (var group in valid.Values)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { group.Id };
                WorkspaceGroup current = group;
                while (current.ParentId != null)
                {
                    WorkspaceGroup parent;
                    if (!valid.TryGetValue(current.ParentId, out parent))
                    {
                        current.ParentId = null;
                        break;
                    }
                    if (!seen.Add(parent.Id))
                    {
                        // Break the edge that closes the cycle. This preserves
                        // valid descendants that merely point into the cycle.
                        current.ParentId = null;
                        break;
                    }
                    current = parent;
                }
            }
            settings.WorkspaceGroups = normalized;

            var validIds = new HashSet<string>(valid.Keys, StringComparer.OrdinalIgnoreCase);
            var assignments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var assignment in settings.WorkspaceRootGroups)
            {
                string path = assignment.Key;
                if (string.IsNullOrWhiteSpace(path)) continue;
                try { path = Path.GetFullPath(path); } catch { }
                if (!string.IsNullOrWhiteSpace(assignment.Value) && validIds.Contains(assignment.Value))
                    assignments[path] = assignment.Value;
            }
            settings.WorkspaceRootGroups = assignments;
        }
    }

    public sealed class DrawingInstance
    {
        public int Pid { get; set; }
        public int Port { get; set; }
        public string Url { get { return Port > 0 ? "http://localhost:" + Port : ""; } }
        public string FilePath { get; set; }
        public string FileName { get { return string.IsNullOrEmpty(FilePath) ? Localization.T("(unknown)") : Path.GetFileName(FilePath); } }
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
                throw new InvalidDataException(Localization.T("Not a valid .excalidrawlib file: libraryItems is missing."));
            var enumerable = raw as IEnumerable;
            if (enumerable == null) throw new InvalidDataException(Localization.T("Not a valid .excalidrawlib file: libraryItems is not an array."));
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
            if (fields == null) return Localization.F("Item {0}", index + 1);
            object id, status, elements;
            string itemId = fields.TryGetValue("id", out id) ? Convert.ToString(id) : "item-" + (index + 1);
            string itemStatus = fields.TryGetValue("status", out status) ? Convert.ToString(status) : "unknown";
            int elementCount = 0;
            var elementList = fields.TryGetValue("elements", out elements) ? elements as IEnumerable : null;
            if (elementList != null) foreach (object ignored in elementList) elementCount++;
            return itemId + "   [" + Localization.T(itemStatus) + ", " + Localization.F("{0} elements", elementCount) + "]";
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
                throw new InvalidOperationException(Localization.T("Cannot find node.exe or the global excalidraw-edit installation. Run: npm i -g excalidraw-edit"));
            string packageJson = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_cliPath)), "package.json");
            Match versionMatch = File.Exists(packageJson) ? Regex.Match(File.ReadAllText(packageJson), "\"version\"\\s*:\\s*\"([^\"]+)\"") : Match.Empty;
            _cliVersion = versionMatch.Success ? versionMatch.Groups[1].Value : "unknown";
            if (!string.Equals(_cliVersion, "0.1.1", StringComparison.Ordinal))
                throw new InvalidOperationException(Localization.F("This release requires excalidraw-edit 0.1.1, but found {0}. Run: npm i -g excalidraw-edit@0.1.1", _cliVersion));
            if (!File.Exists(_managedServerPath))
                throw new InvalidOperationException(Localization.F("Managed Excalidraw runtime is missing: {0}", _managedServerPath));
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
            return FindFreePort(start, 0);
        }

        public static int FindFreePort(int start, int excludedPort)
        {
            var used = new HashSet<int>(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(x => x.Port));
            for (int port = Math.Max(1, start); port <= 65535; port++)
                if (port != excludedPort && !used.Contains(port)) return port;
            throw new InvalidOperationException(Localization.T("No free TCP port is available."));
        }

        public static DrawingInstance Start(string filePath, int requestedPort, string theme)
        {
            EnsureEnvironment();
            filePath = Path.GetFullPath(filePath);
            if (!File.Exists(filePath)) CreateEmptyScene(filePath);
            int port = requestedPort > 0 ? requestedPort : FindFreePort(6417);
            if (FindFreePort(port) != port) throw new InvalidOperationException(Localization.F("Port {0} is already in use.", port));

            string formulaEditorUrl = null;
            try
            {
                formulaEditorUrl = FormulaEditorService.EnsureStarted(
                    Localization.Language,
                    "http://localhost:" + port + "/");
            }
            catch
            {
                // Formula editing is an optional companion. A provider or editor startup
                // failure must never prevent an existing board from opening.
            }

            string serverArguments =
                Quote(_managedServerPath) + " " + Quote(filePath) + " --port " + port + " --theme " + theme +
                " --public-dir " + Quote(_publicDir) + " --library " + Quote(SettingsStore.SharedLibraryPath);
            if (!string.IsNullOrEmpty(formulaEditorUrl))
                serverArguments += " --formula-url " + Quote(formulaEditorUrl);
            var psi = new ProcessStartInfo(_nodePath, serverArguments);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.WorkingDirectory = Path.GetDirectoryName(filePath);
            var process = Process.Start(psi);
            if (process == null) throw new InvalidOperationException(Localization.T("Failed to start excalidraw-edit."));

            var error = new StringBuilder();
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) error.AppendLine(e.Data); };
            process.BeginErrorReadLine();
            for (int i = 0; i < 50; i++)
            {
                if (process.HasExited) throw new InvalidOperationException(Localization.F("excalidraw-edit exited: {0}", error.ToString().Trim()));
                if (IsPortListening(port)) break;
                Thread.Sleep(100);
            }
            if (!IsPortListening(port))
            {
                try { process.Kill(); } catch { }
                throw new TimeoutException(Localization.F("excalidraw-edit did not listen on port {0} within 5 seconds.", port));
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
                        bool managedRuntime = command.IndexOf("ExcalidrawManager", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            command.IndexOf("server.mjs", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            command.IndexOf("formula-server.mjs", StringComparison.OrdinalIgnoreCase) < 0;
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
                            if ((args[i] == "--public-dir" || args[i] == "--library" || args[i] == "--formula-url") && i + 1 < args.Count) { i++; continue; }
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

    public sealed class ManagedFormulaOcrSession
    {
        public string ConfigPath { get; set; }
        public string Token { get; set; }
    }

    public static class ManagedFormulaOcrService
    {
        public const string TokenVariable = "EXCALIDRAW_MANAGER_FORMULA_OCR_TOKEN";
        private const int PreferredPort = 6527;
        private static readonly object Sync = new object();
        private static Process _process;
        private static string _root;
        private static string _configPath;
        private static string _token;
        private static int _port;

        public static ManagedFormulaOcrSession EnsureStarted(string root, int excludedPort)
        {
            if (string.IsNullOrWhiteSpace(root)) return null;
            string normalizedRoot;
            try { normalizedRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(root.Trim())); }
            catch { return null; }

            lock (Sync)
            {
                try
                {
                    if (IsReady(normalizedRoot))
                        return new ManagedFormulaOcrSession { ConfigPath = _configPath, Token = _token };
                    DropLocked();
                    return StartLocked(normalizedRoot, excludedPort);
                }
                catch
                {
                    DropLocked();
                    return null;
                }
            }
        }

        public static bool IsInstalled(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return false;
            try
            {
                string normalizedRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(root.Trim()));
                string python = Path.Combine(normalizedRoot, "venv", "Scripts", "python.exe");
                string modelRoot = Path.Combine(normalizedRoot, "models", "rapid-latex-ocr");
                string providerDirectory = Path.Combine(Path.GetDirectoryName(typeof(ManagedFormulaOcrService).Assembly.Location), "runtime", "formula-ocr-provider");
                string[] modelFiles = { "image_resizer.onnx", "encoder.onnx", "decoder.onnx", "tokenizer.json" };
                return File.Exists(python) && File.Exists(Path.Combine(providerDirectory, "server.py")) &&
                    File.Exists(Path.Combine(modelRoot, "manifest.json")) &&
                    File.Exists(Path.Combine(normalizedRoot, "install-complete.json")) &&
                    modelFiles.All(x => File.Exists(Path.Combine(modelRoot, x)));
            }
            catch { return false; }
        }

        public static bool IsReadyForRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return false;
            try
            {
                string normalizedRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(root.Trim()));
                lock (Sync) return IsReady(normalizedRoot);
            }
            catch { return false; }
        }

        public static void Stop()
        {
            lock (Sync) DropLocked();
        }

        private static ManagedFormulaOcrSession StartLocked(string root, int excludedPort)
        {
            string python = Path.Combine(root, "venv", "Scripts", "python.exe");
            string modelRoot = Path.Combine(root, "models", "rapid-latex-ocr");
            string providerDirectory = Path.Combine(Path.GetDirectoryName(typeof(ManagedFormulaOcrService).Assembly.Location), "runtime", "formula-ocr-provider");
            string serverPath = Path.Combine(providerDirectory, "server.py");
            if (!IsInstalled(root) || !File.Exists(python) || !File.Exists(serverPath))
                return null;

            string runDirectory = Path.Combine(root, "run");
            string tempDirectory = Path.Combine(root, "temp");
            string cacheDirectory = Path.Combine(root, "cache");
            Directory.CreateDirectory(runDirectory);
            Directory.CreateDirectory(tempDirectory);
            Directory.CreateDirectory(cacheDirectory);
            Directory.CreateDirectory(Path.Combine(cacheDirectory, "pip"));
            Directory.CreateDirectory(Path.Combine(cacheDirectory, "torch"));
            Directory.CreateDirectory(Path.Combine(cacheDirectory, "huggingface"));

            int port = ProcessService.FindFreePort(PreferredPort, excludedPort);
            string token = CreateToken();
            string configPath = Path.Combine(runDirectory, "formula-providers.active.json");
            var config = new Dictionary<string, object>
            {
                { "providers", new object[]
                    {
                        new Dictionary<string, object>
                        {
                            { "id", "rapid-latex-ocr-local" },
                            { "name", "RapidLaTeXOCR Local" },
                            { "baseUrl", "http://127.0.0.1:" + port },
                            { "enabled", true },
                            { "tokenEnv", TokenVariable }
                        }
                    }
                }
            };
            File.WriteAllText(configPath, new JavaScriptSerializer().Serialize(config), new UTF8Encoding(false));

            var arguments = new StringBuilder();
            arguments.Append(Quote(serverPath));
            arguments.Append(" --host 127.0.0.1 --port ").Append(port);
            arguments.Append(" --model-root ").Append(Quote(modelRoot));
            arguments.Append(" --parent-pid ").Append(Process.GetCurrentProcess().Id);
            arguments.Append(" --token-env ").Append(TokenVariable);
            var psi = new ProcessStartInfo(python, arguments.ToString())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = providerDirectory
            };
            psi.EnvironmentVariables[TokenVariable] = token;
            foreach (string variable in new[] { "PYTHONHOME", "PYTHONPATH", "PYTHONUSERBASE", "VIRTUAL_ENV" })
                psi.EnvironmentVariables.Remove(variable);
            psi.EnvironmentVariables["PYTHONNOUSERSITE"] = "1";
            psi.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1";
            psi.EnvironmentVariables["TEMP"] = tempDirectory;
            psi.EnvironmentVariables["TMP"] = tempDirectory;
            psi.EnvironmentVariables["PIP_CACHE_DIR"] = Path.Combine(cacheDirectory, "pip");
            psi.EnvironmentVariables["TORCH_HOME"] = Path.Combine(cacheDirectory, "torch");
            psi.EnvironmentVariables["HF_HOME"] = Path.Combine(cacheDirectory, "huggingface");

            Process process = null;
            var error = new StringBuilder();
            try
            {
                process = Process.Start(psi);
                if (process == null) return null;
                process.OutputDataReceived += delegate { };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (error) if (error.Length < 8192) error.AppendLine(e.Data);
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                bool ready = false;
                var timer = Stopwatch.StartNew();
                while (timer.Elapsed < TimeSpan.FromSeconds(12))
                {
                    if (process.HasExited) break;
                    if (IsHttpReady(port, token)) { ready = true; break; }
                    Thread.Sleep(100);
                }
                if (!ready)
                {
                    try { if (!process.HasExited) process.Kill(); } catch { }
                    try { process.Dispose(); } catch { }
                    return null;
                }

                _process = process;
                _root = root;
                _configPath = configPath;
                _token = token;
                _port = port;
                process.EnableRaisingEvents = true;
                process.Exited += delegate
                {
                    lock (Sync)
                    {
                        if (ReferenceEquals(_process, process))
                        {
                            _process = null;
                            _root = null;
                            _configPath = null;
                            _token = null;
                            _port = 0;
                        }
                    }
                    try { process.Dispose(); } catch { }
                };
                return new ManagedFormulaOcrSession { ConfigPath = configPath, Token = token };
            }
            catch
            {
                try { if (process != null && !process.HasExited) process.Kill(); } catch { }
                try { if (process != null) process.Dispose(); } catch { }
                return null;
            }
        }

        private static bool IsReady(string root)
        {
            try
            {
                return _process != null && !_process.HasExited && _port > 0 &&
                    string.Equals(_root, root, StringComparison.OrdinalIgnoreCase) &&
                    IsHttpReady(_port, _token);
            }
            catch { return false; }
        }

        private static bool IsHttpReady(int port, string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            try
            {
                var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/v1/health");
                request.Method = "GET";
                request.Proxy = null;
                request.AllowAutoRedirect = false;
                request.Timeout = 400;
                request.ReadWriteTimeout = 400;
                request.Headers[HttpRequestHeader.Authorization] = "Bearer " + token;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    reader.ReadToEnd();
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch { return false; }
        }

        private static void DropLocked()
        {
            var process = _process;
            _process = null;
            _root = null;
            _configPath = null;
            _token = null;
            _port = 0;
            if (process == null) return;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
            }
            catch { }
            finally { try { process.Dispose(); } catch { } }
        }

        private static string CreateToken()
        {
            var bytes = new byte[32];
            using (var random = new RNGCryptoServiceProvider()) random.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
    }

    public static class FormulaEditorService
    {
        private const int PreferredPort = 6517;
        private static readonly object Sync = new object();
        private static Process _process;
        private static int _port;
        private static bool _ocrEnabled = true;
        private static string _ocrRoot = "";

        public static void ConfigureOcr(bool enabled, string root)
        {
            lock (Sync)
            {
                _ocrEnabled = enabled;
                _ocrRoot = root ?? "";
            }
        }

        public static string EnsureStarted(string language, string boardUrl)
        {
            string normalizedLanguage = string.Equals(language, "zh-CN", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en";
            string normalizedBoardUrl = NormalizeBoardUrl(boardUrl);
            int reservedBoardPort = 0;
            if (!string.IsNullOrEmpty(normalizedBoardUrl))
            {
                try { reservedBoardPort = new Uri(normalizedBoardUrl).Port; } catch { }
            }

            lock (Sync)
            {
                bool restart = !IsTrackedProcessReady();
                if (!restart && _ocrEnabled && ManagedFormulaOcrService.IsInstalled(_ocrRoot) &&
                    !ManagedFormulaOcrService.IsReadyForRoot(_ocrRoot))
                    restart = true;
                if (restart)
                {
                    DropTrackedProcess();
                    Start(normalizedLanguage, normalizedBoardUrl, reservedBoardPort);
                }
                return BuildEditorUrl(_port, normalizedLanguage, normalizedBoardUrl);
            }
        }

        public static void OpenInDefaultBrowser(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                !uri.IsLoopback || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidOperationException(Localization.T("Refusing to open a non-local formula editor URL."));

            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }

        public static void OpenNativePalette(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                !uri.IsLoopback || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidOperationException(Localization.T("Refusing to open a non-local formula editor URL."));

            string separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
            NativeFormulaWindow.Open(uri.AbsoluteUri + separator + "compact=1&native=1");
        }

        public static void Stop()
        {
            NativeFormulaWindow.Close();
            Process process;
            lock (Sync)
            {
                process = _process;
                _process = null;
                _port = 0;
            }

            if (process != null) try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
            }
            catch { }
            finally { try { process.Dispose(); } catch { } }
            ManagedFormulaOcrService.Stop();
        }

        private static void Start(string language, string boardUrl, int reservedBoardPort)
        {
            string runtimeDirectory = Path.Combine(Path.GetDirectoryName(typeof(FormulaEditorService).Assembly.Location), "runtime");
            string serverPath = Path.Combine(runtimeDirectory, "formula-server.mjs");
            string assetsPath = Path.Combine(runtimeDirectory, "formula-editor");
            if (!File.Exists(serverPath))
                throw new InvalidOperationException(Localization.F("Formula editor runtime is missing: {0}", serverPath));
            if (!File.Exists(Path.Combine(assetsPath, "index.html")))
                throw new InvalidOperationException(Localization.F("Formula editor assets are missing: {0}", assetsPath));

            ManagedFormulaOcrSession ocrSession = null;
            if (_ocrEnabled && !string.IsNullOrWhiteSpace(_ocrRoot))
            {
                try { ocrSession = ManagedFormulaOcrService.EnsureStarted(_ocrRoot, reservedBoardPort); }
                catch { ocrSession = null; }
            }

            int port = ProcessService.FindFreePort(PreferredPort, reservedBoardPort);
            var arguments = new StringBuilder();
            arguments.Append(Quote(serverPath));
            arguments.Append(" --port ").Append(port);
            arguments.Append(" --host 127.0.0.1");
            arguments.Append(" --assets ").Append(Quote(assetsPath));
            arguments.Append(" --parent-pid ").Append(Process.GetCurrentProcess().Id);
            arguments.Append(" --lang ").Append(language);
            if (ocrSession != null && !string.IsNullOrEmpty(ocrSession.ConfigPath))
                arguments.Append(" --provider-config ").Append(Quote(ocrSession.ConfigPath));
            if (!string.IsNullOrEmpty(boardUrl)) arguments.Append(" --board-url ").Append(Quote(boardUrl));

            var psi = new ProcessStartInfo(ProcessService.NodePath, arguments.ToString())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = runtimeDirectory
            };
            if (ocrSession != null && !string.IsNullOrEmpty(ocrSession.Token))
                psi.EnvironmentVariables[ManagedFormulaOcrService.TokenVariable] = ocrSession.Token;
            var process = Process.Start(psi);
            if (process == null) throw new InvalidOperationException(Localization.T("Failed to start formula editor."));

            var error = new StringBuilder();
            process.OutputDataReceived += delegate { };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (error) error.AppendLine(e.Data);
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            bool ready = false;
            var startupTimer = Stopwatch.StartNew();
            while (startupTimer.Elapsed < TimeSpan.FromSeconds(8))
            {
                if (process.HasExited) break;
                if (IsHttpReady(port)) { ready = true; break; }
                Thread.Sleep(100);
            }
            if (!ready)
            {
                string detail;
                lock (error) detail = error.ToString().Trim();
                bool exited = process.HasExited;
                try { if (!exited) process.Kill(); } catch { }
                try { process.Dispose(); } catch { }
                if (exited)
                    throw new InvalidOperationException(Localization.F("Formula editor exited: {0}", detail));
                throw new TimeoutException(Localization.T("Formula editor did not start within 8 seconds."));
            }

            _process = process;
            _port = port;
            process.Exited += delegate
            {
                lock (Sync)
                {
                    if (ReferenceEquals(_process, process))
                    {
                        _process = null;
                        _port = 0;
                    }
                }
                try { process.Dispose(); } catch { }
            };
            process.EnableRaisingEvents = true;
        }

        private static bool IsTrackedProcessReady()
        {
            try { return _process != null && !_process.HasExited && _port > 0 && IsHttpReady(_port); }
            catch { return false; }
        }

        private static void DropTrackedProcess()
        {
            var process = _process;
            _process = null;
            _port = 0;
            if (process == null) return;
            try { if (!process.HasExited) process.Kill(); } catch { }
            try { process.Dispose(); } catch { }
        }

        private static bool IsHttpReady(int port)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/api/status");
                request.Method = "GET";
                request.Proxy = null;
                request.AllowAutoRedirect = false;
                request.Timeout = 300;
                request.ReadWriteTimeout = 300;
                using (var response = (HttpWebResponse)request.GetResponse())
                    return response.StatusCode == HttpStatusCode.OK;
            }
            catch { return false; }
        }

        private static string NormalizeBoardUrl(string boardUrl)
        {
            Uri uri;
            if (string.IsNullOrWhiteSpace(boardUrl) || !Uri.TryCreate(boardUrl, UriKind.Absolute, out uri)) return null;
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                !uri.IsLoopback || !string.IsNullOrEmpty(uri.UserInfo)) return null;
            return uri.AbsoluteUri;
        }

        private static string BuildEditorUrl(int port, string language, string boardUrl)
        {
            string url = "http://127.0.0.1:" + port + "/?lang=" + Uri.EscapeDataString(language);
            if (!string.IsNullOrEmpty(boardUrl)) url += "&board=" + Uri.EscapeDataString(boardUrl);
            return url;
        }

        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
    }

    internal static class NativeFormulaWindow
    {
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpShowWindow = 0x0040;
        private const int SwRestore = 9;
        private const string WindowMarkerName = "ExcalidrawManager.FormulaPalette";
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private static readonly object Sync = new object();
        private static IntPtr _activeWindow;
        private static IntPtr _activeMarker;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maximum);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int maximum);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int command);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetProp(IntPtr hWnd, string name, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetProp(IntPtr hWnd, string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr RemoveProp(IntPtr hWnd, string name);

        public static void Open(string url)
        {
            lock (Sync) OpenLocked(url);
        }

        public static void Close()
        {
            lock (Sync)
            {
                if (IsTrackedWindow())
                {
                    RemoveProp(_activeWindow, WindowMarkerName);
                    PostMessage(_activeWindow, 0x0010, IntPtr.Zero, IntPtr.Zero);
                }
                ClearTracking();
            }
        }

        private static void OpenLocked(string url)
        {
            if (IsTrackedWindow())
            {
                ShowWindow(_activeWindow, SwRestore);
                SetWindowPos(_activeWindow, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);
                SetForegroundWindow(_activeWindow);
                return;
            }
            ClearTracking();
            string browser = FindBrowser();
            if (string.IsNullOrEmpty(browser))
                throw new InvalidOperationException(Localization.T("Microsoft Edge or Google Chrome is required for the native formula palette."));

            var previousWindows = new HashSet<IntPtr>(EnumerateBrowserWindows().Select(x => x.Handle));
            var arguments = "--app=" + QuoteArgument(url) +
                " --new-window --window-size=380,640 --disable-session-crashed-bubble --no-first-run";
            var process = Process.Start(new ProcessStartInfo(browser, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process == null) throw new InvalidOperationException(Localization.T("Failed to start the formula palette window."));

            IntPtr handle = IntPtr.Zero;
            try
            {
                var timer = Stopwatch.StartNew();
                while (timer.Elapsed < TimeSpan.FromSeconds(10))
                {
                    var titled = EnumerateBrowserWindows().FirstOrDefault(x =>
                        !previousWindows.Contains(x.Handle) && IsPaletteTitle(x.Title));
                    if (titled != null)
                    {
                        handle = titled.Handle;
                        break;
                    }
                    Thread.Sleep(100);
                }
            }
            finally { try { process.Dispose(); } catch { } }

            if (handle == IntPtr.Zero)
                throw new InvalidOperationException(Localization.T("The formula palette window did not appear."));
            ShowWindow(handle, SwRestore);
            var marker = new IntPtr(Guid.NewGuid().GetHashCode());
            if (marker == IntPtr.Zero) marker = new IntPtr(1);
            if (!SetProp(handle, WindowMarkerName, marker))
            {
                PostMessage(handle, 0x0010, IntPtr.Zero, IntPtr.Zero);
                throw new InvalidOperationException(Localization.T("The formula palette window did not appear."));
            }
            if (!SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow))
            {
                RemoveProp(handle, WindowMarkerName);
                PostMessage(handle, 0x0010, IntPtr.Zero, IntPtr.Zero);
                throw new InvalidOperationException(Localization.T("The formula palette could not be kept on top."));
            }
            _activeWindow = handle;
            _activeMarker = marker;
            SetForegroundWindow(handle);
        }

        private static bool IsPaletteTitle(string title)
        {
            return string.Equals(title, "Formula palette", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(title, "公式悬浮窗", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTrackedWindow()
        {
            return _activeWindow != IntPtr.Zero && _activeMarker != IntPtr.Zero && IsWindow(_activeWindow) &&
                GetProp(_activeWindow, WindowMarkerName) == _activeMarker;
        }

        private static void ClearTracking()
        {
            _activeWindow = IntPtr.Zero;
            _activeMarker = IntPtr.Zero;
        }

        private static string FindBrowser()
        {
            var candidates = new List<string>();
            foreach (string key in new[]
            {
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe",
                @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe",
                @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"
            })
            {
                var value = Registry.GetValue(key, "", null) as string;
                if (!string.IsNullOrWhiteSpace(value)) candidates.Add(value.Trim('"'));
            }
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            candidates.Add(Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"));
            candidates.Add(Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe"));
            candidates.Add(Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"));
            candidates.Add(Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"));
            candidates.Add(Path.Combine(local, "Google", "Chrome", "Application", "chrome.exe"));
            return candidates.FirstOrDefault(File.Exists);
        }

        private sealed class BrowserWindow
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; }
        }

        private static List<BrowserWindow> EnumerateBrowserWindows()
        {
            var result = new List<BrowserWindow>();
            EnumWindows(delegate(IntPtr handle, IntPtr ignored)
            {
                if (!IsWindowVisible(handle)) return true;
                var className = new StringBuilder(128);
                GetClassName(handle, className, className.Capacity);
                if (className.ToString().IndexOf("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase) < 0) return true;
                var title = new StringBuilder(512);
                GetWindowText(handle, title, title.Capacity);
                result.Add(new BrowserWindow { Handle = handle, Title = title.ToString() });
                return true;
            }, IntPtr.Zero);
            return result;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }

    internal static class UiPalette
    {
        public static readonly Color Background = Color.FromArgb(255, 255, 255);
        public static readonly Color SoftBackground = Color.FromArgb(247, 248, 250);
        public static readonly Color Line = Color.FromArgb(229, 231, 236);
        public static readonly Color LineStrong = Color.FromArgb(211, 215, 223);
        public static readonly Color HoverLine = Color.FromArgb(178, 184, 196);
        public static readonly Color Text = Color.FromArgb(28, 31, 42);
        public static readonly Color Muted = Color.FromArgb(105, 112, 127);
        public static readonly Color Accent = Color.FromArgb(67, 72, 81);
        public static readonly Color AccentHover = Color.FromArgb(47, 51, 58);
        public static readonly Color AccentPressed = Color.FromArgb(35, 38, 44);
        public static readonly Color AccentSoft = Color.FromArgb(240, 241, 243);
        public static readonly Color RowAlternate = Color.FromArgb(250, 251, 252);
        public static readonly Color HoverNeutral = Color.FromArgb(242, 243, 247);
        public static readonly Color PressedNeutral = Color.FromArgb(232, 234, 240);
        public static readonly Color Success = Color.FromArgb(23, 138, 91);
        public static readonly Color Danger = Color.FromArgb(196, 64, 60);
        public static readonly Color DangerSoft = Color.FromArgb(253, 236, 234);
        public static readonly Color DisabledBackground = Color.FromArgb(243, 244, 246);
    }

    internal static class Ui
    {
        public static Color EffectiveBackColor(Control control)
        {
            for (var current = control == null ? null : control.Parent; current != null; current = current.Parent)
                if (current.BackColor.A == 255) return current.BackColor;
            return Color.White;
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0) { path.AddRectangle(bounds); return path; }
            int d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            if (d <= 0) { path.AddRectangle(bounds); return path; }
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal enum UiButtonStyle { Default, Primary, Subtle, Danger }

    internal sealed class UiButton : Button
    {
        private bool _hover;
        private bool _pressed;

        public UiButtonStyle Style { get; set; }
        public string GlyphText { get; set; }
        public Font GlyphFont { get; set; }

        public UiButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Style = UiButtonStyle.Default;
            Height = 30;
            Margin = new Padding(0, 0, 8, 0);
            UseVisualStyleBackColor = false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        public void FitToContent()
        {
            int textWidth = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
            int glyphWidth = string.IsNullOrEmpty(GlyphText) ? 0 : 20;
            int minimumWidth = textWidth == 0 && glyphWidth > 0 ? 38 : 66;
            int horizontalPadding = textWidth == 0 && glyphWidth > 0 ? 16 : 26;
            Width = Math.Max(minimumWidth, textWidth + glyphWidth + horizontalPadding);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Ui.EffectiveBackColor(this));
            Color background, border, foreground;
            PickColors(out background, out border, out foreground);
            using (var path = Ui.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 7))
            {
                if (background.A > 0) using (var brush = new SolidBrush(background)) g.FillPath(brush, path);
                if (border.A > 0) using (var pen = new Pen(border, 1f)) g.DrawPath(pen, path);
            }
            if (Focused && Enabled)
            {
                using (var pen = new Pen(Color.FromArgb(100, UiPalette.Accent), 1f))
                using (var path = Ui.RoundedRect(new Rectangle(2, 2, Width - 5, Height - 5), 5)) g.DrawPath(pen, path);
            }
            int glyphWidth = string.IsNullOrEmpty(GlyphText) ? 0 : 20;
            var textSize = TextRenderer.MeasureText(g, Text ?? "", Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int x = Math.Max(8, (Width - glyphWidth - textSize.Width) / 2);
            if (!string.IsNullOrEmpty(GlyphText))
            {
                var glyphFont = GlyphFont ?? Font;
                using (var brush = new SolidBrush(foreground))
                    g.DrawString(GlyphText, glyphFont, brush, new PointF(x - 1, (Height - glyphFont.Height) / 2f + 1f));
            }
            TextRenderer.DrawText(g, Text ?? "", Font, new Point(x + glyphWidth, (Height - textSize.Height) / 2), foreground, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        private void PickColors(out Color background, out Color border, out Color foreground)
        {
            background = Color.Transparent;
            border = Color.Transparent;
            foreground = UiPalette.Text;
            if (!Enabled)
            {
                foreground = UiPalette.Muted;
                if (Style == UiButtonStyle.Default) { background = UiPalette.SoftBackground; border = UiPalette.Line; }
                else if (Style == UiButtonStyle.Primary) background = UiPalette.DisabledBackground;
                return;
            }
            switch (Style)
            {
                case UiButtonStyle.Primary:
                    foreground = UiPalette.Text;
                    border = _hover ? UiPalette.Accent : UiPalette.HoverLine;
                    background = _pressed ? UiPalette.PressedNeutral : _hover ? UiPalette.HoverNeutral : Color.White;
                    break;
                case UiButtonStyle.Subtle:
                    background = _pressed ? UiPalette.PressedNeutral : _hover ? UiPalette.HoverNeutral : Color.Transparent;
                    break;
                case UiButtonStyle.Danger:
                    foreground = UiPalette.Danger;
                    background = _pressed ? Color.FromArgb(250, 219, 216) : _hover ? UiPalette.DangerSoft : Color.Transparent;
                    break;
                default:
                    border = _hover ? UiPalette.HoverLine : UiPalette.LineStrong;
                    background = _pressed ? UiPalette.PressedNeutral : _hover ? UiPalette.HoverNeutral : Color.White;
                    break;
            }
        }
    }

    internal sealed class LightMenuRenderer : ToolStripRenderer
    {
        public static readonly LightMenuRenderer Shared = new LightMenuRenderer();

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var brush = new SolidBrush(Color.White)) e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var pen = new Pen(UiPalette.Line)) e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var pen = new Pen(UiPalette.Line)) e.Graphics.DrawLine(pen, 10, e.Item.Height / 2, e.Item.Width - 10, e.Item.Height / 2);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Ui.RoundedRect(new Rectangle(3, 1, e.Item.Width - 7, e.Item.Height - 3), 5))
            using (var brush = new SolidBrush(UiPalette.AccentSoft)) e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? UiPalette.Text : UiPalette.Muted;
            base.OnRenderItemText(e);
        }
    }

    internal static class UiIcons
    {
        private static bool? _mdl2Available;
        private static Font _mdl2Toolbar;
        private static Font _sigmaGlyph;

        public static bool Mdl2Available
        {
            get
            {
                if (!_mdl2Available.HasValue)
                {
                    bool found = false;
                    try
                    {
                        using (var fonts = new InstalledFontCollection())
                            foreach (var family in fonts.Families)
                                if (string.Equals(family.Name, "Segoe MDL2 Assets", StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                    }
                    catch { }
                    _mdl2Available = found;
                }
                return _mdl2Available.Value;
            }
        }

        public static Font Mdl2Toolbar
        {
            get
            {
                if (_mdl2Toolbar == null && Mdl2Available)
                    _mdl2Toolbar = new Font("Segoe MDL2 Assets", 12f, FontStyle.Regular, GraphicsUnit.Pixel);
                return _mdl2Toolbar;
            }
        }

        public static Font SigmaGlyph
        {
            get
            {
                if (_sigmaGlyph == null) _sigmaGlyph = new Font("Georgia", 14f, FontStyle.Bold, GraphicsUnit.Pixel);
                return _sigmaGlyph;
            }
        }

        public static ImageList CreateTreeImages()
        {
            var list = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
            list.Images.Add("folder", CreateFolderIcon());
            list.Images.Add("board", CreateBoardIcon(false));
            list.Images.Add("boardRunning", CreateBoardIcon(true));
            return list;
        }

        private static Bitmap CreateFolderIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(Color.FromArgb(155, 165, 182)))
                {
                    g.FillRectangle(brush, 2, 3, 5, 3);
                    using (var path = Ui.RoundedRect(new Rectangle(1, 5, 14, 9), 2)) g.FillPath(brush, path);
                }
            }
            return bmp;
        }

        private static Bitmap CreateBoardIcon(bool running)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var border = running ? UiPalette.Accent : Color.FromArgb(143, 153, 168);
                Point[] outline = { new Point(4, 1), new Point(10, 1), new Point(13, 4), new Point(13, 14), new Point(4, 14) };
                using (var brush = new SolidBrush(running ? UiPalette.AccentSoft : Color.White)) g.FillPolygon(brush, outline);
                using (var pen = new Pen(border, 1f))
                {
                    g.DrawPolygon(pen, outline);
                    g.DrawLine(pen, 10, 2, 10, 4);
                    g.DrawLine(pen, 10, 4, 12, 4);
                }
                if (running) using (var brush = new SolidBrush(UiPalette.Accent)) g.FillEllipse(brush, 6.5f, 7.5f, 4f, 4f);
            }
            return bmp;
        }
    }

    internal sealed class WorkspaceGroupNodeTag
    {
        public string Id { get; private set; }
        public WorkspaceGroupNodeTag(string id) { Id = id; }
    }

    public sealed class MainForm : Form
    {
        private const int EmSetCueBanner = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private readonly AppSettings _settings;
        private readonly string _initialFile;
        private readonly TreeView _tree = new TreeView();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _status = new Label();
        private readonly NotifyIcon _tray = new NotifyIcon();
        private readonly System.Windows.Forms.Timer _refreshTimer = new System.Windows.Forms.Timer();
        private readonly TextBox _search = new TextBox();
        private List<DrawingInstance> _instances = new List<DrawingInstance>();
        private bool _reallyExit;
        public bool RestartRequested { get; private set; }

        public MainForm(string initialFile)
        {
            _initialFile = initialFile;
            _settings = SettingsStore.Load();
            Localization.Configure(_settings.Language);
            FormulaEditorService.ConfigureOcr(_settings.FormulaOcrEnabled, _settings.FormulaOcrRoot);
            Text = T("Excalidraw Manager");
            MinimumSize = new Size(980, 600);
            Size = new Size(1180, 720);
            StartPosition = FormStartPosition.CenterScreen;
            RestoreWindowPlacement();
            Font = new Font("Segoe UI", 9F);
            DoubleBuffered = true;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            BuildUi();
            BuildTray();
            Load += OnLoaded;
            KeyDown += OnGlobalKeyDown;
            FormClosing += OnFormClosing;
            FormClosed += delegate
            {
                _refreshTimer.Stop();
                FormulaEditorService.Stop();
                _tray.Dispose();
            };
        }

        private void RestoreWindowPlacement()
        {
            if (_settings.WindowWidth >= MinimumSize.Width && _settings.WindowHeight >= MinimumSize.Height)
                Size = new Size(_settings.WindowWidth, _settings.WindowHeight);
            var anchor = new Rectangle(_settings.WindowX, _settings.WindowY, 64, 64);
            if ((_settings.WindowX != 0 || _settings.WindowY != 0) && Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(anchor)))
            {
                StartPosition = FormStartPosition.Manual;
                Location = new Point(_settings.WindowX, _settings.WindowY);
            }
            if (_settings.WindowMaximized) WindowState = FormWindowState.Maximized;
        }

        private void SaveWindowPlacement()
        {
            try
            {
                _settings.WindowMaximized = WindowState == FormWindowState.Maximized;
                var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                if (bounds.Width < MinimumSize.Width || bounds.Height < MinimumSize.Height) return;
                _settings.WindowX = bounds.X;
                _settings.WindowY = bounds.Y;
                _settings.WindowWidth = bounds.Width;
                _settings.WindowHeight = bounds.Height;
                SettingsStore.Save(_settings);
            }
            catch { }
        }

        private void BuildUi()
        {
            BackColor = UiPalette.Background;

            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 360, Panel1MinSize = 260, BackColor = UiPalette.Background, SplitterWidth = 6 };
            Shown += delegate
            {
                int desired = Math.Max(280, split.ClientSize.Width / 3);
                split.SplitterDistance = Math.Min(desired, Math.Max(split.Panel1MinSize, split.ClientSize.Width - 560));
            };
            split.Panel1.Padding = new Padding(12, 4, 8, 10);
            split.Panel2.Padding = new Padding(8, 4, 12, 10);

            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = UiPalette.SoftBackground };
            statusBar.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(UiPalette.Line)) e.Graphics.DrawLine(pen, 0, 0, statusBar.Width, 0);
            };
            _status.Dock = DockStyle.Fill;
            _status.Padding = new Padding(14, 0, 14, 0);
            _status.ForeColor = UiPalette.Muted;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            statusBar.Controls.Add(_status);

            Controls.Add(split);
            Controls.Add(statusBar);
            Controls.Add(BuildTopBar());

            var leftTitle = MakeSectionTitle(T("WORKSPACES"));
            _tree.Dock = DockStyle.Fill;
            _tree.HideSelection = false;
            _tree.ShowNodeToolTips = true;
            _tree.BorderStyle = BorderStyle.None;
            _tree.BackColor = Color.White;
            _tree.ForeColor = UiPalette.Text;
            _tree.ItemHeight = 24;
            _tree.Indent = 20;
            _tree.ShowLines = false;
            _tree.FullRowSelect = true;
            _tree.ImageList = UiIcons.CreateTreeImages();
            _tree.BeforeExpand += TreeBeforeExpand;
            _tree.NodeMouseDoubleClick += TreeDoubleClick;
            _tree.NodeMouseClick += TreeMouseClick;
            split.Panel1.Controls.Add(CreateFrame(_tree));
            split.Panel1.Controls.Add(leftTitle);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 10, 0, 0), WrapContents = false, BackColor = Color.White };
            actions.Controls.Add(MakeUiButton(T("Open"), delegate { OpenSelected(); }, UiButtonStyle.Primary));
            actions.Controls.Add(MakeUiButton(T("Copy URL"), delegate { CopySelectedUrl(); }, UiButtonStyle.Default));
            actions.Controls.Add(MakeUiButton(T("Restart"), delegate { RestartSelected(); }, UiButtonStyle.Default));
            actions.Controls.Add(MakeUiButton(T("Stop"), delegate { StopSelected(); }, UiButtonStyle.Default));
            actions.Controls.Add(MakeUiButton(T("Stop all"), delegate { StopAll(); }, UiButtonStyle.Danger));

            ConfigureGrid();
            var rightTitle = MakeSectionTitle(T("RUNNING INSTANCES"));
            split.Panel2.Controls.Add(CreateFrame(_grid));
            split.Panel2.Controls.Add(actions);
            split.Panel2.Controls.Add(rightTitle);
        }

        private Control BuildTopBar()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.White };
            header.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(UiPalette.Line)) e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Margin = Padding.Empty,
                Padding = new Padding(12, 9, 0, 8),
                BackColor = Color.Transparent
            };
            var right = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = new Padding(8, 9, 12, 8),
                BackColor = Color.Transparent
            };

            var actionButtons = new List<KeyValuePair<UiButton, string>>();
            var headerButtons = new List<KeyValuePair<UiButton, string>>();
            var toolbarToolTip = new ToolTip { AutomaticDelay = 300, AutoPopDelay = 6000, ReshowDelay = 100 };
            header.Disposed += delegate { toolbarToolTip.Dispose(); };
            Action<FlowLayoutPanel, UiButton, string> addButton = delegate(FlowLayoutPanel flow, UiButton button, string label)
            {
                button.AccessibleName = label;
                button.Tag = label;
                toolbarToolTip.SetToolTip(button, label);
                flow.Controls.Add(button);
                var item = new KeyValuePair<UiButton, string>(button, label);
                if (object.ReferenceEquals(flow, actions)) actionButtons.Add(item); else headerButtons.Add(item);
            };

            addButton(actions, MakeUiButton(T("Add root"), delegate { AddRoot(); }, UiButtonStyle.Default, ""), T("Add root"));
            addButton(actions, MakeUiButton(T("Open file"), delegate { OpenFile(); }, UiButtonStyle.Default, ""), T("Open file"));
            addButton(actions, MakeUiButton(T("New board"), delegate { NewBoard(); }, UiButtonStyle.Default, ""), T("New board"));
            addButton(actions, MakeUiButton(T("New group"), delegate { NewWorkspaceGroup(null); }, UiButtonStyle.Default, ""), T("New group"));
            addButton(actions, MakeUiButton(T("Refresh"), delegate { RefreshAll(); }, UiButtonStyle.Default, ""), T("Refresh"));
            var formula = MakeUiButton(T("Formula Editor"), delegate { OpenFormulaEditor(); }, UiButtonStyle.Default);
            formula.GlyphText = "∑";
            formula.GlyphFont = UiIcons.SigmaGlyph;
            formula.FitToContent();
            addButton(actions, formula, T("Formula Editor"));
            var palette = MakeUiButton(T("Formula Palette"), delegate { OpenFormulaPalette(); }, UiButtonStyle.Default);
            palette.GlyphText = "↗";
            palette.FitToContent();
            addButton(actions, palette, T("Formula Palette"));

            _search.Width = 200;
            _search.BorderStyle = BorderStyle.FixedSingle;
            _search.BackColor = UiPalette.SoftBackground;
            _search.ForeColor = UiPalette.Text;
            _search.AccessibleName = T("Search boards");
            _search.Margin = new Padding(0, 3, 8, 0);
            toolbarToolTip.SetToolTip(_search, T("Search boards"));
            _search.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { SearchFiles(_search.Text); e.SuppressKeyPress = true; } };
            right.Controls.Add(_search);
            addButton(right, MakeHeaderButton(T("Settings"), delegate { ShowSettings(); }, ""), T("Settings"));
            addButton(right, MakeHeaderButton(T("Libraries"), delegate { ShowLibraryManager(); }, ""), T("Libraries"));
            addButton(right, MakeHeaderButton(T("Environment"), delegate { ShowEnvironment(); }, ""), T("Environment"));

            layout.Controls.Add(actions, 0, 0);
            layout.Controls.Add(right, 1, 0);
            header.Controls.Add(layout);

            bool applyingLayout = false;
            Action updateLayout = delegate
            {
                if (applyingLayout || header.ClientSize.Width <= 0) return;
                applyingLayout = true;
                try
                {
                    layout.SuspendLayout();
                    actions.SuspendLayout();
                    right.SuspendLayout();

                    foreach (var item in actionButtons.Concat(headerButtons))
                    {
                        item.Key.Text = item.Value;
                        item.Key.FitToContent();
                    }
                    _search.Width = 200;

                    Func<FlowLayoutPanel, int> preferredWidth = delegate(FlowLayoutPanel flow)
                    {
                        int width = flow.Padding.Horizontal;
                        foreach (Control control in flow.Controls) width += control.Width + control.Margin.Horizontal;
                        return width;
                    };
                    Func<int> requiredWidth = delegate { return preferredWidth(actions) + preferredWidth(right) + 4; };

                    if (requiredWidth() > header.ClientSize.Width) _search.Width = 150;

                    // Preserve the task-oriented labels for as long as possible: the
                    // familiar settings/library/environment controls become icons first.
                    foreach (var item in headerButtons.AsEnumerable().Reverse())
                    {
                        if (requiredWidth() <= header.ClientSize.Width) break;
                        item.Key.Text = string.Empty;
                        item.Key.FitToContent();
                    }

                    if (requiredWidth() > header.ClientSize.Width) _search.Width = 110;

                    // At very narrow widths, collapse lower-priority task labels one at a
                    // time. Every button remains reachable with an accessible name and tip;
                    // horizontal scrolling is only the final high-DPI fallback.
                    var actionCollapseOrder = new[] { actionButtons[4], actionButtons[5], actionButtons[6] }
                        .Concat(actionButtons.Take(4).Reverse());
                    foreach (var item in actionCollapseOrder)
                    {
                        if (requiredWidth() <= header.ClientSize.Width) break;
                        item.Key.Text = string.Empty;
                        item.Key.FitToContent();
                    }
                }
                finally
                {
                    right.ResumeLayout(true);
                    actions.ResumeLayout(true);
                    layout.ResumeLayout(true);
                    applyingLayout = false;
                }
            };
            header.SizeChanged += delegate { updateLayout(); };
            header.HandleCreated += delegate { header.BeginInvoke((MethodInvoker)delegate { updateLayout(); }); };
            return header;
        }

        private UiButton MakeHeaderButton(string text, EventHandler click, string mdl2Glyph)
        {
            var button = MakeUiButton(text, click, UiButtonStyle.Subtle, mdl2Glyph);
            button.Margin = new Padding(0, 0, 2, 0);
            return button;
        }

        private static UiButton MakeUiButton(string text, EventHandler click, UiButtonStyle style, string mdl2Glyph)
        {
            var button = MakeUiButton(text, click, style);
            var glyphFont = UiIcons.Mdl2Toolbar;
            if (glyphFont != null && !string.IsNullOrEmpty(mdl2Glyph)) { button.GlyphText = mdl2Glyph; button.GlyphFont = glyphFont; }
            button.FitToContent();
            return button;
        }

        private static UiButton MakeUiButton(string text, EventHandler click, UiButtonStyle style)
        {
            var button = new UiButton { Text = text, Style = style, Height = 30 };
            button.Click += click;
            button.FitToContent();
            return button;
        }

        private static UiButton MakeUiButton(string text, EventHandler click) { return MakeUiButton(text, click, UiButtonStyle.Default); }

        private static Label MakeSectionTitle(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = Color.White,
                ForeColor = UiPalette.Muted,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(3, 0, 0, 0)
            };
        }

        private static Panel CreateFrame(Control content)
        {
            var frame = new Panel { Dock = DockStyle.Fill, BackColor = UiPalette.LineStrong, Padding = new Padding(1) };
            content.Dock = DockStyle.Fill;
            frame.Controls.Add(content);
            return frame;
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
            _grid.BorderStyle = BorderStyle.None;
            _grid.BackgroundColor = Color.White;
            _grid.GridColor = UiPalette.Line;
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = UiPalette.Muted;
            _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiPalette.Muted;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = 34;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.DefaultCellStyle.BackColor = Color.White;
            _grid.DefaultCellStyle.ForeColor = UiPalette.Text;
            _grid.DefaultCellStyle.SelectionBackColor = UiPalette.AccentSoft;
            _grid.DefaultCellStyle.SelectionForeColor = UiPalette.Text;
            _grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            _grid.AlternatingRowsDefaultCellStyle.BackColor = UiPalette.RowAlternate;
            _grid.AlternatingRowsDefaultCellStyle.ForeColor = UiPalette.Text;
            _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = UiPalette.AccentSoft;
            _grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = UiPalette.Text;
            _grid.RowTemplate.Height = 32;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = T("File"), DataPropertyName = "DisplayName", Width = 165 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PID", DataPropertyName = "Pid", Width = 65 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = T("Port"), DataPropertyName = "Port", Width = 60 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "URL", DataPropertyName = "Url", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = T("Source"), DataPropertyName = "Source", Width = 75 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = T("Theme"), DataPropertyName = "Theme", Width = 82 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = T("Started"), DataPropertyName = "StartedAt", Width = 125, DefaultCellStyle = new DataGridViewCellStyle { Format = "MM-dd HH:mm:ss" } });
            _grid.CellDoubleClick += delegate { OpenSelected(); };
            _grid.CellMouseDown += GridCellMouseDown;
            _grid.CellFormatting += delegate(object sender, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
                var row = _grid.Rows[e.RowIndex];
                var item = row.DataBoundItem as DrawingInstance;
                if (item == null || row.Cells.Count == 0) return;
                row.Cells[0].ToolTipText = item.FilePath + "\r\n" + item.CommandLine;
                if (e.ColumnIndex == 4 || e.ColumnIndex == 5) { e.Value = T(Convert.ToString(e.Value)); e.FormattingApplied = true; }
            };
        }

        private void GridCellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
            _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[0];
            if (SelectedInstance() == null) return;
            var menu = new ContextMenuStrip { Renderer = LightMenuRenderer.Shared };
            menu.Items.Add(T("Open"), null, delegate { OpenSelected(); });
            menu.Items.Add(T("Copy URL"), null, delegate { CopySelectedUrl(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(T("Restart"), null, delegate { RestartSelected(); });
            menu.Items.Add(T("Stop"), null, delegate { StopSelected(); });
            menu.Show(_grid, e.Location);
        }

        private void OnGlobalKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                RefreshAll();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode != Keys.Enter) return;
            if (_grid.Focused) { OpenSelected(); e.SuppressKeyPress = true; }
            else if (_tree.Focused) { OpenSelectedTreeNode(); e.SuppressKeyPress = true; }
        }

        private void OpenSelectedTreeNode()
        {
            var node = _tree.SelectedNode;
            string path = node == null ? null : node.Tag as string;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            var existing = _instances.FirstOrDefault(x => SamePath(x.FilePath, path));
            if (existing != null) OpenUrl(existing.Url); else StartFileAsync(path, true, 0);
        }

        private void BuildTray()
        {
            _tray.Icon = Icon ?? SystemIcons.Application;
            _tray.Text = T("Excalidraw Manager");
            _tray.Visible = true;
            _tray.DoubleClick += delegate { RestoreWindow(); };
            var menu = new ContextMenuStrip { Renderer = LightMenuRenderer.Shared };
            menu.Items.Add(T("Show"), null, delegate { RestoreWindow(); });
            menu.Items.Add(T("Formula Editor"), null, delegate { OpenFormulaEditor(); });
            menu.Items.Add(T("Formula Palette"), null, delegate { OpenFormulaPalette(); });
            menu.Items.Add(T("Stop all services"), null, delegate { StopAll(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(T("Exit..."), null, delegate { ExitFromTray(); });
            _tray.ContextMenuStrip = menu;
        }

        private void OnLoaded(object sender, EventArgs e)
        {
            SendMessage(_search.Handle, EmSetCueBanner, IntPtr.Zero, T("Search boards"));
            RefreshInstances();
            LoadRoots();
            _refreshTimer.Interval = 5000;
            _refreshTimer.Tick += delegate { RefreshInstances(); };
            _refreshTimer.Start();
            if (!string.IsNullOrEmpty(_initialFile)) StartFileAsync(_initialFile, false, 0);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            SaveWindowPlacement();
            if (_reallyExit) return;
            e.Cancel = true;
            Hide();
            if (!_settings.TrayHintShown)
            {
                _tray.ShowBalloonTip(2500, T("Excalidraw Manager"), T("Still running in the system tray. Use Exit there to close it."), ToolTipIcon.Info);
                _settings.TrayHintShown = true;
                SettingsStore.Save(_settings);
            }
        }

        private void ExitFromTray()
        {
            var answer = MessageBox.Show(
                T("Yes: stop all boards and formula services, then exit.\r\nNo: keep boards running and exit; the formula palette will stop.\r\nCancel: return to the tray."),
                T("Exit Excalidraw Manager"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
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
            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string selectedKey = null;
            CaptureTreeState(_tree.Nodes, expanded, ref selectedKey);
            _tree.BeginUpdate();
            try
            {
                _tree.Nodes.Clear();
                var recent = new TreeNode(T("Recent boards")) { Name = "__recent", ImageKey = "folder", SelectedImageKey = "folder" };
                foreach (string file in _settings.RecentFiles.Where(File.Exists).Take(20)) recent.Nodes.Add(MakeFileNode(file));
                if (recent.Nodes.Count > 0) _tree.Nodes.Add(recent);

                var validGroups = new HashSet<string>(_settings.WorkspaceGroups.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
                AddWorkspaceGroupNodes(null, _tree.Nodes, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                foreach (string root in ExistingRoots())
                {
                    string groupId;
                    if (!_settings.WorkspaceRootGroups.TryGetValue(root, out groupId) || !validGroups.Contains(groupId))
                        AddRootNode(root, _tree.Nodes);
                }
                RestoreTreeState(_tree.Nodes, expanded, selectedKey);
                if (recent.Nodes.Count > 0 && !recent.IsExpanded) recent.Expand();
            }
            finally { _tree.EndUpdate(); }
        }

        private IEnumerable<string> ExistingRoots()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in _settings.Roots.Where(Directory.Exists))
            {
                string root;
                try { root = Path.GetFullPath(value); }
                catch { continue; }
                if (seen.Add(root)) yield return root;
            }
        }

        private void AddWorkspaceGroupNodes(string parentId, TreeNodeCollection destination, HashSet<string> visiting)
        {
            foreach (var group in _settings.WorkspaceGroups.Where(x =>
                string.Equals(x.ParentId ?? "", parentId ?? "", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (!visiting.Add(group.Id)) continue;
                var node = new TreeNode(group.Name)
                {
                    Tag = new WorkspaceGroupNodeTag(group.Id),
                    ToolTipText = T("Virtual group; local folders are not moved"),
                    ImageKey = "folder",
                    SelectedImageKey = "folder"
                };
                destination.Add(node);
                AddWorkspaceGroupNodes(group.Id, node.Nodes, visiting);
                foreach (string root in ExistingRoots())
                {
                    string groupId;
                    if (_settings.WorkspaceRootGroups.TryGetValue(root, out groupId) &&
                        string.Equals(groupId, group.Id, StringComparison.OrdinalIgnoreCase)) AddRootNode(root, node.Nodes);
                }
                visiting.Remove(group.Id);
            }
        }

        private void AddRootNode(string root, TreeNodeCollection destination)
        {
            var node = new TreeNode(GetRootDisplayName(root)) { Tag = root, ToolTipText = root, ImageKey = "folder", SelectedImageKey = "folder" };
            node.Nodes.Add(new TreeNode(T("Loading...")));
            destination.Add(node);
        }

        private string GetRootDisplayName(string root)
        {
            return GetPathDisplayName(root) + "  [" + root + "]";
        }

        private string GetPathDisplayName(string path)
        {
            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(name)) name = trimmed;
            string alias;
            return _settings.Aliases.TryGetValue(Path.GetFullPath(path), out alias) && !string.IsNullOrWhiteSpace(alias)
                ? alias + "  (" + name + ")" : name;
        }

        private void CaptureTreeState(TreeNodeCollection nodes, HashSet<string> expanded, ref string selectedKey)
        {
            foreach (TreeNode node in nodes)
            {
                string key = TreeNodeKey(node);
                if (node.IsExpanded && key != null) expanded.Add(key);
                if (ReferenceEquals(node, _tree.SelectedNode)) selectedKey = key;
                CaptureTreeState(node.Nodes, expanded, ref selectedKey);
            }
        }

        private void RestoreTreeState(TreeNodeCollection nodes, HashSet<string> expanded, string selectedKey)
        {
            foreach (TreeNode node in nodes)
            {
                string key = TreeNodeKey(node);
                if (key != null && expanded.Contains(key)) node.Expand();
                RestoreTreeState(node.Nodes, expanded, selectedKey);
                if (key != null && string.Equals(key, selectedKey, StringComparison.OrdinalIgnoreCase)) _tree.SelectedNode = node;
            }
        }

        private static string TreeNodeKey(TreeNode node)
        {
            var group = node.Tag as WorkspaceGroupNodeTag;
            if (group != null) return "group:" + group.Id;
            string path = node.Tag as string;
            if (!string.IsNullOrEmpty(path))
            {
                try { return "path:" + Path.GetFullPath(path); }
                catch { return "path:" + path; }
            }
            return string.IsNullOrEmpty(node.Name) ? null : "special:" + node.Name;
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
                    var child = new TreeNode(GetPathDisplayName(dir)) { Tag = dir, ToolTipText = dir, ImageKey = "folder", SelectedImageKey = "folder" };
                    child.Nodes.Add(new TreeNode(T("Loading...")));
                    node.Nodes.Add(child);
                }
                foreach (string file in Directory.GetFiles(path, "*.excalidraw").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    node.Nodes.Add(MakeFileNode(file));
                }
            }
            catch (Exception ex) { node.Nodes.Add(new TreeNode(F("Access failed: {0}", ex.Message))); }
        }

        private void TreeBeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (!(e.Node.Tag is WorkspaceGroupNodeTag)) PopulateDirectory(e.Node);
        }

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
            var groupTag = e.Node.Tag as WorkspaceGroupNodeTag;
            if (groupTag != null)
            {
                ShowWorkspaceGroupMenu(groupTag.Id, e.Location);
                return;
            }
            string path = e.Node.Tag as string;
            if (string.IsNullOrEmpty(path)) return;
            var menu = new ContextMenuStrip { Renderer = LightMenuRenderer.Shared };
            if (Directory.Exists(path))
            {
                menu.Items.Add(T("New board here"), null, delegate { NewBoard(path); });
                menu.Items.Add(T("New folder..."), null, delegate { NewFolder(path); });
                menu.Items.Add(T("Refresh"), null, delegate { PopulateDirectory(e.Node); });
                menu.Items.Add(T("Set alias..."), null, delegate { SetAlias(path); });
                menu.Items.Add(T("Open in Explorer"), null, delegate { OpenExplorer(path); });
                if (_settings.Roots.Any(x => SamePath(x, path)))
                {
                    menu.Items.Add(CreateMoveRootMenu(path));
                    menu.Items.Add(T("Remove root"), null, delegate { RemoveRoot(path); });
                }
            }
            else if (File.Exists(path))
            {
                menu.Items.Add(T("Start / Open"), null, delegate { TreeDoubleClick(sender, e); });
                menu.Items.Add(T("Start with port..."), null, delegate { StartWithPort(path); });
                menu.Items.Add(T("Force new instance"), null, delegate { StartFileAsync(path, true, 0, true); });
                menu.Items.Add(T("Set alias..."), null, delegate { SetAlias(path); });
                menu.Items.Add(T("Rename..."), null, delegate { RenameBoard(path); });
                menu.Items.Add(T("Copy path"), null, delegate { Clipboard.SetText(path); });
                menu.Items.Add(T("Show in Explorer"), null, delegate { Process.Start("explorer.exe", "/select,\"" + path + "\""); });
            }
            menu.Show(_tree, e.Location);
        }

        private void ShowWorkspaceGroupMenu(string groupId, Point location)
        {
            var group = FindWorkspaceGroup(groupId);
            if (group == null) return;
            var menu = new ContextMenuStrip { Renderer = LightMenuRenderer.Shared };
            menu.Items.Add(T("Add workspace here..."), null, delegate { AddRoot(group.Id); });
            menu.Items.Add(T("New subgroup..."), null, delegate { NewWorkspaceGroup(group.Id); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(T("Rename group..."), null, delegate { RenameWorkspaceGroup(group.Id); });
            menu.Items.Add(CreateMoveGroupMenu(group.Id));
            menu.Items.Add(T("Delete virtual group"), null, delegate { DeleteWorkspaceGroup(group.Id); });
            menu.Show(_tree, location);
        }

        private ToolStripMenuItem CreateMoveRootMenu(string root)
        {
            string full = Path.GetFullPath(root);
            string currentGroup;
            _settings.WorkspaceRootGroups.TryGetValue(full, out currentGroup);
            var move = new ToolStripMenuItem(T("Move to group"));
            var top = new ToolStripMenuItem(T("Top level")) { Checked = string.IsNullOrEmpty(currentGroup) };
            top.Click += delegate { MoveRootToGroup(full, null); };
            move.DropDownItems.Add(top);
            foreach (var group in _settings.WorkspaceGroups)
            {
                var captured = group;
                var item = new ToolStripMenuItem(GetWorkspaceGroupPath(group.Id))
                {
                    Checked = string.Equals(currentGroup, group.Id, StringComparison.OrdinalIgnoreCase)
                };
                item.Click += delegate { MoveRootToGroup(full, captured.Id); };
                move.DropDownItems.Add(item);
            }
            return move;
        }

        private ToolStripMenuItem CreateMoveGroupMenu(string groupId)
        {
            var group = FindWorkspaceGroup(groupId);
            var descendants = WorkspaceGroupDescendants(groupId);
            var move = new ToolStripMenuItem(T("Move to group"));
            var top = new ToolStripMenuItem(T("Top level")) { Checked = group == null || string.IsNullOrEmpty(group.ParentId) };
            top.Click += delegate { MoveWorkspaceGroup(groupId, null); };
            move.DropDownItems.Add(top);
            foreach (var candidate in _settings.WorkspaceGroups.Where(x =>
                !string.Equals(x.Id, groupId, StringComparison.OrdinalIgnoreCase) && !descendants.Contains(x.Id)))
            {
                var captured = candidate;
                var item = new ToolStripMenuItem(GetWorkspaceGroupPath(candidate.Id))
                {
                    Checked = group != null && string.Equals(group.ParentId, candidate.Id, StringComparison.OrdinalIgnoreCase)
                };
                item.Click += delegate { MoveWorkspaceGroup(groupId, captured.Id); };
                move.DropDownItems.Add(item);
            }
            return move;
        }

        private WorkspaceGroup FindWorkspaceGroup(string id)
        {
            return _settings.WorkspaceGroups.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        private string GetWorkspaceGroupPath(string id)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = FindWorkspaceGroup(id);
            while (current != null && seen.Add(current.Id))
            {
                names.Insert(0, current.Name);
                current = string.IsNullOrEmpty(current.ParentId) ? null : FindWorkspaceGroup(current.ParentId);
            }
            return string.Join(" / ", names.ToArray());
        }

        private HashSet<string> WorkspaceGroupDescendants(string groupId)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Queue<string>();
            pending.Enqueue(groupId);
            while (pending.Count > 0)
            {
                string parent = pending.Dequeue();
                foreach (var child in _settings.WorkspaceGroups.Where(x =>
                    string.Equals(x.ParentId, parent, StringComparison.OrdinalIgnoreCase)))
                    if (result.Add(child.Id)) pending.Enqueue(child.Id);
            }
            return result;
        }

        private void NewWorkspaceGroup(string parentId)
        {
            string name = Prompt.Show(T("Group name:"), T("New virtual group"), T("New group"));
            if (string.IsNullOrWhiteSpace(name)) return;
            if (parentId != null && FindWorkspaceGroup(parentId) == null) parentId = null;
            _settings.WorkspaceGroups.Add(new WorkspaceGroup { Name = name.Trim(), ParentId = parentId });
            SettingsStore.Save(_settings);
            LoadRoots();
        }

        private void RenameWorkspaceGroup(string groupId)
        {
            var group = FindWorkspaceGroup(groupId);
            if (group == null) return;
            string name = Prompt.Show(T("Group name:"), T("Rename virtual group"), group.Name);
            if (string.IsNullOrWhiteSpace(name)) return;
            group.Name = name.Trim();
            SettingsStore.Save(_settings);
            LoadRoots();
        }

        private void MoveWorkspaceGroup(string groupId, string parentId)
        {
            var group = FindWorkspaceGroup(groupId);
            if (group == null || string.Equals(groupId, parentId, StringComparison.OrdinalIgnoreCase) ||
                WorkspaceGroupDescendants(groupId).Contains(parentId)) return;
            if (!string.IsNullOrEmpty(parentId) && FindWorkspaceGroup(parentId) == null) return;
            group.ParentId = string.IsNullOrEmpty(parentId) ? null : parentId;
            SettingsStore.Save(_settings);
            LoadRoots();
        }

        private void DeleteWorkspaceGroup(string groupId)
        {
            var group = FindWorkspaceGroup(groupId);
            if (group == null) return;
            if (MessageBox.Show(F("Delete virtual group '{0}'? Its workspaces and subgroups will move up one level. No local files will be changed.", group.Name),
                T("Delete virtual group"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (var child in _settings.WorkspaceGroups.Where(x => string.Equals(x.ParentId, group.Id, StringComparison.OrdinalIgnoreCase)))
                child.ParentId = group.ParentId;
            foreach (string root in _settings.WorkspaceRootGroups.Keys.ToList())
            {
                if (!string.Equals(_settings.WorkspaceRootGroups[root], group.Id, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrEmpty(group.ParentId)) _settings.WorkspaceRootGroups.Remove(root);
                else _settings.WorkspaceRootGroups[root] = group.ParentId;
            }
            _settings.WorkspaceGroups.Remove(group);
            SettingsStore.Save(_settings);
            LoadRoots();
        }

        private void MoveRootToGroup(string root, string groupId)
        {
            string full = Path.GetFullPath(root);
            if (string.IsNullOrEmpty(groupId)) _settings.WorkspaceRootGroups.Remove(full);
            else if (FindWorkspaceGroup(groupId) != null) _settings.WorkspaceRootGroups[full] = groupId;
            SettingsStore.Save(_settings);
            LoadRoots();
        }

        private void AddRoot() { AddRoot(null); }

        private void AddRoot(string groupId)
        {
            using (var dialog = new FolderBrowserDialog { Description = T("Choose a folder containing .excalidraw boards"), ShowNewFolderButton = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string full = Path.GetFullPath(dialog.SelectedPath);
                if (!_settings.Roots.Any(x => SamePath(x, full))) _settings.Roots.Add(full);
                if (string.IsNullOrEmpty(groupId)) _settings.WorkspaceRootGroups.Remove(full);
                else if (FindWorkspaceGroup(groupId) != null) _settings.WorkspaceRootGroups[full] = groupId;
                SettingsStore.Save(_settings);
                LoadRoots();
            }
        }

        private void OpenFile()
        {
            using (var dialog = new OpenFileDialog { Title = T("Open Excalidraw board"), Filter = T("Excalidraw board (*.excalidraw)|*.excalidraw"), Multiselect = false })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                RememberRecent(dialog.FileName);
                StartFileAsync(dialog.FileName, true, 0);
            }
        }

        private void RemoveRoot(string root)
        {
            _settings.Roots.RemoveAll(x => SamePath(x, root));
            try { _settings.WorkspaceRootGroups.Remove(Path.GetFullPath(root)); } catch { }
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
                Title = T("Create Excalidraw board"), Filter = T("Excalidraw board (*.excalidraw)|*.excalidraw"),
                AddExtension = true, DefaultExt = "excalidraw", InitialDirectory = directory
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ProcessService.CreateEmptyScene(dialog.FileName);
                    RememberRecent(dialog.FileName);
                    RefreshAll();
                    if (MessageBox.Show(T("Board created. Start it now?"), T("New board"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        StartFileAsync(dialog.FileName, true, 0);
                }
                catch (IOException) { MessageBox.Show(T("The file already exists."), T("New board"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                catch (Exception ex) { ShowError(ex); }
            }
        }

        private void NewFolder(string parent)
        {
            string name = Prompt.Show(T("Folder name:"), T("New folder"), T("New Folder"));
            if (string.IsNullOrWhiteSpace(name)) return;
            try { Directory.CreateDirectory(Path.Combine(parent, name.Trim())); RefreshAll(); }
            catch (Exception ex) { ShowError(ex); }
        }

        private TreeNode MakeFileNode(string file)
        {
            bool running = _instances.Any(x => SamePath(x.FilePath, file));
            string label = GetPathDisplayName(file);
            string iconKey = running ? "boardRunning" : "board";
            var child = new TreeNode((running ? "● " : "○ ") + label) { Tag = file, ToolTipText = file, ImageKey = iconKey, SelectedImageKey = iconKey };
            child.ForeColor = running ? UiPalette.Success : UiPalette.Text;
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
            string alias = Prompt.Show(T("Display alias (leave blank to clear):"),
                Directory.Exists(full) ? T("Folder alias") : T("Board alias"), existing ?? "");
            if (alias == null) return;
            if (string.IsNullOrWhiteSpace(alias)) _settings.Aliases.Remove(full); else _settings.Aliases[full] = alias.Trim();
            SettingsStore.Save(_settings);
            RefreshInstances();
            LoadRoots();
        }

        private void RenameBoard(string path)
        {
            if (_instances.Any(x => SamePath(x.FilePath, path)))
            {
                MessageBox.Show(T("Stop this board before renaming it."), T("Rename"), MessageBoxButtons.OK, MessageBoxIcon.Information); return;
            }
            string name = Prompt.Show(T("New file name:"), T("Rename board"), Path.GetFileName(path));
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
                foreach (string root in ExistingRoots()) FindFilesRecursive(root, query, results, 500);
                results = results.Distinct(StringComparer.OrdinalIgnoreCase).Take(500).ToList();
                _tree.Nodes.Clear();
                var node = new TreeNode(F("Search results: {0}  ({1})", query, results.Count)) { ImageKey = "folder", SelectedImageKey = "folder" };
                foreach (string file in results) node.Nodes.Add(MakeFileNode(file));
                _tree.Nodes.Add(node); node.Expand();
            }
            finally { Cursor = Cursors.Default; }
        }

        private void FindFilesRecursive(string directory, string query, List<string> results, int limit)
        {
            if (results.Count >= limit) return;
            try
            {
                foreach (string file in Directory.GetFiles(directory, "*.excalidraw"))
                {
                    string alias;
                    bool aliasMatch = _settings.Aliases.TryGetValue(Path.GetFullPath(file), out alias) &&
                        !string.IsNullOrWhiteSpace(alias) && alias.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (aliasMatch || Path.GetFileName(file).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) results.Add(file);
                    if (results.Count >= limit) return;
                }
                foreach (string child in Directory.GetDirectories(directory)) { FindFilesRecursive(child, query, results, limit); if (results.Count >= limit) return; }
            }
            catch { }
        }

        private void StartWithPort(string path)
        {
            string text = Prompt.Show(T("TCP port:"), T("Start with port"), ProcessService.FindFreePort(_settings.StartPort).ToString());
            int port;
            if (int.TryParse(text, out port) && port > 0 && port <= 65535) StartFileAsync(path, true, port, true);
        }

        private async void StartFileAsync(string path, bool open, int port, bool force)
        {
            try
            {
                var existing = _instances.FirstOrDefault(x => SamePath(x.FilePath, path));
                if (existing != null && !force) { if (open) OpenUrl(existing.Url); return; }
                _status.Text = F("Starting {0}...", Path.GetFileName(path));
                int target = port > 0 ? port : ProcessService.FindFreePort(_settings.StartPort);
                var instance = await Task.Run(() => ProcessService.Start(path, target, _settings.Theme));
                RememberRecent(path);
                RefreshInstances();
                _status.Text = F("Started PID {0} on port {1}", instance.Pid, instance.Port);
                if (open) OpenUrl(instance.Url);
            }
            catch (Exception ex) { ShowError(ex); _status.Text = T("Start failed"); }
        }

        private void StartFileAsync(string path, bool open, int port) { StartFileAsync(path, open, port, false); }

        private DrawingInstance SelectedInstance()
        {
            return _grid.CurrentRow == null ? null : _grid.CurrentRow.DataBoundItem as DrawingInstance;
        }

        private async void OpenFormulaEditor()
        {
            var selected = SelectedInstance();
            string boardUrl = selected == null ? _instances.Select(x => x.Url).FirstOrDefault(x => !string.IsNullOrEmpty(x)) : selected.Url;
            string language = Localization.Language;
            _status.Text = T("Starting formula editor...");
            try
            {
                string editorUrl = await Task.Run(() => FormulaEditorService.EnsureStarted(language, boardUrl));
                FormulaEditorService.OpenInDefaultBrowser(editorUrl);
                _status.Text = T("Formula editor opened");
            }
            catch (Exception ex)
            {
                ShowError(ex);
                _status.Text = T("Start failed");
            }
        }

        private async void OpenFormulaPalette()
        {
            var selected = SelectedInstance();
            string boardUrl = selected == null ? _instances.Select(x => x.Url).FirstOrDefault(x => !string.IsNullOrEmpty(x)) : selected.Url;
            string language = Localization.Language;
            _status.Text = T("Starting formula palette...");
            try
            {
                string editorUrl = await Task.Run(() => FormulaEditorService.EnsureStarted(language, boardUrl));
                await Task.Run(() => FormulaEditorService.OpenNativePalette(editorUrl));
                _status.Text = T("Formula palette opened and kept on top");
            }
            catch (Exception ex)
            {
                ShowError(ex);
                _status.Text = T("Start failed");
            }
        }

        private void OpenSelected() { var item = SelectedInstance(); if (item != null) OpenUrl(item.Url); }
        private void CopySelectedUrl() { var item = SelectedInstance(); if (item != null && !string.IsNullOrEmpty(item.Url)) Clipboard.SetText(item.Url); }

        private async void StopSelected()
        {
            var item = SelectedInstance();
            if (item == null) return;
            _status.Text = F("Stopping PID {0} (brief save grace period)...", item.Pid);
            await Task.Run(() => ProcessService.Stop(item.Pid));
            RefreshAll();
        }

        private async void StopAll()
        {
            if (MessageBox.Show(T("Stop all managed board and formula services?"), T("Stop all"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            _status.Text = T("Stopping all services...");
            int count = await Task.Run(() =>
            {
                int stoppedBoards = ProcessService.StopAllDiscovered();
                FormulaEditorService.Stop();
                return stoppedBoards;
            });
            _status.Text = F("Stopped {0} board service(s); formula services stopped", count);
            RefreshAll();
        }

        private async void RestartSelected()
        {
            var item = SelectedInstance();
            if (item == null || string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath)) return;
            string file = item.FilePath; int port = item.Port; string theme = item.Theme;
            _status.Text = F("Restarting PID {0}...", item.Pid);
            await Task.Run(() => ProcessService.Stop(item.Pid));
            try { await Task.Run(() => ProcessService.Start(file, port, theme)); RefreshAll(); OpenUrl("http://localhost:" + port); }
            catch (Exception ex) { ShowError(ex); RefreshAll(); }
        }

        private void RefreshAll() { RefreshInstances(); LoadRoots(); }

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
                _status.Text = F("{0} running instance(s)", _instances.Count);
                RebuildTrayInstances();
            }
            catch (Exception ex) { _status.Text = F("Refresh failed: {0}", ex.Message); }
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
            while (menu.Items.Count > 6) menu.Items.RemoveAt(1);
            int at = 1;
            foreach (var instance in _instances.Take(12))
            {
                var captured = instance;
                menu.Items.Insert(at++, new ToolStripMenuItem(F("Open {0}  :{1}", instance.FileName, instance.Port), null, delegate { OpenUrl(captured.Url); }));
            }
        }

        private void ShowEnvironment()
        {
            try
            {
                string nodeVersion = RunAndRead(ProcessService.NodePath, "--version").Trim();
                string ocrRoot = string.IsNullOrWhiteSpace(_settings.FormulaOcrRoot) ? T("Not configured") : _settings.FormulaOcrRoot;
                string ocrStatus = ManagedFormulaOcrService.IsInstalled(_settings.FormulaOcrRoot) ? T("Installed") : T("Not installed");
                MessageBox.Show("node.exe\r\n" + ProcessService.NodePath + "\r\n" + T("Version:") + " " + nodeVersion + "\r\n\r\nexcalidraw-edit\r\n" + ProcessService.CliPath + "\r\n" + T("Version:") + " " + ProcessService.CliVersion + "\r\n\r\n" + T("Managed runtime") + "\r\n" + ProcessService.ManagedServerPath + "\r\n\r\n" + T("Formula OCR folder:") + " " + ocrStatus + "\r\n" + ocrRoot + "\r\n\r\n" + T("Shared library") + "\r\n" + SettingsStore.SharedLibraryPath + "\r\n\r\n" + T("Settings") + "\r\n" + SettingsStore.FilePath,
                    T("Environment"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ShowLibraryManager()
        {
            using (var form = new Form { Width = 760, Height = 530, Text = T("Excalidraw Libraries"), StartPosition = FormStartPosition.CenterParent, MinimumSize = new Size(620, 420), BackColor = UiPalette.Background, Font = Font, Padding = new Padding(12, 8, 12, 12) })
            {
                try { form.Icon = Icon; } catch { }
                var title = new Label { Dock = DockStyle.Top, Height = 26, Text = T("SHARED LOCAL LIBRARY"), ForeColor = UiPalette.Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 0, 0), BackColor = Color.White };
                var path = new TextBox { Dock = DockStyle.Top, Height = 26, ReadOnly = true, Text = SettingsStore.SharedLibraryPath, BorderStyle = BorderStyle.FixedSingle, BackColor = UiPalette.SoftBackground, ForeColor = UiPalette.Muted };
                var summary = new Label { Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleLeft, ForeColor = UiPalette.Muted, Padding = new Padding(2, 0, 0, 0), BackColor = Color.White };
                var list = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true, BorderStyle = BorderStyle.None, BackColor = Color.White, ForeColor = UiPalette.Text, ItemHeight = 20 };
                var listFrame = CreateFrame(list);
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 84, Padding = new Padding(0, 12, 0, 0), AutoScroll = true, BackColor = Color.White };
                DateTime lastLibraryWrite = DateTime.MinValue;

                Action reload = delegate
                {
                    try
                    {
                        var items = LibraryStore.Load(SettingsStore.SharedLibraryPath);
                        lastLibraryWrite = File.Exists(SettingsStore.SharedLibraryPath) ? File.GetLastWriteTimeUtc(SettingsStore.SharedLibraryPath) : DateTime.MinValue;
                        list.Items.Clear();
                        for (int i = 0; i < items.Count; i++) list.Items.Add(LibraryStore.Describe(items[i], i));
                        summary.Text = F("{0} library item(s). Changes made in managed boards are saved here automatically.", items.Count);
                    }
                    catch (Exception ex) { summary.Text = F("Load failed: {0}", ex.Message); }
                };

                var import = MakeUiButton(T("Import / Merge..."), delegate
                {
                    using (var dialog = new OpenFileDialog { Filter = T("Excalidraw library (*.excalidrawlib)|*.excalidrawlib|JSON (*.json)|*.json"), Multiselect = false })
                    {
                        if (dialog.ShowDialog(form) != DialogResult.OK) return;
                        try
                        {
                            int count = LibraryStore.MergeFrom(dialog.FileName); reload();
                            MessageBox.Show(F("{0} item(s) are now stored locally. Refresh already-open board tabs to load the updated library.", count), T("Library imported"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex) { ShowError(ex); }
                    }
                }, UiButtonStyle.Primary);
                var replace = MakeUiButton(T("Replace..."), delegate
                {
                    using (var dialog = new OpenFileDialog { Filter = T("Excalidraw library (*.excalidrawlib)|*.excalidrawlib|JSON (*.json)|*.json"), Multiselect = false })
                    {
                        if (dialog.ShowDialog(form) != DialogResult.OK) return;
                        if (MessageBox.Show(T("Replace the complete shared local library?"), T("Replace library"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                        try { LibraryStore.Save(SettingsStore.SharedLibraryPath, LibraryStore.Load(dialog.FileName)); reload(); }
                        catch (Exception ex) { ShowError(ex); }
                    }
                });
                var export = MakeUiButton(T("Export..."), delegate
                {
                    using (var dialog = new SaveFileDialog { Filter = T("Excalidraw library (*.excalidrawlib)|*.excalidrawlib"), AddExtension = true, DefaultExt = "excalidrawlib", FileName = "shared.excalidrawlib" })
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
                var browse = MakeUiButton(T("Browse via active board"), delegate
                {
                    var active = SelectedInstance() ?? _instances.FirstOrDefault();
                    if (active == null || string.IsNullOrEmpty(active.Url))
                    {
                        MessageBox.Show(T("Start a board first. Public libraries must be opened from a running board so Excalidraw can generate the secure return token."), T("Browse libraries"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    OpenUrl(active.Url);
                    MessageBox.Show(T("In the board, open the Library panel and click 'Browse libraries'.\r\nChoose a library on the website, then click 'Add to Excalidraw'. It will return to this local board and be saved automatically."), T("Install a public library"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
                var folder = MakeUiButton(T("Open local folder"), delegate { Directory.CreateDirectory(SettingsStore.LibraryDirectory); OpenExplorer(SettingsStore.LibraryDirectory); });
                var clear = MakeUiButton(T("Clear"), delegate
                {
                    if (MessageBox.Show(T("Clear every item from the shared local library?\r\nOpen board tabs must be refreshed afterward."), T("Clear library"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    try { LibraryStore.Save(SettingsStore.SharedLibraryPath, new object[0]); reload(); }
                    catch (Exception ex) { ShowError(ex); }
                }, UiButtonStyle.Danger);
                buttons.Controls.AddRange(new Control[] { import, replace, export, browse, folder, clear });
                form.Controls.Add(listFrame); form.Controls.Add(summary); form.Controls.Add(path); form.Controls.Add(title); form.Controls.Add(buttons);
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
            bool restartForLanguage = false;
            bool restartFormulaServices = false;
            string selectedLanguage = _settings.Language;
            using (var form = new Form { Width = 580, Height = 348, Text = T("Settings"), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, BackColor = UiPalette.Background, Font = Font })
            {
                var portLabel = new Label { Left = 18, Top = 24, Width = 150, Text = T("Starting port:"), ForeColor = UiPalette.Text };
                var port = new NumericUpDown { Left = 175, Top = 20, Width = 355, Minimum = 1, Maximum = 65535, Value = Math.Max(1, Math.Min(65535, _settings.StartPort)), BackColor = Color.White, ForeColor = UiPalette.Text };
                var themeLabel = new Label { Left = 18, Top = 64, Width = 150, Text = T("Board theme:"), ForeColor = UiPalette.Text };
                var theme = new ComboBox { Left = 175, Top = 60, Width = 355, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.White, ForeColor = UiPalette.Text, FlatStyle = FlatStyle.Flat };
                var themeChoices = new[] { new LocalizedChoice("system", T("system")), new LocalizedChoice("dark", T("dark")), new LocalizedChoice("light", T("light")) };
                theme.Items.AddRange(themeChoices); theme.SelectedItem = themeChoices.FirstOrDefault(x => x.Value == _settings.Theme) ?? themeChoices[0];
                var languageLabel = new Label { Left = 18, Top = 104, Width = 150, Text = T("Interface language:"), ForeColor = UiPalette.Text };
                var language = new ComboBox { Left = 175, Top = 100, Width = 355, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.White, ForeColor = UiPalette.Text, FlatStyle = FlatStyle.Flat };
                var languageChoices = new[] { new LocalizedChoice("system", T("Follow system")), new LocalizedChoice("zh-CN", T("Simplified Chinese")), new LocalizedChoice("en", T("English")) };
                language.Items.AddRange(languageChoices); language.SelectedItem = languageChoices.FirstOrDefault(x => x.Value == _settings.Language) ?? languageChoices[0];
                var ocrEnabled = new CheckBox { Left = 18, Top = 145, Width = 512, Text = T("Automatically start local formula OCR when installed"), Checked = _settings.FormulaOcrEnabled, ForeColor = UiPalette.Text };
                var ocrRootLabel = new Label { Left = 18, Top = 182, Width = 150, Text = T("Formula OCR folder:"), ForeColor = UiPalette.Text };
                var ocrRoot = new TextBox { Left = 175, Top = 178, Width = 280, Text = _settings.FormulaOcrRoot ?? "", BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, ForeColor = UiPalette.Text };
                var browseOcr = new UiButton { Left = 463, Top = 177, Width = 67, Height = 26, Text = T("Browse..."), Style = UiButtonStyle.Default };
                browseOcr.Click += delegate
                {
                    using (var dialog = new FolderBrowserDialog { Description = T("Choose the D-drive folder containing the local formula OCR environment"), ShowNewFolderButton = true, SelectedPath = Directory.Exists(ocrRoot.Text) ? ocrRoot.Text : "" })
                        if (dialog.ShowDialog(form) == DialogResult.OK) ocrRoot.Text = dialog.SelectedPath;
                };
                var ocrHint = new Label { Left = 175, Top = 209, Width = 355, Height = 34, ForeColor = UiPalette.Muted, Text = T("Models, Python packages, caches and temporary files stay under this folder.") };
                var ok = new UiButton { Text = T("Save"), Style = UiButtonStyle.Primary, Left = 366, Top = 258, Width = 80, Height = 30, DialogResult = DialogResult.OK };
                var cancel = new UiButton { Text = T("Cancel"), Style = UiButtonStyle.Default, Left = 454, Top = 258, Width = 80, Height = 30, DialogResult = DialogResult.Cancel };
                form.Controls.AddRange(new Control[] { portLabel, port, themeLabel, theme, languageLabel, language, ocrEnabled, ocrRootLabel, ocrRoot, browseOcr, ocrHint, ok, cancel }); form.AcceptButton = ok; form.CancelButton = cancel;
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    _settings.StartPort = (int)port.Value;
                    _settings.Theme = ((LocalizedChoice)theme.SelectedItem).Value;
                    selectedLanguage = ((LocalizedChoice)language.SelectedItem).Value;
                    restartForLanguage = !string.Equals(selectedLanguage, _settings.Language, StringComparison.OrdinalIgnoreCase);
                    string selectedOcrRoot = (ocrRoot.Text ?? "").Trim();
                    restartFormulaServices = _settings.FormulaOcrEnabled != ocrEnabled.Checked ||
                        !string.Equals(_settings.FormulaOcrRoot ?? "", selectedOcrRoot, StringComparison.OrdinalIgnoreCase);
                    _settings.FormulaOcrEnabled = ocrEnabled.Checked;
                    _settings.FormulaOcrRoot = selectedOcrRoot;
                    _settings.Language = selectedLanguage;
                    SettingsStore.Save(_settings);
                    if (restartFormulaServices)
                    {
                        FormulaEditorService.Stop();
                        FormulaEditorService.ConfigureOcr(_settings.FormulaOcrEnabled, _settings.FormulaOcrRoot);
                    }
                }
            }
            if (restartForLanguage)
            {
                Localization.Configure(selectedLanguage);
                MessageBox.Show(T("The interface will restart to apply the new language. Running board services will stay open."), T("Language changed"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                RestartRequested = true;
                _reallyExit = true;
                _tray.Visible = false;
                Close();
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
        private static void ShowError(Exception ex) { MessageBox.Show(ex.Message, T("Excalidraw Manager"), MessageBoxButtons.OK, MessageBoxIcon.Error); }

        private static string T(string text) { return Localization.T(text); }
        private static string F(string text, params object[] args) { return Localization.F(text, args); }
    }

    public static class Prompt
    {
        public static string Show(string text, string caption, string value)
        {
            using (var form = new Form { Width = 440, Height = 172, Text = caption, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, BackColor = UiPalette.Background })
            {
                form.Font = new Font("Segoe UI", 9f);
                var label = new Label { Left = 14, Top = 14, Width = 396, Text = text, ForeColor = UiPalette.Text };
                var input = new TextBox { Left = 14, Top = 40, Width = 396, Text = value, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, ForeColor = UiPalette.Text };
                var ok = new UiButton { Text = Localization.T("OK"), Style = UiButtonStyle.Primary, Left = 238, Width = 82, Height = 30, Top = 88, DialogResult = DialogResult.OK };
                var cancel = new UiButton { Text = Localization.T("Cancel"), Style = UiButtonStyle.Default, Left = 328, Width = 82, Height = 30, Top = 88, DialogResult = DialogResult.Cancel };
                form.Controls.AddRange(new Control[] { label, input, ok, cancel });
                form.AcceptButton = ok; form.CancelButton = cancel;
                return form.ShowDialog() == DialogResult.OK ? input.Text : null;
            }
        }
    }
}
