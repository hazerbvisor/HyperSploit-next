using HyperSploit.Adb;
namespace HyperSploit.Xiaomi;

public sealed record UnlockStatus(string Bootloader, string VerifiedBoot, string OemUnlocking) {
    public static UnlockStatus From(DeviceInformation info) => new(info.Bootloader,
        info.Get("ro.boot.verifiedbootstate").ToLowerInvariant() switch {
            "green" => "green", "yellow" => "yellow", "orange" => "orange", "red" => "red", _ => "Unknown"
        }, info.Get("sys.oem_unlock_allowed") switch { "1" => "Allowed", "0" => "Not allowed", _ => "Unknown" });
}
