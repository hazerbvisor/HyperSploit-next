namespace HyperSploit.Adb;

public sealed class DeviceSelectionStore(string? path = null) {
    private readonly ConfigStore store = new(path);
    public string? Load() => store.Load().LastSelectedDevice;
    public void Save(string serial) => store.Update(config => config.LastSelectedDevice = serial);
}
