# Naqla progress — 0.8.0-development.1

Public beta 0.8.0-beta.1: installer-only network code fetches pinned FFmpeg/HEIF
explicitly during setup, then all processing remains offline. Added unattended
per-user install/startup, a guarded Start-menu shortcut and local component import.
Public packs exclude standalone Microsoft CRT; the official system runtime is a
prerequisite. Five public packs passed actual import/engine checks; Office is not
distributed. Installer cache/refusal/cancellation and real Zstandard/native engine
tests pass. The historical processing/physical acceptance distinction remains.

Public repository preparation (2026-10-20): added English/Arabic usage guides,
source-build prerequisites, contribution/issue templates, clean Windows build CI,
Pages documentation navigation, and an allowlisted historical test snapshot.
Native CRT DLLs are kept local and excluded from the current public source tree.
No new public binary release or physical Windows acceptance is claimed.

**DEVELOPMENT READY for local testing.** Processing implementation, focused fixes,
real test outputs and optional local packs are complete for the documented scope;
physical Windows/clean-PC/disconnected acceptance and public release gates remain pending.

- Self-contained .NET 10.0.13 acceptance: **123/123 distinct checks
  passing after a targeted pack-fixture isolation fix**. Initial full run 122/123; failed evidence retained.
- Actual conversion matrix **136/136**: 49 image, 27 video, 30 audio, 5 PDF, 25 documents.
- Added actual compact media/model/privacy/archive dialogs and local component/plugin
  management. Every new contextual action reaches its real handler and validated parameters.
- Preserved wheel geometry/palettes/capsule and shared pixel buttons. Tools pagination
  was explicitly approved and integrated; it is not awaiting that approval again.
- Six actual local packs imported and all installed engines executed; pinned models,
  native dependencies, licenses and collision-safe output verified. No runtime downloads.
- Privacy extra tests fixed packed-depth false mismatches and derived GPS/XMP/IPTC
  reserialization. Unsafe >8-bit re-encoding is refused; originals remain untouched.
- JPEG batch stress produced **310 real outputs** over 10/50/50/200-file jobs.
- Native/static advisory checks, workload sampling and development package verification
  are recorded under artifacts. Exact packaging hash/result is external in
  `artifacts/release-verification.json`; the checklist remains [manual](docs/MANUAL-WINDOWS-TESTS.md).
- [Complete audit](docs/FINAL-FEATURE-AUDIT.md) · [Evidence](docs/END-TO-END-TEST-REPORT.md)
  · [Bug log](docs/BUG-FIX-LOG.md) · [Limits](docs/KNOWN-LIMITATIONS.md).

## Earlier development history
# Naqla progress — 2026-10-19

**Current packaged build: 0.7.0-pdfpreview.1. Final-roadmap development is IN PROGRESS;
the new code is not yet a completed release. The latest complete specification is
[FINAL-DEVELOPMENT-SPEC.md](docs/FINAL-DEVELOPMENT-SPEC.md).**

## Final stabilization assignment — current work, 2026-10-19

- Completed initial source/action audit before functional changes: 180 rows in
  [FINAL-FEATURE-AUDIT.md](docs/FINAL-FEATURE-AUDIT.md), with machine-readable baseline.
- Fresh baseline execution passed 86/86 groups (`artifacts/final-baseline-1/results.json`).
- Fresh actual-handler conversion matrix passed **111/111 pairs**: 49 image, 27 video,
  30 audio, 5 PDF. Every pair has codec/content checks and original-hash verification:
  `artifacts/fresh-matrix-2/conversion-matrix.json`.
- New backend suites passed: advanced media 5 groups, local models 4, archives 3,
  constrained plugins 4, privacy 5. Evidence folders respectively:
  `advanced-tests-2`, `model-tests-3`, `archive-tests-3`, `plugin-tests-1`, `privacy-tests-5`.
  These are processing tests, **not evidence of new wheel/editor integration or physical OLE input**.
- Added actual 30-second frame extraction, accurate trims, independently measured
  two-pass normalization, edge silence trimming, mono/dual-mono, local Tesseract,
  actual U2NetP CPU inference and whisper.cpp SRT/WebVTT. Unicode native-engine paths
  are handled through owned ASCII-relative working names. Models never auto-download.
- ZIP/7Z/TAR processing has bounded expansion, path/link checks, independent hashes,
  empty-directory preservation and collision-safe output. Plugin packages remain
  declarative, disabled until explicitly enabled, with separate fixed-code workers.
- Privacy processing includes selective Office properties, lossless JPEG marker cleanup,
  image pixel verification, qpdf metadata changes with rendered PDF comparison, and
  media stream-copy packet hashes. It is **not content anonymization/redaction**.
  HEIC/AVIF privacy cleanup requires explicit permission for lossless PNG fallback.
- PDF editor retains its three columns; now has fit/zoom/pan and debounced preview.
  Existing pixel buttons and wheel geometry/palettes are preserved; approved Tools pagination is the scoped renderer change.
- Offline LibreOffice document processing and a fresh document matrix are currently
  being validated. Optional component packaging, complete interface integration,
  final regression/security/performance runs, installer and final reports are unfinished.
- No second Tools-page mechanism existed in the repository. A separate
  [navigation approval preview](preview/tools-pagination.html) proposes <=6 sectors,
  center page dots and Tab/Shift+Tab. **Explicitly approved by the user; Tools-only paging is now integrated. Physical OLE Tab acceptance remains manual.**
- Real Explorer/Desktop gesture, monitor/DPI, disconnected-network, clean-PC install,
  upgrade and uninstall acceptance remain **MANUAL PENDING**. No result above changes that.

