using HyperSploit;

namespace HyperSploit.Tests;

public class AdbResolutionTests {
    [Fact]
    public void ResolvesPathAndOverrideAndRejectsInvalidOverride() {
        var oldPath = Environment.GetEnvironmentVariable("PATH");
        var oldOverride = Environment.GetEnvironmentVariable("HYPERSPLOIT_ADB_PATH");
        var root = Path.Combine(Path.GetTempPath(), "adb-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try {
            var adb = Path.Combine(root, OperatingSystem.IsWindows() ? "adb.exe" : "adb");
            File.WriteAllText(adb, "test fixture");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(adb, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            Environment.SetEnvironmentVariable("HYPERSPLOIT_ADB_PATH", null);
            Environment.SetEnvironmentVariable("PATH", root);
            Assert.Equal(adb, AdbExecutable.Resolve());
            Environment.SetEnvironmentVariable("PATH", "");
            Environment.SetEnvironmentVariable("HYPERSPLOIT_ADB_PATH", adb);
            Assert.Equal(adb, AdbExecutable.Resolve());
            Environment.SetEnvironmentVariable("HYPERSPLOIT_ADB_PATH", Path.Combine(root, "missing"));
            Assert.Throws<IOException>(() => AdbExecutable.Resolve());
            if (!OperatingSystem.IsWindows()) {
                Environment.SetEnvironmentVariable("HYPERSPLOIT_ADB_PATH", null);
                Assert.Null(AdbExecutable.Resolve());
                File.SetUnixFileMode(adb, UnixFileMode.UserRead);
                Environment.SetEnvironmentVariable("PATH", root);
                Assert.Null(AdbExecutable.Resolve());
            }
        } finally {
            Environment.SetEnvironmentVariable("PATH", oldPath);
            Environment.SetEnvironmentVariable("HYPERSPLOIT_ADB_PATH", oldOverride);
            Directory.Delete(root, true);
        }
    }
}
