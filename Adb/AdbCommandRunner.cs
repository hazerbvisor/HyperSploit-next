using System.Diagnostics;
using System.Text;

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
    Task<AdbResult> StreamAsync(IReadOnlyList<string> arguments, Action<string, bool> output,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<AdbResult> InteractiveAsync(IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>One process implementation for buffered, streaming and terminal operations.</summary>
public sealed class AdbCommandRunner(TimeSpan? timeout = null) : IAdbCommandRunner {
    public Task<AdbResult> RunAsync(IReadOnlyList<string> arguments, string? input = null,
        CancellationToken cancellationToken = default) => ExecuteAsync(arguments, input, null, false,
            timeout ?? TimeSpan.FromSeconds(20), cancellationToken);
    public Task<AdbResult> StreamAsync(IReadOnlyList<string> arguments, Action<string, bool> output,
        CancellationToken cancellationToken = default) => ExecuteAsync(arguments, null, output, false,
            timeout ?? Timeout.InfiniteTimeSpan, cancellationToken);
    public Task<AdbResult> InteractiveAsync(IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default) => ExecuteAsync(arguments, null, null, true,
            timeout ?? Timeout.InfiniteTimeSpan, cancellationToken);

    private static async Task<AdbResult> ExecuteAsync(IReadOnlyList<string> arguments, string? input,
        Action<string, bool>? output, bool interactive, TimeSpan duration, CancellationToken ct) {
        var path = AdbExecutable.Resolve() ??
            throw new IOException("ADB not found. Install android-tools or set HYPERSPLOIT_ADB_PATH.");
        var start = new ProcessStartInfo(path) {
            UseShellExecute = false, RedirectStandardOutput = !interactive,
            RedirectStandardError = !interactive, RedirectStandardInput = !interactive,
            CreateNoWindow = !interactive
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        using var deadline = new CancellationTokenSource(duration);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        ct.ThrowIfCancellationRequested();
        try {
            process.Start();
            async Task<string> ReadOutputAsync(StreamReader reader, Action<string>? callback, CancellationToken token) {
                try { return await PumpAsync(reader, callback, token); }
                catch { linked.Cancel(); throw; }
            }
            // Retain only a bounded diagnostic tail during streaming. Never retain log history.
            var stdout = interactive ? Task.FromResult("") : ReadOutputAsync(process.StandardOutput,
                output == null ? null : line => output(line, false), linked.Token);
            var stderr = interactive ? Task.FromResult("") : ReadOutputAsync(process.StandardError,
                output == null ? null : line => output(line, true), linked.Token);
            try {
                if (!interactive) {
                    if (input != null) await process.StandardInput.WriteLineAsync(input.AsMemory(), linked.Token);
                    process.StandardInput.Close();
                }
                // If a callback or output read fails, kill the child instead of leaving it running.
                var exited = process.WaitForExitAsync(linked.Token);
                var pumps = Task.WhenAll(stdout, stderr);
                if (await Task.WhenAny(exited, pumps) == pumps) await pumps;
                await exited;
                await pumps;
            } catch {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                try { await Task.WhenAll(stdout, stderr); } catch { /* preserve original failure */ }
                if (stdout.IsFaulted) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(stdout.Exception!.InnerException!).Throw();
                if (stderr.IsFaulted) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(stderr.Exception!.InnerException!).Throw();
                throw;
            }
            return new(process.ExitCode, await stdout, await stderr);
        } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            throw new TimeoutException("ADB timed out. Check Wi-Fi and Wireless debugging.");
        } catch (System.ComponentModel.Win32Exception e) {
            throw new IOException($"Cannot run ADB: {e.Message}", e);
        }
    }

    private static async Task<string> PumpAsync(StreamReader reader, Action<string>? output, CancellationToken ct) {
        var retained = new StringBuilder();
        var line = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), ct)) != 0) {
            if (output == null) {
                if (retained.Length + count > 4 * 1024 * 1024)
                    throw new IOException("ADB output exceeds 4 MiB. Use streaming for large output.");
                retained.Append(buffer, 0, count);
                continue;
            }
            for (var i = 0; i < count; i++) {
                var ch = buffer[i];
                if (ch is '\r' or '\n' || line.Length >= 16384) {
                    if (line.Length > 0) {
                        var text = line.ToString();
                        output(text);
                        retained.AppendLine(text);
                        if (retained.Length > 32768) retained.Remove(0, retained.Length - 32768);
                        line.Clear();
                    }
                    if (ch is '\r' or '\n') continue;
                }
                line.Append(ch);
            }
        }
        if (line.Length > 0) { output?.Invoke(line.ToString()); retained.Append(line); }
        return retained.ToString();
    }
}
