# Smart Rename — 0.6.0-targetsize.1

Rename the actual files after reviewing Before → After and giving explicit consent.
No contents are copied. The existing wheel, Shift gesture and Ctrl toggle are retained.
Use disposable local test files for initial desktop acceptance.

## Desktop acceptance

1. Launch this build and mark the test using Diagnostics → Mark test start.
2. Select one or several supported local files in Explorer. Drag, hold the mouse,
   press Shift, tap Ctrl and release over **تسمية**. Text documents support this tool too.
3. A separate editor opens after Drop. Initially names are unchanged and confirmation
   is disabled. The editor is never a startup window.
4. Enter prefix `test-`, pattern `{n}-{name}`, start `7`, padding `3`. Expect e.g.
   `photo.PNG` → `test-007-photo.PNG`. Preview changes live; Explorer files must remain
   unchanged. Suffix is inserted before the preserved extension.
5. Try find/replace, case modes, cleanup, `{date}` with modified/created/today and
   the three displayed date formats. Optional regex supports .NET groups such as
   `image_(\d+)` → `shot-$1`. A match has a 100 ms timeout. Invalid patterns show errors.
6. Set a reserved name (`CON`), duplicate names or an existing unrelated filename.
   The editor must report the conflict and block confirmation; it never silently adds
   suffixes. Cancel/Escape must leave every source and its content unchanged.
7. Restore valid names. Check the explicit consent box, then click the amber confirm
   button. Any setting change clears consent and disables it until a fresh valid preview.
   Expect the old names to disappear and the new names to refer to the same files,
   with identical bytes. No duplicate originals or conversion outputs are created.
8. Tray → **Undo last rename…**, review the inverse Before → After, explicitly consent
   and confirm. Expect the original names and content restored. A modified, replaced,
   missing or occupied file blocks safe undo. Cancel undo leaves names unchanged.
9. Repeat with Arabic names, a single file, mixed image/text selections and an occupied
   target created after preview. A stale file or new target must fail without replacement.
10. Check the blue Hide, coral Cancel, violet Details and mint Results pixel buttons;
    hover, Tab/Space, focus, disabled states and 125%/150% DPI. Hiding progress keeps
    work running; reopen it from the tray. Exit cancels and waits for rollback/cleanup.

Return the attempt's diagnostic session, marker/time, file count, action and cropped
editor/progress screenshots with private names hidden. Do not send original files or
the local recovery map. Actual desktop clicks/drags are not established by render tests.

## Transformation rules

- 1–200 files; sequence follows the real OLE FileDrop order, not a promised visual
  selection order. Numbers are padded to a minimum width, not truncated.
- Find/replace applies to the source stem, followed by pattern expansion, prefix/suffix,
  invariant case conversion and optional cleanup. The exact original extension is then
  appended by default. Cleanup replaces Windows-invalid characters, collapses whitespace,
  trims trailing dots/spaces and prefixes reserved device names with `_`.
- `{name}`, `{n}`, `{date}` are available; `{{`/`}}` produce literal braces. Date choices
  use local filesystem modified/creation times or today's local date, not EXIF dates.
- Unchecking Preserve extension explicitly edits the full filename. Changing an extension
  does **not** convert content; the preview and confirmation remain mandatory.
- Names are limited to 240 UTF-16 characters. Directories, UNC paths, hard links,
  symlinks/reparse-point files and files without stable Windows IDs are safely refused.
  Cloud placeholders need a separate supported path; download/copy test files to a normal
  local folder rather than assuming every OneDrive item qualifies.
- The same-directory native operation never replaces an existing target. Files are locked
  against writers/deleters and IDs, length and timestamps are rechecked before any move.
  Swaps and case-only changes use staged original file objects. NTFS creation-time
  tunneling is corrected to retain each file's captured creation time.
- Undo covers the last successful rename **in this running session**, after fresh checks
  and confirmation. No cross-session undo, power-loss atomicity or content-hash guarantee
  against deliberately forged metadata is claimed. Tests independently compare hashes,
  IDs, timestamps and alternate streams. Ordinary use never writes file contents.

## Recovery and privacy

Before any name changes, the service flushes a local map to
`%LOCALAPPDATA%\Naqla\RenameRecovery\rename-….json`. This recovery record contains
local paths and file identity metadata; it is separate from diagnostic logs and is not
uploaded. Successful completion or rollback removes the owned record. A disk failure
can prevent removal. Tray → **Rename recovery records…** opens the folder on request.

Cancellation/failure attempts to restore every original name. If a different program
creates an unrelated file that blocks restoration, neither file is overwritten or deleted.
The UI reports recovery required and retains the map. A forced termination/power loss
can likewise interrupt the sequence; there is no automatic destructive recovery.

**`Naqla-Rename-Recovery-…` files beside sources are the ORIGINAL DATA under temporary
names. Never delete or clean them as disposable output.** Use the map's Before, After,
Recovery and identity information to locate each surviving file. Close Naqla before
manual recovery, inspect names and content, choose an unoccupied destination and keep
unrelated occupants. Request help with a redacted map if anything is uncertain.

Diagnostics record only typed Rename/UndoRename operations, counts, stages and error
codes. They contain no rename patterns, typed text, source/target paths or recovery maps.

Implementation references: Microsoft [rename by handle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle),
[no-replacement rename structure](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_rename_info),
[filename rules](https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file),
[SetFileTime](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfiletime)
and [filesystem behavior / tunneling](https://download.microsoft.com/download/4/3/8/43889780-8d45-4b2e-9d3a-c696a890309f/file%20system%20behavior%20overview.pdf).
