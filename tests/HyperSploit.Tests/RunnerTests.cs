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

public class StreamingRunnerTests {
    [Fact]
    public async Task StreamingEmitsBeforeExitBoundsMemoryAndStopsOnCallbackFailure() {
        if (OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "adb-stream-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "fake-adb");
        var previous = Environment.GetEnvironmentVariable("HYPERSPLOIT_ADB_PATH");
        try {
            File.WriteAllText(file, """
                #!/bin/sh
                printf 'first\n'
                printf 'error fixture\n' >&2
                if [ "$1" = many ]; then
                    i=0
                    while [ "$i" -lt 10000 ]; do
                        printf 'a fairly long log line with a payload and more text\n'
                        i=$((i+1))
                    done
                    exit 0
                fi
                exec sleep 30
                """ + "\n");
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Environment.SetEnvironmentVariable("HYPERSPLOIT_ADB_PATH", file);
            using var ct = new CancellationTokenSource();
            var seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var stream = new AdbCommandRunner().StreamAsync(["wait"], (line, _) => { if (line == "first") seen.TrySetResult(); }, ct.Token);
            await seen.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(stream.IsCompleted);
            ct.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stream);
            var count = 0;
            var result = await new AdbCommandRunner().StreamAsync(["many"], (_, _) => Interlocked.Increment(ref count));
            Assert.Equal(10002, count); Assert.True(result.Stdout.Length < 50000);
            Assert.Contains("error fixture", result.Stderr);
            await Assert.ThrowsAsync<IOException>(() => new AdbCommandRunner().StreamAsync(["wait"], (_, _) => throw new IOException("sink failed")));
            await Assert.ThrowsAsync<TimeoutException>(() => new AdbCommandRunner(TimeSpan.FromMilliseconds(100)).StreamAsync(["wait"], (_, _) => {}));
        } finally {
            Environment.SetEnvironmentVariable("HYPERSPLOIT_ADB_PATH", previous);
            Directory.Delete(root, true);
        }
    }
}
