using HyperSploit.Adb;
using HyperSploit.Fastboot;
using HyperSploit.Xiaomi;
namespace HyperSploit.Tests;

public class Phase4Tests {
    private static DeviceInformation Info(params (string Key, string Value)[] values) => new(
        new("serial", AdbDeviceState.Online, "device", new Dictionary<string, string>()), values.ToDictionary(v => v.Key, v => v.Value));
    private sealed class Runner(params AdbResult[] results) : IAdbCommandRunner {
        public List<string[]> Calls { get; } = [];
        private int index;
        public Task<AdbResult> RunAsync(IReadOnlyList<string> arguments, string? input = null, CancellationToken cancellationToken = default) {
            Calls.Add(arguments.ToArray()); return Task.FromResult(results[index++]);
        }
    }
    [Fact]
    public void ParsesMultipleDevicesWithoutDiagnostics() {
        Assert.Equal(new[] { "one", "two" }, FastbootParser.Devices("one\tfastboot\ntwo fastboot\nwaiting for device\none fastboot\n").Select(d => d.Serial));
        Assert.Empty(FastbootParser.Devices(""));
    }
    [Fact]
    public async Task GetvarReadsStderrAndTargetsExplicitSerial() {
        var runner = new Runner(new AdbResult(0, "one fastboot\ntwo fastboot", ""),
            new AdbResult(0, "", "(bootloader) product: zeus\n(bootloader) unlocked: yes\n(bootloader) current-slot: b\n(bootloader) secure: yes\nFinished. Total time: 0.001s"));
        var info = await new FastbootClient(runner).GetAsync("two", "all");
        Assert.Equal(new[] { "-s", "two", "getvar", "all" }, runner.Calls[1]);
        Assert.Equal("zeus", info.Get("product")); Assert.Equal("Unlocked", info.Bootloader);
        Assert.Equal("b", info.CurrentSlot); Assert.Equal("yes", info.Get("secure"));
        Assert.Equal(4, info.Variables.Count);
    }
    [Theory]
    [InlineData("yes", "Unlocked")][InlineData("no", "Locked")][InlineData("true", "Unlocked")]
    [InlineData("false", "Locked")][InlineData("", "Unknown")][InlineData("maybe", "Unknown")]
    public void BootloaderParsing(string value, string expected) => Assert.Equal(expected,
        new FastbootInformation(new("serial"), new Dictionary<string, string> { ["unlocked"] = value }).Bootloader);
    [Theory]
    [InlineData("a", "a")][InlineData("b", "b")][InlineData("unknown", "Unknown")]
    public void SlotParsing(string value, string expected) => Assert.Equal(expected,
        new FastbootInformation(new("serial"), new Dictionary<string, string> { ["current-slot"] = value }).CurrentSlot);
    [Fact]
    public void ConflictsAndPartitionKeys() {
        var values = FastbootParser.GetVariables("unlocked: yes\nunlocked: no\npartition-size:boot: 0x100\nFAILED (remote: unavailable)");
        Assert.Equal("Unknown", values["unlocked"]); Assert.Equal("0x100", values["partition-size:boot"]);
    }
    [Theory]
    [InlineData("flash")][InlineData("erase")][InlineData("unlock")][InlineData("format")][InlineData("wipe")][InlineData("partition-size:boot")]
    public async Task RejectsUnapprovedOperationsBeforeProcess(string variable) {
        var runner = new Runner();
        await Assert.ThrowsAsync<ArgumentException>(() => new FastbootClient(runner).GetAsync("serial", variable));
        Assert.Empty(runner.Calls);
    }
    [Fact]
    public async Task DisconnectionAndFailuresAreNotSuccess() {
        await Assert.ThrowsAsync<IOException>(() => new FastbootClient(new Runner(new AdbResult(0, "", ""))).GetAsync("serial", "product"));
        await Assert.ThrowsAsync<IOException>(() => new FastbootClient(new Runner(new AdbResult(0, "serial fastboot", ""), new AdbResult(1, "", "FAILED (remote: unsupported)"))).GetAsync("serial", "unlocked"));
        await Assert.ThrowsAsync<IOException>(() => new FastbootClient(new Runner(new AdbResult(0, "serial fastboot", ""), new AdbResult(0, "", "FAILED (remote: unsupported)"))).GetAsync("serial", "product"));
    }
    [Theory]
    [InlineData("OS1.0.4.0", "1")][InlineData("OS2.0.4.0.VMAMIXM", "2")]
    [InlineData("3.0.4.0", "3")][InlineData("OS4.0.1.0", "Unknown")][InlineData("future", "Unknown")]
    public void HyperOsGeneration(string version, string expected) => Assert.Equal(expected,
        HyperOsDetector.Detect(Info(("ro.mi.os.version.name", version))).Generation);
    [Fact]
    public void ConflictingGenerationAndAndroidVersionDoNotGuess() {
        Assert.Equal("Unknown", HyperOsDetector.Detect(Info(("ro.mi.os.version.name", "OS2.0.1.0"), ("ro.mi.os.version.code", "3"))).Generation);
        Assert.Equal("Unknown", HyperOsDetector.Detect(Info(("ro.build.version.release", "16"))).Os);
        Assert.Equal("Unknown", HyperOsDetector.Detect(Info()).Version);
    }
    [Theory]
    [InlineData("OS2.0.4.0.VMAMIXM", "Global")][InlineData("OS2.0.4.0.VMAEUXM", "EEA")]
    [InlineData("V14.0.4.0.TMACNXM", "China")][InlineData("unknown", "Unknown")]
    [InlineData("OS2.0.4.0.VMAZZXM", "Unknown")]
    public void RegionParsing(string version, string expected) => Assert.Equal(expected,
        HyperOsDetector.Detect(Info(("ro.build.version.incremental", version))).Region);
    [Fact]
    public void ExplicitRegionChannelAndUnlockProperties() {
        var info = Info(("ro.miui.region", "IN"), ("ro.miui.build.type", "stable"), ("ro.boot.flash.locked", "1"),
            ("ro.boot.verifiedbootstate", "green"), ("sys.oem_unlock_allowed", "0"), ("ro.bootmode", "recovery"));
        var firmware = HyperOsDetector.Detect(info);
        Assert.Equal("IN", firmware.Region); Assert.Equal("Stable", firmware.BuildChannel); Assert.Equal("Recovery", firmware.BootMode);
        Assert.Equal(new UnlockStatus("Locked", "green", "Not allowed"), UnlockStatus.From(info));
        Assert.Equal(new UnlockStatus("Unknown", "Unknown", "Unknown"), UnlockStatus.From(Info()));
    }
    [Fact]
    public void CompatibilityHasAllCategoriesAndDoesNotClaimEligibility() {
        var entries = CompatibilityChecker.Check(Info(("ro.product.brand", "POCO"), ("ro.mi.os.version.name", "OS2.0.4.0")));
        Assert.Equal(8, entries.Count); Assert.Equal(CompatibilityStatus.Supported, entries[0].Status);
        Assert.Equal(CompatibilityStatus.Supported, entries[1].Status);
        Assert.Equal(CompatibilityStatus.CheckRequired, entries[6].Status);
        Assert.Equal(CompatibilityStatus.Unsupported, entries[7].Status);
        Assert.Contains("Check required", CompatibilityChecker.Format(entries));
        Assert.Equal(CompatibilityStatus.Unknown, CompatibilityChecker.Check(null)[1].Status);
        Assert.Equal(CompatibilityStatus.Incompatible, CompatibilityChecker.Check(Info(("ro.miui.ui.version.name", "V14")))[1].Status);
    }
    [Theory]
    [InlineData("10008", "Binding / service")][InlineData("error 86006", "Binding")]
    [InlineData("Couldn't verify device", "Verification")][InlineData("Current account is not bound to this device", "Account binding")]
    [InlineData("99999", "Unknown")][InlineData("1100089", "Unknown")]
    public void ErrorLookup(string message, string category) {
        var error = XiaomiErrorDatabase.Lookup(message); Assert.Equal(category, error.Category); Assert.NotEmpty(error.Troubleshooting);
    }
    [Theory]
    [InlineData("")][InlineData("-s")][InlineData("one two")][InlineData("one\n")]
    public async Task RejectsInvalidSerial(string serial) {
        var runner = new Runner();
        await Assert.ThrowsAsync<ArgumentException>(() => new FastbootClient(runner).GetAsync(serial, "all"));
        Assert.Empty(runner.Calls);
    }
    [Fact]
    public async Task DevicesMayAlsoBeReportedOnStderr() {
        var client = new FastbootClient(new Runner(new AdbResult(0, "", "serial fastboot")));
        Assert.Equal("serial", Assert.Single(await client.DevicesAsync()).Serial);
    }
    [Fact]
    public void RecoveryAndFastbootReportsRemainObservable() {
        var recovery = Info() with { Device = new("serial", AdbDeviceState.Unknown, "recovery", new Dictionary<string, string>()) };
        Assert.Equal("Recovery", HyperOsDetector.BootMode(recovery));
        var fastboot = new FastbootInformation(new("serial"), new Dictionary<string, string> { ["unlocked"] = "no", ["product"] = "zeus" });
        var entries = CompatibilityChecker.Check(null, fastboot);
        Assert.Equal("Locked", entries[2].Detail); Assert.Equal(CompatibilityStatus.Supported, entries[5].Status);
        Assert.Equal("zeus", entries[0].Detail); Assert.Equal(CompatibilityStatus.Unknown, entries[3].Status);
        Assert.Equal(CompatibilityStatus.Unknown, entries[6].Status);
        Assert.Equal("Unknown", XiaomiErrorDatabase.Lookup("please unlock someday").Category);
        Assert.Equal("Waiting period", XiaomiErrorDatabase.Lookup("Please unlock 168 hours later").Category);
    }
    [Fact]
    public async Task FastbootOverrideUsesSharedProcessAndStderr() {
        if (OperatingSystem.IsWindows()) return;
        var path = Path.Combine(Path.GetTempPath(), "fastboot-" + Guid.NewGuid());
        var previous = Environment.GetEnvironmentVariable("HYPERSPLOIT_FASTBOOT_PATH");
        try {
            File.WriteAllText(path, "#!/bin/sh\nif [ \"$1\" = devices ]; then printf 'serial fastboot\\n'; else printf 'unlocked: no\\n' >&2; fi\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Environment.SetEnvironmentVariable("HYPERSPLOIT_FASTBOOT_PATH", path);
            Assert.Equal("Locked", (await new FastbootClient().GetAsync("serial", "unlocked")).Bootloader);
        } finally { Environment.SetEnvironmentVariable("HYPERSPLOIT_FASTBOOT_PATH", previous); File.Delete(path); }
    }
}
