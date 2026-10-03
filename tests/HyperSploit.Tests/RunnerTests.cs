using HyperSploit.Adb;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace HyperSploit.Tests;

public class RunnerTests {
    [Fact]
    public async Task DirectProcessCapturesStreamsArgumentsTimeoutAndCancellation() {
        if (OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "adb-runner-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "fake-adb");
        var previous = Environment.GetEnvironmentVariable("HYPERSPLOIT_ADB_PATH");
        try {
            File.WriteAllText(file, """
                #!/bin/sh
                if [ "$1" = wait ]; then
                    exec sleep 30
                fi
                printf '%s\n' "$1" "$2"
                printf 'stderr fixture\n' >&2
                exit 7
                """ + "\n");
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Environment.SetEnvironmentVariable("HYPERSPLOIT_ADB_PATH", file);
            var result = await new AdbCommandRunner().RunAsync(["literal; $(echo injected)", "with spaces"]);
            Assert.Equal(7, result.ExitCode);
            Assert.Contains("literal; $(echo injected)", result.Stdout);
            Assert.Contains("with spaces", result.Stdout);
            Assert.Contains("stderr fixture", result.Stderr);
            await Assert.ThrowsAsync<TimeoutException>(() =>
                new AdbCommandRunner(TimeSpan.FromMilliseconds(100)).RunAsync(["wait"]));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new AdbCommandRunner().RunAsync(["wait"], cancellationToken: cancellation.Token));
        } finally {
            Environment.SetEnvironmentVariable("HYPERSPLOIT_ADB_PATH", previous);
            Directory.Delete(root, true);
        }
    }
}
