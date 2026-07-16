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
                if (result.FormulaOcrRoot == null) result.FormulaOcrRoot = "";
                result.Language = Localization.NormalizePreference(result.Language);
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

        public static void Stop()
        {
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
            Font = new Font("Segoe UI", 9F);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            BuildUi();
            BuildTray();
            Load += OnLoaded;
            FormClosing += OnFormClosing;
            FormClosed += delegate
            {
                _refreshTimer.Stop();
                FormulaEditorService.Stop();
                _tray.Dispose();
            };
        }

        private void BuildUi()
        {
            var tool = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6, 3, 6, 3) };
            tool.Items.Add(MakeButton(T("Add root"), delegate { AddRoot(); }));
            tool.Items.Add(MakeButton(T("Open file"), delegate { OpenFile(); }));
            tool.Items.Add(MakeButton(T("New board"), delegate { NewBoard(); }));
            tool.Items.Add(MakeButton(T("Formula Editor"), delegate { OpenFormulaEditor(); }));
            tool.Items.Add(MakeButton(T("Refresh"), delegate { RefreshAll(); }));
            tool.Items.Add(new ToolStripSeparator());
            tool.Items.Add(new ToolStripLabel(T("Search:")));
            _search.AutoSize = false;
            _search.Width = 180;
            _search.ToolTipText = T("Search all workspace roots; press Enter");
            _search.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { SearchFiles(_search.Text); e.SuppressKeyPress = true; } };
            tool.Items.Add(_search);
            tool.Items.Add(new ToolStripSeparator());
            tool.Items.Add(MakeButton(T("Settings"), delegate { ShowSettings(); }));
            tool.Items.Add(MakeButton(T("Libraries"), delegate { ShowLibraryManager(); }));
            tool.Items.Add(MakeButton(T("Environment"), delegate { ShowEnvironment(); }));
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

            var leftTitle = new Label { Text = T("WORKSPACES"), Dock = DockStyle.Top, Height = 26, ForeColor = Color.DimGray };
            _tree.Dock = DockStyle.Fill;
            _tree.HideSelection = false;
            _tree.ShowNodeToolTips = true;
            _tree.BeforeExpand += TreeBeforeExpand;
            _tree.NodeMouseDoubleClick += TreeDoubleClick;
            _tree.NodeMouseClick += TreeMouseClick;
            split.Panel1.Controls.Add(_tree);
            split.Panel1.Controls.Add(leftTitle);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 7, 0, 0) };
            actions.Controls.Add(MakeUiButton(T("Open"), delegate { OpenSelected(); }));
            actions.Controls.Add(MakeUiButton(T("Copy URL"), delegate { CopySelectedUrl(); }));
            actions.Controls.Add(MakeUiButton(T("Restart"), delegate { RestartSelected(); }));
            actions.Controls.Add(MakeUiButton(T("Stop"), delegate { StopSelected(); }));
            actions.Controls.Add(MakeUiButton(T("Stop all"), delegate { StopAll(); }));

            _status.Dock = DockStyle.Bottom;
            _status.Height = 24;
            _status.ForeColor = Color.DimGray;
            _status.TextAlign = ContentAlignment.MiddleLeft;

            ConfigureGrid();
            var rightTitle = new Label { Text = T("RUNNING INSTANCES"), Dock = DockStyle.Top, Height = 26, ForeColor = Color.DimGray };
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
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = T("File"), DataPropertyName = "DisplayName", Width = 165 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PID", DataPropertyName = "Pid", Width = 65 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = T("Port"), DataPropertyName = "Port", Width = 60 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "URL", DataPropertyName = "Url", Width = 145 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = T("Source"), DataPropertyName = "Source", Width = 75 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = T("Theme"), DataPropertyName = "Theme", Width = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = T("Started"), DataPropertyName = "StartedAt", Width = 125, DefaultCellStyle = new DataGridViewCellStyle { Format = "MM-dd HH:mm:ss" } });
            _grid.CellDoubleClick += delegate { OpenSelected(); };
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

        private void BuildTray()
        {
            _tray.Icon = Icon ?? SystemIcons.Application;
            _tray.Text = T("Excalidraw Manager");
            _tray.Visible = true;
            _tray.DoubleClick += delegate { RestoreWindow(); };
            var menu = new ContextMenuStrip();
            menu.Items.Add(T("Show"), null, delegate { RestoreWindow(); });
            menu.Items.Add(T("Formula Editor"), null, delegate { OpenFormulaEditor(); });
            menu.Items.Add(T("Stop all services"), null, delegate { StopAll(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(T("Exit..."), null, delegate { ExitFromTray(); });
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
            _tree.Nodes.Clear();
            var recent = new TreeNode(T("Recent boards")) { Name = "__recent" };
            foreach (string file in _settings.RecentFiles.Where(File.Exists).Take(20)) recent.Nodes.Add(MakeFileNode(file));
            if (recent.Nodes.Count > 0) { _tree.Nodes.Add(recent); recent.Expand(); }
            foreach (string root in _settings.Roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase)) AddRootNode(root);
        }

        private void AddRootNode(string root)
        {
            var node = new TreeNode(Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar)) + "  [" + root + "]") { Tag = root, ToolTipText = root };
            node.Nodes.Add(new TreeNode(T("Loading...")));
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
                menu.Items.Add(T("New board here"), null, delegate { NewBoard(path); });
                menu.Items.Add(T("New folder..."), null, delegate { NewFolder(path); });
                menu.Items.Add(T("Refresh"), null, delegate { PopulateDirectory(e.Node); });
                menu.Items.Add(T("Open in Explorer"), null, delegate { OpenExplorer(path); });
                if (_settings.Roots.Any(x => SamePath(x, path))) menu.Items.Add(T("Remove root"), null, delegate { RemoveRoot(path); });
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

        private void AddRoot()
        {
            using (var dialog = new FolderBrowserDialog { Description = T("Choose a folder containing .excalidraw boards"), ShowNewFolderButton = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!_settings.Roots.Any(x => SamePath(x, dialog.SelectedPath))) _settings.Roots.Add(dialog.SelectedPath);
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
            string alias;
            string label = _settings.Aliases.TryGetValue(Path.GetFullPath(file), out alias) && !string.IsNullOrWhiteSpace(alias)
                ? alias + "  (" + Path.GetFileName(file) + ")" : Path.GetFileName(file);
            var child = new TreeNode((running ? "\u25cf " : "\u25cb ") + label) { Tag = file, ToolTipText = file };
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
            string alias = Prompt.Show(T("Display alias (leave blank to clear):"), T("Board alias"), existing ?? "");
            if (alias == null) return;
            if (string.IsNullOrWhiteSpace(alias)) _settings.Aliases.Remove(full); else _settings.Aliases[full] = alias.Trim();
            SettingsStore.Save(_settings); RefreshAll();
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
                foreach (string root in _settings.Roots.Where(Directory.Exists)) FindFilesRecursive(root, query, results, 500);
                _tree.Nodes.Clear();
                var node = new TreeNode(F("Search results: {0}  ({1})", query, results.Count));
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
            while (menu.Items.Count > 5) menu.Items.RemoveAt(1);
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
            using (var form = new Form { Width = 760, Height = 530, Text = T("Excalidraw Libraries"), StartPosition = FormStartPosition.CenterParent, MinimumSize = new Size(620, 420) })
            {
                try { form.Icon = Icon; } catch { }
                var title = new Label { Dock = DockStyle.Top, Height = 28, Text = T("SHARED LOCAL LIBRARY"), ForeColor = Color.DimGray };
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
                });
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
            bool restartForLanguage = false;
            bool restartFormulaServices = false;
            string selectedLanguage = _settings.Language;
            using (var form = new Form { Width = 580, Height = 340, Text = T("Settings"), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false })
            {
                var portLabel = new Label { Left = 18, Top = 22, Width = 150, Text = T("Starting port:") };
                var port = new NumericUpDown { Left = 175, Top = 18, Width = 355, Minimum = 1, Maximum = 65535, Value = Math.Max(1, Math.Min(65535, _settings.StartPort)) };
                var themeLabel = new Label { Left = 18, Top = 62, Width = 150, Text = T("Board theme:") };
                var theme = new ComboBox { Left = 175, Top = 58, Width = 355, DropDownStyle = ComboBoxStyle.DropDownList };
                var themeChoices = new[] { new LocalizedChoice("system", T("system")), new LocalizedChoice("dark", T("dark")), new LocalizedChoice("light", T("light")) };
                theme.Items.AddRange(themeChoices); theme.SelectedItem = themeChoices.FirstOrDefault(x => x.Value == _settings.Theme) ?? themeChoices[0];
                var languageLabel = new Label { Left = 18, Top = 102, Width = 150, Text = T("Interface language:") };
                var language = new ComboBox { Left = 175, Top = 98, Width = 355, DropDownStyle = ComboBoxStyle.DropDownList };
                var languageChoices = new[] { new LocalizedChoice("system", T("Follow system")), new LocalizedChoice("zh-CN", T("Simplified Chinese")), new LocalizedChoice("en", T("English")) };
                language.Items.AddRange(languageChoices); language.SelectedItem = languageChoices.FirstOrDefault(x => x.Value == _settings.Language) ?? languageChoices[0];
                var ocrEnabled = new CheckBox { Left = 18, Top = 143, Width = 512, Text = T("Automatically start local formula OCR when installed"), Checked = _settings.FormulaOcrEnabled };
                var ocrRootLabel = new Label { Left = 18, Top = 180, Width = 150, Text = T("Formula OCR folder:") };
                var ocrRoot = new TextBox { Left = 175, Top = 176, Width = 280, Text = _settings.FormulaOcrRoot ?? "" };
                var browseOcr = new Button { Left = 463, Top = 175, Width = 67, Height = 25, Text = T("Browse...") };
                browseOcr.Click += delegate
                {
                    using (var dialog = new FolderBrowserDialog { Description = T("Choose the D-drive folder containing the local formula OCR environment"), ShowNewFolderButton = true, SelectedPath = Directory.Exists(ocrRoot.Text) ? ocrRoot.Text : "" })
                        if (dialog.ShowDialog(form) == DialogResult.OK) ocrRoot.Text = dialog.SelectedPath;
                };
                var ocrHint = new Label { Left = 175, Top = 207, Width = 355, Height = 34, ForeColor = Color.DimGray, Text = T("Models, Python packages, caches and temporary files stay under this folder.") };
                var ok = new Button { Text = T("Save"), Left = 370, Top = 255, Width = 75, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = T("Cancel"), Left = 455, Top = 255, Width = 75, DialogResult = DialogResult.Cancel };
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
                var ok = new Button { Text = Localization.T("OK"), Left = 250, Width = 75, Top = 72, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = Localization.T("Cancel"), Left = 332, Width = 75, Top = 72, DialogResult = DialogResult.Cancel };
                form.Controls.AddRange(new Control[] { label, input, ok, cancel });
                form.AcceptButton = ok; form.CancelButton = cancel;
                return form.ShowDialog() == DialogResult.OK ? input.Text : null;
            }
        }
    }
}
