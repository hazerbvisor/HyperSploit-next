using System.Text.RegularExpressions;

namespace HyperSploit.Adb;

public sealed record DeviceInformation(AdbDevice Device, IReadOnlyDictionary<string, string> Properties) {
    public string Get(string key) => Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value : "Unknown";
    public static IReadOnlyDictionary<string, string> ParseProperties(string text) {
        var result = new Dictionary<string, string>();
        foreach (var line in text.Split('\n')) {
            var match = Regex.Match(line.Trim(), @"^\[([^\]]+)\]: \[(.*)\]$");
            if (match.Success) result[match.Groups[1].Value] = match.Groups[2].Value;
        }
        return result;
    }
    public string Vendor {
        get {
            foreach (var key in new[] { "ro.product.brand", "ro.product.manufacturer" }) {
                var value = Get(key);
                foreach (var brand in new[] { "POCO", "Redmi", "Xiaomi" })
                    if (value.Equals(brand, StringComparison.OrdinalIgnoreCase)) return brand;
            }
            return "Unknown";
        }
    }
    public string Os {
        get {
            var hyper = Get("ro.mi.os.version.name");
            if (hyper == "Unknown") hyper = Get("ro.mi.os.version.incremental");
            var major = Get("ro.mi.os.version.code");
            if (hyper != "Unknown") {
                var match = Regex.Match(hyper, @"^(?:OS)?(\d+)\.\d+(?:\.\d+)*(?:\.[A-Za-z0-9]+)?$");
                if (match.Success) major = match.Groups[1].Value;
                else return $"HyperOS Unknown version ({hyper})";
            }
            if (major != "Unknown") {
                var generation = major switch {
                    "1" => "1", "2" => "2", "3" => "3", _ => "Unknown/future"
                };
                var region = Get("ro.miui.region");
                return $"HyperOS {generation}: {hyper}" + (region == "Unknown" ? "" : $" ({region})");
            }
            var miui = Get("ro.miui.ui.version.name");
            return miui != "Unknown" ? $"MIUI {miui}" : "Unknown";
        }
    }
    public string Bootloader {
        get {
            var states = new List<string>();
            var locked = Get("ro.boot.flash.locked");
            if (locked == "1") states.Add("Locked");
            if (locked == "0") states.Add("Unlocked");
            var vbmeta = Get("ro.boot.vbmeta.device_state");
            if (vbmeta.Equals("locked", StringComparison.OrdinalIgnoreCase)) states.Add("Locked");
            if (vbmeta.Equals("unlocked", StringComparison.OrdinalIgnoreCase)) states.Add("Unlocked");
            return states.Distinct().Count() == 1 ? states[0] : "Unknown";
        }
    }
}
