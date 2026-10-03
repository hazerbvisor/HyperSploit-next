# HyperSploit Next

Phase 2 provides system ADB Wireless Debugging and read-only Android/Xiaomi
device information. The default CLI does not expose the historical bypass code.
No exploit restoration, binding manipulation, unlocking, flashing, APK manager,
interactive shell, file transfer, or logcat UI is included.

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

1. Pair wireless device
2. Connect/reconnect device
3. Disconnect
4. List devices
5. Select device
6. Device information
7. Xiaomi/HyperOS information
8. Doctor
0. Exit

See [Wireless ADB and Alpine instructions](docs/wireless-adb.md) and the
[Phase 1 platform audit](docs/alpine-arm64.md).

## Scope and evidence

Only explicitly available properties are reported. Missing values, uncertain
transport, contradictory bootloader flags, and insufficient OS evidence show
Unknown. Future HyperOS generations are labeled Unknown/future. Android version
and build fingerprint alone do not establish a HyperOS version or marketing
region. Information is device-reported, not an independent security attestation.

Historical source and Windows ADB assets remain in the repository for history;
the Phase 2 menu cannot invoke the legacy workflow and builds exclude ADB assets.
