using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Text.RegularExpressions;

[assembly: AssemblyTitle("Excalidraw Manager CLI")]
[assembly: AssemblyDescription("Command-line companion for Excalidraw Manager")]
[assembly: AssemblyProduct("Excalidraw Manager")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Excalidraw Manager contributors")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

internal static class ExcalidrawManagerCli
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || string.Equals(args[0], "help", StringComparison.OrdinalIgnoreCase) || args[0] == "--help" || args[0] == "-h")
        {
            Console.WriteLine("Excalidraw Manager 0.1.0");
            Console.WriteLine("Usage:");
            Console.WriteLine("  excalidraw-manager                         Open the GUI");
            Console.WriteLine("  excalidraw-manager <board.excalidraw>      Open a board through the GUI");
            Console.WriteLine("  excalidraw-manager list                    List running instances");
            Console.WriteLine("  excalidraw-manager stop-all                Stop all excalidraw-edit Node processes");
            return 0;
        }

        if (args[0] == "--version" || args[0] == "-V") { Console.WriteLine("0.1.0"); return 0; }

        if (string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase))
        {
            int count = 0;
            using (var searcher = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='node.exe'"))
            foreach (ManagementObject row in searcher.Get())
            {
                string command = Convert.ToString(row["CommandLine"]);
                if (!IsExcalidrawProcess(command)) continue;
                Console.WriteLine("PID {0}  {1}", row["ProcessId"], command);
                count++;
            }
            Console.WriteLine("{0} instance(s)", count);
            return 0;
        }

        if (string.Equals(args[0], "stop-all", StringComparison.OrdinalIgnoreCase))
        {
            int count = 0;
            using (var searcher = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='node.exe'"))
            foreach (ManagementObject row in searcher.Get())
            {
                string command = Convert.ToString(row["CommandLine"]);
                if (!IsExcalidrawProcess(command)) continue;
                try
                {
                    var process = Process.GetProcessById(Convert.ToInt32((uint)row["ProcessId"]));
                    process.Kill(); process.WaitForExit(3000); count++;
                }
                catch { }
            }
            Console.WriteLine("Stopped {0} instance(s)", count);
            return 0;
        }

        Console.Error.WriteLine("Unknown command: " + string.Join(" ", args));
        return 2;
    }

    private static bool IsExcalidrawProcess(string command)
    {
        return command.IndexOf("excalidraw-edit", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (command.IndexOf("ExcalidrawManager", StringComparison.OrdinalIgnoreCase) >= 0 &&
             command.IndexOf("server.mjs", StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
