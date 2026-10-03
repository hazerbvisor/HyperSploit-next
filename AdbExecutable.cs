using System.Runtime.InteropServices;

namespace HyperSploit;

public static class AdbExecutable {
    public static string? Resolve() {
        var configured = Environment.GetEnvironmentVariable("HYPERSPLOIT_ADB_PATH");
        if (!string.IsNullOrEmpty(configured)) {
            var path = Path.GetFullPath(configured);
            if (!IsExecutable(path))
                throw new IOException($"HYPERSPLOIT_ADB_PATH is not an executable file: {path}");
            return path;
        }
        var executable = OperatingSystem.IsWindows() ? "adb.exe" : "adb";
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)) {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            var path = Path.GetFullPath(Path.Combine(directory, executable));
            if (IsExecutable(path)) return path;
        }
        return null;
    }

    private static bool IsExecutable(string path) {
        if (!File.Exists(path)) return false;
        if (OperatingSystem.IsWindows()) return true;
        var mode = File.GetUnixFileMode(path);
        return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
    }
}
