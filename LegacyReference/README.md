# Historical reference only

This directory preserves original HyperSploit source and assets by TheAirBlow and
contributors under MPL-2.0 (see ../LICENCE and ../NOTICE). Files are moved without
functional changes to preserve attribution and source history.

Original HyperSploit attempted to alter Xiaomi account-binding behavior by using
information emitted by older Settings implementations and representing an older
MIUI client. It also carried an older Settings APK. Its historical target was
unpatched early HyperOS 1 / V816 firmware and affected Settings builds; the source
does not establish a complete model or firmware-version compatibility list.
Patched Settings implementations stopped exposing the information it relied on,
and newer HyperOS security and server validation invalidate those assumptions.
Do not infer compatibility from an Android version or model alone.

This is historical/source-analysis material, not a supported application or
standalone build. The default project explicitly excludes every source file and
asset here, has no legacy command or loader, and distributes none of these assets.
HyperSploit Next does not bypass patched HyperOS security mechanisms. No exploit
restoration, request forgery, key extraction, bootloader circumvention, flashing,
wiping, or unlocking is provided by Next's diagnostic workflows.
