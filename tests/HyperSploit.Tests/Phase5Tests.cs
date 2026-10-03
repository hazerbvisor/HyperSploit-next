using System.Runtime.InteropServices;
using HyperSploit.Adb;

namespace HyperSploit.Tests;

public class Phase5Tests {
    [Theory]
    [InlineData("--help", CliCommand.Help)]
    [InlineData("-h", CliCommand.Help)]
    [InlineData("--version", CliCommand.Version)]
    [InlineData("doctor", CliCommand.Doctor)]
    [InlineData("--diagnostics", CliCommand.Diagnostics)]
    public void ParsesCommands(string arg, CliCommand expected) => Assert.Equal(expected, Program.Parse([arg]));
    [Fact]
    public void RejectsUnknownAndExtraArguments() {
        Assert.Equal(CliCommand.Menu, Program.Parse([]));
        Assert.Throws<ArgumentException>(() => Program.Parse(["--bypass"]));
        Assert.Throws<ArgumentException>(() => Program.Parse(["doctor", "extra"]));
    }
    [Fact]
    public void ConfigRoundTripRecoversAndPreservesPreferencesWithoutUnknownSecrets() {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var file = Path.Combine(root, "config.json");
        try {
            var store = new ConfigStore(file);
            Assert.False(store.Load().AutoReconnect);
            store.Update(c => { c.AdbPath = "/bin/adb"; c.FastbootPath = "/bin/fastboot"; c.LastWirelessAddress = "192.0.2.1:5555"; c.AutoReconnect = true; c.Cli.TerminalWidth = 30; });
            new DeviceSelectionStore(file).Save("device");
            var config = store.Load();
            Assert.Equal("device", config.LastSelectedDevice);
            Assert.Equal("/bin/adb", config.AdbPath);
            Assert.Equal("/bin/fastboot", config.FastbootPath);
            Assert.Equal("192.0.2.1:5555", config.LastWirelessAddress);
            Assert.True(config.AutoReconnect);
            Assert.Equal(30, config.Cli.TerminalWidth);
            File.WriteAllText(file, "{\"pairingCode\":\"123456\",\"cli\":null}");
            store.Update(c => c.LastSelectedDevice = "new");
            Assert.DoesNotContain("123456", File.ReadAllText(file));
            foreach (var corrupt in new[] { "bad", "null", "[]", "{\"cli\":false}" }) {
                File.WriteAllText(file, corrupt);
                Assert.Null(store.Load().LastSelectedDevice);
            }
        } finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public void EnvironmentOverridesConfiguredExecutable() {
        var previousRoot = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var previousAdb = Environment.GetEnvironmentVariable("HYPERSPLOIT_ADB_PATH");
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        try {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", root);
            var file = Path.Combine(root, "adb");
            File.WriteAllText(file, "fixture");
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            new ConfigStore().Update(c => c.AdbPath = file);
            Environment.SetEnvironmentVariable("HYPERSPLOIT_ADB_PATH", null);
            Assert.Equal(file, AdbExecutable.Resolve());
            Environment.SetEnvironmentVariable("HYPERSPLOIT_ADB_PATH", Path.Combine(root, "missing"));
            Assert.Throws<IOException>(() => AdbExecutable.Resolve());
            Assert.Equal(file, new ConfigStore().Load().AdbPath);
        } finally {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", previousRoot);
            Environment.SetEnvironmentVariable("HYPERSPLOIT_ADB_PATH", previousAdb);
            Directory.Delete(root, true);
        }
    }
    [Theory]
    [InlineData(Architecture.Arm64, "system android-tools")]
    [InlineData(Architecture.X64, "system android-tools")]
    [InlineData(Architecture.X86, "Unsupported")]
    public void DetectsGuestArchitecture(Architecture architecture, string expected) =>
        Assert.Contains(expected, Doctor.PlatformAdvice(architecture, true));
    private sealed class MissingRunner : IAdbCommandRunner {
        public Task<AdbResult> RunAsync(IReadOnlyList<string> args, string? input = null, CancellationToken cancellationToken = default) => throw new IOException("dependency missing");
    }
    [Fact]
    public async Task DoctorReportsBothMissingDependenciesAndHost() {
        var output = new StringWriter();
        var previous = Console.Out;
        try {
            Console.SetOut(output);
            Assert.Equal(1, await Doctor.RunAsync(new MissingRunner(), fastboot: new MissingRunner()));
        } finally { Console.SetOut(previous); }
        var text = output.ToString();
        foreach (var label in new[] { "OS:", "OS architecture:", "Process architecture:", "Runtime:", "ADB:", "Fastboot:", "Unknown", "Wireless:" }) Assert.Contains(label, text);
    }
    private sealed class HealthyRunner : IAdbCommandRunner {
        public List<string[]> Calls { get; } = new();
        public Task<AdbResult> RunAsync(IReadOnlyList<string> args, string? input = null, CancellationToken cancellationToken = default) {
            Calls.Add(args.ToArray());
            return Task.FromResult(new AdbResult(0, args[0] == "devices" ? "serial device" : "tool version", ""));
        }
    }
    [Fact]
    public async Task DoctorChecksVersionsAndListsOnly() {
        var adb = new HealthyRunner(); var fastboot = new HealthyRunner();
        var previous = Console.Out;
        using var output = new StringWriter();
        try {
            Console.SetOut(output);
            Assert.Equal(0, await Doctor.RunAsync(adb, fastboot: fastboot));
        } finally { Console.SetOut(previous); }
        Assert.Equal(new[] { "version", "devices" }, adb.Calls.Select(c => c.Single()));
        Assert.Equal(new[] { "--version", "devices" }, fastboot.Calls.Select(c => c.Single()));
        Assert.Contains("ADB device status:", output.ToString());
        Assert.Contains("Fastboot device status:", output.ToString());
    }
    [Fact]
    public async Task AutoReconnectIsOptInAndRejectsInvalidEndpoint() {
        var previousRoot = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", root);
            var runner = new HealthyRunner();
            new ConfigStore().Update(c => c.LastWirelessAddress = "192.0.2.1:5555");
            await Doctor.AutoReconnectAsync(runner);
            Assert.Empty(runner.Calls);
            new ConfigStore().Update(c => { c.AutoReconnect = true; c.LastWirelessAddress = "invalid"; });
            await Doctor.AutoReconnectAsync(runner);
            Assert.Empty(runner.Calls);
            new ConfigStore().Update(c => c.LastWirelessAddress = "192.0.2.1:5555");
            await Doctor.AutoReconnectAsync(runner);
            Assert.Equal(new[] { "connect", "192.0.2.1:5555" }, Assert.Single(runner.Calls));
        } finally {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", previousRoot);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
    [Fact]
    public void NormalAssemblyHasNoLegacyResourcesOrTypes() {
        var assembly = typeof(Program).Assembly;
        Assert.Empty(assembly.GetManifestResourceNames());
        Assert.Null(assembly.GetType("HyperSploit.JsonResponse"));
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name == "AdvancedSharpAdbClient");
        Assert.DoesNotContain(typeof(Program).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static), m => m.Name == "Bypass");
    }
}
