using System.Text.Json;

namespace HyperSploit.Adb;

public sealed class DeviceSelectionStore(string? path = null) {
    private readonly string file = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HyperSploit", "selected-device.json");
    public string? Load() {
        try { return JsonSerializer.Deserialize<string>(File.ReadAllText(file)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
    public void Save(string serial) {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllText(temp, JsonSerializer.Serialize(serial));
            File.Move(temp, file, overwrite: true);
        } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
