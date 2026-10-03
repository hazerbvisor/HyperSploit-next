# HyperSploit Next

Phase 4 adds Xiaomi/Redmi/POCO firmware diagnostics, a compatibility report,
an offline Xiaomi error reference, and read-only system Fastboot inspection.
Standard Android management over Wireless ADB remains available.

The new diagnostic paths do not implement flashing, wiping, unlocking,
account-binding manipulation or security bypasses.

## Build and run

Install a .NET 9 SDK and your platform's Android platform tools (ADB and Fastboot).

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
7. Xiaomi diagnostics
8. Compatibility report
9. Fastboot information
10. Xiaomi error lookup
11. Wireless setup / select device
12. Doctor
0. Exit
```

Start with **11** to pair, connect and select a device. See
[Phase 4 diagnostics](docs/xiaomi-fastboot.md),
[Android management and ARM64 validation](docs/android-tools.md),
[Wireless setup](docs/wireless-adb.md) and the
[historical Phase 1 platform audit](docs/alpine-arm64.md).

## Scope and evidence

Only explicitly available properties are reported. Missing values, uncertain
transport, contradictory bootloader flags, and insufficient OS evidence show
Unknown. Future HyperOS generations are labeled Unknown in Phase 4 diagnostics. Android version
and build fingerprint alone do not establish a HyperOS version or marketing
region. Information is device-reported, not an independent security attestation.

Historical source and Windows ADB assets remain in the repository for history;
the CLI cannot invoke the legacy workflow and builds exclude ADB assets.
