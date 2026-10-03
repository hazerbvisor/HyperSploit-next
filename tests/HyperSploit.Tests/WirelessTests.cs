using HyperSploit.Adb;

namespace HyperSploit.Tests;

public class WirelessTests {
    private sealed class FakeRunner(params AdbResult[] results) : IAdbCommandRunner {
        public List<(string[] Args, string? Input)> Calls { get; } = [];
        private int index;
        public Task<AdbResult> RunAsync(IReadOnlyList<string> arguments, string? input = null,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add((arguments.ToArray(), input));
            return Task.FromResult(results[index++]);
        }
    }
    private static DeviceInformation Info(params (string Key, string Value)[] pairs) =>
        new(new("192.0.2.1:1234", AdbDeviceState.Online, "device", new Dictionary<string, string>()),
            pairs.ToDictionary(p => p.Key, p => p.Value));
    [Fact]
    public void DeviceListPreservesSerialsStatesAndDuplicateModels() {
        var devices = DeviceDiscovery.Parse("""
            List of devices attached
            * daemon started successfully *
            192.0.2.1:1234 device product:zeus model:Xiaomi_12_Pro device:zeus transport_id:1
            192.0.2.2:1234 offline model:Xiaomi_12_Pro
            abc unauthorized usb:1-1
            xyz recovery
            """);
        Assert.Equal(4, devices.Count);
        Assert.Equal(new[] { AdbDeviceState.Online, AdbDeviceState.Offline,
            AdbDeviceState.Unauthorized, AdbDeviceState.Unknown }, devices.Select(d => d.State));
        Assert.Equal("Xiaomi 12 Pro", devices[0].Model);
        Assert.Equal("Wireless", devices[0].Transport);
        Assert.Equal("USB", devices[2].Transport);
        Assert.Empty(DeviceDiscovery.Parse("List of devices attached\n"));
    }
    [Fact]
    public void AndroidPropertiesAndMissingValues() {
        var properties = DeviceInformation.ParseProperties("""
            [ro.product.model]: [Xiaomi 12 Pro]
            [ro.build.version.release]: [16]
            [ro.build.version.sdk]: [36]
            [ro.build.fingerprint]: [vendor/product/device:16/test]
            [ro.product.name]: []
            malformed
            """);
        var info = new DeviceInformation(Info().Device, properties);
        Assert.Equal("16", info.Get("ro.build.version.release"));
        Assert.Equal("36", info.Get("ro.build.version.sdk"));
        Assert.Equal("Unknown", info.Get("ro.product.name"));
        Assert.Equal("Unknown", info.Get("ro.build.version.security_patch"));
        Assert.Equal("Unknown", info.Os);
        Assert.Equal("Unknown", info.Bootloader);
    }
    [Theory]
    [InlineData("Xiaomi", "Xiaomi")]
    [InlineData("redmi", "Redmi")]
    [InlineData("poco", "POCO")]
    [InlineData("Samsung", "Unknown")]
    [InlineData("NotXiaomi", "Unknown")]
    public void DetectsBrandsWithoutGuessing(string brand, string expected) {
        Assert.Equal(expected, Info(("ro.product.brand", brand)).Vendor);
        Assert.Equal(expected, Info(("ro.product.manufacturer", brand)).Vendor);
    }
    [Theory]
    [InlineData("OS1.0.4.0", "HyperOS 1")]
    [InlineData("OS2.0.4.0.VMAMIXM", "HyperOS 2")]
    [InlineData("3.0.4.0", "HyperOS 3")]
    [InlineData("OS4.0.1.0", "HyperOS Unknown/future")]
    [InlineData("future", "HyperOS Unknown version")]
    public void ParsesHyperOs(string version, string expected) {
        Assert.StartsWith(expected, Info(("ro.mi.os.version.name", version)).Os);
    }
    [Fact]
    public void MiuiAndInsufficientVersionEvidence() {
        Assert.Equal("MIUI V14", Info(("ro.miui.ui.version.name", "V14")).Os);
        Assert.Equal("Unknown", Info(("ro.build.version.incremental", "V816.0.1.0")).Os);
        Assert.StartsWith("HyperOS 3", Info(("ro.mi.os.version.code", "3")).Os);
        Assert.Contains("(GLOBAL)", Info(("ro.mi.os.version.name", "OS3.0.4.0"),
            ("ro.miui.region", "GLOBAL")).Os);
    }
    [Theory]
    [InlineData("1", "locked", "Locked")]
    [InlineData("0", "unlocked", "Unlocked")]
    [InlineData("1", "unlocked", "Unknown")]
    [InlineData("", "locked", "Locked")]
    [InlineData("other", "", "Unknown")]
    public void BootloaderUsesExplicitConsistentEvidence(string flash, string vbmeta, string expected) =>
        Assert.Equal(expected, Info(("ro.boot.flash.locked", flash),
            ("ro.boot.vbmeta.device_state", vbmeta)).Bootloader);
    [Fact]
    public async Task PairConnectReconnectDisconnectUseSeparateArguments() {
        var runner = new FakeRunner(new AdbResult(0, "Successfully paired to 192.0.2.1:1234", ""),
            new AdbResult(0, "connected to 192.0.2.1:4567", ""),
            new AdbResult(0, "already connected to 192.0.2.1:4567", ""),
            new AdbResult(0, "disconnected 192.0.2.1:4567", ""));
        var wireless = new WirelessAdb(runner);
        await wireless.PairAsync("192.0.2.1:1234", "123456");
        await wireless.ConnectAsync("192.0.2.1:4567");
        await wireless.ConnectAsync("192.0.2.1:4567");
        await wireless.DisconnectAsync("192.0.2.1:4567");
        Assert.Equal(new[] { "pair", "192.0.2.1:1234" }, runner.Calls[0].Args);
        Assert.Equal("123456", runner.Calls[0].Input);
        Assert.Equal("disconnect", runner.Calls[3].Args[0]);
    }
    [Theory]
    [InlineData("192.0.2.1:5555", true)]
    [InlineData("[2001:db8::1]:5555", true)]
    [InlineData("192.0.2.1:0", false)]
    [InlineData("192.0.2.1:65536", false)]
    [InlineData("192.0.2.1:5555;touch /tmp/x", false)]
    [InlineData("-s", false)]
    public void ValidatesEndpoints(string endpoint, bool expected) =>
        Assert.Equal(expected, WirelessAdb.IsEndpoint(endpoint));
    [Fact]
    public async Task HandlesZeroExitConnectionFailureAndInvalidPairingCode() {
        var runner = new FakeRunner(new AdbResult(0, "failed to connect", ""));
        var wireless = new WirelessAdb(runner);
        await Assert.ThrowsAsync<IOException>(() => wireless.ConnectAsync("192.0.2.1:5555"));
        await Assert.ThrowsAsync<ArgumentException>(() => wireless.PairAsync("192.0.2.1:5555", "123\n45"));
        Assert.Single(runner.Calls);
    }
    [Theory]
    [InlineData("offline")]
    [InlineData("unauthorized")]
    [InlineData("")]
    public async Task ConnectionLossAndInvalidStatesDoNotReadProperties(string state) {
        var runner = new FakeRunner(new AdbResult(0, state == "" ? "" : $"serial {state}", ""));
        await Assert.ThrowsAsync<IOException>(() => new DeviceDiscovery(runner).ReadAsync("serial"));
        Assert.Single(runner.Calls);
    }
    [Fact]
    public async Task ReadsPropertiesOnlyOnSelectedDevice() {
        var runner = new FakeRunner(new AdbResult(0, "one device\ntwo device", ""),
            new AdbResult(0, "[ro.product.model]: [Example]", ""));
        var info = await new DeviceDiscovery(runner).ReadAsync("two");
        Assert.Equal(new[] { "-s", "two", "shell", "getprop" }, runner.Calls[1].Args);
        Assert.Equal("Example", info.Get("ro.product.model"));
    }
    [Fact]
    public void RemembersSelectionAndToleratesCorruptFile() {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var file = Path.Combine(dir, "selection.json");
        try {
            var store = new DeviceSelectionStore(file);
            Assert.Null(store.Load());
            store.Save("192.0.2.1:5555");
            Assert.Equal("192.0.2.1:5555", new DeviceSelectionStore(file).Load());
            File.WriteAllText(file, "bad JSON");
            Assert.Null(store.Load());
        } finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