The following sections preserve the earlier 0.7 development history; their former
Feature-4 stop condition is superseded by the user's final-roadmap assignment.

## Feature 3 — functional processing and editor draft

- Implemented PDF page/document reorder, extract (one/separate PDFs), delete, merge,
  numbering, text/image watermarks and selected-page rotations in a UI-independent
  PdfToolkitService with validated immutable plans/ranges. Page streams/resources are
  imported; whole documents are never flattened. Source hashes and read locks guard
  stale previews and concurrent writes. Each result is independently parsed/count-checked
  and every page decoded before collision-safe new-file/folder publication.
- Shared compact native WPF editor has real thumbnails and main output preview, drag
  reorder plus arrows, contextual controls, RTL, local fonts, numbers/colors/positions,
  alpha/scale/rotation, responsive scrollers and existing unchanged PixelButton styles.
  Save requires a current successful preview. Cancel/close/Exit await owned cleanup.
  Its visual design remains a development draft pending explicit approval.
- Existing contextual wheel adds six PDF actions; multi-PDF Tools adds Merge and retains
  TXT/Rename. Renderer circle/capsule/palettes/geometry and gesture/Ctrl state are retained;
  only action-to-existing-icon mappings are added. Image/rename/optimizer editors and
  implementations remain untouched. No secondary wheel navigation is introduced.
- WPF supplies Arabic/English/BiDi glyph shaping to vector outlines, avoiding simple-script
  PDF text limitations without shipping font files. Added outlined text is not searchable;
  source text remains extractable. Visible crop/rotation coordinates cover all four
  orientations. RGBA image marks retain partial alpha; a palette-PNG alpha defect discovered
  by actual pixel tests was fixed by forcing a soft-mask-compatible RGBA encoding.
- Exact PDFsharp-WPF 6.2.4 is MIT; license/notices for it and new Microsoft dependencies
  are bundled. Later PDFsharp licensing differs; upgrading requires review. Existing
  PDFium/PDFtoImage/PdfPig remain independent validation/rendering backends. No new
  runtime download, cloud client, service or idle processing is added.
- Eighteen new focused automated groups pass in `artifacts/pdf-tests-10/results.json`.
  The first full regression found one old test expecting exactly one PDF tool; it was
  updated for the registered actions. This did not require changing the input state machine.
  Final `scripts/build.ps1` passes with zero warnings/errors and **86/86 groups** in
  `artifacts/tests-20261019-135710/results.json`, including the earlier 68. The complete
  run passes with only the three required PDFsharp runtime libraries present.
- Packaging review found unused embedded Segoe WP demo fonts and sample/signing
  libraries in the upstream NuGet package. `Directory.Build.targets` excludes these six
  assemblies from compile/runtime/deps/publish and removes exact stale copies. No font
  files are redistributed. `artifacts/pdf-license-verification.json` records the exact
  reviewed license/package hash and exclusions; NuGet's dependency audit reported no
  listed vulnerabilities at the time checked (`artifacts/pdf-dependency-audit.json`).
- `artifacts/pdf-wheel-regression.json` confirms all four conversion palettes, the
  family sheet, image optimizer single/batch wheel, rename wheel/pixel buttons/crop,
  and video/audio tool panels are pixel-identical to 0.6. Protected hook/overlay/Ctrl,
  native button, rename and optimizer sources hash-identical. PixelWheel's only source
  change maps new PDF actions to existing icons (`artifacts/pdf-source-preservation.json`).
- Final installer: `artifacts/Naqla-Dev-Setup-win-x64.exe`, 272,639,393 bytes, SHA256
  `879FB44FB0224224601C5CCEECB4329DAB41B55619A216AEF7C60BAE25DC13B5`.
  Package/lifecycle smoke passes in `artifacts/diagnostic-package-smoke.json`: 660 files
  hash-match, zero mismatches, exact reviewed runtime/license/sample exclusions and
  fixture hashes verified, bundled runtime and HEIC round trip, singleton and graceful
  hook/OLE/tray cleanup. No main window; idle sample 0 ms CPU/98.52 MB over 3 seconds
  (not a benchmark). This is extract-only verification, not a clean-PC installation.
- Portable 0.7 was relaunched and checked AppReady with no main window (PID 6288):
  timestamped `artifacts/portable-pdf-status.json`. Fresh unprocessed manual fixtures:
  `artifacts/desktop-pdf-test-20261019-140120`. No install/startup registry changes.
- Added [PDF-TOOLKIT.md](docs/PDF-TOOLKIT.md), native-rendered design gallery
  `preview/pdf-toolkit.html` (separate from approved wheel preview), and genuine vector
  manual fixtures with hashes. Limits: 20 PDFs/200 pages/256 MB each/512 MB total,
  encrypted/forms/signature-permission/layer PDFs refused, document-level navigation/
  conformance not promised. Large synchronous native calls can delay cancellation.
- Real Explorer interaction, physical thumbnail drag/buttons, OS DPI/monitor behavior,
  clean-machine installation and deliberately disconnected networking are unverified.
  Approval is not inferred from these automated tests or previous wheel approval.

## Historical 0.6 increment — Target-Size Image Optimizer

**Previous development build: 0.6.0-targetsize.1. Target-Size Image Optimizer;
real desktop acceptance and any required preview approval remain pending.
This is a development build, not a production release.**

## Feature 2 — implementation/automated checks passed; desktop acceptance pending

- Added Image Tools → حجم مستهدف without changing wheel geometry, palettes, capsule,
  hooks, Ctrl state or PixelButton styles. Contextual single/batch image tools use six/
  seven existing sectors; native previews render at their actual 240 DIP wheel size.
