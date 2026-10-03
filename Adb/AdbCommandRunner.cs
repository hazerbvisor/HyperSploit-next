using System.Diagnostics;

namespace HyperSploit.Adb;

public record AdbResult(int ExitCode, string Stdout, string Stderr) {
    public string Output => (Stdout + "\n" + Stderr).Trim();
    public void EnsureSuccess() {
        if (ExitCode != 0) throw new IOException($"ADB failed ({ExitCode}): {Output}");
    }
}

public interface IAdbCommandRunner {
    Task<AdbResult> RunAsync(IReadOnlyList<string> arguments, string? input = null,
        CancellationToken cancellationToken = default);
}

public sealed class AdbCommandRunner(TimeSpan? timeout = null) : IAdbCommandRunner {
    public async Task<AdbResult> RunAsync(IReadOnlyList<string> arguments, string? input = null,
        CancellationToken cancellationToken = default) {
        var path = AdbExecutable.Resolve() ??
            throw new IOException("ADB not found. Install android-tools or set HYPERSPLOIT_ADB_PATH.");
        var start = new ProcessStartInfo(path) {
            UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, RedirectStandardInput = true, CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        using var deadline = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(20));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        cancellationToken.ThrowIfCancellationRequested();
        try {
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            try {
                if (input != null) await process.StandardInput.WriteLineAsync(input.AsMemory(), linked.Token);
                process.StandardInput.Close();
                await process.WaitForExitAsync(linked.Token);
            } catch {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                await Task.WhenAll(stdout, stderr);
                throw;
            }
            return new(process.ExitCode, await stdout, await stderr);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new TimeoutException("ADB timed out. Check Wi-Fi and the device's Wireless debugging screen.");
        } catch (System.ComponentModel.Win32Exception e) {
            throw new IOException($"Cannot run ADB: {e.Message}", e);
        }
    }
}
