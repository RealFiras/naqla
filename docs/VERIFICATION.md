# Executed verification — 2026-10-19

## Current 0.7.0-pdfpreview.1 — Advanced PDF Toolkit development draft

Final `scripts/build.ps1` passed with zero warnings/errors, **86/86 automated groups**:
`artifacts/tests-20261019-135710/results.json`. All previous 68 pass, plus 18 real PDF
groups. Tests run with only the three required PDFsharp assemblies, excluding unused
sample/signing libraries and embedded demo fonts (`Directory.Build.targets`). The exact
6.2.4 MIT package/license hashes and exclusions are in `artifacts/pdf-license-verification.json`.
NuGet's audit reported no listed dependency vulnerabilities when checked.

PDF tests use independently written genuine vector fixtures with standard unembedded
Helvetica. They verify single/multi-page operations, exact counts/order, all-page
preservation on reorder, actual separate PDF files, reject-all deletion, document merge
order, numbers/Arabic digits/fonts/positions, Arabic/English shaped vector marks, image
alpha, selected rotations and mixed/cropped page geometry. Reordered and unselected
pages retain identical decoded pixels; source text remains extractable. Original hashes,
read locks, stale-preview refusal, Arabic names, file/directory collisions, pre/in-flight
cancellation and atomic separate-folder publication pass. Owner-only encryption is
independently detected before PDFsharp import (which clears its own flag on decryption).
RGBA watermark PNGs avoid the upstream indexed-alpha import issue found by pixel tests.

Native editor tests render real thumbnails/output pages for all seven tools, compact
layout and a 150% render, require fresh preview before Save, and exercise the real Save
event/ShowDialog return offscreen. These are **programmatic WPF tests, not physical input**.
Gallery `preview/pdf-toolkit.html` switches eight native draft views at 100% size;
`artifacts/pdf-preview-verification.json` checks image loading/tab switching and 390 px
page layout. Native Arabic/English/image watermark PNGs were visually reviewed.

`artifacts/pdf-wheel-regression.json` records pixel-identical earlier conversion/family,
image optimizer, rename/button/crop and video/audio views. Only PDF Tools content changes.
`artifacts/pdf-source-preservation.json` records unchanged hook/overlay/Ctrl/button and
rename/optimizer sources. The wheel renderer adds only mappings to existing pixel icons.

Final installer: 272,639,393 bytes, SHA256
`879FB44FB0224224601C5CCEECB4329DAB41B55619A216AEF7C60BAE25DC13B5`.
`artifacts/diagnostic-package-smoke.json`: 660 extracted/published files hash-match, zero
mismatches, reviewed PDF runtime/license/exclusions and fixture hashes verified, 34 HEIF
native hashes and real extracted HEIC round trip pass. Bundled runtime, singleton,
MainWindowHandle 0, AppReady/tray Visible requested, graceful quit, hooks/OLE/tray cleanup
pass. Image native engine unloaded at idle. Three-second sample 0 ms CPU/98.52 MB working
set, not a benchmark. No installation/startup registry changes: installer **extraction
and lifecycle smoke only**, not clean-PC installation or packaged PDF GUI acceptance.

Fresh genuine manual sources: `artifacts/desktop-pdf-test-20261019-140120`, with source
hashes/page counts and a manifest explicitly not claiming Explorer/design acceptance.
Current portable launch is recorded separately in `artifacts/portable-pdf-status.json`.

Limits: 20 PDFs/200 pages, 256 MB each/512 MB total, supported local/available files;
encrypted/forms/signature-permission/layer PDFs refused. Page content remains vector;
new number/text marks are vector outlines, not searchable text. Document-level
bookmarks/tags/labels/conformance and internal annotation destinations are not promised.
Native parsing/rendering/save calls can delay token cleanup until return.

**Editor design/navigation approval and actual Explorer/thumbnail drag/button, OS DPI,
monitor, clean-PC install and disconnected-network checks remain unverified. Feature 4
is unstarted.** [PDF-TOOLKIT.md](PDF-TOOLKIT.md) records the manual acceptance cases.

## Historical 0.6.0-targetsize.1 — Target-Size Image Optimizer