- Presets 100/200/500 KB and 1 MB, custom decimal KB/MB including Arabic digits; strict
  byte limits (1024-based). Preserve oriented dimensions by default; explicit proportional
  downsize bounds never enlarge. Existing basic compression remains separate.
- Real JPG/WebP/AVIF/HEIC encodes search qualities 100→1, testing all higher values before
  success. PNG compares three lossless recipes, TIFF ZIP/LZW; no hidden color quantization.
  Format discovery checks real bundled encoders after Drop, keeping heavy engines lazy.
- Full-resolution measured preview shows current/target/result bytes, dimensions, quality
  and attempts. Settings edits invalidate it. Save re-encodes and validates actual final
  bytes/decode/format/dimensions/orientation/alpha; PNG/TIFF additionally compare pixels.
  A target miss returns a distinct failure and publishes nothing. Results are new files
  beside each source with safe collision names; originals are read-locked and preserved.
- Color profiles normalize to sRGB before stripping; orientation precedes resize. Alpha
  survives alpha-capable formats; JPG uses white. Existing one-frame/40 MP/Q8 limits apply.
  Network drives and offline/recall cloud placeholders are refused rather than hydrated.
- Batch settings are per file, on the same bounded queue as preview and existing jobs.
  A later readable source may supply a numbered preview, without removing corrupt entries
  from processing. Typed failures/cancellation keep unrelated successes and actual reports.
  Stop/close/Exit cancels and awaits owned-candidate cleanup; native calls observe token
  boundaries, so a large encode can delay cleanup. No new dependency/service/network client.
- Fourteen focused groups pass in `artifacts/optimizer-tests-20261019-6/results.json`:
  all output/input formats, maximal measured quality, strict caps, real pixels/alpha/color/
  orientation, resize, impossible targets, names/collisions, cancellation/source locks,
  partial batches, privacy, native settings/Save gates and independent disk/helper checks.
  Initial native render QA fixed RTL numeric grouping and kept pixel buttons intact.
- Added [TARGET-SIZE.md](docs/TARGET-SIZE.md), updated offline guide/audits and packaged
  fixture/verifier helpers. Helpers always record `ActualExplorerDragVerified:false`.
  No physical Explorer/settings-button, DPI/monitor or disconnected-network acceptance is
  claimed. **Feature 3 has not started**; its acceptance/preview gate remains closed.
- Final full build passed with zero warnings/errors and all **68 groups** in
  `artifacts/tests-20261019-032845/results.json`. The first full attempt exposed the test
  harness's default WPF shutdown mode after closing offscreen editors; it now matches
  the app's existing explicit-shutdown lifecycle. This fix changes tests, not app lifecycle.
- Native current settings/measured/unattainable views and six/seven-sector previews were
  reviewed. RTL numeric units were corrected with directional Runs. Four conversion
  palettes and video/audio/PDF Tools snapshots remain pixel-identical to 0.5. The family
  contact sheet intentionally differs in its image Tools panel because it adds Target
  Size. `artifacts/target-size-wheel-regression.json` records the scoped comparison;
  `artifacts/target-size-gesture-preservation.json` records unchanged hook/overlay/Ctrl hashes.
- Final package smoke passed: 632 files hash-match, 34 HEIF native hashes and extracted
  HEIC round trip verified, bundled runtime, singleton, no main window, graceful exit and
  hook/OLE/tray cleanup. Image native engine remains unloaded at idle. Three-second smoke
  sample: 0 ms CPU, 98.68 MB working set, not a benchmark. No new runtime dependency.
- Installer is `artifacts/Naqla-Dev-Setup-win-x64.exe` (259.2 MB), SHA256
  `8C1447F81ABE78846E341C77CED296A5E406588BE9C9E7AE2C699C4E54C8DA39`.
  Portable 0.6.0-targetsize.1 was relaunched and checked on 2026-10-19 (PID 2072),
  AppReady, tray Visible requested, MainWindowHandle 0. Timestamped runtime snapshot:
  `artifacts/portable-target-size-status.json`. No installation/startup
  registry changes were made. This is lifecycle evidence, not a physical tray/drag test.
- Fresh user-acceptance sources: `artifacts/desktop-target-size-test-20261019-033024`,
  images/partial-failure/unattainable. No outputs or real Explorer acceptance exist there.
  The updated HTML guide fits 390 px without overflow. Remaining practical limits and
  the exact desktop/preview gate are recorded in the Feature 2 guide and audit.

## Historical 0.5 increment — Smart Rename implemented, desktop acceptance pending

- Added a live native Before → After editor with find/replace, prefix/suffix, padded
  numbering, case, cleanup, filesystem date tokens and optional 100 ms regex matching.
  Preserve extension defaults on; invalid names, duplicate sources/targets and occupied
  names block confirmation. Edits clear consent; no mutation happens during preview.
- Explicit confirmation performs same-file no-replacement Windows handle renames,
  retaining IDs, bytes, ADS/MOTW and timestamps. Rechecks/locks precede any move; swaps
  and case-only names stage original objects, not copies. A test with distinct creation
  dates exposed NTFS tunneling; the same handle now restores its captured creation time.
- Safe last-operation undo is session-only, requires a fresh preview and confirmation,
  and refuses changed/replaced files or occupied destinations. Cancellation/commit
  failures attempt rollback. Blocked rollback preserves original data and unrelated
  occupants, surfaces a typed recovery result and keeps a flushed local recovery map.
  Recovery maps contain local paths, separate from path-free diagnostic logs. Staged
  Naqla-Rename-Recovery files contain ORIGINAL DATA and must never be cleaned as output.
