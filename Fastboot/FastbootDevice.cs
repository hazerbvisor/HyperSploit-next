namespace HyperSploit.Fastboot;

public sealed record FastbootDevice(string Serial);
public sealed record FastbootInformation(FastbootDevice Device, IReadOnlyDictionary<string, string> Variables) {
    public string Get(string key) => Variables.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : "Unknown";
    public string Bootloader => Get("unlocked").ToLowerInvariant() switch { "yes" or "true" or "1" => "Unlocked", "no" or "false" or "0" => "Locked", _ => "Unknown" };
    public string CurrentSlot => Get("current-slot") is "a" or "b" ? Get("current-slot") : "Unknown";
}
