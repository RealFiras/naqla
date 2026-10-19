# NAQLA — FINAL COMPLETE DEVELOPMENT, FEATURE AUDIT, END-TO-END TESTING & STABILIZATION

## MISSION

You are the lead Windows application engineer, QA engineer, UI/UX engineer, and release engineer responsible for completing the existing **Naqla — نقلة** application.

This is a complete development and stabilization assignment.

**Do not create a new project. Do not rebuild the application from scratch. Work directly inside the existing Naqla repository.**

The objective is to deliver a genuinely working, polished, fully offline Windows utility in which every advertised conversion, tool, and interaction has been implemented, exercised, and validated.

The application must be tested as a real product, not simply compiled successfully.

You are responsible for:

1. Auditing the entire existing project.
2. Completing all missing features.
3. Repairing broken or partially functional features.
4. Improving and unifying the current UI.
5. Refining the PDF Toolkit editor.
6. Testing actual conversions with real files.
7. Testing contextual tools for every supported category.
8. Validating generated outputs.
9. Checking real Explorer drag-and-drop behavior.
10. Improving reliability, security, and performance.
11. Producing an installable, self-contained Windows development release.
12. Reporting remaining limitations transparently.

**Do not stop after implementing one feature. Continue through the complete roadmap and verification process, unless a genuine blocker or required design decision prevents further work.**

---

# 1. CURRENT PROJECT CONTEXT

Application name: Naqla  
Arabic name: نقلة  
Platform: Windows 10 and Windows 11  
Primary technology: Existing C#/.NET/WPF/Win32 architecture  
Design: Approved Pixel-Modern radial wheel  
Operation: Local and offline

The application is a Windows background utility.

Primary interaction:

Drag file → Press Shift → Radial wheel → Choose action → Release.

Additional behavior:

- Ctrl switches between Convert and Tools.
- Escape cancels.
- The wheel is displayed near the cursor.
- The center shows the file type and file size.
- The bottom segmented selector displays تحويل | أدوات.
- Outputs are saved beside their original files.
- The application runs from the system tray.

The current project has previously included work on:

- Native drag interaction.
- Radial wheel rendering.
- Image conversion.
- Smart Rename.
- Target-Size Image Optimizer.
- PDF Toolkit.
- Shared processing services.
- Development installers.
- Automated tests.

Other features may be implemented, partially implemented, or missing.

Do not assume completion from previous descriptions.

Inspect the actual repository and verify the code, executables, integrations, and tests.

---

# 2. ABSOLUTE RULES

These requirements are non-negotiable.

### Preserve the existing application

- Keep the current approved radial wheel.
- Keep the Pixel-Modern visual identity.
- Keep connected radial segments.
- Keep existing colors unless refinements are justified.
- Preserve Drag + Shift.
- Preserve Ctrl mode switching.
- Preserve Escape cancellation.
- Preserve system tray operation.
- Preserve localization support.
- Preserve existing working features.

### Functional correctness

- Never substitute renaming extensions for actual conversions.
- Never display success before validating the output.
- Never create fake output files.
- Never use hardcoded sample inputs in production handlers.
- Never hide missing dependencies.
- Never silently skip failed files.
- Never claim tests passed if they were not executed.
- Never use mocked results as evidence of real conversion success.

### Privacy and file safety

- Processing must be fully offline.
- No cloud APIs.
- No telemetry.
- No advertisements.
- No user accounts.
- No unrequested file uploads.
- Never overwrite original content by default.
- Preserve source files unless the user explicitly confirms an in-place rename.
- Clean temporary files safely.
- Handle Unicode and Arabic file paths.
- Treat all input files as untrusted.

### UI

- No unnecessary dashboard.
- No permanently open main window.
- Avoid excessive decorations or animations.
- Use compact floating editors when needed.
- All buttons and controls must work.
- No decorative fake buttons.
- No dead menu options.
- No inaccessible or clipped controls.

---

# 3. FIRST TASK — COMPLETE PROJECT AUDIT

Before modifying functionality, inspect:

- All solution/project files.
- File detection.
- Win32 input hooks.
- OLE drag/drop handling.
- Radial wheel.
- Action registration.
- Conversion backends.
- Existing tools.
- PDF processing.
- FFmpeg integration.
- OCR implementation.
- Local AI integration.
- Archive handling.
- Plugin infrastructure.
- Installer.
- Automated tests.
- Documentation.
- PROGRESS.md.

