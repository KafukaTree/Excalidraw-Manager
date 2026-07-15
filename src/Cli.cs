using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Text.RegularExpressions;
using ExcalidrawManager;

[assembly: AssemblyTitle("Excalidraw Manager CLI")]
[assembly: AssemblyDescription("Command-line companion for Excalidraw Manager")]
[assembly: AssemblyProduct("Excalidraw Manager")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Excalidraw Manager contributors")]
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]

internal static class ExcalidrawManagerCli
{
    private static int Main(string[] args)
    {
        Localization.Configure("system");
        if (args.Length == 0 || string.Equals(args[0], "help", StringComparison.OrdinalIgnoreCase) || args[0] == "--help" || args[0] == "-h")
        {
            Console.WriteLine("Excalidraw Manager 0.2.0");
            Console.WriteLine(Localization.T("Usage:"));
            Console.WriteLine("  excalidraw-manager                         " + Localization.T("Open the GUI"));
            Console.WriteLine("  excalidraw-manager <board.excalidraw>      " + Localization.T("Open a board through the GUI"));
            Console.WriteLine("  excalidraw-manager list                    " + Localization.T("List running instances"));
            Console.WriteLine("  excalidraw-manager stop-all                " + Localization.T("Stop all excalidraw-edit Node processes"));
            return 0;
        }

        if (args[0] == "--version" || args[0] == "-V") { Console.WriteLine("0.2.0"); return 0; }

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
            Console.WriteLine(Localization.F("{0} instance(s)", count));
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
            Console.WriteLine(Localization.F("Stopped {0} instance(s)", count));
            return 0;
        }

        Console.Error.WriteLine(Localization.F("Unknown command: {0}", string.Join(" ", args)));
        return 2;
    }

    private static bool IsExcalidrawProcess(string command)
    {
        return command.IndexOf("excalidraw-edit", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (command.IndexOf("ExcalidrawManager", StringComparison.OrdinalIgnoreCase) >= 0 &&
             command.IndexOf("server.mjs", StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
