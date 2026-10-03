using HyperSploit.Adb;
using HyperSploit.Fastboot;
using HyperSploit.Xiaomi;
namespace HyperSploit;

public static class DiagnosticsCli {
    public static void Write(string text) {
        var width = new ConfigStore().Load().Cli.TerminalWidth;
        try { if (!Console.IsOutputRedirected) width = Math.Clamp(Console.WindowWidth - 1, 20, 72); } catch (IOException) { }
        foreach (var original in text.Split('\n')) {
            var line = original;
            while (line.Length > width) {
                var split = line.LastIndexOf(' ', width - 1, width);
                if (split < 1) split = width;
                Console.WriteLine(line[..split]);
                line = line[split..].TrimStart();
            }
            Console.WriteLine(line);
        }
    }
    private static string Read(string prompt) {
        Console.Write(prompt + ": ");
        return Console.ReadLine()?.Trim() ?? throw new EndOfStreamException();
    }
    private static async Task<string?> SelectFastbootAsync(FastbootClient client, CancellationToken ct) {
        var devices = await client.DevicesAsync(ct);
        if (devices.Count == 0) { Write("Fastboot: Disconnected / Unknown"); return null; }
        Write("Fastboot devices:");
        foreach (var device in devices) Write("  " + device.Serial);
        var serial = Read("Serial (empty = back)");
        if (serial.Length == 0) return null;
        if (!devices.Any(device => device.Serial == serial)) throw new ArgumentException("Choose a listed serial.");
        return serial;
    }
    public static async Task FastbootAsync(CancellationToken ct) {
        var client = new FastbootClient();
        var serial = await SelectFastbootAsync(client, ct);
        if (serial == null) return;
        Write("Mode: Fastboot");
        var variable = Read("Variable: product/unlocked/current-slot/secure/all");
        var info = await client.GetAsync(serial, variable, ct);
        Write($"Bootloader: {info.Bootloader}\nCurrent slot: {info.CurrentSlot}");
        foreach (var pair in info.Variables) Write($"{pair.Key}:\n  {pair.Value}");
        if (info.Variables.Count == 0) Write("Variables: Unknown");
    }
    public static async Task XiaomiAsync(DeviceDiscovery discovery, string? selected, CancellationToken ct) {
        if (selected == null) throw new IOException("Select an ADB device using option 11.");
        var info = await discovery.ReadAsync(selected, ct);
        var firmware = HyperOsDetector.Detect(info);
        var unlock = UnlockStatus.From(info);
        Write($"Vendor: {firmware.Vendor}\nModel: {firmware.Model}\nCodename: {firmware.Codename}\nOS: {firmware.Os}\nVersion: {firmware.Version}\nGeneration: {firmware.Generation}\nRegion: {firmware.Region}\nChannel: {firmware.BuildChannel}\nAndroid: {firmware.Android}\nSecurity patch: {firmware.SecurityPatch}\nBootloader: {unlock.Bootloader}\nVerified boot: {unlock.VerifiedBoot}\nOEM unlocking: {unlock.OemUnlocking}\nBoot mode: {firmware.BootMode}");
    }
    public static async Task CompatibilityAsync(DeviceDiscovery discovery, string? selected, CancellationToken ct) {
        DeviceInformation? info = null;
        var adbState = "Unknown / not selected";
        try {
            var devices = await discovery.ListAsync(ct);
            var device = devices.FirstOrDefault(device => device.Serial == selected);
            adbState = device?.RawState ?? "Disconnected / not selected";
            if (device != null && (device.State == AdbDeviceState.Online || device.RawState == "recovery"))
                info = await discovery.ReadAsync(device.Serial, ct);
        } catch (Exception e) when (e is IOException or TimeoutException) { adbState = e.Message; }
        FastbootInformation? fastboot = null;
        var fastbootState = "Unknown";
        try {
            var client = new FastbootClient();
            var devices = await client.DevicesAsync(ct);
            fastbootState = devices.Count == 0 ? "Disconnected / Unknown" : "Visible; inspection requires explicit serial";
            if (devices.Count > 0 && info == null) {
                var serial = await SelectFastbootAsync(client, ct);
                if (serial != null) fastboot = await client.GetAsync(serial, "all", ct);
            }
        } catch (Exception e) when (e is IOException or TimeoutException) { fastbootState = e.Message; }
        Write(CompatibilityChecker.Format(CompatibilityChecker.Check(info, fastboot, adbState, fastbootState)));
        Write($"Boot mode: { (info != null ? HyperOsDetector.BootMode(info) : fastboot != null ? "Fastboot" : "Unknown / disconnected") }");
    }
}
