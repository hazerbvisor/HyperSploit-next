using HyperSploit.Adb;

namespace HyperSploit.Fastboot;

/// <summary>Only device discovery and the approved read-only getvars are exposed.</summary>
public sealed class FastbootClient {
    private readonly IAdbCommandRunner runner;
    public static IReadOnlyList<string> AllowedVariables { get; } = Array.AsReadOnly(new[] { "product", "unlocked", "current-slot", "secure", "all" });
    public FastbootClient(IAdbCommandRunner? runner = null) {
        this.runner = runner ?? new AdbCommandRunner(executableResolver: () => AdbExecutable.Resolve("fastboot", "HYPERSPLOIT_FASTBOOT_PATH"), tool: "Fastboot");
    }
    public async Task<IReadOnlyList<FastbootDevice>> DevicesAsync(CancellationToken ct = default) {
        var result = await runner.RunAsync(["devices"], cancellationToken: ct);
        Check(result);
        return FastbootParser.Devices(result.Output);
    }
    public async Task<FastbootInformation> GetAsync(string serial, string variable, CancellationToken ct = default) {
        if (string.IsNullOrWhiteSpace(serial) || serial.StartsWith('-') || serial.Any(char.IsWhiteSpace) || serial.Any(char.IsControl))
            throw new ArgumentException("Choose an explicit Fastboot serial.");
        if (!AllowedVariables.Contains(variable)) throw new ArgumentException("Unsupported inspection variable.");
        if (!(await DevicesAsync(ct)).Any(device => device.Serial == serial)) throw new IOException("Selected Fastboot device disconnected.");
        var result = await runner.RunAsync(["-s", serial, "getvar", variable], cancellationToken: ct);
        Check(result);
        return new(new(serial), FastbootParser.GetVariables(result.Output));
    }
    private static void Check(AdbResult result) {
        if (result.ExitCode != 0 || result.Output.Split('\n').Any(line => line.TrimStart().StartsWith("FAILED", StringComparison.OrdinalIgnoreCase)))
            throw new IOException($"Fastboot inspection failed ({result.ExitCode}): {result.Output}");
    }
}
