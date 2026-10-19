# Target-Size Image Optimizer — 0.6.0-targetsize.1

Drag actual local images → Shift → tap Ctrl → **حجم مستهدف** → drop.
The existing wheel geometry, palettes, Shift/Ctrl behavior and pixel button styles are
retained. Image Tools has six sectors for one image, seven for a selection including
Merge PDF. The settings dialog opens only after the real Drop, never on startup.

## Behavior

- Presets: **100 KB, 200 KB, 500 KB, 1 MB**, plus custom KB/MB. Here KB = 1024 bytes,
  MB = 1,048,576 bytes. Decimal custom sizes are floored to a strict byte cap; Arabic
  digits and the Arabic decimal separator are accepted. Supported range: 1 byte–1 GB.
- Preserve the oriented image dimensions by default. Optional width/height bounds
  preserve aspect ratio and only downsize. There is no automatic downsize if a target
  cannot be met; the user must enable it or choose another format/limit.
- Output formats are based on the actual bundled encoders: PNG, JPG, WebP, AVIF, TIFF,
  HEIC. Missing HEIC is omitted. The source format defaults selected when available;
  BMP defaults to PNG. Existing basic Compress remains separate.
- **قياس الناتج** performs actual full-image iterative encoding and decodes the selected
  result; it is not a guessed ratio or a thumbnail-size extrapolation. It reports current
  bytes, target bytes, measured result bytes/dimensions, encoder quality and attempts.
  No user output is published during measurement. Editing any setting invalidates it.
- JPG/WebP/AVIF/HEIC test integer encoder qualities **100 down to 1**, stopping at the
  first measured result within the cap. Every higher quality has therefore been tested;
  this avoids assuming byte sizes are monotonic. This maximizes that encoder's tested
  quality parameter, not a universal perceptual score across codecs. The UI reports the
  best measured size when none fits. Container overhead can make small targets impossible.
- PNG compares three lossless compression recipes; TIFF compares ZIP/LZW. They do not
  secretly quantize colors or invent quality values. Pixel equality is checked against
  the normalized, oriented/resized 8-bit image. Another format or explicit downsize may
  be required. This is not an exhaustive search over all possible lossless encoders.
- Color is normalized to sRGB before removing profiles/metadata. EXIF orientation is
  applied before dimension decisions. PNG/WebP/AVIF/TIFF/HEIC retain transparency;
  JPG uses a white background, explained in the dialog. This uses the existing Q8
  pipeline: high-bit-depth/HDR and multiple frames/pages are not preserved or supported.

**حفظ نسخة** re-encodes and validates the actual final file, using the same settings.
Preview bytes can differ if the source or encoder changes. Success requires a nonempty
decodable file with the right format, dimensions/orientation/alpha and final byte count
**≤ the requested cap**. A failed target publishes nothing and reports failure, not success.
A successful file is named `original optimized.ext` beside its source; existing files
or directories receive collision numbering. Source bytes are never changed. A cap above
the original size can produce a larger file; actual sizes are shown without claiming
compression savings.

## Batch and cancellation

One settings set applies independently to every selected image, with the limit per file.
The preview is explicitly one numbered source, normally the first. If early images
cannot be decoded, a later readable image supplies the preview; **all** original entries
remain in the batch and unreadable ones become reported failures. If none can be decoded,
there is no usable preview and the request stops. Even a failed measured target allows
an explicitly labeled batch attempt, because another source may fit. Single-file Save
remains disabled for an unmet target.

Preview and final jobs share the existing sequential queue. No native image engine is
loaded while idle and no new service or dependency is added. Up to 200 images, 40 MP each,
with existing native thread/memory limits. HEIC's local process is cancellable; native
Magick operations observe cancellation between bounded encode/decode calls. A single
large AVIF call can delay completion of cleanup. At most 100 lossy trials per image can
be expensive. No fake time/byte percentage is shown.

Stop measurement, Cancel, Escape or tray Exit requests cancellation. Closing the dialog
awaits measurement cleanup before the app accepts another operation/exits. Hiding batch
progress keeps work running; cancelling retains already published successes and removes
only owned candidates. Originals are read-locked during encoding. Network drives and
offline/recall cloud placeholders are refused rather than deliberately hydrated; use
fully available local files. No cloud API, telemetry or automatic codec/model download.

## Real Windows acceptance — not yet claimed

