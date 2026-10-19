# Phase 1 — real desktop acceptance, 0.6.0-targetsize.1

Phase 1 is **implemented but not accepted** until a tester performs a real Explorer
multi-file drag and verifies its outputs. The native Computer Use pipe was unavailable
(OS error 2). File/codec/render/process tests do not pass this gate.

## Main acceptance: ten PNG images → ten JPG files

1. Run the current development installer or portable app; the tray menu must show
   `0.6.0-targetsize.1`. No main window opens. Use the same normal integrity level as Explorer.
2. Open the prepared `artifacts/desktop-batch-test-<timestamp>/10-images` folder.
   It initially contains ten actual PNG images with Arabic names and transparent edges.
   A developer can prepare another fresh set with `scripts/prepare-batch-desktop-test.ps1`;
   generating fixtures is not a drag test.
3. Tray → Diagnostics → Mark test start. Select **only the ten PNG sources** in Explorer.
   Keep the left button down while dragging, move past the drag threshold, press Shift.
4. The same circle/capsule opens; the center shows **10 ملف** and the summed real size.
   JPG is the top conversion segment. Hover it, then release the left button.
5. The wheel closes. A compact panel reports completed/total jobs, success/failure,
   and Cancel. Percent is based on finished jobs, not invented byte progress.
   A very small batch may finish before intermediate numbers can be seen.
6. Expected: ten successes, zero failures/cancellations; ten corresponding JPGs beside
   their PNGs. Open some in Photos: dimensions 256×192, colored center, white edges.
   Sources must retain their original hashes. Repeat using only PNG sources and verify
   numbered collision names preserve earlier outputs.
7. Hide and reopen progress with tray → **Show current / recent results…**. When done,
   use **مجلدات النتائج** to open output directories, or dismiss the panel.

Optional file verification from the workspace PowerShell (built into Windows):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-batch-desktop-test.ps1 -Directory "artifacts/desktop-batch-test-<timestamp>"
```

This checks actual source hashes, JPG decoding/dimensions and white transparency
matte. It deliberately leaves `ActualExplorerDragVerified:false`: only a tester and
the real diagnostic session can establish how the conversion was invoked.

## Partial failure

Use the fixture's `partial-failure` folder. Select its four `.png` files and perform
the same real drag/drop to JPG. Three sources are genuine images; one is deliberately
corrupt. Expect three successes, one failure, three JPGs and no output for the corrupt
source. Details identify the failed source locally. Successful outputs remain.
Verify with the same script plus `-Group partial-failure`.

## Additional acceptance checks

- [ ] Desktop-origin and Explorer-origin single and multi-file drags; stationary
  pointer vs slight continued movement after Shift. No fake drag acceptance evidence.
- [ ] Multiple videos → common destination; mixed-format images → a format valid
  for every input. Current-source formats are omitted from the shared destinations.
- [ ] Mixed image/audio → Ctrl → shared Compress/Strip tools. No shared conversion
  is shown for this pair. PNG + TXT now shares the explicit-confirmation Rename tool; it has no shared conversion.
- [ ] Unsupported/unreadable member prevents silently processing a subset. Duplicate
  basenames in different directories remain distinct, with outputs beside each source.
- [ ] A destination with genuinely denied write permission reports its own failure;
  other writable jobs succeed. A read-only folder attribute alone is not an ACL test.
- [ ] Cancel a sufficiently long batch after at least one output; retain those
  successes, cancel the remainder and remove owned temporary files. Do not claim 100%
  processed when jobs were cancelled. Tray Exit also cancels and cleans up.
- [ ] Crop multiple images: first-source preview, same selected oriented pixel bounds
  applied individually; smaller incompatible images fail explicitly. Cancel editor
  creates no batch. Explicit Tools → Merge PDF creates one ordered multi-page PDF;
  Convert → PDF creates one PDF per source.
- [ ] Ctrl taps, repeat suppression, Escape, normal drag without Shift, source hashes,
  Explorer restart, clean Exit, no duplicate instance, multiple monitors and high DPI.
- [ ] Disconnect network manually and repeat these operations. This has not been
  claimed from merely observing that the app contains no network client.
- [ ] Record observed Shift-to-wheel latency against the previous build. Automated
  SetSelection timing measures only capability/render data preparation, not this latency.

## Return on success or failure

Return the failed/successful attempt's `session-*.jsonl`, the marker/time, source count,
format/action, Desktop vs Explorer, Windows/DPI/monitor setup, and output counts.
Useful events: `OleFileConfirmed` / `OleDropAccepted` Count, `SelectionAnalyzed`,
`ConversionStarted`, `BatchProgressChanged`, per-job success/failure and `BatchFinished`.
For the main scenario expect accepted Count 10 and BatchFinished Count 10,
CompletedCount 10, FailedCount 0, CancelledCount 0. Cropped panel/hub screenshots help;
hide personal paths in failure details and directory menus. No private originals needed.

The user's advanced request says: **“Phase 1 is complete only when a real multi-file
drag from Explorer successfully performs a batch operation.”** Phase 2 OCR and the
later phases follow that gate; see [ADVANCED-PLAN.md](ADVANCED-PLAN.md).

Smart Rename uses one aggregate guarded transaction rather than independent per-file
conversion jobs. Complete its separate [acceptance checklist](SMART-RENAME.md); the old
10-file JPG acceptance remains unverified until a real Explorer test is reported.
