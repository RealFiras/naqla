# Local conversion support — 0.6.0-targetsize.1

Every destination shown in the native wheel is connected to a real local operation.
The current source format (including the JPEG/JPG alias) is omitted. The capability
contract filters unavailable operations before drawing the wheel.

Select up to 200 real local files. The wheel intersects per-source operations and
omits a destination if it is already a current source format for any member. Mixed
categories can share tools or targets only when every backend supports the action.
Invalid/unsupported members are reported; no subset is silently selected. During jobs,
content corruption and destination errors are per-file failures, with other jobs kept.
The hub uses **N ملف** and total size; its geometry and design are retained.

| Input | Available destinations |
| --- | --- |
| PNG, JPG/JPEG, WEBP, AVIF, HEIC, TIFF, BMP | JPG, PNG, WEBP, AVIF, TIFF, HEIC, PDF |
| MP4, MOV, MKV, WEBM, AVI | MP4, MOV, M4A, MP3, GIF, WebM |
| MP3, WAV, FLAC, M4A, AIFF, OPUS | MP3, M4A, WAV, FLAC, AIFF, OPUS |
| PDF | PNG, JPG, TXT, DOCX, RTF |

Image tools: **ضغط** (recompress to a smaller copy), **قص** (selected pixel rectangle),
**تدوير** (90° clockwise), **بدون بيانات** (strip metadata after applying orientation),
**دمج PDF** (selected images to one PDF). These save separately. **تسمية** opens the confirmed Smart Rename editor for
all supported file categories; see [SMART-RENAME.md](SMART-RENAME.md). Rename changes
actual filenames and has its own confirmation/rollback/recovery contract.
**حجم مستهدف** has its own full-resolution measured preview and per-image strict byte
cap; [TARGET-SIZE.md](TARGET-SIZE.md) explains encoders, lossless recipes, quality search,
downsize, cancellation and actual desktop acceptance. It does not replace basic Compress.
Compression reports that no smaller result was found rather than publishing a larger
copy. PNG compression is lossless; TIFF uses ZIP; BMP compression creates a PNG copy.

Crop: drag one image → Shift → Ctrl → قص → release. After OLE Drop finishes, a separate
editor displays the oriented image. Drag a marquee in either direction, or enter X, Y,
Width and Height in original-image pixels. Save copy publishes beside the source;
Cancel/Escape closes without output. Reset selects the entire image. No center crop is
silently applied. The wheel renderer and activation behavior are unchanged.
For a crop batch the first image supplies the preview; its oriented pixel rectangle
is applied to each source. An out-of-bounds source fails explicitly without clamping.

Merge PDF: select 2–200 fully local images and drag them together. Shift → Ctrl → دمج PDF
writes one image per A4 page in the order provided by Explorer's
actual FileDrop list. The image fits without cropping or resampling; transparent
areas become white and PDF embeds JPEG at quality 92. Portrait/landscape is chosen
per image. There is no reorder dialog: use Explorer sorting/selection to influence its
list and check the result. The file is saved beside the first received image with a
` merged` suffix and collision numbering. If sources are in different directories,
the first-source rule still applies. The center shows first-source type and total size.
Convert → PDF writes **one separate PDF per image**. Other common conversions/tools
run separately per source. Repeated identical paths, folders and selections exceeding
200 files are rejected; duplicate basenames in different directories remain distinct.

Tap Ctrl once while the wheel is open to select Tools; tap again to return to Convert.
Release does not undo a tap, auto-repeat does not toggle, and each drag resets to Convert.
The requested mode is retained if Ctrl arrives before the actual OLE file metadata.

Video tools: compress, rotate 90° clockwise, mute, clean metadata. Mute copies the
primary video without re-encoding and removes audio. Metadata cleaning copies existing
streams in the original container and removes user tags/chapters; technical codec tags,
cover images and attachments may remain, so it is not a complete document-sanitizing tool.
Rotation/compression preserve MP4/MOV/WebM containers; MKV/AVI export a new MP4 copy.
Compression uses H.264 CRF 30 or VP9 CRF 40, and reduced audio bitrate; publish only if
smaller. Audio tools compress (MP3/AAC 96 kb/s, Opus 80 kb/s; WAV/AIFF/FLAC to FLAC)
and clean tags in a separate same-format copy. PDF Tools exposes the real text extractor.

Important practical limits:

- HEIC output uses a real bundled libheif 1.23.6 / x265 4.3 encoder in a separate,
  below-normal-priority process. Native worker counts are bounded. HEVC brands,
  alpha, tiny/odd dimensions and Arabic paths are tested, and output is independently
  decoded by Magick.NET before publication. No Store codec or MSYS2 install is needed.
- Raster images: one frame, up to 40 megapixels. Multi-frame TIFF is rejected rather
  than silently losing frames. The Q8 pipeline uses 8-bit color channels; HDR/high-bit
  depth is not preserved. Transparent JPG/PDF backgrounds become white. JPEG
  quality is 92; compressed copies use 78. PDF image export embeds a raster image.
