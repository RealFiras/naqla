# Desktop acceptance tests — 0.6.0-targetsize.1

Use [the offline Arabic guide](TESTING.html) from the tray. These checks require an
actual interactive Windows desktop. Codec fixtures and render snapshots do not pass them.
Start with [BATCH-TESTING.md](BATCH-TESTING.md). Phase 1 remains unaccepted until a real
Explorer multi-file batch produces its outputs. Later phases wait for that user-defined gate.

Prior user evidence: Desktop-origin MP4 DragEnter, actual CF_HDROP metadata (267,919,434
bytes) and OLE hover selection were recorded in the diagnostic build. No accepted
conversion drop was recorded in that earlier log. The user now reports the integrated
wheel working. The new crop editor and multi-image OLE delivery still need live testing.

- [ ] No main window; transparent supplied logo visible in tray/overflow; version 0.6.0-targetsize.1.
- [ ] Desktop and Explorer: grab → move → Shift while left held → actual type/size.
- [ ] Compare stationary pointer on Shift with slight continued movement.
- [ ] PNG → JPG: actual accepted Drop, output opens independently, alpha white,
      source unchanged; existing names get suffixes.
- [ ] MP4 with audio → MOV, WebM, MP3, M4A, GIF; full duration and expected sound.
- [ ] MP4 without audio → MP3 fails clearly and leaves no partial output.
- [ ] Audio source → other available formats, independently playable.
- [ ] Image → PNG/JPG/WebP/AVIF/TIFF/HEIC/PDF; source format omitted; each result decodes.
- [ ] Compress, rotate and remove metadata save separate copies; an already compact source reports no reduction.
- [ ] Crop: actual Drop → editor; drag/reverse-drag marquee or exact pixel fields → saved pixels/dimensions. Oriented EXIF and Arabic paths.
- [ ] Crop Reset, invalid rectangle, Cancel/Escape and tray Exit while editor is open; no unwanted output.
- [ ] Drag ten PNGs → Shift → JPG: hub count/total bytes, ten verified JPGs beside each source, originals unchanged.
- [ ] Common conversions/tools for multiple videos, mixed-format images and mixed image/audio; no silent subset for unsupported inputs.
- [ ] Compact real completed/total progress, hide/reopen from tray, Cancel, failure details and all output directories.
- [ ] Corrupt source/denied destination reports partial failure while preserving other successes.
- [ ] Drag 2–200 selected images → Shift → Ctrl → Merge PDF; all pages/order/colors and summed size. Convert → PDF instead creates individual PDFs.
- [ ] Multi-image output beside first source, collision suffixes and unchanged originals. A corrupt image leaves no partial PDF.
- [ ] Multi-image selection exposes common conversions/tools and explicit Merge. Mixed categories expose only shared supported actions.
- [ ] Batch crop applies the first preview's selected pixel bounds; incompatible smaller images fail individually without clamping.
- [ ] Two-page PDF → two PNG/JPG pages in a new folder; text exports preserve both pages.
- [ ] Arabic names/directories and small/large files, all requested source categories.
- [ ] Quick Ctrl tap stays in Tools after release; next tap returns to Convert; repeats do not toggle.
- [ ] Ctrl before OLE confirmation retains Tools; new drags reset to Convert; MP4 has four real tools.
- [ ] Video rotate/mute/tag cleaning, audio compress/tag cleaning and PDF text tool; source unchanged.
- [ ] Matching capsule indication, Escape, hub release and DragLeave cleanup.
- [ ] Only circle + capsule; format icons/colors, compact size and no rectangular shadow.
- [ ] Multiple monitors, work-area edges, 100/125/150/200% and mixed DPI.
- [ ] Cancel current conversion, including a large merge or HEIC encode; no published partial output, no orphan encoder process.
- [ ] Duplicate launch; clean Exit including cancellation/cleanup during conversion.
- [ ] Normal drag without Shift and Shift without drag behave normally.
- [ ] Explorer restart restores tray icon; next drag works.
- [ ] Startup after next Windows logon; normal-account install/uninstall on a clean PC.

On failure return the session JSONL and cropped status/tray/overlay screenshots, with
test marker/time, OS/build, DPI, origin, format/size/target and stationary/moving result.
No source path or private file is needed. [Diagnostic interpretation](DIAGNOSTICS.md).

## Smart Rename and pixel controls

- [ ] Follow [SMART-RENAME.md](SMART-RENAME.md) with disposable Arabic/English files.
- [ ] Live Before → After, each transform/regex/date, invalid/collision rows, explicit
  consent, preview edits clearing consent; Cancel/Escape never change source names.
- [ ] Confirm actual same-file rename (no duplicate content), tray undo with confirmation,
  refusal for changed files/occupied names, and Exit/cancel lifecycle.
- [ ] Hide/Cancel/Details/Results pixel glyphs and colors, mouse/Tab/Space, focus/disabled
  states, crop buttons, 125%/150% DPI and screen bounds. Native renders are not click tests.
- [ ] Do not send local recovery maps containing paths with path-free diagnostic logs.

## Target Size / حجم مستهدف

Follow [TARGET-SIZE.md](TARGET-SIZE.md): all presets/custom KB/MB, measure before Save,
unchanged default dimensions, explicit proportional downsize, alpha/orientation,
impossible target with no publication, independent batch outcomes, collisions and
mid-measurement/batch Cancel/Exit. Check six/seven Image Tools sectors and the native
settings preview at 100%/125%/150%. Feature 3 does not start until Feature 2 acceptance
and any required preview approval. No actual desktop input is claimed from snapshots.
