# HyperSploit Next

A portable terminal application for standard Android management over system
Wireless ADB, Xiaomi/Redmi/POCO diagnostics, and read-only Fastboot inspection.
HyperSploit Next **does not bypass patched HyperOS security mechanisms**. There
are no account-binding, request-forgery, flashing, wiping, or unlocking workflows.
Historical HyperSploit source is isolated in [LegacyReference](LegacyReference/README.md)
and excluded from builds and releases.

## Supported platforms and ARM64

Managed .NET 9 builds support Linux ARM64/x64, macOS ARM64/x64, Windows x64, and
Alpine Linux AArch64 (`linux-musl-arm64`). No NativeAOT toolchain is needed.
Alpine uses musl; `linux-arm64` packages require glibc and are not Alpine packages.
The app needs no GUI, systemd, root, USB passthrough, or privileged kernel APIs for
Wireless ADB. A supported .NET runtime and a working system `adb` are required.

The intended iPad workflow is **iPad → AArch64 iSH guest → Alpine AArch64 → system
adb → Wireless ADB → Android**. Verify the guest first: standard upstream iSH
uses an **i386** guest, even on ARM64 iPads, and cannot run .NET 9. An ARM64 iSH
variant must actually report `aarch64` and support .NET runtime requirements.
Native Alpine support does not prove any particular iSH variant works.

## Alpine / iSH AArch64 quick-start

