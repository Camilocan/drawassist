using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace DrawAssist.Core
{
    public class PhotoshopDetector
    {
        public static List<PhotoshopInstall> FindInstalled()
        {
            var results = new List<PhotoshopInstall>();

            // Search common install paths
            var searchRoots = new[]
            {
                @"C:\Program Files\Adobe",
                @"C:\Program Files (x86)\Adobe"
            };

            foreach (var root in searchRoots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var dir in Directory.GetDirectories(root, "Adobe Photoshop*"))
                {
                    var exe = Path.Combine(dir, "Photoshop.exe");
                    if (File.Exists(exe))
                    {
                        var version = FileVersionInfo.GetVersionInfo(exe);
                        results.Add(new PhotoshopInstall
                        {
                            ExePath = exe,
                            DisplayName = Path.GetFileName(dir),
                            Version = version.FileVersion ?? "Unknown"
                        });
                    }
                }
            }

            return results;
        }

        public static bool IsForegroundWindowPhotoshop(List<string> targetPaths, bool allVersions)
        {
            var hwnd = WinApi.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;

            WinApi.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return false;

            try
            {
                var proc = Process.GetProcessById((int)pid);
                if (!proc.ProcessName.Equals("Photoshop", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (allVersions) return true;

                var exePath = proc.MainModule?.FileName ?? "";
                return targetPaths.Any(t =>
                    string.Equals(t, exePath, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
    }

    public class PhotoshopInstall
    {
        public string ExePath { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Version { get; set; } = "";
        public override string ToString() => $"{DisplayName} (v{Version})";
    }
}