Final `scripts/build.ps1` passed, zero warnings/errors, **68 passing automated groups**:
`artifacts/tests-20261019-032845/results.json`. Fourteen new groups exercise real presets/
custom/Arabic units, all six encoders and all existing image input formats, pixel lossless
PNG/TIFF, exact caps, JPEG maximal tested integer quality, alpha/white matte, sRGB/EXIF,
aspect-preserving downsize/no upscale, Unicode names/collisions, original hashes/locks,
impossible targets, corrupt/multiframe images, batch partial failure/cancellation, native
preview/Save gates/close cleanup, privacy and independent System.Drawing/helper checks.

One real noisy PNG: 345,317 bytes, 384×256 → JPEG **77,561 bytes**, quality 89, 12 encodes,
under the 102,400-byte preset without dimension changes. Independent same-codec encodes
confirm every higher integer quality exceeded the cap. Other presets and all output
formats are decoded and measured. An impossible one-byte JPEG target actually tries all
100 qualities, reports measured failure and publishes nothing. PNG/TIFF validate equality
against normalized pixels, not just file extension/dimensions. Fresh fixtures and a second
System.Drawing decoder independently check exact cap, source hashes, dimensions and alpha.

The initial full run found that old offscreen tests used WPF's implicit last-window
shutdown; this shut down the shared renderer before the new preview test. The harness
now uses the app's already-existing explicit-shutdown mode from startup. The final run
passes all old/new tests. This is a test setup correction, not a change to tray lifecycle.

Reviewed native `native-optimizer-settings.png`, `native-optimizer-preview.png`,
`native-optimizer-unattainable.png`, `native-optimizer-wheel-1.png`,
`native-optimizer-wheel-2.png` and result render. They are WPF renderer artifacts, not
physical screenshots or Explorer input evidence. Directional numeric Runs corrected
RTL size grouping. PixelButton source/style, circle geometry, colors and capsule remain;
contextual image Tools now uses six/seven sectors. Four conversion panel PNGs and the
video/audio/PDF Tools sheet match 0.5 exactly. The family contact sheet intentionally
changes its image Tools panel. Scoped evidence: `artifacts/target-size-wheel-regression.json`;
unchanged overlay/hook/Ctrl hashes: `artifacts/target-size-gesture-preservation.json`.

HTML guide layout passes at 390 px without horizontal overflow and contains Feature 2
acceptance/limits (`artifacts/feature-guide-verification.json`). Genuine unprocessed
desktop test fixtures: `artifacts/desktop-target-size-test-20261019-033024`.
They contain no app outputs or physical drag evidence. Helpers always keep
`ActualExplorerDragVerified:false`, including after automatic file validation.

Final installer/package smoke passes (`artifacts/optimizer-package-smoke-0.6.json`):
632 published/extracted files hash-identical, 34 locked HEIF files verified, extracted
HEIC encode/decode passed, self-contained runtime loaded, duplicate exits zero, no main
window, graceful quit and native hook/OLE/tray cleanup. Image native engine not loaded
at idle; three-second smoke sample 0 ms CPU / 98.68 MB working set, not a benchmark.
Installer 259.2 MB, SHA256
`8C1447F81ABE78846E341C77CED296A5E406588BE9C9E7AE2C699C4E54C8DA39`.
Portable 0.6.0-targetsize.1 was relaunched and checked on 2026-10-19, PID 2072,
AppReady/tray Visible requested, MainWindowHandle 0. The timestamped runtime snapshot
is `artifacts/portable-target-size-status.json`. No install/startup
registry changes were made. The installer was extracted/smoke-tested, not installed
on a clean target PC.

Limits: existing Q8/single-frame/40 MP pipeline, max 200 images and target range 1 byte–1 GB;
lossy quality domain 1–100, three PNG/two TIFF recipes, sRGB normalization/metadata
removal, white JPG transparency, no implicit downsize and no universal perceptual-quality
claim. Up to 100 encodes per lossy image can be slow; native Magick calls complete before
token cleanup, while the HEIC process can be terminated. Preview and final output are
separately encoded/validated. A cap above source size need not shrink it.

Actual Explorer/settings/button interaction, mixed DPI/monitors, clean-machine install
and intentionally disconnected network are **unverified**. Feature 2's real acceptance
and any required explicit preview approval remain pending. **Feature 3 has not started.**
[TARGET-SIZE.md](TARGET-SIZE.md) gives the acceptance procedure and honest failure cases.

