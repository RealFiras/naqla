# Local diagnostics — 0.8 development

Current acceptance and actual Windows steps: [manual checklist](MANUAL-WINDOWS-TESTS.md).
The stable event sequence below remains applicable; contextual processing has expanded
since the original target-size phase. Use the build field in each session header.

Manual quick start: [Arabic checklist](MANUAL-TEST-AR.md) and the packaged offline
`TESTING.html` (tray → Manual testing guide). The current build uses the smaller native
pixel wheel and real local converters. The drag architecture and gesture are retained.
ConversionStarted includes an allowlisted `Target` and typed `Operation` (Convert,
Compress, Crop, Rotate, StripMetadata, MergePdf, Mute, Rename, UndoRename, TargetSize); no engine stderr is logged. ControlDown/Up are
recorded only during an armed gesture. ModeChanged reports the actual mode and option
Count after a Ctrl down edge, rather than a requested mode rejected by the renderer.

## Storage and privacy

Default: `%LOCALAPPDATA%\Naqla\Diagnostics\session-<UTC>-<random>.jsonl`.
Default-on for this development test build; session-only toggle in the Diagnostics menu.
Each session has a build/OS/bitness/time-offset header, then sequenced JSON events
with UTC, elapsed milliseconds, and a per-session gesture attempt number. `TestMarked`
includes the tray marker number. No names, user paths, file contents, hashes of paths,
window titles, app names, pointer coordinates, general keystrokes, stack traces or
exception messages are stored. Metadata contains only an allowlisted format, category
and byte count. For a batch, `OleFileConfirmed.Count` is the actual source count,
the format/category refer to the first source and bytes are the sum. `OleDropAccepted`
also carries the actual Count. Merge success
Count is the verified PDF page count. Crop logs only selected Width/Height after
confirmation, with no X/Y, image data or source path. This logging is independent of local success notifications that may
display the output path on the user's own screen. Failure details and output directory
menus are local UI and can contain source paths; they are never serialized to logs.

`SelectionAnalyzed` records source Count, Enabled (all metadata valid), and separate
ConversionOptionsCount/ToolOptionsCount. `BatchProgressChanged` records total Count,
CompletedCount and FailedCount. `BatchFinished` adds CancelledCount. Completed includes
success/failure and excludes cancelled jobs; ordinary batches have one job per source,
whereas explicit Merge has one aggregate job. Per-job codec failures use typed error
class/HResult only. No source list, name or failure message field was added.

The Core contract accepts only a closed event enum and typed fields. It does not
accept an arbitrary message/dictionary or serialize `DraggedFile`/Exception objects.
The implementation enqueues to a bounded 512-record channel; hooks never perform
disk I/O or JSON serialization. Records flush asynchronously as events arrive; there
is no idle polling timer for logging. Queue overflow is counted. Size cap: about 2MB
plus a final `LogLimitReached` record; last 8 session-pattern files are retained.
Status reports unavailable storage, cap reached, and dropped records. Errors in log
storage do not prevent starting the tray app. Normal shutdown drains the queue for
up to two seconds. A killed process or failing disk can lose its final records.

## Reading an actual user's attempt

Locate `TestMarked`, then the `ShiftDown` and nearby events with that attempt number.
Sequence is queue order; compare UTC/elapsed time as well when multiple threads report.
Do not infer an OLE drag merely from `GestureCandidate` or `OverlayShown`.

