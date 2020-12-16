﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;

namespace VoiDPlugins.Tool
{
    [PluginName("OEM Kill"), SupportedPlatform(PluginPlatform.Windows)]
    public class OemKill : ITool
    {
        public bool Initialize()
        {
            Terminate();
            return true;
        }

        [Action("Driver", "Kill OEM Drivers"), ToolTip("Terminate known OEM Drivers")]
        public static void Terminate()
        {
            var processList = Process.GetProcesses();
            var oemProcesses = from process in processList
                               where OemData.OemProcesses.Contains(process.ProcessName)
                               select process;

            if (oemProcesses.ToList().Count == 0)
            {
                Log.Write("OemKill", "No oem process found");
                return;
            }

            foreach (var process in oemProcesses)
            {
                try
                {
                    process.Kill();
                    Log.Write("OemKill", "Killing " + process.ProcessName);
                }
                catch (Exception e)
                {
                    Log.Write("OemKill", "Failed. Reason: " + e.Message, LogLevel.Error);
                    return;
                }
            }

            Log.Write("OemKill", "Oem process killed successfully");
        }

        [Action("Driver", "Restore Driver Defaults"), ToolTip("Fix issues with remnant drivers from Huion and Gaomon")]
        public static void RestoreDriver()
        {
            RunRestore(false);
        }

        [Action("Driver", "Simulate 'Restore Driver Defaults'"), ToolTip("Simulates driver restoration without actually doing any driver related action, and opens assets folder for inspection")]
        public static void SimulateRestoreDriver()
        {
            RunRestore(true);
        }

        private static void RunRestore(bool simulate)
        {
            var location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var assets = Path.Join(location, "assets");
            if (!Directory.Exists(assets))
            {
                Directory.CreateDirectory(assets);
            }

            RunEnum(assets, out var enumOutput);

            List<IndexedInf> oemDrivers = new List<IndexedInf>();

            for (int i = 0; i < enumOutput.Length; i++)
            {
                var infMatch = inf.Match(enumOutput[i]);
                if (infMatch.Success)
                {
                    var infName = infMatch.Groups[1].Value;
                    var driverName = driver.Match(enumOutput[i + 1]).Groups[1].Value;
                    if (OemData.OemDrivers.Contains(driverName))
                    {
                        Log.Write("OemKill", $"Found '{driverName}' with driver '{infName}'");
                        oemDrivers.Add(new IndexedInf(i, driverName, infName));
                    }
                }
            }

            if (oemDrivers.Count == 0)
            {
                Log.Write("OemKill", "No remnant driver found");
                if (simulate)
                {
                    Log.Write("OemKill", $"You may now inspect generated scripts on: '{assets}'");

                    var startInfo = new ProcessStartInfo("explorer", $"{assets}");
                    Process.Start(startInfo);
                }
                return;
            }

            var restoreHelperScript = Path.Join(assets, "restore.bat");
            using (var restoreHelperStream = File.CreateText(restoreHelperScript))
            {
                restoreHelperStream.WriteLine("@echo off");
                restoreHelperStream.WriteLine("echo WARNING! OTD will be closed after you press any key.");
                restoreHelperStream.WriteLine("pause");
                restoreHelperStream.WriteLine("taskkill /F /IM OpenTabletDriver.UX.Wpf.exe");
                restoreHelperStream.WriteLine("taskkill /F /IM OpenTabletDriver.Daemon.exe");
                restoreHelperStream.WriteLine("echo Script auto-generated by OemKill OTD plugin");
                foreach (var oemDriver in oemDrivers)
                {
                    restoreHelperStream.WriteLine($"echo Uninstalling '{oemDriver.name}' with driver '{oemDriver.inf}'...");
                    restoreHelperStream.WriteLine($"pnputil -f -d {oemDriver.inf}");
                }
                restoreHelperStream.WriteLine("echo Installing default driver...");
                restoreHelperStream.WriteLine($"pnputil -i -a %SystemRoot%/INF/input.inf");
                restoreHelperStream.WriteLine("echo Driver restore done! You may now start OTD again");
            }

            if (simulate)
            {
                Log.Write("OemKill", $"You may now inspect generated scripts on: '{assets}'");

                var startInfo = new ProcessStartInfo("explorer", $"{assets}");
                Process.Start(startInfo);
            }
            else
            {
                Terminate();
                Call(restoreHelperScript, true);
            }
        }

        private static void RunEnum(string assetsLocation, out string[] output)
        {
            var enumHelperScript = Path.Join(assetsLocation, "enumerator.bat");
            var enumHelperOutput = Path.Join(assetsLocation, "drivers.txt");

            using (var enumHelperStream = File.CreateText(enumHelperScript))
            {
                enumHelperStream.WriteLine(":: Script auto-generated by OemKill OTD plugin");
                enumHelperStream.WriteLine($"pnputil -e > \"{enumHelperOutput}\"");
            }

            Log.Write("OemKill", "Enumerating oem driver installations");
            Call(enumHelperScript);

            while (!File.Exists(enumHelperOutput))
            {
                Thread.Sleep(100);
            }

            bool read = false;
            while (!read)
            {
                try
                {
                    output = File.ReadAllLines(enumHelperOutput);
                    read = true;
                    return;
                } catch { }
            }

            output = null;
        }

        private static void Call(string file, bool asAdmin = false)
        {
            Log.Write("OemKill", $"    Calling script: '{file}'");

            var info = new ProcessStartInfo("powershell")
            {
                CreateNoWindow = true,
                Arguments = asAdmin ? $"-c start -verb runas {file}" : $"-c start {file}"
            };
            var process = Process.Start(info);
            process.WaitForExit();
        }

        private static readonly Regex inf = new Regex(":\\s*(.*\\.inf)$", RegexOptions.Compiled);
        private static readonly Regex driver = new Regex(@":\s*(.*)$", RegexOptions.Compiled);

        public void Dispose() { }
    }
}