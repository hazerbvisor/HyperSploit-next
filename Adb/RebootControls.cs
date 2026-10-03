namespace HyperSploit.Adb;

public enum RebootMode { Android, Recovery, Bootloader }
public sealed class RebootControls(SelectedDeviceCommands device) {
    public static string[] Command(RebootMode mode) => mode switch {
        RebootMode.Android => ["reboot"],
        RebootMode.Recovery => ["reboot", "recovery"],
        RebootMode.Bootloader => ["reboot", "bootloader"],
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
    public Task<AdbResult> RebootAsync(RebootMode mode, bool confirmed, CancellationToken ct = default) {
        if (!confirmed) throw new InvalidOperationException("Reboot requires explicit confirmation.");
        return device.RunAsync(Command(mode), ct);
    }
}
