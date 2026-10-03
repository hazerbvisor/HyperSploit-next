using HyperSploit.Adb;
using HyperSploit.Fastboot;
namespace HyperSploit.Xiaomi;

public enum CompatibilityStatus { Supported, Unsupported, Incompatible, Unknown, CheckRequired }
public sealed record CompatibilityEntry(string Feature, CompatibilityStatus Status, string Detail);
public static class CompatibilityChecker {
    public static IReadOnlyList<CompatibilityEntry> Check(DeviceInformation? info, FastbootInformation? fastboot = null,
        string adbState = "Unknown", string fastbootState = "Unknown") {
        var firmware = info == null ? null : HyperOsDetector.Detect(info);
        var unlock = info == null ? null : UnlockStatus.From(info);
        var bootloader = unlock?.Bootloader ?? fastboot?.Bootloader ?? "Unknown";
        return [
            new("Device", firmware?.Vendor is "Xiaomi" or "Redmi" or "POCO" ? CompatibilityStatus.Supported : CompatibilityStatus.Unknown,
                firmware == null ? fastboot?.Get("product") ?? "Unknown" : $"{firmware.Vendor} / {firmware.Model} / {firmware.Codename}"),
            new("HyperOS", firmware?.Os == "HyperOS" && firmware.Generation != "Unknown" ? CompatibilityStatus.Supported :
                firmware?.Os == "MIUI" ? CompatibilityStatus.Incompatible : CompatibilityStatus.Unknown,
                firmware == null ? "Unknown" : $"{firmware.Os} {firmware.Version}; generation {firmware.Generation}"),
            new("Bootloader", bootloader == "Unknown" ? CompatibilityStatus.Unknown : CompatibilityStatus.CheckRequired, bootloader),
            new("Verified boot", unlock?.VerifiedBoot is null or "Unknown" ? CompatibilityStatus.Unknown : CompatibilityStatus.CheckRequired, unlock?.VerifiedBoot ?? "Unknown"),
            new("ADB", info != null ? CompatibilityStatus.Supported : CompatibilityStatus.Unknown, info?.Device.RawState ?? adbState),
            new("Fastboot", fastboot != null ? CompatibilityStatus.Supported : CompatibilityStatus.Unknown, fastboot?.Device.Serial ?? fastbootState),
            new("Official unlock", firmware?.Vendor is "Xiaomi" or "Redmi" or "POCO" ? CompatibilityStatus.CheckRequired : CompatibilityStatus.Unknown,
                "Eligibility, binding and waiting period require Xiaomi's official tools; not observable locally."),
            new("Legacy HyperSploit", CompatibilityStatus.Unsupported, "Legacy bypass workflow is not implemented or tested.")
        ];
    }
    public static string Format(IEnumerable<CompatibilityEntry> entries) => string.Join("\n", entries.Select(entry =>
        $"{entry.Feature}: { (entry.Status == CompatibilityStatus.CheckRequired ? "Check required" : entry.Status.ToString()) }\n  {entry.Detail}"));
}