## Historical 0.5.0-rename.1 — Smart Rename and pixel controls

Final `scripts/build.ps1` passed with zero warnings/errors and **54 passing groups**
in `artifacts/tests-20261019-023643/results.json`. Eleven new groups cover all rename
transforms/regex/dates, invalid/colliding names, preview-only behavior, explicit consent,
same-file Windows rename/undo, Arabic names, IDs, byte hashes, MOTW/ADS, different
directories, swaps/case-only changes, stale files, locking, cancellation and rollback.
Injected failures wrap actual Windows moves, not simulated Explorer drags. A deliberately
blocked rollback preserves both original staged data and the unrelated occupant, keeps
its recovery map and returns a distinct local recovery failure. Diagnostics tests prove
that even a recovery exception carrying a private path does not log that path.

An initial swap test exposed NTFS creation-time tunneling; distinct creation dates now
exercise the fix that restores the same locked file's captured creation time. File ID,
length and last-write checks remain strict. Preview edits clear consent immediately and
disable it until a new valid preview. Undo results accurately report restored names.

Native WPF snapshots reviewed: `native-pixel-buttons.png`, `native-batch-progress.png`,
`native-crop-editor.png`, `native-rename-editor.png`, `native-rename-wheel.png` and
`native-rename-undo-result.png`. Arabic clipping found during QA was fixed. Pixel
Button automation names and focusability pass; actual mouse/keyboard input remains a
desktop acceptance test. The old conversion palette snapshots match 0.4 exactly
(`artifacts/rename-wheel-render-regression.json`); real Tools adds the contextual Rename
action using an existing icon. OverlayWindow, DragGestureMonitor and WheelModeState
hashes match the baseline (`artifacts/rename-gesture-preservation.json`). No wheel
geometry, approved preview, Shift gesture or Ctrl-mode redesign was made.

The offline HTML guide passes a 390 px layout check and includes rename confirmation,
undo/recovery and pixel controls (`artifacts/feature-guide-verification.json`). Fresh
disposable rename fixtures are in `artifacts/desktop-rename-test-20261019-023443/10-images`;
preparation performs no application rename or Explorer drag. The prior batch fixtures
remain untouched and the user confirmed that real ten-file batch test is still untried.

No actual Explorer/editor/button click, mixed-DPI/monitor, clean-machine installer or
disconnected-network result is claimed. Computer Use native enumeration was unavailable.
The earlier batch acceptance gate and new rename desktop checklist remain pending.
Features 2–8 are recorded as pending in [FEATURE-AUDIT.md](FEATURE-AUDIT.md); basic
compression/STRIP/PDF conversion is not relabeled as their full implementation. No
plugin execution/isolation or model backend is implemented. No new runtime dependency,
service, telemetry or network client was added by this increment.

Final package smoke passed (`artifacts/rename-package-smoke-0.5.json`): 628 published /
extracted files hash-identical, 34 locked HEIF native files verified, extracted HEIC
encode/decode passed. Bundled runtime, no main window, singleton and graceful quit
passed; native hooks, OLE and tray were cleaned up. No image native engine loaded at
idle. Three-second sample: 0 ms CPU / 98.68 MB working set, not a benchmark.
Installer size 259.2 MB, SHA256
`E1B49B8FD89AC82DDAAE7920C17A4C9B8D5EBBE23C0619298BBE4C8EB2D74BDD`.
Portable 0.5.0-rename.1 is running, PID 12920, AppReady, tray Visible requested true,
MainWindowHandle 0 (`artifacts/portable-rename-status.json`). This is process/lifecycle
evidence, not a physical tray screenshot or Explorer acceptance. No install/startup
registry changes were made.

## Historical 0.4.0-batch.1 — Phase 1 implementation, Explorer acceptance pending

The first expanded run passed 42 groups in `artifacts/batch-tests-20261019-1/results.json`.
Real codec/filesystem coverage includes multiple PNGs and mixed-format images,
multiple videos, mixed image/audio tools, Arabic names, duplicate basenames in different
directories, non-overwriting names, original hashes and decoded outputs. A corrupt
source leaves two valid neighboring jobs successful. A real NTFS ACL denying writes
produces one reported failure while a writable source succeeds; the original ACL is
restored in `finally`. Mid-batch cancellation retains the published first result,
cancels the other eleven and does not show 100% completion. Queue concurrency is
separately checked with an explicitly labeled scheduling test double.

