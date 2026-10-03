using System.Runtime.InteropServices;

namespace HyperSploit;

public static class EnvironmentDiagnostics {
    public static void Print() {
        Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"OS architecture: {RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"Runtime identifier: {RuntimeInformation.RuntimeIdentifier}");
        try {
            Console.WriteLine($"ADB: {AdbExecutable.Resolve() ?? "not found"}");
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            Console.WriteLine($"ADB: {e.Message}");
            Environment.ExitCode = 1;
        }
        try {
            Console.WriteLine($"Fastboot: {AdbExecutable.Resolve("fastboot", "HYPERSPLOIT_FASTBOOT_PATH") ?? "not found"}");
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            Console.WriteLine($"Fastboot: {e.Message}");
            Environment.ExitCode = 1;
        }
    }
}
