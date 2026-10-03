using System.Text.RegularExpressions;
namespace HyperSploit.Adb;

public sealed class PackageManager(SelectedDeviceCommands device) {
    public static void ValidatePackage(string package) {
        if (!Regex.IsMatch(package, @"\A[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*\z"))
            throw new ArgumentException("Enter a package identifier, e.g. com.example.app.");
    }
    public static IReadOnlyList<string> ParsePackages(string output, string? filter = null) => output.Split('\n')
        .Select(l => l.Trim()).Where(l => l.StartsWith("package:", StringComparison.Ordinal))
        .Select(l => l[8..]).Where(p => !string.IsNullOrWhiteSpace(p) &&
            (string.IsNullOrEmpty(filter) || p.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        .Distinct().Order(StringComparer.Ordinal).ToArray();
    public async Task<IReadOnlyList<string>> ListAsync(string? filter = null, CancellationToken ct = default) =>
        ParsePackages((await device.RunAsync(AdbShell.Command(["pm", "list", "packages"]), ct)).Stdout, filter);
    public Task<AdbResult> InfoAsync(string package, CancellationToken ct = default) {
        ValidatePackage(package);
        return device.RunAsync(AdbShell.Command(["dumpsys", "package", package]), ct);
    }
    public static void EnsurePackageSuccess(AdbResult result) {
        result.EnsureSuccess();
        var lines = result.Output.Split('\n').Select(l => l.Trim()).ToArray();
        if (!lines.Contains("Success") || lines.Any(l => l.StartsWith("Failure", StringComparison.OrdinalIgnoreCase)))
            throw new IOException($"Package operation failed: {result.Output}");
    }
    public async Task InstallAsync(string apk, bool confirmed, Action<string, bool> progress, CancellationToken ct = default) {
        if (!confirmed) throw new InvalidOperationException("Installation requires explicit confirmation.");
        var path = Path.GetFullPath(apk);
        if (!File.Exists(path)) throw new FileNotFoundException("APK not found.", path);
        if (!path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Select an APK file.");
        EnsurePackageSuccess(await device.StreamAsync(["install", path], progress, ct));
    }
    public async Task UninstallAsync(string package, bool confirmed, CancellationToken ct = default) {
        if (!confirmed) throw new InvalidOperationException("Uninstallation requires explicit confirmation.");
        ValidatePackage(package);
        EnsurePackageSuccess(await device.RunAsync(["uninstall", package], ct));
    }
}
