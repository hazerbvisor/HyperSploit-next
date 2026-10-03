# Phase 3 Android tools

Use a system ADB executable and Android Wireless debugging. Choose main menu 11
for pairing, connection, device listing and selection. Return with 0. Duplicate
models remain distinguishable by serial. Management commands preflight that exact
serial and reject missing, offline, unauthorized or non-wireless devices. They
never switch to another visible device. A race with a disconnect is reported
using ADB's error output; reconnect/reselect through menu 11.

## Shell

Menu 2 opens an interactive terminal shell or runs a one-shot executable.
Interactive mode inherits the terminal; type `exit` to return. Ctrl+C stops an
operation and returns to the menu (terminal behavior may also interrupt a remote
command). One-shot mode asks for the executable, then **one argument per line**;
enter a blank line to run. Do not add surrounding quotes: spaces within a line
are preserved. Host process arguments use `ProcessStartInfo.ArgumentList`;
remote shell tokens are individually POSIX-quoted because ADB joins them.
Shell operators and substitutions are literal arguments. If shell syntax is
wanted, explicitly run `sh` with arguments `-c` and the script as a single line.
The user is responsible for commands they enter.

Both output streams are reported. The default one-shot timeout is 20 seconds,
configurable up to 86400 seconds in the CLI. Interactive shell and streams have
no automatic deadline; Ctrl+C cancels. Buffered output is limited to 4 MiB per
stream; use an interactive shell for large output. Cancellation terminates the
local ADB child; it does not guarantee termination of detached Android processes.

## Applications

Menu 3 lists installed package identifiers (optional case-insensitive substring
filter), shows `dumpsys package` information, installs a local `.apk`, or
uninstalls a specified package. Install/uninstall require typing `yes` after
showing the selected serial and requested change. Uninstall removes app data.
No automatic downgrade, uninstall, reinstall, force flag or system package
modification is attempted. Standard Android installation restrictions apply.
Success requires ADB exit zero and an exact `Success` result line; `Failure [...]`
is reported even if ADB exits zero. Install output streams as ADB produces it.
Large `dumpsys` output can hit the buffered-output limit.

## Files

Menu 4 pushes a local file to an absolute Android path or pulls an Android file
to a local **file name**. Paths with spaces need no added quotes. Push requires
confirmation because it can replace remote files. Pull refuses existing local
files unless overwrite is explicitly confirmed; the API also accepts an explicit
`overwrite` option. Downloads stage beside the destination and move into place
only after successful transfer. Cancellation/failure removes staging files and
preserves any prior destination. A file created during transfer is protected when
overwrite is false. Directory transfers are outside this interface.

ADB progress and summary output are streamed when available. Progress cadence
and percentages depend on the installed ADB version and whether it emits progress
on a redirected stream. A final success/error message is always shown. Android
storage permissions still apply; no root or privileged file access is attempted.
A cancelled push may leave a partial remote file.

## Logcat

Menu 5 streams logcat, optionally filtered by one exact tag and a case-insensitive
text substring. Tag filters use `TAG:V *:S`; empty tag/text means all output.
Errors always appear even if they do not match the text filter. Optionally save
matching log lines to a local file. Existing files require overwrite confirmation;
without that option the file is opened with create-new semantics. Ctrl+C stops
streaming, flushes/closes the file and returns to the menu. Log history is not
retained: the process runner keeps only a small diagnostic tail (about 32 KiB per
stream) and splits lines longer than 16 KiB. Saved logs can grow on disk until
stopped; callers must manage available storage. Clear logs is a separate action
that requires confirmation and targets only the selected serial.

## Reboot

Menu 6 offers standard `adb reboot`, `adb reboot recovery`, and
`adb reboot bootloader`. The selected serial and mode are shown before typing
`yes`. Success means ADB accepted the request; Wi-Fi will normally disconnect.
Recovery/bootloader availability depends on the device. Fastboot inspection is available in menu 9. There is no Fastboot write,
flash, wipe, unlock or partition management UI. Reconnect through wireless setup
after returning to Android and enabling Wireless debugging again.

## Alpine ARM64 / prospective iSH ARM64 validation

Stock iSH uses an i386 guest, even on an ARM64 iPad, and cannot run .NET 9.
These commands require an actual Alpine AArch64 guest supporting .NET runtime
requirements. No USB passthrough, GUI, systemd, root at runtime, or privileged
kernel API is required. Package installation may require the guest's normal
administrative setup. Linux x64, macOS and Windows use their system platform tools.

```sh
uname -m # must report aarch64
apk add --no-cache git dotnet9-sdk android-tools icu-libs
git clone https://github.com/hazerbvisor/HyperSploit-next.git
cd HyperSploit-next
git checkout phase-4/xiaomi-fastboot
dotnet restore
dotnet build
dotnet test
adb version
HYPERSPLOIT_ADB_PATH=/usr/bin/adb dotnet run --project HyperSploit.csproj -- --diagnostics
HYPERSPLOIT_ADB_PATH=/usr/bin/adb dotnet run --project HyperSploit.csproj
dotnet publish HyperSploit.csproj -c Release -r linux-musl-arm64 --self-contained true -p:PublishAot=false -o out/alpine-arm64
./out/alpine-arm64/HyperSploit --diagnostics
```

The native Alpine ARM64 CI job restores, builds, runs phone-free tests, publishes
a self-contained musl ARM64 binary and runs diagnostics. For a physical-device
check, use menu 11 to pair/connect/select, then:

1. Run one-shot `id` and `ls` with argument `/sdcard`, then open/exit an interactive shell.
2. List/filter packages; inspect a known package. Explicitly install/uninstall only
   a disposable test APK you control, verifying the confirmation prompts.
3. Push/pull a disposable file with spaces in its name; verify overwrite refusal
   and compare contents. Cancel a transfer and check the local destination survives.
4. Stream a known tag/text filter to a new log file; Ctrl+C and inspect the file.
   Clear logs only after explicitly confirming.
5. On a test device, confirm an Android reboot. Recovery/bootloader checks are
   optional device-specific operations; return to Android using the device controls.
6. Disconnect Wi-Fi during an operation; verify an error/cancellation and reselect.
   With two connected devices, verify only the displayed serial receives commands.

Mocks cannot establish real-device behavior or compatibility with a particular
ARM64 iSH guest. Physical-device and actual iPad guest testing remain manual.