The existing single-file `native-wheel-family.png` and `native-tools-modes.png` match
the 0.3.0 final run byte-for-byte. New `native-batch-wheel.png` and
`native-batch-progress.png` are renderer snapshots, not Explorer drag screenshots.
OverlayWindow, DragGestureMonitor and WheelModeState hashes remain unchanged; the
wheel's only changes are selection/action data and its central count label.

For 200 actual local files, data preparation measured median 0.1344 ms / P95 3.1995 ms,
versus baseline SetFile median 0.0005 ms. This is **not Shift activation latency**.
Final build passed with zero warnings/errors and 43 groups in
`artifacts/tests-20261019-015359/results.json`. Follow-up inspection caught the fixture
generator's BOM-less UTF-8 Arabic literals being read as ANSI by Windows PowerShell
5.1. Corrected the scripts to UTF-8 BOM, asserted exact Arabic source names and ran all
43 groups again successfully in `artifacts/batch-final-tests-20261019-1/results.json`.
This changed only developer scripts/tests, not packaged application code.
Its real fixture verifier proves original hashes, decoded outputs/dimensions/white
matte and 3-success/1-corrupt partial failure; it explicitly leaves
`ActualExplorerDragVerified:false`. No empty-output or automatic fixture generation
can accidentally satisfy the desktop gate.

Final snapshot equality is recorded in `artifacts/batch-wheel-render-regression.json`.
Latest 200-file SetSelection sample: median 0.124 ms / P95 5.0836 ms, baseline SetFile
0.0004 ms. These are data preparation samples, not actual activation latency.

Final package/process smoke (`artifacts/batch-package-smoke-0.4.json`) passed:

- 623 extracted/published files hash-match; 34 locked HEIF native hashes match;
  extracted HEIC encode/decode still passes with real dimensions/color/alpha.
- Bundled runtime, no main window, duplicate instance exits zero, graceful quit
  exits zero, native hooks/OLE/tray cleaned up. Image native engine is not loaded
  at idle startup. No installation/startup registry changes were made.
- Three-second idle sample: 0 ms process CPU, 98.55 MB working set. Prior 0.3.0 sample:
  15.62 ms / 101.11 MB. These are smoke samples, not a controlled benchmark.
- Offline Arabic guide fits 390 px without horizontal overflow and includes the batch
  gate/cancellation/partial-failure instructions. The root guide's Markdown link targets
  are included in the installer. Browser design preview stays unchanged.

Installer: `artifacts/Naqla-Dev-Setup-win-x64.exe`, 259.1 MB. SHA256:
`F0A82AD09D73E62344E794F8010C700D51FF4EC9D8E2FA2882BD8DBC1FDC191A`.
Portable 0.4.0-batch.1 is running (PID 7440), AppReady, requested tray visibility,
MainWindowHandle 0; `artifacts/portable-batch-status.json` records the lifecycle check.
Fresh Arabic acceptance sources: `artifacts/desktop-batch-test-20261019-015624`.

Computer Use initialized, but `sky.list_apps()` returned unavailable native pipe
(OS error 2). No new real Explorer multi-file drag, native progress/crop input,
mixed-DPI/monitor check, clean-machine install or disconnected-network test is claimed.
Per the user's explicit requirement, Phase 1 remains unaccepted until real Explorer
drop → batch outputs succeeds. OCR, archives and AI background removal have not been
started; they follow that gate. [Real acceptance checklist](BATCH-TESTING.md).

## Previous 0.3.0-features.1 — tools, multi-image PDF and real HEIC

All 30 automated groups passed in `artifacts/features-tests-20261019-3/results.json`.
`image-format-matrix.json` records 37 real non-source image-to-image/PDF routes.
Fixtures use actual encoded bytes and local filesystem metadata; they do not claim
to reproduce Explorer input. New coverage includes:

- Crop exact pixels, alpha, EXIF orientation, reverse/outside/invalid rectangles,
  original hashes and existing-output collisions.
- Ordered multi-image payloads, mixed/duplicate/oversized rejection, total bytes,
  context-specific wheel capabilities; every merged PDF page rendered and checked
  for dimensions/color/order, cross-directory first-source publication and cancellation.