- Added coordinated stepped-corner pixel buttons/glyphs for Hide/Cancel/Details/Results,
  crop and rename dialogs; retained Button keyboard/automation/dialog semantics. Native
  render QA found and corrected Arabic clipping. Wheel geometry/palettes, hooks, overlay
  placement and Ctrl state remain unchanged; contextual Tools adds Rename.
- Eleven focused automated groups pass in `artifacts/rename-tests-20261019-5`.
  All 54 groups pass after the final undo-result wording fix in
  `artifacts/tests-20261019-023643/results.json`, with zero build warnings/errors.
  Native button/crop/rename/progress render QA passed; undo reports restored names.
  `artifacts/rename-wheel-render-regression.json` records existing palettes identical
  to 0.4, and `artifacts/rename-gesture-preservation.json` records unchanged overlay,
  hook and Ctrl-state hashes. The HTML guide fits 390 px and covers the new controls.
  Package/process verification is recorded below. Features 2–8
  remain pending in the user's exact order; earlier OCR/archive/background backlog stays.
- Final installer/package smoke passed: 628 payload files hash-identical, all 34 HEIF
  native hashes and extracted HEIC round trip verified, self-contained runtime,
  singleton, no main window, graceful exit and hook/OLE/tray cleanup. The image native
  engine is absent at idle. Three-second sample: 0 ms CPU, 98.68 MB working set; this
  is a smoke sample, not a benchmark. `artifacts/diagnostic-package-smoke.json`.
- Installer `artifacts/Naqla-Dev-Setup-win-x64.exe` is 259.2 MB; SHA256
  `E1B49B8FD89AC82DDAAE7920C17A4C9B8D5EBBE23C0619298BBE4C8EB2D74BDD`.
  Portable 0.5.0-rename.1 is running (PID 12920), AppReady, tray Visible requested true,
  MainWindowHandle 0: `artifacts/portable-rename-status.json`. No install/startup registry
  changes were made. Real Explorer/editor/button interaction is still unverified.
- Fresh rename-only acceptance fixtures: `artifacts/desktop-rename-test-20261019-023443/10-images`.
  Preparation creates genuine sources without performing an app rename or desktop drag.
  Updated the offline guide, [Smart Rename acceptance/recovery](docs/SMART-RENAME.md),
  root [FEATURE-AUDIT.md](FEATURE-AUDIT.md) and full [verification](docs/VERIFICATION.md).

- Recorded the full corrected feature 1–6 requirements and detailed Privacy Clean /
  Plugin requirements in [FEATURE-AUDIT.md](docs/FEATURE-AUDIT.md). These do not exist
  merely because older compression/STRIP/PDF converters exist. Preserve the earlier
  OCR/archive/background-removal backlog and report it separately.
- The user explicitly requires actual filesystem renaming, not copies, after a live
  preview and confirmation. No copy-based Smart Rename implementation was written.
  Implemented transforms, collision detection, name validation and guarded undo first.
- The user confirmed the 0.4 real Explorer batch test is still untried. It is not marked
  accepted. Added the requested coordinated pixel treatment for transient UI buttons
  to the current work; wheel appearance/gestures stay intact.
- Preliminary [plugin threat model](docs/PLUGIN-THREAT-MODEL.md) is a design draft only;
  no arbitrary plugin execution, sandbox or network permission guarantee is enabled.

## Earlier batch increment — implementation history, desktop acceptance still pending

- At initial inspection this workspace had no Git metadata. The prior multi-image
  adapter offered only aggregate PDF, without a general batch queue/OCR/archive/AI
  implementation. Plan: [ADVANCED-PLAN.md](docs/ADVANCED-PLAN.md).
- Extended the existing conversion service and safe output handling into a bounded
  batch queue; preserving hooks, overlay geometry, Ctrl toggle and wheel appearance.
- Computer Use initialization succeeded, but app enumeration failed with unavailable
  native pipe (OS error 2). Real Explorer acceptance remains a user desktop test.
  Phase 1 will not be marked complete before that test; later phases follow its gate.
- Generalized real OLE payload inspection to up to 200 local files, retaining every
  source's metadata validation and categories. Common operations are intersected
  across all members; an invalid input is never silently removed. No common action
  requests a concise local notification. Drop rechecks paths/order and records Count.
- Added reusable BatchProcessor, with a sequential async queue shared across requests.
  Each normal conversion/tool job saves beside its own source. Explicit Merge remains
  aggregate; Convert → PDF now produces a separate PDF for each image. Crop uses the
  first preview's oriented pixel rectangle for every image and reports bounds failures.
- Added a transient compact progress/results window after accepted Drop. Completed/
  total jobs drive percent, cancellations do not count as completed, and earlier
  successful outputs survive later failures/cancellation. Hide/reopen from tray,
  Cancel, local failure details and distinct output-directory menus are available.
- Image native limits/initialization are lazy; backend availability checks are cached
  for metadata-only wheel capability evaluation. No new runtime dependency/service.
  New typed diagnostic counts contain no source lists, personal paths or raw errors.
- First 42-group run passed at `artifacts/batch-tests-20261019-1/results.json`, including
  actual multi-video/image conversions, Arabic/cross-directory duplicate names,
  actual ACL-denied output, corrupt input partial failure, cancellation, batch crop,
  queue concurrency (explicit scheduling test double), native hub/progress rendering.
  A 43rd test now exercises the manual fixture/verifier scripts, including negative
  missing-output checks and a flag that never claims Explorer input from file evidence.
