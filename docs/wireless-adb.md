# Wireless ADB setup (Phase 2 foundation)

## Pair, connect, select

Android 11+ Wireless Debugging must be supported and enabled by the device.
Keep the host and device on the same reachable Wi-Fi network.

Open main menu **11** for the wireless setup submenu. The numbered choices below
refer to that submenu; choose 0 to return to the management menu.

1. In Android developer options, open Wireless debugging, then Pair device with
   pairing code. Keep that screen open.
2. Choose menu 1; enter its IP:pairing-port and six-digit code.
3. Return to the main Wireless debugging screen. Choose menu 2 and enter its
   IP:debugging-port. **The connection port differs from the pairing port.**
4. Choose 4 to list devices and 5 to select by number. Duplicate model names
   are disambiguated by serial. Offline and unauthorized entries remain visible.
5. Choose 6 for Android/device properties, or 7 for Xiaomi/OS/boot status.
6. Choose 3 to disconnect a specific IP:port. It does not disconnect all devices.

Pairing code is sent over ADB stdin rather than included in its process arguments.
Endpoints accept IPv4 or IPv6 addresses with a valid port; use [IPv6]:port.
ADB is launched directly with separate arguments, captured stdout/stderr, and a
20-second deadline. Ctrl+C cancels an in-flight setup command and returns to the main menu. No host
shell is used. Phase 3 management commands are documented in [Android tools](android-tools.md).

Selection and successful connection addresses are saved in
`~/.config/hypersploit-next/config.json` (or under `XDG_CONFIG_HOME`). Missing or
corrupt config falls back to defaults; save failures retain the session selection.
Only non-secret preferences are stored, never pairing codes. Reconnect by entering
the current debugging endpoint in menu 2, or leave it empty to retry the last
successful wireless address. Android can change IP/port; reconnect/reselect then.
An mDNS serial requires a numeric IP:port for connect/disconnect. Opt-in
`autoReconnect` attempts the last wireless address once at management-menu startup.
See the [current configuration and quick-start](../README.md).

Device state is refreshed before reading information; connection loss, ADB errors,
unauthorized/offline states and timeouts return to the menu. This is on-demand
discovery, not continuous monitoring or background retries.

## Read-only detection

Required Android properties are displayed when available: manufacturer, brand,
model, device, name, release, SDK, security patch, fingerprint, verified boot,
flash lock and vbmeta device state. Missing/empty properties show Unknown.

Xiaomi, Redmi and POCO detection uses exact case-insensitive brand/manufacturer
values. HyperOS detection uses `ro.mi.os.version.name`,
`ro.mi.os.version.incremental` and `ro.mi.os.version.code`; MIUI detection uses
`ro.miui.ui.version.name`. HyperOS 1, 2 and 3 are recognized, future generations
are explicitly labeled unknown/future, and malformed versions are retained as
unknown evidence. `ro.miui.region` is displayed literally when available; build
suffixes/fingerprints are not guessed into regions. No version is inferred from
Android release alone. Bootloader flags must agree; verified boot is shown
separately and does not substitute for bootloader lock evidence.

## Native Alpine AArch64 / prospective ARM64 iSH guest

Standard iSH runs an i386 guest and cannot run .NET 9. Check the guest architecture
first; the iPad's physical ARM64 CPU is not sufficient. These commands are for an
actual AArch64 Alpine guest with a supported .NET runtime, not stock iSH:

```sh
uname -m # must report aarch64
apk add --no-cache git dotnet9-sdk android-tools icu-libs
git clone https://github.com/hazerbvisor/HyperSploit-next.git
cd HyperSploit-next
git checkout phase-5/release-polish # use main after Phase 5 merges
dotnet restore
dotnet build
dotnet test
adb version
HYPERSPLOIT_ADB_PATH=/usr/bin/adb dotnet run --project HyperSploit.csproj -- --diagnostics
HYPERSPLOIT_ADB_PATH=/usr/bin/adb dotnet run --project HyperSploit.csproj
# In the CLI: pair, connect, list, select, inspect, disconnect; test Wi-Fi loss.
dotnet publish HyperSploit.csproj -c Release -r linux-musl-arm64 --self-contained true -p:PublishAot=false -o out/alpine-arm64
./out/alpine-arm64/HyperSploit.Next
```

No USB passthrough, /dev/bus/usb, systemd, GUI or privileged kernel feature is
required by this workflow. Host network reachability and Android authorization
are still necessary. Wireless menu 8 and main menu 12 run the same doctor command, reporting both tool versions and device status.

## Validation limits

Mock tests cover discovery, states, property parsing, missing values, brand/OS
detection, bootloader flags, safe command arguments and saved selection.
The native Alpine ARM64 CI job restores, builds, tests, publishes and runs
diagnostics. A successful build or mock test does not establish real-device
pairing: a physical Android device and reachable Wi-Fi must be exercised
separately. Stock iSH remains unsupported because of its i386 guest architecture.
