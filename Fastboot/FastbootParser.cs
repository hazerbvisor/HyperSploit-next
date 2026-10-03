using System.Text.RegularExpressions;
namespace HyperSploit.Fastboot;

public static class FastbootParser {
    public static IReadOnlyList<FastbootDevice> Devices(string output) => output.Split('\n')
        .Select(line => Regex.Match(line.Trim(), @"^(\S+)\s+fastboot$"))
        .Where(match => match.Success).Select(match => new FastbootDevice(match.Groups[1].Value)).Distinct().ToArray();

    public static IReadOnlyDictionary<string, string> GetVariables(string output) {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in output.Split('\n')) {
            var text = Regex.Replace(line.Trim(), @"^\((?:bootloader|fastboot)\)\s*", "");
            if (text.StartsWith("FAILED", StringComparison.OrdinalIgnoreCase) || text.StartsWith("INFO", StringComparison.OrdinalIgnoreCase)) continue;
            // Split on colon + whitespace; partition keys may themselves contain a colon.
            var match = Regex.Match(text, @"^([A-Za-z0-9_.:-]+):\s+(.*?)\s*$");
            if (!match.Success) continue;
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            if (values.TryGetValue(key, out var previous) && previous != value) values[key] = "Unknown";
            else values[key] = value;
        }
        return values;
    }
}