Create:

`docs/FINAL-FEATURE-AUDIT.md`

Include a complete feature matrix.

For every action record:

- Feature name.
- Supported input types.
- Supported outputs.
- Backend implementation.
- Wheel integration.
- Functional state.
- Automated test state.
- Real processing test state.
- Manual Windows test state.
- Known limitations.
- Required fixes.

Use the following statuses:

- VERIFIED — implemented and independently validated.
- IMPLEMENTED / UNVERIFIED — exists but lacks sufficient validation.
- PARTIAL — incomplete functionality.
- BROKEN — fails a real processing test.
- MISSING — not implemented.
- UNSUPPORTED — technically unsupported with documented reason.

No feature should be marked VERIFIED based exclusively on source inspection.

Do not delete or weaken existing tests to achieve green results.

---

# 4. COMPLETE IMAGE CONVERSION SUPPORT

Audit, implement, and validate conversions involving:

### Input formats

PNG, JPG, JPEG, WEBP, BMP, TIFF, AVIF, HEIC and other formats genuinely supported by the available backend.

### Output formats

- PNG
- JPG/JPEG
- PDF
- AVIF
- TIFF
- HEIC

For every supported input/output pair:

1. Load a real file.
2. Decode it.
3. Perform actual encoding.
4. Save a new output.
5. Reopen the result.
6. Confirm actual output format.
7. Verify dimensions.
8. Verify transparency behavior.
9. Verify orientation where relevant.
10. Confirm original hash remains unchanged.

Test transparency to JPG using an appropriate matte.

Test EXIF orientation, ICC profiles, large images, corrupted images, unusual aspect ratios, and filenames containing Arabic characters.

Do not mark a format supported merely because its extension is listed in the wheel.

When a required codec is unavailable, implement or package an appropriate licensed local backend, or clearly mark the operation unsupported.

Avoid unnecessary quality loss.

---

# 5. COMPLETE IMAGE TOOLS

Implement and thoroughly test:

## 5.1 Compress

- Real image compression.
- Adjustable quality where appropriate.
- Preserve dimensions by default.
- Report original and resulting sizes.
- Do not guarantee size reduction for every already-optimized image.

## 5.2 Crop

- Real crop editor.
- Actual image preview.
- Resizable crop selection.
- Freeform mode.
- 1:1, 4:3, 16:9, 9:16.
- Confirm and Cancel.
- Accurate coordinate mapping at different zoom levels and DPI settings.
- Save a new valid output.

## 5.3 Rotate

- 90° clockwise.
- 90° counterclockwise.
- 180°.
- Correct width/height changes.
- Preserve pixels and orientation appropriately.

## 5.4 Remove Metadata

- Remove supported nonessential EXIF/XMP/IPTC and other metadata.
- Verify removal by independently rereading the output.
- Preserve correct image appearance.

## 5.5 Merge Images to PDF

- One image to one-page PDF.
- Multiple images to one PDF.
- Correct image order.
- Preserve aspect ratio.
- Sensible page sizing.
- Validate page count.

## 5.6 Target-Size Image Optimizer

- 100KB, 200KB, 500KB, 1MB presets.
- Custom size.
- Preserve dimensions by default.
- Optional resizing.
- Iterative quality optimization.
- Support compatible formats.
- Validate final size.
- Explain impossible targets.
- Support batch processing.

## 5.7 Remove Background

- Use an actual local segmentation model.
- No online AI calls.
- Optional offline model pack.
- Preserve original.
- Produce PNG with real transparency.
- Preview before saving.
- Verify alpha channel.
- Test CPU-only operation.

## 5.8 OCR

- Extract text from images.
- Arabic and English recognition.
- Real local OCR backend.
- Copy text.
- Save TXT.
- Handle mixed-language text when supported.
- Report empty/unrecognizable content honestly.

Every image tool must have a real execution handler and real output validation.

---

# 6. COMPLETE VIDEO CONVERSIONS

Support actual conversions to:

- MP4
- MOV
- WebM
- GIF
- MP3
- M4A

Use the existing offline FFmpeg backend or another suitable local implementation.

Validate:

- Container.
- Video codec.
- Audio codec.
- Duration.
- Frame dimensions.
- Frame rate.
- Output decoding.
- Metadata behavior.

Support:

