using HyperSploit.Adb;
namespace HyperSploit.Tests;

public class AndroidToolsTests {
    private const string Serial = "192.168.1.10:5555";
    private sealed class Fake : IAdbCommandRunner {
        public List<string[]> Calls { get; } = [];
        public string Devices = $"List of devices attached\n{Serial} device model:Pixel_9\n192.168.1.11:5555 device\n";
        public AdbResult Result = new(0, "Success\n", "");
        public List<(string Text, bool Error)> Lines = [];
        public Action<string[]>? OnStream;
        public bool Wait;
        public Task<AdbResult> RunAsync(IReadOnlyList<string> args, string? input = null, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested(); Calls.Add(args.ToArray());
            return Task.FromResult(args[0] == "devices" ? new AdbResult(0, Devices, "") : Result);
        }
        public async Task<AdbResult> StreamAsync(IReadOnlyList<string> args, Action<string, bool> output, CancellationToken cancellationToken = default) {
            Calls.Add(args.ToArray()); OnStream?.Invoke(args.ToArray());
            foreach (var line in Lines) output(line.Text, line.Error);
            if (Wait) await Task.Delay(Timeout.Infinite, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return Result;
        }
        public Task<AdbResult> InteractiveAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default) => RunAsync(args, cancellationToken: cancellationToken);
    }
    private static SelectedDeviceCommands Device(Fake fake) => new(fake, Serial);
    private static void Targeted(Fake fake, params string[] command) => Assert.Equal(new[] { "-s", Serial }.Concat(command), fake.Calls.Last());