- HEIC genuine HEVC brands, independent pixel/alpha decode, 1×1 and odd dimensions,
  Arabic paths, crop/rotate/strip, missing encoder gating, in-flight native process
  cancellation and no owned temporary file/child process leak.
- PNG pixel-identical lossless compression; TIFF/BMP reduction; EXIF and XMP removal
  across PNG/JPG/WebP/AVIF/TIFF/HEIC. All prior audio/video/PDF tests still pass.
- Crop editor content rendered without displaying a native window; four exact pixel
  fields and preview present. Live interaction remains a desktop acceptance check.

Unchanged SHA256 values (also checked against `artifacts/wheel-baseline-hashes.json`):

| Source | SHA256 |
| --- | --- |
| `PixelWheel.cs` | `AE6D63FBC21DFE704DBD96F4B1EE5379D7D52E11E57106AD86DA460CCA4DF53B` |
| `OverlayWindow.cs` | `8B7FEA3E729106B0862FB7327991E288BC7B5D4045F0345910254699A05D60E0` |
| `DragGestureMonitor.cs` | `C44F4C9345A7A878B405CA3A241DABB3EF4396FD2EE711029DD84428C233700A` |
| `WheelModeState.cs` | `CC3AF11BCDBE2FE1C55096AD1F0A42AFF66BADFD35E0FFB16083BAB4E1DB3D95` |

The user reports the integrated wheel working. Newly added multi-image OLE delivery,
crop marquee/numeric editing/cancel/Exit and clean-PC installation still require the
actual Windows desktop checklist. No live new-feature input test is claimed here.
Final `scripts/build.ps1` completed with zero warnings/errors. All 30 groups passed
again in `artifacts/tests-20261019-013427/results.json`, with 37/37 image routes in
`image-format-matrix.json`. The unchanged-source hashes above and original/transparent
logo hashes were verified after publishing; results are in
`artifacts/feature-wheel-preservation.json`.

`scripts/verify-diagnostic-package.ps1` passed for the final installer:

- 619 published/extracted files hash-identical; all 34 native HEIF files match the
  locked manifest. Extracted HEIC encoder/decoder work without PATH changes, preserving
  dimensions, visible color and alpha in a real PNG → HEIC → PNG round trip.
- Self-contained runtime loaded from the extracted payload; no main window, duplicate
  launch exits zero, graceful quit zero, native hooks/OLE/tray cleaned up successfully.
- Short idle sample: 15.62 ms CPU in 3 seconds, 101.11 MB working set. This is a smoke
  sample, not a performance benchmark. NotifyIcon.Visible is verified as a property;
  no physical tray screenshot or new-feature Explorer input claim is made.
- Arabic HTML guide fits 390 px with no horizontal overflow and contains the new
  crop/merge/HEIC instructions. `artifacts/feature-guide-verification.json` and
  `feature-guide-tools.png` record this check. HTML design preview stays separate.

Installer: `artifacts/Naqla-Dev-Setup-win-x64.exe`, 259.1 MB. SHA256:
`73BC4AE60EF4D4D4664252BD61B23BD92FCCDAA6FE329C022FC95E7C7CF8314E`.
Package/process report: `artifacts/diagnostic-package-smoke.json`. The test extracts
without installing or changing startup/installation registry settings.

## Previous 0.2.1-ctrl.1 — Ctrl switching and media tools

User desktop logs from 0.2.0 showed real MP4 OLE file receipt and rapid Tools true/false
requests on Ctrl taps. Source inspection confirmed that the renderer silently forced
non-image files back to Convert, and the hold/release contract reverted taps immediately.

Twenty groups passed in `artifacts/ctrl-tools-tests-20261019-1/results.json`:

- Ctrl press/release persistence, next-press return, auto-repeat suppression and reset.
- Ctrl before file confirmation, MP4/audio/PDF Tools, count/action capability checks,
  stale conversion selection removal and one-action annulus rendering.
- Actual video compression size reduction; 90° rotation and retained audio; mute with
  no audio and unchanged decoded video hash; title stripping with retained pixels/audio.
- Actual audio compression/title removal, unchanged source hashes, no private temp leak.
- All prior conversion, cancellation, metadata, diagnostics and native-render groups.