1. Check tray version **0.7.0-pdfpreview.1** (optimizer behavior unchanged), diagnostics ON/available, and mark a test.
2. Use disposable local PNG/JPGs with Arabic filenames. Drag one → Shift → Ctrl →
   حجم مستهدف. Confirm current dimensions/bytes and all four presets/custom KB/MB.
3. Choose JPG, 100 KB, no resize. Measure: actual result ≤102,400 bytes, same displayed
   dimensions, white transparency. Save; open the final file and check **actual file
   size** and dimensions, not Explorer's rounded size-on-disk display.
4. Repeat the other presets and a decimal custom size. Compare preview and final details.
   An already-small source need not shrink; the result must still obey the cap.
5. Use PNG and the custom target `0.0009765625 KB` (=1 byte). Measure: red failure,
   measured size above target, no file and single-file Save disabled. Increase the target,
   change format or explicitly enable downsize, remeasure and verify the new dimensions.
6. Test PNG/TIFF transparency and a small/odd HEIC/AVIF/WebP. If Windows Photos lacks a
   viewer, convert the output back to PNG locally. Test EXIF rotation: preserve displayed
   oriented dimensions, then explicit 300×300 bounds without stretching or upscaling.
7. Select several images and repeat. Check the numbered preview, per-file cap, final
   details, partial failures, original hashes and outputs beside each source. A corrupt
   first image must not silently disappear. Mix sizes/formats and separate source folders.
8. Repeat into occupied names (including a directory) and check collision numbering.
   Cancel measurement midway; then cancel/Exit a long batch after one success. Keep
   completed results and no partial `.naqla-*` output. Check hide/reopen/tray/singleton.
9. Check new settings/tool labels at actual 100%/125%/150%, screen edges/multiple monitors,
   keyboard Tab/Space, Ctrl return, Escape and ordinary dragging without Shift.
10. Perform a deliberately disconnected-network test if needed; no such test is inferred
    merely from the application containing no network client.

Return a cropped settings/preview/results screenshot, selected format/cap/resize/count,
the attempt marker/time, OS/DPI and its `session-*.jsonl`. Hide private names in screenshots.
Relevant new events: OptimizationPreviewStarted/Measured/Failed/Cancelled, typed TargetSize,
TargetBytes/ResultBytes, accepted Drop and BatchFinished. Logs have no paths or image content.

### Optional genuine test sources and independent disk verifier

From the repo run the commands below. Installed packages also include these two scripts
in `testing/`; pass an explicit new `-Destination` when running the installed copy.
Windows PowerShell/System.Drawing suffice, without an SDK or development tools.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/prepare-target-size-desktop-test.ps1
# Open the printed folder. Use images/ for 3 genuine transparent PNGs.
# Perform an actual Explorer batch to JPG, 100 KB, no resize, then:
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-target-size-desktop-test.ps1 -Directory "<printed folder>"
```

`partial-failure/` has one valid PNG plus `00-corrupt.png`: expect one success/one failure;
verify with `-Group partial-failure`. `unattainable/` is a valid image for the 1-byte
scenario: expect no output; verify with `-Group unattainable -TargetBytes 1 -ExpectNoOutput`.
The helper uses System.Drawing to independently decode JPG/PNG/TIFF, compares original
hashes and exact cap/dimensions/matte. Other formats use the native app/automated decoders.
Use fresh fixtures before the first run; the simple verifier targets the first `optimized`
name, not numbered repetitions. It always records `ActualExplorerDragVerified:false`:
disk evidence cannot establish a physical Explorer gesture or GUI approval.

## Feature gate

Automated acceptance and native renderer previews are separate from actual desktop input
and visual approval. The subsequent explicit Phase 3 request authorizes the PDF increment;
it does not establish Feature 2 desktop acceptance. The PDF editor design is a draft
awaiting approval; Feature 4 remains gated on Phase 3 validation.
See [FEATURE-AUDIT.md](FEATURE-AUDIT.md). The source repository also records executed
evidence in `docs/VERIFICATION.md`.

Primary references: [Magick.NET](https://github.com/dlemstra/Magick.NET),
[runtime encoder discovery](https://github.com/dlemstra/Magick.NET/discussions/727),
[PNG compression settings](https://imagemagick.org/script/defines.php),
[quality parameters](https://imagemagick.org/script/command-line-options.php#quality).