    [Fact]
    public async Task ShellQuotesRemoteTokensAndTargetsOnlySelectedDevice() {
        var fake = new Fake { Result = new(0, "out", "err") };
        var args = new[] { "printf", "with spaces", "a'b", "$(touch /tmp/x); *" };
        var result = await new AdbShell(Device(fake)).RunAsync(args);
        Targeted(fake, "shell", "-T", "--", "'printf'", "'with spaces'", "'a'\\''b'", "'$(touch /tmp/x); *'");
        Assert.Equal("out", result.Stdout); Assert.Equal("err", result.Stderr);
        await new AdbShell(Device(fake)).InteractiveAsync(); Targeted(fake, "shell", "-t");
        Assert.Throws<ArgumentException>(() => AdbShell.Command([]));
        Assert.Throws<ArgumentException>(() => AdbShell.Quote("bad\0arg"));
    }
    [Theory]
    [InlineData("", "disconnected")]
    [InlineData("offline", "offline")]
    [InlineData("unauthorized", "unauthorized")]
    public async Task RefusesUnavailableSelectedDeviceEvenIfAnotherIsOnline(string state, string error) {
        var fake = new Fake { Devices = $"192.168.1.11:5555 device\n" + (state.Length == 0 ? "" : $"{Serial} {state}\n") };
        var exception = await Assert.ThrowsAsync<IOException>(() => new AdbShell(Device(fake)).RunAsync(["id"]));
        Assert.Contains(error, exception.Message);
        Assert.Single(fake.Calls);
    }
    [Fact]
    public async Task RequiresSelectionAndWirelessTransport() {
        var fake = new Fake();
        await Assert.ThrowsAsync<IOException>(() => new SelectedDeviceCommands(fake, null).RunAsync(["reboot"]));
        Assert.Empty(fake.Calls);
        fake.Devices = "usbserial device usb:1-2\n";
        await Assert.ThrowsAsync<IOException>(() => new SelectedDeviceCommands(fake, "usbserial").RunAsync(["reboot"]));
        Assert.Single(fake.Calls);
    }
    [Fact]
    public async Task DisconnectDuringOperationReportsAdbError() {
        var fake = new Fake { Result = new(1, "", "error: device disconnected") };
        var exception = await Assert.ThrowsAsync<IOException>(() => new AdbShell(Device(fake)).RunAsync(["id"]));
        Assert.Contains("disconnected", exception.Message);
    }
    [Fact]
    public async Task PackageListFiltersSortsAndDeduplicates() {
        var fake = new Fake { Result = new(0, "package:com.z.app\r\nnoise\npackage:com.a.app\npackage:com.z.app\npackage:com.other.tool\n", "") };
        Assert.Equal(new[] { "com.a.app", "com.z.app" }, await new PackageManager(Device(fake)).ListAsync("APP"));
        Targeted(fake, AdbShell.Command(["pm", "list", "packages"]));
        PackageManager.ValidatePackage("android");
        await new PackageManager(Device(fake)).InfoAsync("com.a.app");
        Targeted(fake, AdbShell.Command(["dumpsys", "package", "com.a.app"]));
    }
    [Theory]
    [InlineData(0, "Success", true)]
    [InlineData(0, "Failure [INSTALL_FAILED_INVALID_APK]", false)]
    [InlineData(1, "Success", false)]
    [InlineData(0, "", false)]
    [InlineData(0, "Success\nFailure [DELETE_FAILED_INTERNAL_ERROR]", false)]
    public void PackageResultRequiresExitZeroAndSuccess(int code, string output, bool success) {
        var result = new AdbResult(code, output, "");
        if (success) PackageManager.EnsurePackageSuccess(result);
        else Assert.Throws<IOException>(() => PackageManager.EnsurePackageSuccess(result));
    }
    [Fact]
    public async Task PackageChangesRequireConfirmationAndPreserveApkPath() {
        var fake = new Fake(); var packages = new PackageManager(Device(fake));
        await Assert.ThrowsAsync<InvalidOperationException>(() => packages.InstallAsync("x.apk", false, (_, _) => {}));
        await Assert.ThrowsAsync<InvalidOperationException>(() => packages.UninstallAsync("com.a.app", false));
        Assert.Empty(fake.Calls);
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(root);
        try {
            var apk = Path.Combine(root, "some app.apk"); File.WriteAllText(apk, "fixture");
            await packages.InstallAsync(apk, true, (_, _) => {}); Targeted(fake, "install", apk);
            await packages.UninstallAsync("com.a.app", true); Targeted(fake, "uninstall", "com.a.app");
            await Assert.ThrowsAsync<ArgumentException>(() => packages.UninstallAsync("com.a;reboot", true));
        } finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task TransfersPreserveSpacesAndProtectLocalFiles() {
        var fake = new Fake(); var transfer = new FileTransfer(Device(fake));
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(root);
        try {
            var local = Path.Combine(root, "my file.txt"); File.WriteAllText(local, "original");
            await transfer.PushAsync(local, "/sdcard/my file.txt", (_, _) => {});
            Targeted(fake, "push", local, "/sdcard/my file.txt");
            var before = fake.Calls.Count;
            await Assert.ThrowsAsync<IOException>(() => transfer.PullAsync("/sdcard/my file.txt", local, false, (_, _) => {}));
            Assert.Equal(before, fake.Calls.Count); Assert.Equal("original", File.ReadAllText(local));
            fake.OnStream = args => { Assert.Equal(new[] { "-s", Serial, "pull", "/sdcard/my file.txt" }, args[..4]); File.WriteAllText(args[4], "download"); };
            await transfer.PullAsync("/sdcard/my file.txt", local, true, (_, _) => {});
            Assert.Equal("download", File.ReadAllText(local));
            fake.Result = new(1, "", "disconnected");
            await Assert.ThrowsAsync<IOException>(() => transfer.PullAsync("/sdcard/my file.txt", local, true, (_, _) => {}));
            Assert.Equal("download", File.ReadAllText(local));
            Assert.Single(Directory.GetFiles(root)); Assert.Empty(Directory.GetDirectories(root));
        } finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task PullDoesNotOverwriteAFileCreatedDuringTransfer() {
        var fake = new Fake(); var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(root);
        try {
            var local = Path.Combine(root, "new.txt");
            fake.OnStream = args => { File.WriteAllText(args[4], "download"); File.WriteAllText(local, "raced"); };
            await Assert.ThrowsAsync<IOException>(() => new FileTransfer(Device(fake)).PullAsync("/sdcard/file", local, false, (_, _) => {}));
            Assert.Equal("raced", File.ReadAllText(local)); Assert.Empty(Directory.GetDirectories(root));
        } finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task LogcatFiltersSavesAndCancelsWithoutSwallowingErrors() {
        var fake = new Fake { Lines = [("hello", false), ("Needle matches", false), ("disconnected", true)], Wait = true };
        var lines = new List<string>(); var log = new Logcat(Device(fake));
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".log");
        try {
            using var ct = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => log.StreamAsync("MyTag", "needle", (line, _) => lines.Add(line), path, ct: ct.Token));
            Targeted(fake, "logcat", "-v", "threadtime", "MyTag:V", "*:S");
            Assert.Equal(new[] { "Needle matches", "disconnected" }, lines);
            Assert.Equal("Needle matches" + Environment.NewLine, File.ReadAllText(path));
            await Assert.ThrowsAsync<IOException>(() => log.StreamAsync(null, null, (_, _) => {}, path));
            Assert.Throws<ArgumentException>(() => Logcat.Command("bad:V *:V"));
            await log.ClearAsync(); Targeted(fake, "logcat", "-c");
            fake.Wait = false; fake.Result = new(1, "", "device disconnected");
            var error = await Assert.ThrowsAsync<IOException>(() => log.StreamAsync(null, null, (_, _) => {}));
            Assert.Contains("disconnected", error.Message);
        } finally { File.Delete(path); }
    }
    [Theory]
    [InlineData(RebootMode.Android, null)]
    [InlineData(RebootMode.Recovery, "recovery")]
    [InlineData(RebootMode.Bootloader, "bootloader")]
    public async Task RebootUsesOnlyStandardSelectedCommand(RebootMode mode, string? target) {
        var fake = new Fake(); var reboot = new RebootControls(Device(fake));
        await Assert.ThrowsAsync<InvalidOperationException>(() => reboot.RebootAsync(mode, false));
        Assert.Empty(fake.Calls);
        await reboot.RebootAsync(mode, true);
        Targeted(fake, target == null ? ["reboot"] : ["reboot", target]);
        Assert.Throws<ArgumentOutOfRangeException>(() => RebootControls.Command((RebootMode)99));
    }
}
