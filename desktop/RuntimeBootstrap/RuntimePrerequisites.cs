using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace PanelDeck.RuntimeSetup
{
    // Shared with the .NET Framework bootstrapper: do not use modern runtime APIs here.
    public sealed class RuntimeStatus
    {
        public string Root = "";
        public string Core = "";
        public string Desktop = "";
        public string AspNet = "";
        public bool SelfContained;
        public bool SharedReady { get { return Core.Length > 0 && Desktop.Length > 0 && AspNet.Length > 0; } }
        public bool CanLaunch { get { return SelfContained || SharedReady; } }
        public bool NeedsDesktop { get { return Core.Length == 0 || Desktop.Length == 0; } }
        public bool NeedsAspNet { get { return AspNet.Length == 0; } }
        public string Summary { get { return "桌面运行时 · " + Display(Desktop) + Environment.NewLine + "ASP.NET Core · " + Display(AspNet) + Environment.NewLine + ".NET 基础运行时 · " + Display(Core); } }
        private static string Display(string value) { return value.Length == 0 ? "缺少 x64 .NET 10" : value + " · x64"; }
    }

    public static class RuntimePrerequisites
    {
        public static RuntimeStatus Inspect(string appDirectory, string rootOverride = "")
        {
            bool bundled = File.Exists(Path.Combine(appDirectory, "coreclr.dll")) &&
                File.Exists(Path.Combine(appDirectory, "System.Windows.Forms.dll")) &&
                File.Exists(Path.Combine(appDirectory, "Microsoft.AspNetCore.dll"));
            RuntimeStatus best = new RuntimeStatus { SelfContained = bundled };
            foreach (string root in rootOverride.Length == 0 ? Roots() : new[] { rootOverride })
            {
                if (!IsX64Host(Path.Combine(root, "dotnet.exe"))) continue;
                var status = new RuntimeStatus {
                    Root = root, SelfContained = bundled,
                    Core = FindVersion(root, "Microsoft.NETCore.App", "coreclr.dll"),
                    Desktop = FindVersion(root, "Microsoft.WindowsDesktop.App", "System.Windows.Forms.dll"),
                    AspNet = FindVersion(root, "Microsoft.AspNetCore.App", "Microsoft.AspNetCore.dll")
                };
                if (status.SharedReady) return status;
                if (best.Root.Length == 0) best = status;
            }
            return best;
        }
        private static IEnumerable<string> Roots()
        {
            var roots = new List<string>();
            foreach (string variable in new[] { "DOTNET_ROOT_X64", "DOTNET_ROOT" }) {
                string value = Environment.GetEnvironmentVariable(variable) ?? "";
                if (!string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value)) roots.Add(value);
            }
            roots.Add(SystemRoot());
            return roots;
        }
        public static string SystemRoot()
        {
            try {
                using (var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (var installed = key.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions\x64")) {
                    string location = installed == null ? "" : installed.GetValue("InstallLocation") as string ?? "";
                    if (!string.IsNullOrWhiteSpace(location)) return location;
                }
            } catch (System.Security.SecurityException) { } catch (UnauthorizedAccessException) { }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
        }
        private static string FindVersion(string root, string framework, string requiredFile)
        {
            string directory = Path.Combine(root, "shared", framework);
            Version best = new Version(0, 0);
            try {
                if (!Directory.Exists(directory)) return "";
                foreach (string candidate in Directory.GetDirectories(directory)) {
                    try {
                        var version = new Version(Path.GetFileName(candidate));
                        if (version.Major == 10 && version.Minor == 0 && File.Exists(Path.Combine(candidate, requiredFile)) && version > best) best = version;
                    } catch (ArgumentException) { } catch (FormatException) { } catch (OverflowException) { }
                }
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return best.Major == 0 ? "" : best.ToString();
        }
        private static bool IsX64Host(string path)
        {
            try {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream)) {
                    if (stream.Length < 64 || reader.ReadUInt16() != 0x5a4d) return false;
                    stream.Position = 0x3c;
                    uint offset = reader.ReadUInt32();
                    if (offset > stream.Length - 6) return false;
                    stream.Position = offset;
                    return reader.ReadUInt32() == 0x4550 && reader.ReadUInt16() == 0x8664;
                }
            } catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
        }
    }
}
