namespace HyperSploit;

public enum CliCommand { Menu, Help, Version, Diagnostics, Doctor }
public static class Program {
    public static CliCommand Parse(string[] args) => args switch {
        [] => CliCommand.Menu,
        ["--help" or "-h" or "help"] => CliCommand.Help,
        ["--version" or "version"] => CliCommand.Version,
        ["--diagnostics"] => CliCommand.Diagnostics,
        ["doctor"] => CliCommand.Doctor,
        _ => throw new ArgumentException("Unknown arguments. Use --help for available commands.")
    };
    public static async Task<int> Main(string[] args) {
        try {
            switch (Parse(args)) {
                case CliCommand.Help:
                    Console.WriteLine("HyperSploit Next\nUsage: HyperSploit.Next [command]\n  (no command)   Android management menu\n  doctor         Host, dependencies and device status\n  --diagnostics  Host and paths; no device contact\n  --version      Print application version\n  --help, -h     Show this help\nRun DLL: dotnet HyperSploit.Next.dll [command]\nUses system adb/fastboot. Wireless setup is menu 11.\nNo patched HyperOS security bypasses.");
                    break;
                case CliCommand.Version:
                    Console.WriteLine($"HyperSploit Next {typeof(Program).Assembly.GetName().Version!.ToString(3)}");
                    break;
                case CliCommand.Diagnostics: EnvironmentDiagnostics.Print(); break;
                case CliCommand.Doctor: return await Doctor.RunAsync();
                default: await ManagementCli.RunAsync(); break;
            }
            return Environment.ExitCode;
        } catch (ArgumentException e) { Console.Error.WriteLine(e.Message); return 2; }
    }
}
