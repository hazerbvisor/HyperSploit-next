namespace HyperSploit.Xiaomi;

public sealed record FirmwareInfo(string Vendor, string Model, string Codename, string Os,
    string Version, string Generation, string Region, string BuildChannel, string Android,
    string SecurityPatch, string BootMode);