Use Alpine **3.22 AArch64**, with its matching `main` and `community` repositories
enabled in `/etc/apk/repositories`. Package names verified against Alpine's
[dotnet9-runtime](https://pkgs.alpinelinux.org/package/v3.22/community/aarch64/dotnet9-runtime),
[dotnet9-sdk](https://pkgs.alpinelinux.org/package/v3.22/community/aarch64/dotnet9-sdk),
and [android-tools](https://pkgs.alpinelinux.org/package/v3.22/community/aarch64/android-tools)
index. Package installation needs the guest's package-management privileges;
running the application does not require root.

```sh
uname -m                         # must print aarch64, not i386/i686
cat /etc/alpine-release           # use a supported matching repository branch
apk update
apk add android-tools dotnet9-runtime icu-libs
adb version
dotnet --info
```

Copy the framework-dependent `portable` release directory into the guest, then:

```sh
cd /path/to/portable
adb pair IP:PAIRING_PORT          # enter the current code at adb's prompt
adb connect IP:DEBUGGING_PORT     # a different port from pairing
adb devices -l
dotnet HyperSploit.Next.dll --version
dotnet HyperSploit.Next.dll doctor
dotnet HyperSploit.Next.dll
```

Use menu **11** to select the online device. Alternatively use the self-contained
`linux-musl-arm64` package and run `./HyperSploit.Next`; it still needs system
`android-tools` and OS runtime dependencies. This release is validated on the
available Linux host; native Alpine ARM64 CI is configured, and physical iPad,
iSH variant and Android connectivity validation require those environments.

## Wireless ADB setup

Android 11+ normally provides Developer options → Wireless debugging. Enable it,
put the host and Android device on the same reachable Wi-Fi, then choose “Pair
device with pairing code.” Pair using that dialog's IP/port and six-digit code.
Connect using the separate IP/port on the Wireless debugging screen. Both ports
may change. Menu 11 offers pair, connect/reconnect, disconnect, list and select.
No USB bootstrap is required for Android's supported wireless pairing flow.
The app never stores pairing codes or Xiaomi credentials. ADB itself maintains
its own normal host authorization files; these are not copied into app config.

## Commands and Android tools

```sh
dotnet HyperSploit.Next.dll --help
dotnet HyperSploit.Next.dll --version
dotnet HyperSploit.Next.dll --diagnostics # host/path inspection; no ADB startup
dotnet HyperSploit.Next.dll doctor        # tool versions and device status
```

Run without arguments for a numbered terminal menu. It supports device
properties, interactive or one-shot ADB shell, package listing/info and normal
APK install/uninstall, push/pull, filtered logcat streaming/saving, and confirmed
reboots. These standard ADB operations act on the selected authorized device;
Android permissions still apply. Application changes, overwrites, log clearing
and reboot prompts are retained from Phase 3. See [Android tools](docs/android-tools.md)
and [Wireless ADB](docs/wireless-adb.md).

Xiaomi diagnostics report firmware properties, vendor, codename, HyperOS/MIUI
signals, region, boot mode and device-reported bootloader status. Compatibility
reports distinguish evidence from Unknown; an offline error database provides
safe troubleshooting. This is not an independent security attestation or a claim
that historical exploit code works. See [Xiaomi diagnostics](docs/xiaomi-fastboot.md).

Fastboot uses system `fastboot` and only `devices` and allowlisted `getvar`
inspection. It requires explicit serial selection. No Fastboot mutation is
provided. Fastboot normally requires physical USB in bootloader mode and is
optional/unavailable in the iSH Wireless ADB workflow.

## Configuration

Default: `~/.config/hypersploit-next/config.json`. If `XDG_CONFIG_HOME` is set,
use `$XDG_CONFIG_HOME/hypersploit-next/config.json`. Missing, inaccessible, or
corrupt files fall back to defaults; preferences are saved atomically. Edit the
file while the app is closed. Example (executable paths are platform specific):

```json
{
  "adbPath": "/usr/bin/adb",
  "fastbootPath": "/usr/bin/fastboot",
  "lastSelectedDevice": null,
  "lastWirelessAddress": "192.168.1.10:37001",
  "autoReconnect": false,
  "cli": { "terminalWidth": 48 }
}
```

Resolution order: `HYPERSPLOIT_ADB_PATH` / `HYPERSPLOIT_FASTBOOT_PATH`, config
path, then `PATH`. Overrides are executable file paths, not shell commands; an
invalid explicit override produces an error. Environment values are never saved.
The selected device and successful wireless connection address are remembered.
Opt-in `autoReconnect` attempts that address once on menu startup. The old
`HyperSploit/selected-device.json` is no longer used: select the device again.
No pairing codes, Xiaomi credentials, keys, or unrelated secrets belong in this
file. Unknown fields are discarded when the app writes preferences.

## Build and package

Install a .NET 9 SDK, then from the repository root:

```sh
dotnet restore
dotnet build
dotnet test
dotnet run --project HyperSploit.csproj -- --help
# Framework-dependent, architecture-portable DLL release:
dotnet publish HyperSploit.csproj -c Release --self-contained false \
  -p:UseAppHost=false -o artifacts/release/portable
# Example self-contained native-host release (managed, no AOT):
dotnet publish HyperSploit.csproj -c Release -r linux-musl-arm64 \
  --self-contained true -p:PublishAot=false -o artifacts/release/linux-musl-arm64
# Repeatable clean packages for all targets, including LICENCE and NOTICE:
sh scripts/publish.sh
```

Build from source on Alpine with `apk add dotnet9-sdk android-tools icu-libs git`.
The publish script produces `linux-arm64`, `linux-x64`, `osx-arm64`, `osx-x64`,
`win-x64`, `linux-musl-arm64` and `portable` directories. Each native package
contains its executable, managed application and .NET runtime files; portable
requires an installed .NET 9 runtime. No tests, legacy code/APK, bundled ADB,
Fastboot, or debug symbols are packaged. Copy `LICENCE` and `NOTICE` alongside
manual publish output when redistributing. CI builds/tests and smoke tests on
native target hosts and Alpine ARM64. Cross-publish success alone does not prove
execution on another OS.

## Troubleshooting and limitations

- Run `doctor` to see OS, OS/process architectures, .NET runtime/RID, tool paths,
  versions and both device lists. Missing/failing tools produce Unknown and exit
  status 1; Fastboot absence does not prevent Wireless ADB use. Unknown arguments
  exit 2. `--diagnostics` requires no device contact.
- `adb` missing: install `android-tools`, check `PATH`, and remove invalid config
  or environment overrides. “Exec format” errors indicate the wrong architecture;
  glibc builds do not run on Alpine musl. Narrow diagnostics wrap to the terminal
  width; redirected output uses the configured width (default 48).
- Pairing failed: reopen the pairing dialog and use its current port/code. For
  offline devices reconnect with the current debugging port; unauthorized
  devices require approval on Android. Guest networking must reach the device;
  client-isolated Wi-Fi or blocked ports can prevent connection.
- On stock iSH i386, installing an ARM64 binary will not solve the architecture
  mismatch. A working AArch64 guest and .NET runtime are external prerequisites.
  No physical iPad/Android hardware validation is claimed.
- .NET 9 is the existing target and requires a supported runtime distribution;
  it is a short-term release, so a future runtime upgrade is needed for continued
  upstream servicing. NativeAOT is disabled. No bundled platform tools exist.
- Device-reported properties may be absent or inconsistent. Future firmware
  generations remain Unknown where evidence is insufficient. No security bypass,
  key extraction, bootloader circumvention or exploit restoration is supported.
- Config is a single-user preference file; simultaneous app instances can
  overwrite each other's latest preferences. Keep only non-secret values in it.

## License and attribution

MPL-2.0, see [LICENCE](LICENCE) and [NOTICE](NOTICE). Derived from
[TheAirBlow's original HyperSploit](https://github.com/TheAirBlow/HyperSploit),
with original-author and contributor credit preserved. Next maintenance and
platform work is by hazerbvisor and contributors. Historical source/assets are
retained solely for historical/source-analysis purposes in
[LegacyReference](LegacyReference/README.md), with no compiled legacy entrypoint.
