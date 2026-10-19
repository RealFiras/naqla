# Naqla 0.8 — actual Windows acceptance

**MANUAL PENDING.** Backend processing, offscreen WPF images and process smoke tests
do not prove Explorer/OLE interaction. Record Windows version, display scale,
monitor count, Naqla build and the row IDs below. Use disposable **copies** of your
own fixtures in a local writable folder; keep an independent original/hash list.

## Install and first activation

1. Run `artifacts/Naqla-Dev-Setup-win-x64.exe`. Keep “Launch Naqla” checked.
   Startup is a visible per-user option, also available in the tray menu.
2. Expect a tray icon and **no main window**. Right-click it → Diagnostics →
   Show diagnostic status. Confirm logging is available, then **Mark test start**.
3. In Explorer, grab a supported local file with the left mouse button. Keep the
   button down, move beyond the normal drag threshold, and press Shift.
4. Expect the existing small transparent Pixel-Modern wheel near the pointer.
   Actual OLE delivery fills its hub with extension/size; a hook alone cannot do so.
   Move slightly across the wheel if Explorer has not delivered DragEnter yet.
5. Release Shift while **still dragging**. Tap Ctrl for Tools. Releasing Ctrl keeps
   Tools selected; another Ctrl press returns to Convert.
6. When Tools has several pages, center dots indicate the page. Tab advances;
   Shift+Tab goes back. At most six tool sectors appear. These keys are intercepted
   only during the confirmed drag, with Tools open and multiple pages available.
7. Hover an available sector and release the left button. The wheel closes. A
   contextual editor opens when the operation needs settings or confirmation;
   otherwise processing starts. Conversion outputs use new collision-safe names
   beside each source. **Smart Rename changes actual names only after confirmation.**
8. Escape, releasing outside the wheel or cancelling an editor must publish no
   output for that cancelled task. A valid Explorer drop elsewhere follows Windows
   behavior; Naqla cannot undo an unrelated destination's normal operation.

## Gesture and lifecycle matrix

| ID | Perform | Required observation |
|---|---|---|
| W01 | First install and launch | Tray only; no console/dashboard/onboarding |
| W02 | Repeat launch | A single resident instance |
| W03 | Desktop file → drag + Shift | Actual extension/size, matching file operation |
| W04 | Explorer folder file → same | Same behavior |
| W05 | Arabic filename and folder | Full operation succeeds, original preserved |
| W06 | Ctrl down/up/down | Tools persists after release; next press converts |
| W07 | Tools Tab / Shift+Tab, every page | Correct actions/dots, no stale hovered action |
| W08 | Tab outside drag, Alt+Tab, Ctrl+Tab | Normal Windows shortcuts remain usable |
| W09 | Escape at hub/sector/editor | No unintended conversion; no lingering overlay |
| W10 | Release outside / unsupported file | No Naqla processing or unrelated file loss |
| W11 | Mixed and 10-file/200-file selections | Only common tools; every source accounted for |
| W12 | Fast repeated activation, busy operation | No duplicate jobs or competing overlays |
| W13 | Screen edges and each monitor at 100/150/200% | Wheel/editor fits work area; usable controls |
| W14 | Restart Explorer using Task Manager | Icon reappears; next real drag works |
| W15 | Cancel job, hide/reopen progress, Exit while busy | Cancel cleanup, retained successes, no child workers |
| W16 | Uncheck startup, upgrade, relaunch | Disabled preference preserved |
| W17 | Uninstall on a clean test account | App/startup removed; originals/results/user-added files remain |

## Functional acceptance matrix

Import optional ZIP packs through tray → **Local components / plugins…**. Import is
explicit and verifies this build's catalog. No network or installed Office is needed.
The `speech` pack includes Base/Tiny; Base is preferred. `office` is a large separate
component, not part of idle startup. Allow time for its first integrity check.