- Video to video.
- Video to GIF.
- Extract audio to MP3.
- Extract audio to M4A.

Test MP4, MOV, MKV, WebM, and other genuinely supported inputs.

Handle videos without audio correctly.

Do not offer audio extraction if no audio stream exists.

Ensure conversions run asynchronously and display accurate processing status.

---

# 7. COMPLETE VIDEO TOOLS

Implement and validate:

## Compress Video

- Real bitrate/quality adjustment.
- Sensible presets.
- Correct codec settings.
- Valid output container.

## Trim Video

- Compact timeline editor.
- Start/end selection.
- Accurate output duration.
- Correct audio/video synchronization.
- Fast processing when stream copy is valid.
- Re-encoding when necessary for accurate cuts.

## Rotate Video

- 90°, 180°, and 270°.
- Correct visual orientation.
- Correct resulting dimensions or rotation metadata.

## Remove Metadata

- Remove supported container metadata.
- Preserve playable streams.

## Extract Frames

Support:

- Single timestamp.
- Every N seconds.
- Fixed frame count.
- Selected time range.
- PNG/JPG output.
- Dedicated output folder.
- Correct timestamp handling.
- Orientation preservation.
- Predictable numbering.
- Cancellation.

Test a real 30-second video, extract frames every five seconds, inspect exported images, and verify timestamp correspondence.

## Generate Subtitles

- Offline speech recognition.
- Arabic and English.
- Mixed-language support where model permits.
- Optional local model pack.
- Real timestamped transcription.
- SRT output.
- WebVTT output.
- Compact subtitle editor.
- Correct RTL text.
- Model load/unload.
- CPU-only support.
- Cancellation.
- No network communication.

Validate subtitles using a real spoken test video with a known reference transcript.

Do not confuse transcription with translation.

---

# 8. COMPLETE AUDIO CONVERSIONS

Support:

- MP3
- M4A
- WAV
- FLAC
- AIFF
- OPUS

Test real source/output combinations using appropriate codecs and containers.

Inspect results using FFprobe or equivalent.

Verify:

- File validity.
- Duration.
- Sample rate.
- Channel count.
- Codec.
- Output playback/decode.

Preserve originals.

Avoid repeated unnecessary lossy encoding.

---

# 9. COMPLETE AUDIO TOOLS

Implement and test:

### Compress Audio

Use proper encoding settings and quality presets.

### Trim Audio

- Compact timeline.
- Start and end selection.
- Valid duration.
- Smooth cancellation.

### Remove Metadata

Strip supported audio/container metadata.

### Loudness Normalization

- Speech preset.
- Podcast preset.
- Custom preset.
- Prefer measured two-pass EBU R128 / FFmpeg loudnorm.
- Respect true-peak ceiling.
- Verify measured output loudness.

### Trim Silence

- Remove leading silence.
- Remove trailing silence.
- Adjustable threshold.
- Avoid removing intentional internal pauses by default.

### Channel Conversion

- Stereo to mono.
- Mono to dual-channel stereo.
- Correct channel mixing.

Use independent audio measurements in tests.

Do not simply confirm that FFmpeg exited with code zero.

---

# 10. COMPLETE PDF CONVERSIONS

Implement and validate:

PDF → PNG  
PDF → JPG  
PDF → TXT  
PDF → DOCX  
PDF → RTF

Use genuine local processing.

For PDF → images:

- Render actual pages.
- Support multi-page export.
- Choose resolution.
- Use collision-safe folders.
- Validate image count and dimensions.

For PDF → TXT:

- Extract native text.
- Recognize scanned-page limitations.
- Offer local OCR where implemented.

For PDF → DOCX/RTF:

- Implement best practical offline conversion.
- Validate resulting documents.
- Document layout fidelity limits honestly.

Do not claim perfect layout reconstruction for arbitrary PDFs.

---

# 11. COMPLETE DOCUMENT CONVERSIONS

Audit and implement supported conversions among:

- DOCX
- PDF
- TXT
- RTF
- HTML
- ODT

Use licensed local backends.

Test:

- English documents.
- Arabic documents.
- Tables.
- Images.
- Formatting.
- Unicode filenames.
- Corrupted files.
- Multi-page documents.

If a conversion necessarily loses formatting, document and communicate the limitation.

---

# 12. PDF TOOLKIT — COMPLETE FUNCTIONAL TESTING