- PDF: up to 200 pages, each at most 40 megapixels at 144 DPI. PNG/JPG exports **all**
  pages; a multi-page document gets a new folder beside the PDF. Text/DOCX/RTF extracts
  text from every page; it does not reproduce layout or perform OCR. Image-only PDF
  text conversion fails with an explanation. Password-protected PDFs have no password
  entry workflow yet. DOCX/TXT/RTF input is detected, but conversion is not exposed yet.
- Video MP4/MOV uses H.264/AAC, WebM uses VP9/Opus. GIF retains the whole duration,
  at 12 FPS and at most 640 px wide. A two-pass palette avoids buffering the full
  video in RAM; the resulting GIF can still be large. Video-to-audio requires an
  actual audio track. Only the first video and first audio stream are converted;
  subtitles, alternate tracks and chapters are not retained. Metadata is removed.
- MP3: 192 kb/s; M4A: AAC 192 kb/s; Opus: 128 kb/s; WAV/AIFF: 16-bit PCM; FLAC lossless
  relative to decoded input samples. Lossy conversions cannot restore source quality.
- One engine job runs at a time through the shared async queue, including across
  submissions. A compact panel reports finished/total jobs, success/failure and Cancel;
  it does not invent byte/frame progress. Hide/reopen from the tray, inspect failures,
  and choose from all output directories. A cancellation preserves completed outputs
  and marks unstarted jobs cancelled, without showing them as 100% processed.
  Right-click tray → **Cancel current conversion**.
  Exit requests cancellation and waits for cleanup. Image/PDF native calls finish the
  current bounded operation before observing cancellation; media processes are killed
  on cancellation, including HEIC. Conversions never move, overwrite or delete source files. Explicit confirmed
  Smart Rename changes names of the original objects and never writes their contents.
- OCR, archive creation/extraction and local AI background removal are not in this
  phase's build. They follow the user's real Explorer batch acceptance gate and are
  not exposed by label-only actions. No new dependency or startup service is added
  for the batch queue. Image native limits/initialization now occur on first processing,
  and backend availability is cached for fast wheel capability checks.

Outputs use random private temporary names beside the source. Successful output is
published without replacement; collisions receive a numeric suffix. Images are
decoded again, PDFs parsed/rendered, and media probed and **fully decoded** before
publication. A media duration mismatch also fails verification. Cancellation/failure
cleans up only temporary output owned by that operation. Forced termination or power
loss during conversion may leave `.naqla-*` temporary output; source files remain untouched.
Rename stages use **Naqla-Rename-Recovery-…**, contain original data and must never be
cleaned as disposable output; see the separate recovery guide.

Engines are bundled and run offline, with no telemetry or cloud calls. The app never
downloads a codec during use. Media input protocols are restricted to local file/pipe,
and input demuxers are selected from the known extension allowlist. FFmpeg runs with
two worker threads at below-normal process priority; it is never started while idle.
Codec errors are sanitized in logs: no raw stderr, source path or file contents.

Primary implementation references / third-party notices:

- [Magick.NET](https://github.com/dlemstra/Magick.NET), Apache-2.0; pinned 15.0.0.
- [libheif](https://github.com/strukturag/libheif), LGPL-3.0, and its separate HEIC CLI
  with x265 GPL-2.0-or-later. Unmodified native executables/DLLs come from
  [official MSYS2 UCRT64 packages](https://packages.msys2.org/packages/mingw-w64-ucrt-x86_64-libheif).
  `scripts/heif-engine.lock.json` pins 33 archives and 34 required native file hashes.
  The bundled `heif-engine/packages.lock.json` includes exact versions and corresponding
  source archive URLs; `NOTICE.txt` and `licenses/` retain component notices.
  `scripts/discover-heif-engine.cjs` is a developer-only manifest refresh tool; normal
  builds use `fetch-heif-engine.ps1` and the locked manifest. These engines are never
  loaded during idle tray operation.
- [FFmpeg](https://ffmpeg.org/documentation.html), unmodified separate GPLv3 executables;
  [Gyan 9.0.2 essentials build](https://www.gyan.dev/ffmpeg/builds/), hash-pinned download.
  See bundled `media-engine/LICENSE` and `NOTICE.txt` for corresponding sources/build recipe.
- [PDFtoImage](https://github.com/sungaila/PDFtoImage), MIT; pinned 5.4.0, PDFium/Skia dependencies.
- [PdfPig](https://github.com/UglyToad/PdfPig), Apache-2.0; pinned 0.1.16.

Automated fixtures test actual encoded pixels, audio samples, video frames, PDF pages,
Arabic filesystem names, collisions and source hashes. They do not simulate Explorer
drags. Test multi-image OLE delivery and the crop editor on your desktop
using [TESTING.html](TESTING.html).
