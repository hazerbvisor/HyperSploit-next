namespace HyperSploit.Adb;

/// <summary>Preflight the exact selected serial; never fall back to another device.</summary>
public sealed class SelectedDeviceCommands(IAdbCommandRunner runner, string? serial) {
    public async Task<string[]> ArgumentsAsync(IReadOnlyList<string> command, CancellationToken ct = default) {
        if (string.IsNullOrWhiteSpace(serial)) throw new IOException("No device selected. Select a wireless device first.");
        var device = (await new DeviceDiscovery(runner).ListAsync(ct)).FirstOrDefault(d => d.Serial == serial)
            ?? throw new IOException("Selected device disconnected. Reconnect or select it again.");
        if (device.State != AdbDeviceState.Online) throw new IOException($"Selected device is {device.RawState}. Authorize or reconnect.");
        if (device.Transport != "Wireless") throw new IOException("Phase 3 supports wireless devices only.");
        return ["-s", serial, .. command];
    }
    public async Task<AdbResult> RunAsync(IReadOnlyList<string> command, CancellationToken ct = default) {
        var result = await runner.RunAsync(await ArgumentsAsync(command, ct), cancellationToken: ct);
        result.EnsureSuccess();
        return result;
    }
    public async Task<AdbResult> StreamAsync(IReadOnlyList<string> command, Action<string, bool> output, CancellationToken ct = default) {
        var result = await runner.StreamAsync(await ArgumentsAsync(command, ct), output, ct);
        result.EnsureSuccess();
        return result;
    }
    public async Task<AdbResult> InteractiveAsync(IReadOnlyList<string> command, CancellationToken ct = default) {
        var result = await runner.InteractiveAsync(await ArgumentsAsync(command, ct), ct);
        result.EnsureSuccess();
        return result;
    }
}