Verify every existing PDF tool:

1. Reorder Pages.
2. Extract Pages.
3. Delete Pages.
4. Merge PDFs.
5. Add Page Numbers.
6. Add Watermark.
7. Rotate Pages.

Do not assume these work merely because the previous 86 automated test groups passed.

Use real PDFs with varied page structures.

Verify:

- Actual page order.
- Correct page count.
- Preserved visible content.
- Valid PDF output.
- No unintended rasterization.
- Correct watermark location.
- Correct watermark opacity.
- Arabic watermark rendering.
- Page numbering position.
- Rotation handling.
- Encrypted-file refusal when unsupported.
- Original hash preservation.

Test PDF files containing different page sizes and rotations.

---

# 13. PDF EDITOR — REQUIRED VISUAL REFINEMENT

The current PDF Toolkit editor has a functional three-column design.

Preserve this structure:

- Left: page thumbnails.
- Center: document preview.
- Right: contextual settings.

The existing appearance is close to the desired style, but needs further refinement.

## 13.1 Right settings panel

Improve:

- Arabic RTL alignment.
- Panel width.
- Consistent padding.
- Clear section hierarchy.
- Label readability.
- Field heights.
- Input spacing.
- Dropdown alignment.
- Slider presentation.
- Checkbox alignment.
- Text contrast.

Prevent clipping or crowded labels.

## 13.2 Scrollbars

Replace thick, ordinary scrollbars with compact Pixel-Modern scrollbars.

They should be:

- Thin.
- Low contrast.
- Easy to grab.
- Visually consistent.
- Clearly visible while interacting.

Do not sacrifice usability for minimalism.

## 13.3 Unify component design

Every control must follow the same visual system.

- Textboxes.
- Dropdowns.
- Sliders.
- Checkboxes.
- Radio buttons.
- Tabs.
- Tooltips.
- Scrollbars.
- Buttons.

Use:

- Graphite backgrounds.
- Warm ivory text.
- Amber active states.
- Thin pixel-inspired borders.
- Subtle shadows.
- Minimal decorative details.

Maintain the existing semantic button colors where currently approved:

- Hide: blue.
- Cancel: coral.
- Details: purple.
- Results: mint.
- Confirm/Save: amber.

Colors should remain readable and restrained.

## 13.4 Main document preview

Add or improve:

- Fit to Page.
- Fit to Width.
- Zoom In.
- Zoom Out.
- Zoom percentage.
- Centered page.
- Smooth panning where appropriate.
- Reliable thumbnail selection.
- Correct page navigation.

Avoid excessive blank space or unwanted clipping.

## 13.5 Watermark editor

Ensure:

- Text/image watermark is visible in preview.
- Size changes update the preview.
- Rotation changes update the preview.
- Opacity changes update the preview.
- Positioning is intuitive.
- Export uses the actual selected opacity.
- No artificial opacity changes in saved PDFs.

## 13.6 Bottom toolbar

Refine:

- Save Copy.
- Preview.
- Cancel.
- Processing/status message.

Make the primary action obvious.

Avoid crowded controls.

Maintain consistent spacing.

## 13.7 Responsive behavior

Test at:

- 100% Windows scaling.
- 125%.
- 150%.
- Small laptop displays.
- 1080p monitors.
- Mixed-DPI setups where available.

All controls must remain accessible.

### Design review rule

Produce updated PDF editor screenshots and an interactive preview.

If the changes are limited to spacing, visual consistency, and control polish, implement and test them.

If navigation structure or major layout behavior needs to change, provide an approval preview before committing to the new design.

---

# 14. SMART RENAME

Fully test:

- Single rename.
- Batch rename.
- Prefix.
- Suffix.
- Sequential numbering.
- Search/replace.
- Case conversion.
- Date-based names.
- Filename cleanup.
- Optional regex.
- Live Before/After preview.
- Conflict detection.
- Confirmation.
- Undo.

Smart Rename is permitted to change the actual filename after explicit user confirmation.

Do not duplicate files merely to simulate renaming.

Preserve file content.

Verify source/output hashes before and after the rename.

Test rename cycles, collisions, locked files, and Arabic filenames.

---

# 15. BATCH OPERATIONS

Test real multi-file dragging from Explorer.

Support:

- Multiple images.
- Multiple video files.
- Multiple audio files.
- Multiple PDFs.
- Compatible mixed-format inputs.
- Common contextual operations.

