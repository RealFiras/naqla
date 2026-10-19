# Native interaction feasibility gate

The current **0.6.0-targetsize.1** build adds real batch features and confirmed native renaming above the existing pixel
renderer. It retains the gesture, hook, overlay placement and Ctrl toggle. Its OLE
adapter now preserves the real file list for all compatible batch actions, with local
diagnostics. Start with [the manual guide](MANUAL-TEST-AR.md) and use
[the diagnostic event map](DIAGNOSTICS.md) when a desktop drag fails.

## What Windows actually provides

An OLE drag originates in Explorer's `DoDragDrop` loop. The active target receives an
`IDataObject` through `IDropTarget.DragEnter`, followed by DragOver, DragLeave, or Drop.
`RegisterDragDrop` registers a target for **its own HWND**, on an OLE-initialized thread
with a working message pump. WPF `AllowDrop` exposes this mechanism through routed
drag events. Naqla uses that supported WPF bridge, rather than re-registering a WPF
HWND or attaching to Explorer's target.

The input hooks do not supply an IDataObject, file path, or definitive OLE-drag flag.
There is no global arbitrary-drag payload interface in these APIs. This is an inference
from their documented contracts, not a claim to have exhaustively tested every Windows
drag implementation. Explorer selection and the clipboard are not a substitute for
the actual payload, and Naqla never reads either to guess a source path.

## Prototype path — original interaction retained, reliability unproven

1. Passive low-level mouse and keyboard hooks recognize left down + drag-distance
   threshold + a subsequent Shift keydown. The gesture must start with an Explorer or
   desktop root window class beneath the initial mouse-down point. Every hook message is passed onward. Hook
   callbacks do not do image processing or filesystem reads.
2. This is only a **candidate**. A small topmost, tool, no-activate window is placed
   under the current pointer, within that monitor's work area. It is not a full-screen
   capture layer. A 1.2 s timeout dismisses an unconfirmed candidate.
3. The window is a real WPF/OLE target. Only a genuine DragEnter with
   CF_HDROP/FileDrop paths confirms input: up to 200 local files. Per-source metadata
   validity and common backend operations determine actionability; no unsupported
   member is silently removed. A concise notification explains no common action.
   Naqla reads the real ordered local path list and FileInfo
   metadata, not the file contents. Fully rendering source data before Drop can stall
   a drag; even path rendering can block for unusual Shell providers, so virtual and
   network files are outside this prototype's scope.
4. All subsequent selection comes from OLE pointer coordinates. A valid enabled Drop
   rechecks every incoming path, list order and local metadata. The adapter advertises and returns
   **Copy only**, never Move or Link, even with Shift/Ctrl held. Unsupported sectors
   return None. Encoding begins only after a real accepted Drop. Crop defers opening
   its separate editor until the OLE callback returns; no modal window opens while
   Explorer's drag remains active. Merge uses the exact confirmed ordered list; ordinary
   batch conversion queues one job for each source, off the dispatcher. Its transient
   progress/results panel opens only after the OLE callback, with no startup window.
5. A Ctrl down edge toggles Tools/Convert; release keeps the selected mode. Key repeat
   is ignored and each new drag starts in Convert. A mode requested before OLE file
   confirmation survives arrival of the actual metadata. Escape is
   passed through to Explorer's drag source as well as dismissing Naqla. DragLeave
   dismisses the target. A short mouse-release fallback handles cancellation races.
   No code warps the cursor or injects synthetic drags.

## Observed evidence and remaining feasibility checks

The user's actual diagnostic session recorded a Desktop-origin MP4 drag, genuine
OleDragEnter, OleFileConfirmed (267,919,434 bytes, Video/MP4), and hover selections on
two attempts. This establishes actual IDataObject file receipt for that case, beyond
just overlay visibility. No accepted drop/conversion was recorded in those attempts.
The user subsequently reports the integrated wheel working. Automated codec/render/
process tests do not replace interactive acceptance for the new crop editor, batch
OLE delivery or the other cases listed below.

The hardest unknown is delivery timing when the target appears directly beneath a
stationary drag pointer on Shift. Windows' drag loop must discover the new HWND.
A small pointer movement may be needed. It has not been established whether this is
reliable enough to satisfy the exact “press Shift, immediately see file info” interaction.
False candidate gestures, drag-image occlusion, DPI placement, cancellation races,
Explorer restart behavior, and interference with normal Shell drags need live testing.
The new 260 × 306 DIP window paints only a circle and capsule, with zero alpha outside
them; unlike the old full rectangle, its real hit surface needs a renewed OLE test.

Run [DESKTOP-TESTS.md](DESKTOP-TESTS.md). If the precise interaction fails consistently,
**stop and present the results before changing the interaction**. Viable alternatives
for user consideration, not adopted here:

- Shift plus a tiny continued pointer movement into the transient target.
- A transient target positioned beside the cursor, entered deliberately during drag.
- An explicit Explorer command/context-menu action with the actual selected file.

The last option changes the requested interaction substantially. No alternative is
silently enabled. A permanent drop zone and file picker are not part of this build.

## Other limits

- Same normal user integrity level as Explorer; cross-elevation drags are not supported.
- Escape cancellation depends on the source following the normal OLE drag contract.
- Files from non-Shell apps are intentionally excluded from gesture activation.
- Extension classification does not establish that arbitrary contents match the suffix.
- DPI placement uses the pointer monitor's DPI/work area; mixed DPI remains untested.
- WinForms NotifyIcon contains TaskbarCreated handling for taskbar reconstruction.
  This implementation choice still needs an actual Explorer restart acceptance test.
- Single-session instance guard: `Local\Naqla.Tray.0.1`. No unnecessary timers run while
  idle; only hooks and message pumps remain. Codecs run on worker threads or local
  cancellable media subprocesses, with no visual dependency.
- Tray notification delivery can depend on Windows notification preferences.

## Primary references

- [RegisterDragDrop: HWND registration, OleInitialize and message pump](https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-registerdragdrop)
- [IDropTarget::DragEnter: IDataObject, modifier flags and allowed effects](https://learn.microsoft.com/en-us/windows/win32/api/oleidl/nf-oleidl-idroptarget-dragenter)
- [Shell Data Object: CF_HDROP and cautions about rendering data during drag](https://learn.microsoft.com/en-us/windows/win32/shell/dataobject)
- [WPF drag-and-drop: AllowDrop, routed events, effects and cross-application operation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/drag-and-drop-overview)
- [Run and RunOnce per-user startup mechanism](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys)
- [GetDpiForWindow for a per-monitor-aware overlay](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdpiforwindow)

## Confirmed native rename

RENAME is processed only after the accepted OLE Drop finishes, in a separate editor.
The adapter, hooks, overlay placement and Ctrl mode state are unchanged. Smart Rename
locks regular local originals and uses no-replacement file-handle renames with a flushed
recovery map and rollback. It never treats a keyboard callback as source discovery.
See [SMART-RENAME.md](SMART-RENAME.md) for limits, confirmation and real desktop tests.
