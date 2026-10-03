namespace HyperSploit.Adb;

public enum AdbDeviceState { Online, Offline, Unauthorized, Unknown }
public record AdbDevice(string Serial, AdbDeviceState State, string RawState,
    IReadOnlyDictionary<string, string> Details) {
    public string Model => Details.GetValueOrDefault("model")?.Replace('_', ' ') ?? "Unknown";
    public string Transport => WirelessAdb.IsEndpoint(Serial) ||
        Serial.EndsWith("._adb-tls-connect._tcp", StringComparison.OrdinalIgnoreCase)
        ? "Wireless" : Details.ContainsKey("usb") ? "USB" : "Unknown";
}