The wheel hub should show:

- File count.
- Total size.

Validate common operation discovery.

Test:

- Ten images.
- Fifty small images.
- Mixed supported files.
- One corrupted file among valid files.
- Cancellation.
- Partial failures.
- Files in different directories.
- Filename collisions.

Report success/failure for each file.

Do not cancel unrelated successful outputs because one file fails.

---

# 16. ARCHIVE TOOLS

Complete and verify:

### Create

- ZIP.
- 7Z.
- TAR.

### Extract

- ZIP.
- 7Z.
- TAR.

Support appropriate additional compressed TAR variants if implemented.

Validate archives using independent extraction/inspection where practical.

Test:

- Nested directories.
- Empty directories.
- Unicode filenames.
- Multi-file selection.
- Compression/extraction cancellation.
- Password-protected archives where supported.
- Corrupt archives.
- Zip Slip.
- Symbolic link traversal.
- Archive bombs.
- File count/size limits.
- Safe destination naming.

No archive may write outside its authorized extraction destination.

---

# 17. PRIVACY CLEAN

Verify metadata cleaning for supported:

- Images.
- PDFs.
- DOCX.
- XLSX.
- PPTX.
- Audio.
- Video.

Test actual EXIF, GPS, XMP, document properties, and media metadata fixtures.

After cleanup, independently re-inspect the output.

Confirm targeted metadata is removed.

Preserve visual or audible file content.

Clearly report metadata that remains unsupported or unremoved.

Do not claim Privacy Clean removes visible personal information, hidden PDF revisions, or all identifying document content unless those are explicitly processed and validated.

Preserve originals.

---

# 18. PLUGIN SYSTEM

Implement and test a safe initial plugin architecture.

Requirements:

- Versioned manifests.
- Local installation.
- Enable/disable.
- Uninstall.
- API compatibility checks.
- Contextual action registration.
- Isolated plugin host process.
- Crash recovery.
- Timeout handling.
- Cancellation.
- Package validation.
- Path traversal prevention.
- Developer documentation.
- Two working sample plugins.

Suggested examples:

1. SHA-256 File Hash.
2. SVG conversion, if a safe local renderer is available.

Do not load arbitrary community plugin binaries into Naqla's main UI process.

Do not advertise sandbox security guarantees that are not enforced by Windows.

Test plugin crashes and malicious packages without compromising the main application or unrelated files.

Do not implement an online marketplace in this phase.

---

# 19. COMPLETE WHEEL AND INTERACTION AUDIT

The wheel is Naqla's defining feature.

Test all relevant gesture transitions:

- Drag then Shift.
- Drag then Shift then Ctrl.
- Ctrl release.
- Return to Convert.
- Escape cancellation.
- Mouse release on valid sector.
- Mouse release outside wheel.
- Fast repeated activation.
- Drag from Desktop.
- Drag from Explorer.
- Different monitor DPI.
- Screen edges.
- Multiple files.
- Unsupported files.
- Explorer restarts.

Validate that every visible wheel action corresponds to a real available handler.

Ensure category-specific options are correct.

For example:

- Image files show image conversions and tools.
- Video files show video conversions and tools.
- Audio files show audio conversions and tools.
- PDF files show PDF conversions and tools.
- Archives show archive actions.
- Mixed selections show valid shared operations.

If action counts exceed the wheel's capacity, use the established secondary page/navigation design.

Do not overcrowd the wheel.

Do not redesign the existing approved wheel unnecessarily.

---

# 20. REAL END-TO-END TESTING — MANDATORY

This is the most important section of the task.

You must execute actual processing operations on real fixture files.

**Unit tests alone are not sufficient.**

Create a dedicated fixture suite containing representative files for all supported categories.

Use committed small fixtures where license and size allow, and reproducible local generators for larger fixtures.

Include:

### Images

- Transparent PNG.
- Standard JPG.
- WEBP.
- TIFF.
- AVIF/HEIC where supported.
- Large image.
- EXIF rotation.
- GPS metadata.
- Corrupted image.

### Videos

- MP4 with audio.
- MP4 without audio.
- MOV.
- WebM.
- Variable frame rate.
- Short and longer clips.
- Test speech clip.
- Silent video.

### Audio

