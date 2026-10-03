# Historical Phase 1 audit

Phase 2 supersedes the runtime workflow and removes bundled ADB resolution.
Phase 5 additionally isolates all legacy code and assets in LegacyReference,
removes the old SDK dependencies, and disables AOT in every release workflow.
See [current instructions](../README.md). The audit below describes Phase 1.

# Phase 1: Alpine Linux AArch64

## Platform audit

The application targets .NET 9 and uses managed AdvancedSharpAdbClient and
Spectre.Console. ADB is a separate executable used to start the local server;
device operations then use the ADB protocol. The Settings APK is Android device
content, independent of the host architecture. Phase 1 leaves the legacy device
workflow unchanged and adds no bypasses for patched firmware.

Previously, resources were chosen using the build host OS, Windows extraction
invoked Unix `chmod`, and Linux/macOS startup passed an unresolved `adb` filename.
Only Windows ADB assets actually exist in this repository. They are retained for
Windows x86/x64 targets, excluded for explicit ARM64 targets, and never used on
Linux or macOS. Resolution prefers HYPERSPLOIT_ADB_PATH, then PATH, then bundled
Windows ADB on compatible Windows processes. An invalid override fails explicitly.
An existing local ADB server can be used without a local adb executable.

Managed builds are the default to avoid requiring native AOT toolchains on Alpine.
Existing release jobs explicitly retain Native AOT. Alpine uses the musl runtime
identifier `linux-musl-arm64`, not `linux-arm64` (glibc).

## Exact native Alpine commands

Run on Alpine AArch64 with the community repository enabled and a .NET 9 SDK
available (the official `mcr.microsoft.com/dotnet/sdk:9.0-alpine` ARM64 image also
provides the SDK). Do not use these commands on a 32-bit guest.

```sh
uname -m # must print aarch64
apk add --no-cache dotnet9-sdk android-tools icu-libs git
adb version
git clone https://github.com/hazerbvisor/HyperSploit-next.git
cd HyperSploit-next
git checkout feature/alpine-arm64-phase1
dotnet --info
dotnet restore HyperSploit.sln
dotnet build HyperSploit.sln -c Release --no-restore
dotnet test HyperSploit.sln -c Release --no-build --no-restore
dotnet run --project HyperSploit.csproj -c Release --no-build -- --diagnostics
HYPERSPLOIT_ADB_PATH=/usr/bin/adb dotnet run --project HyperSploit.csproj -c Release --no-build -- --diagnostics
dotnet publish HyperSploit.csproj -c Release -r linux-musl-arm64 --self-contained true -p:PublishAot=false -o out/alpine-arm64
./out/alpine-arm64/HyperSploit --diagnostics
```

`--diagnostics` prints OS, OS/process architecture, runtime, RID, and resolved ADB
without connecting to a device or starting an ADB server. Run without that flag
for the existing interactive workflow. USB/network device access must be supplied
by the host; passing build checks does not verify device connectivity or unlocking.
Overrides are file paths, not shell commands or command arguments. On Unix, the
selected file must have an execute bit and be runnable for the host architecture.

## iSH limitation

Standard iSH emulates an **i386 (32-bit x86)** Linux guest, even on an ARM64 iPhone.
It cannot execute this ARM64 .NET runtime, and .NET 9 does not provide a supported
Linux i386 runtime. Check `uname -m` inside the guest; physical device architecture
does not determine guest architecture. Native Alpine AArch64 support does not
establish standard iSH support. A guest that actually exposes AArch64 and supports
.NET runtime requirements must be tested separately. This is an external platform
blocker, not an ADB path issue.