`preview/native-tools-modes.png` renders the native MP4/audio/PDF tools. It does not
claim interactive Windows input validation. New tap/OLE desktop acceptance must still
be tested by the user; previous logs prove Ctrl delivery but describe the old behavior.

Final `scripts/build.ps1` passed with 0 warnings/errors and all 20 groups again in
`artifacts/tests-20261019-004544/results.json`. Package/process smoke passed with all
530 hashes matching, bundled runtime, no main window, singleton and clean hook/exit
cleanup. Short idle sample: 0 ms CPU / 3 s, 99.86 MB working set (not a benchmark).
The Arabic guide fits 390 px and contains the new tap instructions. Latest installer
SHA256: `E8714A120E699D7F18DB3830861AD1574EC5DCBEABD4F330C004DDC440A918CC`.

## Previous 0.2.0-pixel.1 update

Latest asset-only follow-up: the consumed logo is now a true RGBA cutout in
`assets/Naqla-logo-transparent.png`, derived with built-in imagegen. The original
`LOGO.png` hash remains `C9E820157C19E57AF1D80168BDD4479D99CFFEF8456D33FFE11F7DAED3C4939D`.
Transparent PNG/ICO corners and extracted executable/installer icons were checked;
dark/light visual QA is in `artifacts/logo-alpha-preview.png`. Publish succeeded and
package/process smoke passed again, all 530 files matching. Latest installer SHA256:
`AF30252AF820BF39D0010350C5B6FA3A0DA8FE31B954661D34827370A4065303`.
No conversion/drag code changed and codec tests were not repeated for the logo edit.
The functional-build results/fingerprint below predate this asset-only follow-up.

The user explicitly requested native pixel styling, smaller transparent wheel and real
visible conversions. Seventeen automated groups passed in
`artifacts/codec-tests-20261019-1/results.json`, including:

- All five destinations from an actual encoded MP4 fixture, MP4 roundtrips and MKV/AVI input.
- MP3/M4A/WAV/FLAC/AIFF/Opus encode/decode, Arabic source names, no-audio/corrupt input
  failure and in-flight media cancellation without temporary-file leakage.
- PNG/JPG/WebP/AVIF/TIFF and raster PDF; alpha matte, source hashes and name collisions.
- Rotation dimensions, metadata removal, measurable JPEG compression.
- Two-page PDF image export and TXT/DOCX/RTF text from both pages; scanned text rejection.
- Native WPF images in five palettes, with zero-alpha exterior corners and a solid hub.
  `preview/native-wheel-family.png` is rendered native code, not an actual drag screenshot.
- Existing classification, geometry, filesystem and diagnostic privacy/retention checks.

The prior real user log recorded Desktop MP4 OLE receipt and actual metadata twice;
not an accepted conversion drop. The new transparent surface still needs actual
Desktop/Explorer testing. No simulated dragging is used as evidence.

Final `scripts/build.ps1` passed with 0 warnings/errors and all 17 groups in
`artifacts/tests-20261019-002834/results.json`, including the two-pass GIF conversion.
`scripts/verify-diagnostic-package.ps1` passed; results are recorded in
`artifacts/diagnostic-package-smoke.json`:

- All **530** extracted files hash-match; bundled runtime loaded.
- Main window handle 0; duplicate launch and clean quit exit 0; hooks removed successfully.
- NotifyIcon.Visible true; actual independent tray screenshot still unverified.
- Idle sample: 0 ms CPU over 3 seconds, 100.61 MB working set; not a benchmark.
- Engines, licenses, separate HTML preview and guide are present. The guide fits a
  390 px viewport without horizontal overflow and contains 17 checkboxes.
- Installer SHA256: `1C5A10FBC65DADFF182BB60B182E96FF5D01829976B47D19A65FABCED0DDF1B1`.

Earlier build fingerprints below are historical and do not identify the new installer.

---

## Historical records — 2026-10-18

## Current diagnostic build — 0.1.1-diagnostics.1

This is the current installer. The initial-foundation build fingerprint and visual
revision checks later in this file are historical records.

`scripts/build.ps1`: passed, 0 warnings/errors and **10** automated tests. New tests
cover diagnostic privacy (including path-bearing exception messages), disabled logging,
queue drain, bounded retention/size and unavailable storage. These are log-schema and
filesystem tests, not injected drag tests. Original codec/preservation tests still pass.

Installer SHA-256:

