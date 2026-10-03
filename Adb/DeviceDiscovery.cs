using System.Text.RegularExpressions;

namespace HyperSploit.Adb;

public sealed class DeviceDiscovery(IAdbCommandRunner runner) {
    public static IReadOnlyList<AdbDevice> Parse(string output) {
        var devices = new List<AdbDevice>();
        foreach (var line in output.Split('\n')) {
            var parts = Regex.Split(line.Trim(), @"\s+");
            if (parts.Length < 2 || line.TrimStart().StartsWith('*') ||
                parts[0] == "List" || parts[0] == "adb:") continue;
            var details = new Dictionary<string, string>();
            foreach (var part in parts.Skip(2)) {
                var colon = part.IndexOf(':');
                if (colon > 0) details[part[..colon]] = part[(colon + 1)..];
            }
            var state = parts[1] switch {
                "device" => AdbDeviceState.Online, "offline" => AdbDeviceState.Offline,
                "unauthorized" => AdbDeviceState.Unauthorized, _ => AdbDeviceState.Unknown
            };
            devices.Add(new(parts[0], state, parts[1], details));
        }
        return devices;
    }
    public async Task<IReadOnlyList<AdbDevice>> ListAsync(CancellationToken ct = default) {
        var result = await runner.RunAsync(["devices", "-l"], cancellationToken: ct);
        result.EnsureSuccess();
        return Parse(result.Stdout);
    }
    public async Task<DeviceInformation> ReadAsync(string serial, CancellationToken ct = default) {
        var device = (await ListAsync(ct)).FirstOrDefault(d => d.Serial == serial)
            ?? throw new IOException("Selected device disconnected. Connect or select a device again.");
        if (device.State != AdbDeviceState.Online)
            throw new IOException($"Device is {device.RawState}. Authorize it or reconnect.");
        var result = await runner.RunAsync(["-s", serial, "shell", "getprop"], cancellationToken: ct);
        result.EnsureSuccess();
        return new(device, DeviceInformation.ParseProperties(result.Stdout));
    }
}