- Baseline single-file `native-wheel-family.png` and `native-tools-modes.png` are
  pixel-identical to the 0.3.0 final run. Only hub count/action data changes inside the
  wheel; circle/capsule geometry, icons, colors, hooks and Ctrl state remain intact.
  For 200 actual files, SetSelection data preparation measured median 0.1344 ms and
  P95 3.1995 ms (baseline SetFile 0.0005 ms). This is not Shift-to-wheel latency.
- Final build passed with zero warnings/errors and 43 groups in
  `artifacts/tests-20261019-015359/results.json`. Manual fixture script inspection then
  found Windows PowerShell 5.1 treating a BOM-less UTF-8 script as ANSI, mangling its
  Arabic literals. Preserved UTF-8 BOMs, added an exact Arabic filename assertion,
  and reran all 43 groups successfully in `artifacts/batch-final-tests-20261019-1`.
  This follow-up changed only developer scripts/tests; packaged app code is unchanged.
- Final package smoke passed: 623 files hash-identical, 34 HEIF native hashes checked,
  extracted HEIC round trip passed, bundled runtime, singleton, no main window,
  native hook cleanup and graceful exit verified. Native Magick engine is not loaded
  at idle startup. Three-second sample: 0 ms process CPU, 98.55 MB working set; prior
  0.3.0 sample was 15.62 ms / 101.11 MB. These samples are not benchmarks.
- Installer SHA256: `F0A82AD09D73E62344E794F8010C700D51FF4EC9D8E2FA2882BD8DBC1FDC191A`,
  259.1 MB including the existing self-contained runtime and local engines. No new
  third-party dependency, installed service or model pack was added.
- Final native single-file wheel snapshots are still pixel-identical to 0.3.0; report:
  `artifacts/batch-wheel-render-regression.json`. Hooks/overlay/Ctrl hash report:
  `artifacts/batch-gesture-preservation.json`. Latest 200-file SetSelection sample:
  median 0.124 ms / P95 5.0836 ms, versus SetFile median 0.0004 ms (not Shift latency).
- Prepared fresh, correctly named Arabic sources in
  `artifacts/desktop-batch-test-20261019-015624/10-images` and `partial-failure`.
  These user-acceptance folders contain no conversion outputs and remain untested
  by Explorer. Earlier script samples are superseded by this folder.
- Portable 0.4.0-batch.1 is running (PID 7440), AppReady, NotifyIcon.Visible requested
  true, MainWindowHandle 0. `artifacts/portable-batch-status.json` records lifecycle
  evidence. No startup/installation registry changes were made.
- Detailed user-required gate: [BATCH-TESTING.md](docs/BATCH-TESTING.md). Asked the user
  to drag the ten real PNGs to JPG and report the hub/output counts. Waiting for that
  actual desktop result; Phase 1 is not marked complete. OCR/archives/background
  removal and later shared UX work have not been implemented in this build.

## Previous 0.3.0-features.1 — Functional features

- Kept PixelWheel, OverlayWindow, DragGestureMonitor and WheelModeState byte-for-byte
  unchanged, verified against `artifacts/wheel-baseline-hashes.json`. Architecture still
  separates Core, Windows/OLE, Wheel and Conversion. Browser preview remains separate.
- All five image tools implemented. Compress publishes only a smaller copy (lossless
  PNG, ZIP TIFF, BMP → PNG); Rotate is 90° clockwise; Strip removes metadata after
  orientation; Crop uses an explicit pixel rectangle in a separate post-drop editor.
- User chose dragging selected images together for Merge PDF. Real OLE FileDrop lists
  now carry 1–200 images, revalidated at Drop. Multi-image selection exposes only PDF/
  Merge; one A4 page per image in source-provided order, output beside the first source.
  Every page is parsed/rendered before publication. No picker or guessed Explorer paths.
- Genuine HEIC output via bundled libheif 1.23.6/x265 4.3. Official MSYS2 archives and
  34 native files are hash-locked; licenses/notices/source URLs travel with the engine.
  Arabic source directories use Unicode process cwd and ASCII relative native argv.
  Independent decoding validates dimensions/alpha; child process cancellation cleans up.
- Image PNG/JPG/WebP/AVIF/TIFF/HEIC/PDF, all listed video/audio destinations, PDF all-page
  PNG/JPG and text TXT/DOCX/RTF are connected. Video has compress/rotate/mute/tag tools,
  audio compress/tag tools and PDF text extraction. Originals and existing outputs stay
  intact; outputs are verified, collision-safe and saved beside the source.
- Thirty automated groups passed in `artifacts/features-tests-20261019-3/results.json`:
  37 real image format routes, crop pixels/alpha/EXIF, merge all pages/order/colors,
  corrupt input/cancellation, real HEVC alpha/tiny/odd sizes/Arabic paths, lossless
  compression and EXIF/XMP removal. All earlier media/Ctrl/privacy/render tests passed.
- Diagnostics add typed Crop/MergePdf, source count and crop input stages/dimensions;
  no source paths, source lists, coordinates, image contents or codec stderr are logged.
- Remaining interactive acceptance: new multi-image Explorer delivery; crop marquee,
  numeric editing/Save/Cancel/Exit on a real desktop; mixed DPI and clean-machine install.
  Crop content render tests do not establish live interaction. User reports the existing
  wheel working. No synthetic Explorer drag is presented as acceptance evidence.
- Limits: one raster frame ≤40MP, Q8 color; merge/PDF ≤200 pages. PDF DOCX/RTF is text
  extraction, without OCR or layout recreation; scanned text-only export fails honestly.
  Files must be fully local; virtual/network inputs remain outside the supported scope.
