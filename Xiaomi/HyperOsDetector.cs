using HyperSploit.Adb;
using System.Text.RegularExpressions;
namespace HyperSploit.Xiaomi;

public static class HyperOsDetector {
    public static FirmwareInfo Detect(DeviceInformation info) {
        var name = info.Get("ro.mi.os.version.name");
        var incremental = info.Get("ro.mi.os.version.incremental");
        var code = info.Get("ro.mi.os.version.code");
        var version = name != "Unknown" ? name : incremental;
        var hyper = version != "Unknown" || code != "Unknown";
        var miui = info.Get("ro.miui.ui.version.name");
        var generation = "Unknown";
        var match = Regex.Match(version, @"^(?:OS)?([1-9]\d*)\.\d+(?:\.\d+)*(?:\.[A-Z0-9]+)?$");
        var major = match.Success ? match.Groups[1].Value : version == "Unknown" ? code : "Unknown";
        if (major is "1" or "2" or "3" && (code == "Unknown" || code == major)) generation = major;
        var build = info.Get("ro.build.version.incremental");
        var region = info.Get("ro.miui.region");
        if (region == "Unknown") {
            var suffix = Regex.Match(build, @"^(?:OS|V)?\d+(?:\.\d+){2,3}\.([A-Z]{3})([A-Z]{2})XM$");
            if (suffix.Success) region = suffix.Groups[2].Value switch {
                "MI" => "Global", "EU" => "EEA", "CN" => "China", "IN" => "India", "ID" => "Indonesia",
                "TR" => "Turkey", "TW" => "Taiwan", "RU" => "Russia", "JP" => "Japan", _ => "Unknown"
            };
        }
        var channel = info.Get("ro.miui.build.type") switch { "stable" => "Stable", "development" => "Development", "beta" => "Beta", _ => "Unknown" };
        var mode = BootMode(info);
        return new(info.Vendor, info.Get("ro.product.model"), info.Get("ro.product.device"),
            hyper ? "HyperOS" : miui != "Unknown" ? "MIUI" : "Unknown",
            hyper ? version : miui, generation, region, channel,
            info.Get("ro.build.version.release"), info.Get("ro.build.version.security_patch"), mode);
    }
    public static string BootMode(DeviceInformation info) {
        if (info.Device.RawState == "recovery") return "Recovery";
        var mode = info.Get("ro.bootmode");
        if (mode == "Unknown") mode = info.Get("ro.boot.mode");
        return mode.ToLowerInvariant() switch {
            "recovery" => "Recovery", "normal" => "Android", "charger" => "Charger", _ => "Unknown"
        };
    }
}