```text
CF601533C563FB64241D381D2CEC6E3E2EC8135DC33637AF7D911D0ADE2144CC
```

`scripts/verify-diagnostic-package.ps1` passed:

- All **489** extracted files matched the published app; bundled runtime loaded.
- Main window handle 0; duplicate exited 0; clean quit exited 0.
- Real SetWindowsHookEx calls reported success for keyboard and mouse hooks.
- Real UnhookWindowsHookEx calls reported successful removal at exit.
- `TrayVisibleRequested` recorded `Visible:true`; cleanup recorded `TrayRemoved`.
  This verifies the app/NotifyIcon lifecycle, not an independent tray screenshot.
- Lifecycle log ended in `AppStopped`; no private installation/profile path in the log.
- Latest standalone HTML preview and manual guide/reference were present in payload.
- Short idle sample: 0 ms CPU over 3 s, about 70.25 MB working set. Not a benchmark.

Evidence: `artifacts/diagnostic-package-smoke.json`. The offline HTML guide rendered
with no JS page errors and no horizontal overflow at 390 px; it has 11 checkboxes.
No visual preview/native wheel design changes were made in this diagnostic update.

The subsequent logo integration uses the supplied `LOGO.png` in the notification
icon, executable and installer resources, installer window, and preview header.
Original and copied preview PNG hashes match. Generated 16–128 px DIB frames and
the 256 px PNG frame decode correctly; 32 px executable/installer icon pixels match
the logo-derived ICO. The header image loads at its original 1254 × 1254 resolution.
Build and the existing ten checks pass. Only branding and icon-format conversion
changed; no production wheel or drag changes were introduced.

The desktop Computer Use pipe failed again across retries/reset. **Actual tray visual
confirmation and real Explorer/Desktop Shift drag → OLE → JPG remain unverified here.**
They require the user's manual test using [MANUAL-TEST-AR.md](MANUAL-TEST-AR.md), also
packaged as `TESTING.html`. No simulated file dragging or hardcoded source paths were used.

Environment: Windows x64, .NET SDK 10.0.102. Local build/publish, not a clean target PC.
Runtime-pack restore succeeded; runtime 10.0.2 is included in the development installer.

## Build

`dotnet build Naqla.sln -c Release`: **passed**, 0 warnings, 0 errors, warnings treated
as errors. `scripts/build.ps1` successfully published the x64 self-contained app and
single-executable installer. No native production wheel has been implemented.

Installer: `artifacts/Naqla-Dev-Setup-win-x64.exe`, approximately 120 MB.

SHA-256:

```text
ABDA37330B533124BDBA3C01B4925FA1B3B665B938DBB18DF3A0A2BD0DC6FCE3
```

## Automated filesystem and codec checks — 7 passed

Executed with `dotnet run --project tests/Naqla.Tests -c Release -- artifacts/final-tests`.

1. Requested extension classification, mixed case, unsupported extension.
2. Actual Arabic filename metadata and 32 MB local file size.
3. Radial sector centers, hub and outside exclusion.
4. Source aliases omitted from destinations; only PNG → JPG is enabled.
5. Actual PNG file encoded to two new JPGs beside its source. Both existing output
   and original hashes/content preserved; dimensions verified; opaque red center
   retained; transparent corner decoded white. JPEG pixels read, not only headers.
6. Corrupt/signature-disguised PNGs fail without JPGs or temporary output leaks.
7. A pre-cancelled conversion creates no output.

These use genuine filesystem and image codecs with generated fixture images. They
are **not** Explorer drag tests and do not establish drag feasibility. Machine-readable
results and generated images remain in `artifacts/final-tests`.

## App lifecycle smoke — passed

Launched the app and sampled the process:

- Process remained alive; main-window handle 0.
- Second launch exited normally with code 0.
- `--quit` stopped the first process cleanly with code 0.
- First smoke sample: approximately 64.91 MB working set; 0 ms CPU measured over a
  three-second interval. This short sample is not a sustained performance benchmark.

Evidence: `artifacts/lifecycle-smoke.json`.

## Self-contained package smoke — passed

Executed the installer using its build-verification `--extract-only` mode. This mode
does not register/unregister the app, create startup entries, or install it for the user.

