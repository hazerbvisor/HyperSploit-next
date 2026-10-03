using System.Runtime.InteropServices;

namespace HyperSploit;

public static class EnvironmentDiagnostics {
    public static void Print() {
        DiagnosticsCli.Write($"OS: {RuntimeInformation.OSDescription}");
        DiagnosticsCli.Write($"OS architecture: {RuntimeInformation.OSArchitecture}");
        DiagnosticsCli.Write($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        DiagnosticsCli.Write($"Runtime: {RuntimeInformation.FrameworkDescription}");
        DiagnosticsCli.Write($"Runtime identifier: {RuntimeInformation.RuntimeIdentifier}");
        try {
            DiagnosticsCli.Write($"ADB: {AdbExecutable.Resolve() ?? "not found"}");
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) {
            DiagnosticsCli.Write($"ADB: {e.Message}");
            Environment.ExitCode = 1;
        }
        try {
            DiagnosticsCli.Write($"Fastboot: {AdbExecutable.Resolve("fastboot", "HYPERSPLOIT_FASTBOOT_PATH") ?? "not found"}");
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) {
            DiagnosticsCli.Write($"Fastboot: {e.Message}");
            Environment.ExitCode = 1;
        }
    }
}
