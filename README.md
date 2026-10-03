# HyperSploit Next

Phase 3 adds standard Android management over Wireless ADB: interactive and
one-shot shell, application list/search/info/install/uninstall, file push/pull,
filtered logcat streaming/saving, and Android/recovery/bootloader reboot.
Every management operation targets the explicitly selected wireless serial.
APK changes, reboot, log clearing, and local overwrite require explicit action.

No Fastboot operations, flashing, wiping, unlocking, account-binding manipulation,
USB workflow or security bypass is exposed by the CLI.

## Build and run

Install a .NET 9 SDK and your platform's Android platform tools (ADB).

```sh
dotnet restore
dotnet build
dotnet test
dotnet run --project HyperSploit.csproj
```

ADB resolves from `HYPERSPLOIT_ADB_PATH`, then `PATH`. The override must be an
executable file path, not a command. No bundled ADB executable is used or embedded.
Use `--diagnostics` for host details without starting ADB or contacting a device.

The plain numbered menu fits narrow terminals:

```text
HyperSploit Next
================
Device: Xiaomi 12 Pro
ADB: Wireless (device)
1. Device information
2. ADB shell
3. Applications
4. File transfer
5. Logcat
6. Reboot
7. Xiaomi information
8. Doctor
9. Wireless setup / select device
0. Exit
```

Start with **9** to pair, connect and select a device. See
[Android management and ARM64 validation](docs/android-tools.md),
[Wireless setup](docs/wireless-adb.md) and the
[historical Phase 1 platform audit](docs/alpine-arm64.md).

## Scope and evidence

Only explicitly available properties are reported. Missing values, uncertain
transport, contradictory bootloader flags, and insufficient OS evidence show
Unknown. Future HyperOS generations are labeled Unknown/future. Android version
and build fingerprint alone do not establish a HyperOS version or marketing
region. Information is device-reported, not an independent security attestation.

Historical source and Windows ADB assets remain in the repository for history;
the CLI cannot invoke the legacy workflow and builds exclude ADB assets.
