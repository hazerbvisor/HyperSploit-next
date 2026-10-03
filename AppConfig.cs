using System.Text.Json;

namespace HyperSploit;

public sealed class AppConfig {
    public string? AdbPath { get; set; }
    public string? FastbootPath { get; set; }
    public string? LastSelectedDevice { get; set; }
    public string? LastWirelessAddress { get; set; }
    public bool AutoReconnect { get; set; }
    public CliPreferences Cli { get; set; } = new();
}
public sealed class CliPreferences {
    public int TerminalWidth { get; set; } = 48;
}

/// <summary>Only non-secret preferences are persisted; environment overrides are never saved.</summary>
public sealed class ConfigStore(string? path = null) {
    public static string DefaultPath => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } root ? root :
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
        "hypersploit-next", "config.json");
    private readonly string file = path ?? DefaultPath;
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true
    };
    public AppConfig Load() {
        try {
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(file), Options) ?? new();
            config.Cli ??= new();
            config.Cli.TerminalWidth = Math.Clamp(config.Cli.TerminalWidth, 20, 120);
            return config;
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Update(Action<AppConfig> update) {
        var config = Load();
        update(config);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllText(temp, JsonSerializer.Serialize(config, Options));
            File.Move(temp, file, overwrite: true);
        } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