| ID | Fixture/action | Verify independently |
|---|---|---|
| F01 | Transparent PNG → JPG, then all image destinations | Decode, dimensions, white JPG matte, real format |
| F02 | JPEG with orientation/ICC; HEIC/AVIF, large/corrupt image | Correct orientation/color or clear refusal; no fake output |
| F03 | Compress at different qualities; rotate each angle | Actual size reported; actual pixels/angle change |
| F04 | Crop free/ratios, handles, move, zoom, middle-button pan | Saved pixel rectangle matches oriented preview |
| F05 | Target Size all four presets/custom, impossible target, optional resize | Actual bytes ≤ target only when reported achieved; dimensions default intact |
| F06 | Drag selected images together → merge PDF | Correct page count and drag-source order |
| F07 | Smart Rename single/batch/regex/swap/Unicode | Before/After correct; Cancel unchanged; Confirm actual rename; Undo restores |
| F08 | OCR English/Arabic/mixed image and scanned PDF | Actual editable text, copy/TXT, no invented text on blank page |
| F09 | Remove Background on real photos/hair/transparency | Actual mask/PNG alpha, unchanged dimensions; judge edge quality manually |
| F10 | Video all advertised formats, silent video, rotation | Decode full video/audio; unavailable audio extraction hidden |
| F11 | Trim selected range; Frames single/every N/count/range | Duration, actual frame times, correct orientation and dedicated output folder |
| F12 | Audio all formats; Normalize presets/custom | Decode; loudness/true-peak measurements, original retained |
| F13 | Edge silence trim, mono, mono → dual-channel | Internal pauses retained; correct channel count |
| F14 | Arabic/English/mixed continuous speech → SRT and VTT | Listen and review words/timestamps; correct model limitations, editable cues |
| F15 | PDF reorder/extract/delete/merge | Real thumbnails, all pages accounted for, ranges validated, original vectors retained |
| F16 | PDF numbers, Arabic/English/image watermark, rotate | Output matches preview, opacity/position/scale, selected pages only |
| F17 | PDF fit-width/page, zoom, pan, resize, RTL | Three columns usable; preview current before Save |
| F18 | PDF → PNG/JPG at each DPI, native text exports | Correct pages/dimensions; DOCX/RTF text-only fidelity disclosed |
| F19 | DOCX/TXT/RTF/HTML/ODT → each advertised destination | Open in a second reader; Arabic/table/image/multipage; HTML assets included |
| F20 | Privacy image/PDF/Office/audio/video, selective categories | Removed/retained report, independent metadata inspection, unchanged content |
| F21 | ZIP/7Z/TAR create/extract nested/empty/Unicode folders | Contents and hashes match; new folder; no overwrite |
| F22 | Corrupt/encrypted archive, traversal/link/bomb fixture | Safe refusal and no outside writes; encrypted extraction unsupported |
| F23 | Install two sample plugins, enable/disable/change/uninstall | Disabled until consent, contextual actions, actual SHA-256/text statistics |
| F24 | Every family: collisions, read-only output, cancel, batch partial failure | Honest per-file outcome; no original content overwritten |

## Offline test

On a disposable test account/VM, install the development app and import desired
packs while local. Disconnect that test environment's networking yourself, then
repeat F01, F05, F08–F23. Expect no login, download or cloud request. Record actual
observations. Source-code inspection is not a substitute for this test. Naqla's
worker Job Objects limit resources/lifetime; they are **not a network sandbox**.

## If Shift shows nothing — return these items

- Row ID, build, Windows version, scale/monitor, source extension/category and
  approximate size, Desktop versus Explorer, exact key order and whether Ctrl was
  held before activation. No personal path is needed.
- The marked session's `session-*.jsonl` from `%LOCALAPPDATA%\Naqla\Diagnostics`
  plus the diagnostic status text. The closed log schema excludes paths, filenames,
  text contents and raw exception messages. Do not send rename recovery records.
- A cropped screenshot/short recording from grabbing the test fixture through Shift,
  plus any error/result panel. Hide unrelated Explorer paths, personal names and files.
  For a conversion failure, provide the result-panel details and output's actual
  size/extension; share a non-sensitive fixture only if comfortable.

Interpret the sequence with [DIAGNOSTICS.md](DIAGNOSTICS.md):

| Last event | Investigation |
|---|---|
| Missing `KeyboardHookInstalled` / `ShiftDown` | Hook registration, process integrity/elevation |
| `ShiftDown` → `GestureSuppressed` | Left button, threshold, Explorer/Desktop origin; reason is logged |
| `GestureCandidate`, no `OverlayShown` | Dispatch/native overlay placement or failure |
| `OverlayShown`, no `OleFileConfirmed`, timeout | Explorer did not deliver CF_HDROP; overlay alone is not drag proof |
| `OleFileConfirmed`, no accepted sector | Mode/page, enabled common action, Copy effect and pointer position |
| `OleDropAccepted` then failure | Editor parameters, actual decoder/permissions/dependency; inspect result details |

The supported interaction cannot intercept every elevated/non-Explorer/virtual/cloud
drag. See [WINDOWS-INTEGRATION.md](WINDOWS-INTEGRATION.md). Do not substitute a file
picker, shell context menu or permanent drop zone when reporting a failed W03/W04.
