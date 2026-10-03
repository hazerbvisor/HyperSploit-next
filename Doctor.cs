using System.Runtime.InteropServices;
using HyperSploit.Adb;

namespace HyperSploit;

public static class Doctor {
    public static string PlatformAdvice(Architecture architecture, bool linux) =>
        linux && architecture is Architecture.Arm64 or Architecture.X64
            ? "Linux ARM64/x64: use system android-tools. Alpine needs a musl runtime."
            : linux ? "Unsupported Linux guest architecture. Standard iSH i386 cannot run .NET 9."
            : "Use platform Android tools for this OS and architecture.";
    public static async Task<int> RunAsync(IAdbCommandRunner? adb = null, CancellationToken ct = default,
        IAdbCommandRunner? fastboot = null) {
        EnvironmentDiagnostics.Print();
        DiagnosticsCli.Write(PlatformAdvice(RuntimeInformation.ProcessArchitecture, OperatingSystem.IsLinux()));
        var status = 0;
        async Task Check(string name, IAdbCommandRunner runner, string version) {
            try {
                var result = await runner.RunAsync([version], cancellationToken: ct);
                result.EnsureSuccess();
                DiagnosticsCli.Write($"{name} version: {result.Output}");
                var devices = await runner.RunAsync(["devices"], cancellationToken: ct);
                devices.EnsureSuccess();
                DiagnosticsCli.Write($"{name} device status:\n{(string.IsNullOrWhiteSpace(devices.Output) ? "Disconnected / Unknown" : devices.Output)}");
            } catch (Exception e) when (e is IOException or TimeoutException or UnauthorizedAccessException or ArgumentException) {
                DiagnosticsCli.Write($"{name}: unavailable / Unknown: {e.Message}"); status = 1;
            }
        }
        await Check("ADB", adb ?? new AdbCommandRunner(), "version");
        await Check("Fastboot", fastboot ?? new AdbCommandRunner(executableResolver: () =>
            AdbExecutable.Resolve("fastboot", "HYPERSPLOIT_FASTBOOT_PATH"), tool: "Fastboot"), "--version");
        DiagnosticsCli.Write("Wireless: same Wi-Fi; enable Wireless debugging. Pair and debugging ports differ. Offline: reconnect. Unauthorized: authorize on Android. Fastboot normally needs USB; it is optional for Wireless ADB on iSH.");
        return status;
    }
    public static async Task AutoReconnectAsync(IAdbCommandRunner runner) {
        var config = new ConfigStore().Load();
        if (!config.AutoReconnect || string.IsNullOrWhiteSpace(config.LastWirelessAddress)) return;
        try { Console.WriteLine(await new WirelessAdb(runner).ConnectAsync(config.LastWirelessAddress)); }
        catch (Exception e) when (e is IOException or TimeoutException or ArgumentException or UnauthorizedAccessException) {
            DiagnosticsCli.Write($"Auto reconnect failed: {e.Message}. Use menu 11.");
        }
    }
}