- MP3.
- WAV.
- FLAC.
- M4A.
- OPUS.
- Quiet speech.
- Loud speech.
- Leading/trailing silence.
- Mono and stereo.

### PDFs

- Single-page PDF.
- Multi-page PDF.
- Arabic PDF.
- Scanned PDF.
- Mixed page sizes.
- Rotated pages.
- Metadata-bearing PDF.
- Corrupted PDF.
- Encrypted PDF.

### Documents

- DOCX.
- TXT.
- RTF.
- HTML.
- ODT.
- Arabic and English content.

### Archives

- ZIP.
- 7Z.
- TAR.
- Nested directories.
- Malicious archive fixtures.

For each supported operation:

1. Create or load a valid fixture.
2. Execute the real production action handler.
3. Wait for actual completion.
4. Locate the generated output.
5. Verify file signature/container.
6. Decode or parse the result.
7. Verify expected content/properties.
8. Verify output naming.
9. Verify source file protection.
10. Repeat with a failure case where relevant.

Test every advertised source-to-destination conversion pair, not just one example per category.

Generate a machine-readable results matrix and an easily readable report.

Suggested report:

`docs/END-TO-END-TEST-REPORT.md`

Each test entry must include:

- Operation.
- Input format.
- Output format.
- Backend used.
- Actual command or handler.
- Expected result.
- Actual result.
- PASS/FAIL/SKIPPED/BLOCKED.
- Duration.
- Error information.
- Output validation method.

Use no fake passing results.

If a dependency is missing, mark BLOCKED and explain how to resolve it.

If a test is skipped, document exactly why.

---

# 21. WINDOWS GUI AUTOMATION

Where a Windows desktop session and automation tools are actually available:

Test the real application through the GUI.

Automate:

- Explorer file selection.
- File dragging.
- Shift activation.
- Ctrl switching.
- Wheel hovering.
- Dropping onto actions.
- Opening compact editors.
- Changing editor settings.
- Confirming operations.
- Cancelling.
- Notifications.
- Tray behavior.

Capture screenshots at useful checkpoints.

Verify outputs through the filesystem and parsers, not screenshots alone.

If Windows desktop automation is unavailable, do not simulate success.

In that case:

1. Run all feasible backend and integration tests.
2. Build a guided manual acceptance test suite.
3. Provide exact reproduction instructions.
4. Enable privacy-safe diagnostic logging.
5. Mark GUI-dependent checks as MANUAL PENDING.

Do not claim complete end-to-end Windows GUI verification until it actually occurs.

---

# 22. AUTOMATED BUG FIXING

When a test fails:

1. Reproduce the failure.
2. Identify the responsible component.
3. Determine root cause.
4. Implement the smallest reliable fix.
5. Add a regression test.
6. Run the failing test again.
7. Run related tests.
8. Verify no previous features broke.
9. Record the fix.

Do not merely catch and suppress exceptions.

Do not delete a failing test because it exposes a bug.

Do not change expected results to match incorrect behavior.

Do not mark a failure as resolved without rerunning it.

Maintain:

`docs/BUG-FIX-LOG.md`

For each bug record:

- Bug ID.
- Description.
- Affected action.
- Reproduction.
- Root cause.
- Fix.
- Regression test.
- Verified result.

Continue the repair cycle until all feasible required tests pass or a genuine, documented blocker remains.

---

# 23. PERFORMANCE AND RELIABILITY

Naqla must stay lightweight despite its expanded feature set.

Measure:

- Idle CPU.
- Idle RAM.
- Wheel activation latency.
- Conversion startup latency.
- Memory use under batch load.
- Memory use during OCR.
- Memory use during background removal.
- Memory use during transcription.
- FFmpeg process cleanup.
- Plugin host resource usage.

Requirements:

- Lazy-load optional dependencies.
- Lazy-load AI models.
- No heavyweight model startup during login.
- Limit simultaneous FFmpeg processes.
- Limit inference concurrency.
- Avoid unbounded job queues.
- Release unmanaged resources.
- Avoid blocking the WPF dispatcher.
- Ensure Ctrl/Shift interaction remains responsive.
- Avoid interfering with normal Explorer dragging.
- Cleanly exit all child processes.

Perform repeated startup/shutdown tests.

Perform stress testing for long conversions and repeated batch jobs.

Report measured results rather than invented performance claims.

---

# 24. SECURITY AND PRIVACY AUDIT

Perform a complete security review covering:

- File path validation.
- Unsafe external process arguments.
- Unicode paths.
- Temporary files.
- Output path restrictions.
- Archive extraction.
- PDF parsing.
- Image decoder failures.
- FFmpeg invocation.
- AI model package integrity.
- Plugin execution.
- Plugin permissions.
- Local diagnostics.
- Sensitive metadata handling.
- DLL loading.
- Dependency distribution.

Requirements:

- No unrequested network access.
- No embedded private API keys.
- No sensitive file content in diagnostic logs.
- No unnecessary absolute paths in exported reports.
- No automatic execution of untrusted archive contents.
- No unrestricted native plugin execution inside the main UI process.
- No accidental deletion of source files.

Document residual risks accurately.

---

# 25. COMPLETE UI CONSISTENCY PASS

Inspect every application window and floating editor:

- Image Crop.
- Image Rotation.
- Image Optimizer.
- Rename.
- PDF Toolkit.
- Video Trim.
- Video Frame Extraction.
- Audio tools.
- Subtitle editor.
- OCR results.
- Remove Background.
- Archive dialogs.
- Privacy Clean.
- Plugin Manager.
- Progress panels.
- Result notifications.

Unify:

- Typography.
- Margins.
- Padding.
- Buttons.
- Controls.
- Iconography.
- Colors.
- Focus states.
- Keyboard behavior.
- RTL support.
- Window positioning.
- DPI scaling.

Remove:

- Inconsistent styling.
- Broken alignments.
- Text clipping.
- Excessive empty space.
- Oversized scrollbars.
- Nonworking buttons.
- Debug placeholders.
- Unnecessary labels.
- Unclear error messages.

Use the established Pixel-Modern design.

Do not redesign the brand.

---

# 26. OFFLINE TESTING

Disable network connectivity during a dedicated verification run.

Verify:

- Image conversions.
- Video conversions.
- Audio conversions.
- PDF processing.
- Document conversions.
- OCR with installed language data.
- Background removal with installed model pack.
- Subtitle generation with installed model pack.
- Archive handling.
- Privacy Clean.
- Local plugins.
- Batch operations.

All installed functionality must operate offline.

Optional model downloads may require internet during explicit installation, but must never be required during processing after installation.

Do not automatically download dependencies in the middle of a file operation.

---

# 27. PACKAGING AND INSTALLER

When implementation and automated testing are complete:

Build a new self-contained Windows development installer.

Requirements:

- Runs without developer tools.
- Starts in the system tray.
- No console window.
- No unwanted onboarding.
- Existing startup preferences respected.
- Clean uninstall.
- Safe application upgrade.
- No duplicate background instances.
- Bundled required native binaries where licensing allows.
- Optional AI model packs handled separately.
- Correct Windows application icon.
- Accurate version information.
- Dependency and license notices.

Verify installation, launch, upgrade, and removal on real Windows environments where available.

If clean-machine tests cannot be executed, mark them pending.

Do not claim a public stable release has been validated without installation testing.

---

# 28. REQUIRED DOCUMENTATION

Create or update:

- `PROGRESS.md`
- `docs/FINAL-FEATURE-AUDIT.md`
- `docs/END-TO-END-TEST-REPORT.md`
- `docs/BUG-FIX-LOG.md`
- `docs/PERFORMANCE-REPORT.md`
- `docs/SECURITY-REVIEW.md`
- `docs/MANUAL-WINDOWS-TESTS.md`
- `docs/KNOWN-LIMITATIONS.md`
- `docs/DEPENDENCIES.md`
- `README.md`
- `README.ar.md`
- `THIRD_PARTY_NOTICES.md`

Include screenshots of the refined PDF editor and other relevant interfaces.

Generate a final machine-readable test report as well.

Keep documentation aligned with actual behavior.

---

# 29. REQUIRED EXECUTION PLAN

Execute in the following order.

## Stage 1 — Audit

Inspect code and establish the feature matrix.

Do not skip this stage.

## Stage 2 — Core conversions

Repair and complete:

- Images.
- Video.
- Audio.
- PDF.
- Documents.

Run real output validation.

## Stage 3 — Contextual tools

Repair and complete:

- Image tools.
- Video tools.
- Audio tools.
- PDF tools.
- General file tools.
- Archive tools.

## Stage 4 — Advanced features

Complete:

- Smart Rename.
- Target-Size Optimizer.
- Batch Actions.
- OCR.
- Remove Background.
- Video Subtitles.
- Frame Extraction.
- Audio Normalization.
- Privacy Clean.
- Plugin System.

Only rework existing functionality when necessary.

## Stage 5 — Visual refinement

Refine PDF Toolkit and unify contextual editors.

Keep the existing wheel unchanged.

## Stage 6 — End-to-end verification

Execute the full source-to-destination conversion matrix and tool test suite.

Fix failures.

Repeat until the verified test set passes.

## Stage 7 — Windows validation

Test Explorer, desktop drag/drop, tray behavior, keyboard gestures, and dialogs using real Windows interaction when available.

Otherwise produce exact manual test instructions and leave those checks pending.

## Stage 8 — Stabilization

Fix regressions, memory leaks, crashes, performance issues, and packaging problems.

## Stage 9 — Development release

Build and validate the development installer.

Update all documentation.

Provide a complete status summary.

---

# 30. AUTONOMY AND APPROVAL RULES

You are authorized to:

- Inspect the repository.
- Refactor broken components.
- Implement missing features.
- Fix bugs.
- Add tests.
- Generate test fixtures.
- Improve UI spacing and consistency.
- Execute local tests.
- Produce development builds.
- Update documentation.

Do not pause after every completed feature.

Continue through the planned stages.

However, request user approval before:

- A major redesign of the approved radial wheel.
- A substantial change to the Drag + Shift workflow.
- Destructive changes to real user files.
- Installing system-wide services or drivers.
- Introducing cloud processing.
- Publishing code or releases to a remote GitHub repository.
- Performing other externally impactful changes outside the local development environment.

Do not publish publicly without approval.

If you encounter an important architectural limitation, explain it and provide viable alternatives.

---

# 31. FINAL VERIFICATION STANDARD

The final development build must demonstrate:

### Conversions

All advertised, backend-supported image, video, audio, PDF, and document conversions produce independently validated outputs.

### Tools

Every visible contextual tool executes a real operation or is clearly unavailable with an explanation.

### Dragging

Actual supported files dragged from Explorer and Desktop activate the correct wheel and actions where the native integration is verified.

### Design

The radial wheel remains polished and consistent.

The PDF editor is refined and visually coherent.

### Offline processing

All installed conversions and tools work without network access.

### Safety

Original content is preserved except for explicitly confirmed rename operations.

### Testing

Every required test has an explicit result.

No unexecuted test is represented as passing.

### Performance

Naqla remains responsive and reasonably lightweight during normal use.

### Packaging

A verifiable development installer is produced.

---

# 32. FINAL REPORT FORMAT

At the end of development, provide:

## A. Feature completion summary

A table:

Feature | Implementation | Automated Tests | Real Output Test | Windows GUI Test | Status

## B. Test summary

- Total executed tests.
- Passed.
- Failed.
- Blocked.
- Skipped.
- Remaining manual tests.

## C. Fixed bugs

List major repaired issues and their regression tests.

## D. Remaining limitations

Be explicit.

Do not hide unsupported codecs, missing models, untested hardware configurations, or unresolved failures.

## E. Performance measurements

Report actual memory, CPU, wheel latency, and workload measurements.

## F. Deliverables

Provide exact local paths for:

- Development installer.
- Test reports.
- Updated documentation.
- UI screenshots.
- Diagnostic logs where appropriate.

## G. Release recommendation

Clearly classify the build as:

- NOT READY.
- DEVELOPMENT READY.
- RELEASE CANDIDATE.
- PUBLIC RELEASE READY.

Do not classify it as PUBLIC RELEASE READY unless all required release gates have genuinely passed.

---

# FINAL INSTRUCTION

Naqla is no longer just a radial wheel demonstration.

It must become a complete, reliable, offline Windows productivity utility.

Your responsibility is to ensure that every advertised action actually works.

Do not merely implement features and assume they work.

**Implement → Execute → Validate Output → Fix → Retest → Document.**

Repeat until the application is stable and all feasible acceptance criteria pass.

Keep Naqla lightweight, elegant, pixel-modern, secure, and easy to use.

Keep its defining interaction:

**Drag → Shift → Choose → Drop.**

Preserve the existing product, complete the missing functionality, refine the interface, repair failures, and deliver a thoroughly tested development release.

Do the engineering work, not just the planning.