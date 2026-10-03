using System.Text.RegularExpressions;
namespace HyperSploit.Adb;

public sealed class Logcat(SelectedDeviceCommands device) {
    public static string[] Command(string? tag) {
        if (!string.IsNullOrEmpty(tag) && !Regex.IsMatch(tag, @"\A[A-Za-z0-9_.-]+\z"))
            throw new ArgumentException("Tag must contain letters, numbers, dots, underscores or hyphens.");
        return string.IsNullOrEmpty(tag) ? ["logcat", "-v", "threadtime"] :
            ["logcat", "-v", "threadtime", tag + ":V", "*:S"];
    }
    public static bool Matches(string line, string? text) => string.IsNullOrEmpty(text) ||
        line.Contains(text, StringComparison.OrdinalIgnoreCase);
    public Task<AdbResult> ClearAsync(CancellationToken ct = default) => device.RunAsync(["logcat", "-c"], ct);
    public async Task StreamAsync(string? tag, string? text, Action<string, bool> output,
        string? savePath = null, bool overwrite = false, CancellationToken ct = default) {
        var command = Command(tag);
        // Preflight before creating any output file.
        await device.ArgumentsAsync(command, ct);
        using var file = savePath == null ? null : new StreamWriter(new FileStream(Path.GetFullPath(savePath),
            overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        var gate = new object();
        await device.StreamAsync(command, (line, error) => {
            if (!error && !Matches(line, text)) return;
            lock (gate) {
                output(line, error);
                if (!error) file?.WriteLine(line);
            }
        }, ct);
    }
}