| Last observed evidence | Interpretation / next check |
| --- | --- |
| No session / `storage: unavailable` | App did not start, wrong build, logging off, or local storage failed; ask for status/startup screenshot |
| `KeyboardHookInstallFailed` / `MouseHookInstallFailed` | Installation failed; numeric Win32 error is included |
| Hooks installed, but no `ShiftDown` after the three-tap check | Keyboard callback was not observed (or logging off/capped); installation alone does not prove a hook stayed alive |
| `ShiftDown` with `LeftDown:false` | Hook did not observe a held left button, or Shift was pressed before dragging |
| `GestureSuppressed:BelowDragThreshold` | Movement threshold has not been observed; check actual drag and mouse input |
| `GestureSuppressed:NonShellOrigin` | Initial mouse-down root was not classified as Explorer/Desktop; `Origin` is a coarse enum, not an app/window name |
| `GestureSuppressed:AlreadyArmed` | Gesture was already attempted; Escape/release and start a fresh drag |
| `GestureCandidate` without `CandidateDispatched` | UI dispatch has not run yet; investigate blocked message pump/latency |
| `CandidateIgnored` | Explicit Busy/Exiting/AlreadyVisible/LeftNotDown reason |
| `OverlayShowRequested`, then `OverlayFailed` | Overlay/native placement failed; error code and safe DPI/size fields identify the stage |
| `OverlayShown` with no visible wheel | This is WPF's visibility state, not an independent visual test; request screenshot, DPI and monitor details |
| `OverlayUnconfirmedTimeout`, no `OleDragEnter` | Window appeared but no source IDataObject was delivered; compare stationary vs continued movement, normal integrity level, target discovery/hit-testing |
| `OleDragEnter`, then `OleRejected` | See NotFileDrop/NotSingleFile/UnsupportedExtension/MetadataFailure; exception type/code only |
| `OleFileConfirmed` | Real CF_HDROP list was received and inspected (up to 200 local files); Count and summed bytes are safe metadata, paths are absent. Consult SelectionAnalyzed.Enabled for validation, not this event alone |
| `SelectionAnalyzed` | Actual source count/metadata validity and common Convert/Tools option counts; zero shared options closes the candidate with NoCommonAction and requests a concise notification |
| `ControlDown` / `ControlUp` | Physical Ctrl transitions observed while the gesture is armed; release does not change selected mode |
| `ModeChanged` | A Ctrl press toggled the actual wheel mode; Tools and Count identify the applied state/options |
| `OleSelectionChanged` with `Enabled:false` / `CopyEffect:false` | Pointer is on hub/disabled option or source disallows Copy; no conversion should happen |
| `OleDragLeave` | Real OLE leave delivered; overlay is dismissed |
| `LeftReleased`, no `OleDropReceived` | Source did not deliver Drop to this target before release cleanup; investigate OLE timing and hit testing |
| `OleDropReceived`, then rejection | Disabled selection, no confirmed file, CopyNotAllowed, or payload mismatch |
| `OleDropAccepted` → `ConversionStarted` | Accepted actual drop; workflow has started (Crop requests a rectangle before encoding) |
| `OperationInputRequested` | Crop or Rename/UndoRename editor requested; typed Operation and file count only, after the OLE Drop callback for a drag; this alone does not independently prove the dialog was visible |
| `OperationInputAccepted` | Crop returned a validated rectangle (Width/Height only), or Rename/UndoRename returned explicit consent on a valid preview (Count only) |
| `ConversionSucceeded` | Conversion output was decoded/verified and published, or confirmed rename completed native file checks; Operation distinguishes these, and Count is items/pages/changed files; notification request is separate from OS delivery |
| `ConversionFailed` | Local codec/filesystem failure; safe error class/HResult only |
| `ConversionCancelled` | User cancellation/exit; conversion removes owned output, while rename attempts original-name rollback and reports any recovery failure separately |
| `BatchProgressChanged` | Actual finished/total job counts; zero Current in UI means the last job settled, not an invented byte percentage |
| `BatchFinished` | Whole queue settled: total, completed, failed and cancelled; successful outputs remain even when unrelated jobs failed |
| `AppExitRequested` → cleanup → `AppStopped` | Normal process lifecycle exit path completed, subject to successful log flush |

`OleAllowDropEnabled` records setting WPF AllowDrop. It is **not** independent proof
that OLE registration/target discovery succeeded. Only an actual `OleDragEnter` with
`OleFileConfirmed` establishes receipt of the real file, and only an actual Explorer
drop-to-output run establishes end-to-end success.

## Evidence to return

Return the failed attempt's session JSONL, a cropped status dialog + tray version/menu,
and optionally a short recording of the transient target. Include marker/time, OS/build,
DPI/monitor count, Desktop versus Explorer, approximate size/format, stationary versus
continued-motion results. Do not request a private original file or full source path.
Log sharing is manual; the app never transmits logs. Review screenshots separately
because Explorer/notification UI can contain personal paths that the logs omit.

## Rename recovery is separate

No patterns, typed rename text, names or recovery records enter diagnostics.
Rename/UndoRename and numeric counts use the existing closed schema. One aggregate
rename transaction counts as one queue job; its success Count is changed files.
Local recovery JSON maps deliberately contain paths and stable file metadata, stored
under `%LOCALAPPDATA%\Naqla\RenameRecovery`, outside Diagnostics. Never include those
maps in a diagnostic-log bundle; redact before requesting recovery help. Staged
`Naqla-Rename-Recovery-…` files are original data, not conversion temporary output.
See [SMART-RENAME.md](SMART-RENAME.md).

## Target-size measurement

OptimizationPreviewStarted/Measured/Failed/Cancelled distinguish measurement from saving.
Measured.Succeeded means the candidate fits, not that an output was published. TargetSize
is a typed Operation; TargetBytes/ResultBytes are numeric. Final per-job success is only
reported after strict validation and publication. A target miss is failure with measured
bytes and no output; it is not a successful oversized result. No paths, settings text,
image content or raw codec errors enter logs. All sources remain in a batch even when
a later readable source supplies the numbered preview. [Rules/testing](TARGET-SIZE.md).

## PDF editor preview

PdfEditorOpened and OperationInputRequested/Accepted identify the actual editor and
confirmed plan. PdfPreviewStarted/Ready/Failed/Cancelled distinguish prospective rendering
from publication. PdfReorder/PdfExtract/PdfDelete/PdfMerge/PdfNumbers/PdfWatermark/PdfRotate
are closed Operation values; Count is numeric pages/files. Changing tool inside the editor
records the actually confirmed operation. PreviewReady does not mean a file was saved.
Final ConversionSucceeded follows independent page-count/decode validation and publication.
Watermark text/images, ranges, fonts/colors, filenames/paths and raw errors are never logged.
See [PDF-TOOLKIT.md](PDF-TOOLKIT.md) for the exact acceptance procedure and limits.