- Final `scripts/build.ps1` completed with 0 warnings/errors and all 30 groups again
  passing in `artifacts/tests-20261019-013427/results.json`; all 37 image routes passed.
  `scripts/verify-diagnostic-package.ps1` passed: all 619 extracted files hash-match,
  all 34 locked HEIF native hashes match, and extracted HEIC encode/decode preserves
  dimensions and alpha. Bundled runtime, no main window, singleton, clean hook release
  and graceful exit passed. Short idle sample: 15.62 ms CPU / 3 s, 101.11 MB working
  set (not a benchmark). No install/startup registry changes were made by these checks.
- Installer: `artifacts/Naqla-Dev-Setup-win-x64.exe` (259.1 MB; self-contained with offline
  engines), SHA256 `73BC4AE60EF4D4D4664252BD61B23BD92FCCDAA6FE329C022FC95E7C7CF8314E`.
  `artifacts/diagnostic-package-smoke.json` records process/package evidence. Arabic
  test guide verified at 390 px with no horizontal overflow; crop/merge instructions
  are included in the installer. The browser design preview was not modified.
- Updated portable app is running (PID 22520), build 0.3.0-features.1: AppReady,
  NotifyIcon.Visible requested true and MainWindowHandle 0. Status is recorded in
  `artifacts/portable-feature-status.json`; this is lifecycle evidence, not an
  interactive crop/multi-drag acceptance test.

## Previous 0.2.1-ctrl.1 — Ctrl switching fix

- Actual user logs showed MP4 OLE confirmation followed by rapid Ctrl mode requests.
  The hook was receiving Ctrl; PixelWheel rejected non-image Tools, while release
  immediately reverted short taps. Prior ModeChanged logs described requests, not
  the actual renderer state.
- One Ctrl down edge now toggles Tools/Convert and persists after release. Repeats
  do not toggle, each drag resets to Convert, and a request before OLE confirmation
  survives the incoming file. Ctrl remains inactive when no wheel is open.
- MP4 tools are real: compress, rotate 90° clockwise, mute without re-encoding the
  video, clean user metadata. Audio has compression/tag cleaning; PDF has text extraction.
  Original files, collision-safe publication, cancellation and full decode checks remain.
- Typed diagnostics add ControlDown/Up only while armed, actual ModeChanged + option
  count, and operation enum. No personal path/key text or codec stderr is added.
- Twenty automated groups passed in `artifacts/ctrl-tools-tests-20261019-1`, including
  quick tap/release/repeat/reset, tools before file confirmation, MP4/audio/PDF modes,
  stale-selection cleanup, single-action ring rendering and real media tool outputs.
  Video pixel hashes survive mute/metadata cleaning and all source hashes are unchanged.
- `preview/native-tools-modes.png` shows native renderer output; the original HTML
  design reference stays separate. It is not a screenshot of a real Explorer drop.
- The local Arabic guide now describes Ctrl taps and the four MP4 actions. New native
  keyboard/OLE acceptance remains for the user's desktop; tests do not simulate drags.
- Final build passed with no warnings/errors and all 20 groups again in
  `artifacts/tests-20261019-004544/results.json`. Installer SHA256:
  `E8714A120E699D7F18DB3830861AD1574EC5DCBEABD4F330C004DDC440A918CC`.
- Package/process smoke passed: all 530 files match, bundled runtime, singleton,
  no main window and clean hook removal/exit. Idle sample: 0 ms CPU / 3 seconds,
  99.86 MB working set (not a benchmark). Arabic tap instructions fit 390 px.
- Updated portable copy is running (PID 16928): build 0.2.1-ctrl.1, AppReady and
  tray visibility requested, no main window. No startup/installation registry changes.

## Previous 0.2.0-pixel.1 updates

### Transparent-logo follow-up

- Removed the black canvas using the built-in imagegen edit tool. The consumed asset
  is `assets/Naqla-logo-transparent.png`; original `LOGO.png` is preserved byte-for-byte.
  Exact edit prompt/tool provenance: `assets/Naqla-logo-edit.md`.
- Regenerated all ICO sizes with alpha, updated preview copies and app resources,
  rebuilt app/installer and verified transparent GDI corners at 16–128 px plus the
  PNG's RGBA corners. Extracted app and installer icons have transparent corners too.
- Rendered against dark and ivory surfaces for visual QA. Published preview PNG
  hash matches the transparent source. No wheel, drag or codec logic changed.
- Latest installer SHA256:
  `AF30252AF820BF39D0010350C5B6FA3A0DA8FE31B954661D34827370A4065303`.
- Package/process smoke passed again: 530 files hash-match, bundled runtime, no main
  window, singleton and clean hook/exit cleanup. Original 17 codec test groups below
  were not repeated for this asset-only update.

### Functional wheel update

- Replaced the temporary rectangular renderer with a compact native PixelWheel:
  240 DIP circle, 260 × 306 DIP transparent canvas, connected sectors, stepped rim,
  actual approved pixel icons, five category palettes and matching translucent capsule.
  No development/status/disabled-option text is drawn over the desktop.
- Drag detection, OLE delivery, renderer and conversion remain separate. Overlay
  dimensions/anchor are injected; Windows code has no wheel-style/codec dependency.
- Every visible destination now dispatches to a real bundled local engine. Image,
  video, audio and PDF support and limits: [CONVERSIONS.md](docs/CONVERSIONS.md).
- Added cancellable, below-normal-priority FFmpeg jobs, bounded workers, sanitized
  codec errors, collision-safe output, full output decode and temporary-file cleanup.
  Cancel from tray; Exit cancels before cleanup. No codec process runs while idle.
- Images: JPG/PNG/WebP/AVIF/TIFF/PDF, white alpha matte. Tools: compress, rotate 90°,
  strip metadata. HEIC output/crop/multi-file merge stay hidden rather than pretending
  to work. PDF DOCX/RTF/TXT is all-page text extraction, without OCR/layout recreation.
