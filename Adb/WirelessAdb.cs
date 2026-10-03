using System.Net;

namespace HyperSploit.Adb;

public sealed class WirelessAdb(IAdbCommandRunner runner) {
    public static bool IsEndpoint(string value) {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace)) return false;
        var split = value.LastIndexOf(':');
        if (split < 1 || !int.TryParse(value[(split + 1)..], out var port) || port is < 1 or > 65535)
            return false;
        var host = value[..split];
        if (host.StartsWith('[') && host.EndsWith(']')) host = host[1..^1];
        return IPAddress.TryParse(host, out _);
    }
    private static void Validate(string endpoint) {
        if (!IsEndpoint(endpoint)) throw new ArgumentException("Enter IP:PORT (IPv6: [IP]:PORT).");
    }
    public async Task<string> PairAsync(string endpoint, string code, CancellationToken ct = default) {
        Validate(endpoint);
        if (code.Length != 6 || code.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Pairing code must contain six digits.");
        // Keep the secret off the process command line.
        var result = await runner.RunAsync(["pair", endpoint], code, ct);
        result.EnsureSuccess();
        if (!result.Output.Contains("Successfully paired", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Pairing failed. Verify the pairing port and code.");
        return result.Output;
    }
    public async Task<string> ConnectAsync(string endpoint, CancellationToken ct = default) {
        Validate(endpoint);
        var result = await runner.RunAsync(["connect", endpoint], cancellationToken: ct);
        result.EnsureSuccess();
        if (!result.Output.Contains("connected to " + endpoint, StringComparison.OrdinalIgnoreCase))
            throw new IOException($"Connection failed: {result.Output}");
        return result.Output;
    }
    public async Task<string> DisconnectAsync(string endpoint, CancellationToken ct = default) {
        Validate(endpoint);
        var result = await runner.RunAsync(["disconnect", endpoint], cancellationToken: ct);
        result.EnsureSuccess();
        return result.Output;
    }
}
