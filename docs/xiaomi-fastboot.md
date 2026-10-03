# Phase 4 Xiaomi diagnostics and Fastboot inspection

Menu 7 reads ordinary ADB `getprop` on the explicitly selected device. It reports
Xiaomi/Redmi/POCO brand, model, codename, MIUI/HyperOS version, known HyperOS
generation (1–3), region/channel, Android release/security patch, bootloader,
verified boot, OEM unlocking and observable boot mode. Select Wireless ADB in
menu 11 first. Recovery properties can be read when a selected serial is listed
as recovery and exposes getprop. No mode transition is requested by diagnostics.

Missing, unrecognized or contradictory state is **Unknown**. HyperOS generation
requires an explicit HyperOS version property or version code; Android version
alone is insufficient. Region uses `ro.miui.region` when exposed, otherwise an
exact recognized Xiaomi build suffix (MIXM, EUXM, CNXM, INXM, IDXM, TRXM, TWXM,
RUXM, JPXM). This describes firmware region, not account eligibility or location.
Channel requires `ro.miui.build.type`; Android user/userdebug is not a Xiaomi
release channel. OEM unlocking uses `sys.oem_unlock_allowed` only and is distinct
from the current bootloader lock state and Xiaomi account approval.

Menu 8 shows Device, HyperOS, Bootloader, Verified boot, ADB, Fastboot, Official
unlock and Legacy HyperSploit rows. Supported means the indicated local diagnostic
is recognized/available; it does not attest security or promise unlocking.
Official unlock is **Check required** for a recognized Xiaomi-family device:
account binding, eligibility and waiting time are not inferable from local
properties. Use Xiaomi's official guidance/tool to check these. Legacy HyperSploit
is **Unsupported** because its bypass workflow is not implemented or tested.
A known MIUI firmware is Incompatible with the HyperOS diagnostic row. Unknown
and disconnected transport states remain visible even when a tool is missing.

## Fastboot

Install system Android platform tools. `HYPERSPLOIT_FASTBOOT_PATH` overrides
PATH and must name an executable file, not a command string. Diagnostics never
use bundled Fastboot. Menu 9 lists all visible devices and asks for an explicit
serial even if only one exists. It then accepts exactly one variable:

```text
product
unlocked
current-slot
secure
all
```

The only invocations are `fastboot devices` and
`fastboot -s SERIAL getvar VARIABLE`. Serial existence is checked before each
query. Both stdout and stderr are parsed, since Fastboot commonly prints getvar
results to stderr. Unlocked yes/no (also true/false or 1/0) maps to lock state;
current-slot a/b maps to slot. Missing or conflicting values are Unknown.
`secure` is a raw bootloader variable, not Android verified boot attestation.
Unsupported getvars produce an inspection error; they are never treated as
success or followed by another command. getvar all varies by bootloader and may
include device identifiers; review before sharing output.

The shared runner uses argument lists, a 20-second timeout, cancellation,
concurrent output capture and a 4 MiB per-stream limit. Fastboot has no unlock,
flash, erase, format, wipe, reboot or partition-write methods. USB availability
is host-dependent; this does not add USB passthrough to iSH. Fastboot requires the
device already be in an observable Fastboot mode. Android boot mode is reported
only from explicit properties; an online ADB connection alone does not prove it.

## Offline Xiaomi error reference

Menu 10 matches selected binding codes (10008, 86006) and known message fragments.
It shows explanation, category and safe official troubleshooting. Numeric meanings
can vary by firmware/tool, so entries are qualified and unknown codes remain
Unknown. Waiting-period text must include a numeric hour count; the lookup never
calculates or overrides the service's wait. No Xiaomi network request is made,
forged, intercepted, modified or resent. This small reference is not exhaustive.

## Validation

Phone-free mocked tests cover multiple devices, stdout/stderr, explicit serials,
getvar failures, locked/unlocked states, slots, firmware/region parsing, missing
properties, compatibility and error lookup. A local fake executable exercises the
system Fastboot override. Run:

```sh
dotnet restore
dotnet build
dotnet test
dotnet publish HyperSploit.csproj -c Release -r linux-musl-arm64 --self-contained true -p:PublishAot=false -o out/alpine-arm64
```

The Alpine ARM64 workflow also builds/tests/publishes and smoke-tests the native
musl ARM64 executable, ADB and Fastboot resolution. Real devices and iPad/iSH
USB transport remain manual validation. New diagnostic output wraps to fit narrow
terminals; menus remain plain numbered text.