- The actual previous user session recorded Desktop MP4 `OleFileConfirmed` twice
  (267,919,434 bytes). This is real dragged-file evidence, with no guessed path.
  It does not prove accepted Drop → output or stationary-pointer reliability.
- Seventeen automated test groups passed on real codec/filesystem fixtures, including
  all MP4 destinations, reverse video/audio formats, all image destinations, PDF pages,
  tools, cancellation, privacy and actual WPF render transparency. Source hashes stay
  unchanged. These fixtures are not represented as Explorer drag tests.
- `preview/index.html` stays an independent design reference. Native renderer snapshots
  are separate and do not claim to be screenshots of a real Explorer drag.
- Rewritten local Arabic checklist covers the new UI/conversions and failed-drag evidence.
- Final build passed with zero warnings/errors and all 17 test groups. Installer
  SHA256: `1C5A10FBC65DADFF182BB60B182E96FF5D01829976B47D19A65FABCED0DDF1B1`.
- Package/process smoke passed: all 530 payload files hash-match, bundled runtime,
  no main window, singleton and clean exit/hook removal. Short idle sample: 0 ms CPU
  over 3 seconds, 100.61 MB working set (not a benchmark). Arabic guide fits 390 px.
- The old portable process was stopped cleanly. Fresh desktop acceptance of the
  transparent target remains required; [DESKTOP-TESTS.md](docs/DESKTOP-TESTS.md)
  records outstanding cases. No end-to-end drag success is claimed from codec tests.
- The updated portable copy is running (PID 19576), build 0.2.0-pixel.1, AppReady
  recorded, tray visibility requested and MainWindowHandle 0.

---

## Historical diagnostic phase — 2026-10-18

**State: development foundation built; native drag feasibility NOT yet verified;
visual design awaits explicit user approval.**

Current scope: diagnostic build **0.1.1-diagnostics.1** for real-world manual testing.
The user praised the preview but explicitly directed us to keep production wheel
implementation on hold and complete real desktop testing first. No radial redesign
or conversion-engine expansion is part of this diagnostic update.

Branding update: the supplied root `LOGO.png` replaces the placeholder mark in the
tray, executable/installer icons, installer window and standalone preview header.
The original PNG is preserved; `assets/Naqla.ico` includes 16–256 px frames generated
by `scripts/update-icons.ps1`. No radial layout, drag behavior or diagnostic schema changed.

## Current diagnostic testing handoff

- [x] Local, typed, asynchronous logging added to the existing Core/Windows/App contracts.
- [x] Hook install, Shift/mouse state, drag threshold, candidate dispatch, overlay
  placement/visibility, actual OLE events, conversion and clean exit are traceable.
- [x] No filenames, paths, contents, window titles, unrelated keys, coordinates or raw
  exception messages in logs. Allowlisted format/category/bytes only.
- [x] Bounded queue, session size/retention cap, safe storage failures and session logging toggle.
- [x] Tray diagnostics status, marker, log-folder action and local testing guide.
- [x] Arabic first-run checklist and expected observations; failed-attempt evidence instructions.
- [x] Ten automated checks passed, including privacy, retention and storage-failure tests.
- [x] Fresh branded diagnostic self-contained installer built; all 489 extracted payload files
  hash-match the published app. Latest HTML preview and testing guides are included.
- [x] Diagnostic process smoke passed: no main window, bundled runtime used, duplicate
  exits 0, clean quit exits 0, actual native hook removal reports success.
- [x] NotifyIcon.Visible is true and tray lifecycle records are present. Independent
  visual confirmation of the actual tray icon still requires the user's desktop.
- [x] Diagnostic privacy/retention/storage checks pass (10 total automated checks).
- [x] Offline Arabic HTML guide checked at desktop/mobile width: no page errors or overflow.
- [ ] Actual Explorer/Desktop drag and PNG drop → JPG acceptance — awaiting user's desktop run.

Quick start: [docs/MANUAL-TEST-AR.md](docs/MANUAL-TEST-AR.md), packaged as `TESTING.html`.
Technical log map: [docs/DIAGNOSTICS.md](docs/DIAGNOSTICS.md).
Smoke evidence: `artifacts/diagnostic-package-smoke.json`. Latest installer SHA-256:
`CF601533C563FB64241D381D2CEC6E3E2EC8135DC33637AF7D911D0ADE2144CC`.
Idle sample for this build: 0 ms CPU over 3 s, about 70.25 MB working set; not a benchmark.
Desktop helper retries including reset again failed with the unavailable native pipe.

No production radial styling has been implemented. The application uses a temporary
diagnostic target. No alternative interaction has replaced Drag + Shift.

## Step 1 — foundation

- [x] Seven-project C#/.NET 10 Windows solution; WPF app, Win32 hooks and WinForms tray.
- [x] Background launch; explicit shutdown; no main window/onboarding/settings startup.
- [x] Per-session mutex prevents simultaneous instances.
- [x] Per-user startup mechanism implemented and documented; opt-out in installer/tray.
- [x] Tray Exit and `--quit` clean shutdown, waiting for active conversion.
- [x] Runtime process smoke check: alive, no main window, duplicate exits, clean quit.
- [ ] Observe actual tray icon, startup at Windows sign-in, and Explorer restart recovery.

## Step 2 — drag integration

