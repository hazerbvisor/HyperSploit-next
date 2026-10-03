namespace HyperSploit.Adb;

public sealed class AdbShell(SelectedDeviceCommands device) {
    // adb joins shell arguments for the remote POSIX shell. ArgumentList only protects
    // the host, so quote each remote token as well (including embedded apostrophes).
    public static string Quote(string value) {
        if (value.Contains('\0')) throw new ArgumentException("Arguments cannot contain NUL.");
        return "'" + value.Replace("'", "'\\''") + "'";
    }
    public static string[] Command(IReadOnlyList<string> arguments) {
        if (arguments.Count == 0 || string.IsNullOrWhiteSpace(arguments[0]))
            throw new ArgumentException("Enter a command executable.");
        return ["shell", "-T", "--", .. arguments.Select(Quote)];
    }
    public Task<AdbResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct = default) =>
        device.RunAsync(Command(arguments), ct);
    public Task<AdbResult> InteractiveAsync(CancellationToken ct = default) =>
        device.InteractiveAsync(["shell", "-t"], ct);
}