- Installer extraction exit 0.
- **484** extracted payload files compared to published source files: no hash mismatches.
- Launched extracted `Naqla.App.exe`; process stayed alive with no main window.
- Process loaded `coreclr.dll` from the extracted payload, establishing that the bundled
  runtime was used. Runtime configuration includes Core and Windows Desktop 10.0.2.
- Duplicate instance exited; `--quit` requested graceful exit successfully.

Evidence: `artifacts/package-smoke.json`.

This is extraction/launch verification, not a claim of completed installer UI,
upgrade/uninstall, startup-at-sign-in, or clean-machine acceptance testing.

## Visual preview — passed

`node scripts/verify-preview.cjs` used a separate headless Edge instance to verify:

- Five joined sectors per state; current PNG omitted from conversion options.
- SVG viewport 320 × 320 CSS pixels at device scale 1.
- Ctrl hold switches to Tools, release restores Convert.
- Hub bounds unchanged during mode switch.
- Both capsule buttons, Escape dismissal, opening replay, animated round trip.
- Hover highlight, no horizontal overflow at a 390 px viewport, no JS page errors.

The revision 01 sheet was visually inspected and is now archived as
`preview/archive/v01-preview.png` with its HTML. See the revision 02 checks below.

## Visual revision 02 — passed

The user requested stronger pixel-art character, per-format colors/icons, category
palettes and capsule styling based on a five-wheel reference. Only preview code,
browser verification and documentation changed; native projects were preserved.

`node scripts/verify-preview.cjs` passed the existing interaction checks plus:

- Six connected image sectors and five tools; source format omitted.
- Five distinctly colored wheel families and their capsule headings.
- Colored pixel-file symbols, including pink HEIC and green WEBP.
- All category capsule switches, hub type changes, and source omission per category.
- Image Ctrl transition switches amber to teal and keeps the hub stationary.
- Both full-size and five-wheel family screenshots rendered and visually inspected.

Latest assets: `preview/approval-preview.png`, `preview/approval-family.png` and
`preview/index.html` at that checkpoint. Revision 02 is archived in `preview/archive`.
The previously built installer fingerprint above is unchanged and bundles the older
preview. No claim of new native integration or installer validation is made here.

## Visual revision 03 — passed

The user liked the wheel and requested a transparent, floating glass Convert/Tools
selector. The capsule now uses translucent layers, a sky-tinted reflection, backdrop
blur, soft shadows and an amber glass selection. The wheel shadow was moved to its
SVG so it does not isolate the selector from the backdrop it needs to blur.

Preview browser interaction checks passed with no page errors, including Ctrl
hold/release, both selector controls, stable hub, Escape, hover and responsive layout.
The current full preview and 18-state sheet were re-rendered. The focused two-state
glass image `preview/glass-selector.png` was rendered at 2160 × 1360 and visually
inspected over a soft sky backdrop to show the actual transparency. Current browser
evidence is `artifacts/preview-tests.json`, revision 3. Native application code and
the existing installer remain unchanged; final visual approval is pending.

## Visual revision 04 — passed

The selector was redesigned as a compact 248 × 42 px frosted control. It now derives
its border, active surface and label colors from the wheel theme rather than a fixed
amber value. Compact pixel arrows and sliders replace the previous selector glyphs.

The preview browser checks passed, including computed active-label color matching
for image, video, audio, PDF and green Tools mode; selector controls; Ctrl hold/release;
stationary hub; Escape; hover; responsive layout; and no page errors. The current full
preview, all-state sheet and five-palette `preview/themed-selector.png` were rendered
and visually inspected. Evidence: `artifacts/preview-tests.json`, revision 4.
No native application code or installer was changed. Explicit approval is still pending.

## Not executed — requires a working Windows desktop test session

The Computer Use native pipe failed to connect three times, including a reset. CUA
also reported no enabled native surfaces. No keyboard/mouse input was injected as
a substitute, and no actual Explorer drag was observed.

Pending: real Explorer/Desktop CF_HDROP receipt, stationary-Shift timing, Arabic file
drag, cancellation races, actual PNG drop → JPG, notifications, ordinary-drag behavior,
mixed DPI and screen edges, Explorer restart recovery, installer UI and uninstall,
Windows sign-in startup, and clean Windows 10/11 verification. Follow
[DESKTOP-TESTS.md](DESKTOP-TESTS.md). Do not mark those tests passed from the results above.