- [x] Mouse movement threshold + subsequent Shift produces a candidate, not a guessed file.
- [x] Shell-root-window filtering, no-activate topmost transient OLE overlay.
- [x] WPF AllowDrop/IDropTarget bridge reads actual source IDataObject FileDrop paths.
- [x] Metadata includes real full path, extension, byte size, category.
- [x] All requested extensions classified; single local-file scope enforced.
- [x] Drop payload is rechecked; only Copy advertised/returned; no original deletion.
- [x] Escape/leave/release cleanup and Ctrl hold/release contracts implemented.
- [ ] **Actual Explorer/Desktop drag acceptance tests. Not executed.**
- [ ] Stationary-pointer Shift activation and OLE delivery timing/reliability gate.
- [ ] Arabic names, varied sizes/extensions and cancellation during real Shell drags.

Desktop automation was attempted three times, including a JS runtime reset. Each
failed because the Computer Use native pipe was unavailable. No fake drags, guessed
Explorer selections, fake filenames in the drop handler, or hardcoded source paths
were used. See [Windows findings](docs/WINDOWS-INTEGRATION.md) and the
[live acceptance matrix](docs/DESKTOP-TESTS.md).

## Step 3 — minimal functional prototype

- [x] Temporary radial diagnostic target, with planned actions marked disabled.
- [x] Real PNG → JPG library implementation using offline WPF/WIC codecs.
- [x] White transparency matte, quality 92, 40 MP limit, private temporary output.
- [x] Decode output pixels and verify dimensions before non-overwriting publication.
- [x] Actual local codec tests including Arabic path, collisions and original hash.
- [x] Corrupt/signature-mismatched source and pre-conversion cancellation tests.
- [ ] **Conversion from an actual Explorer drop** and visible notification delivery.

Passing codec tests does not make this end-to-end step complete; it remains gated by
real OLE drag validation. Errors are surfaced through local tray notifications.

## Step 4 — design preview

- [x] Separate offline HTML/SVG design sheet; follows supplied radial reference.
- [x] Both Convert and Tools states at 320 CSS px; PNG/2.4 MB stationary hub.
- [x] Revision 02: colorful pixel-file sprites, stepped rims, highlights and soft glow.
- [x] Six joined image sectors; current source omitted. WEBP added to the visual
  proposal to match the user's latest reference; native converter scope is unchanged.
- [x] Five color families: amber images, coral video, purple audio, blue PDF, teal tools.
- [x] Capsule category labels/controls, rounded mode selector, and five-wheel gallery.
- [x] Active/inactive sectors and both Arabic capsule states.
- [x] Opening replay, 200 ms sector fade/capsule slide, hover and Escape demo.
- [x] Browser checks: Ctrl hold/release, stationary hub, mode buttons, hover, Escape,
  replay, actual dimensions, 390 px responsive layout and no page errors.
- [x] Rendered preview visually inspected: `preview/approval-preview.png`.

Revision 02 follows the user's request for more pixel character, life, per-format icon
colors and capsule styling. Revision 01 is archived in `preview/archive`.

Revision 03 follows the request for a translucent floating glass Convert/Tools capsule:
cool reflections, soft elevation shadow, backdrop blur and a translucent amber active
pill. The user liked the wheel and requested this selector revision; explicit final
design approval is still pending. Revision 02 is archived in `preview/archive`.
The focused two-state visual is `preview/glass-selector.png`; the all-state sheet is
`preview/wheel-all-states.png`. Native production UI remains unchanged.

Revision 04 follows the request to match the selector to each wheel's color and
redesign its shape. The selector is now a slimmer 248 × 42 px frosted control with
14 px outer corners, a quieter inset selection, compact pixel arrows/sliders and no
large glossy reflection. Borders, selection and labels inherit the current wheel's
palette, including teal Tools mode. The five-palette image is
`preview/themed-selector.png`. Revision 03 is archived; final approval remains pending.

Open [the interactive preview](preview/index.html) at 100% browser zoom, or see
[the five-wheel family](preview/approval-family.png).
The visual page uses sample file data and performs no file conversion.

The visual revisions above changed only the standalone preview and its verification/docs.
The diagnostic update packages that HTML separately and adds logging to the existing
native drag/tray modules. It preserves the temporary test UI and the architecture.

## Step 5 — approval checkpoint

**STOP. The user explicitly requires live desktop testing before production UI work.**

- [x] User praised revision 04; no production implementation authorized in the latest request.
- [ ] User explicitly authorizes final wheel implementation after the manual testing gate.
- [ ] After revisions, show updated preview and obtain approval again.

This is a user-requested checkpoint. Silence is not approval. Even with design approval,
do not declare native gesture feasibility solved without the pending live tests.

## Step 6 — production implementation

- [ ] Deliberately deferred until explicit visual approval.
- [ ] Native final wheel, production animation/input polish, and additional converters.

## Packaging and verification

- [x] Per-user installer/uninstaller source and reproducible self-contained build script.
- [x] Build with warnings as errors; no external runtime converter dependencies.
- [x] Seven meaningful automated filesystem/codec/geometry checks passed.
- [x] Process smoke: main-window handle 0, duplicate exits 0, clean exit 0.
- [x] Short idle sample: 0 ms CPU over 3 s; working set about 65 MB. Not a benchmark.
- [x] Self-contained x64 development installer built (about 120 MB including runtimes).
- [x] Installer extraction exit 0; all 484 payload files hash-match published files.
- [x] Extracted app smoke: bundled coreclr loaded from payload, no main window,
  duplicate exits, and graceful quit. No install-time registry changes made by this check.
- [ ] Actual installer UI, upgrade, uninstall, clean Windows 10/11 machine verification.

Build instructions and current scope: [README.md](README.md).
Recorded verification and build fingerprint: [docs/VERIFICATION.md](docs/VERIFICATION.md).
