# Stabilization bug log

Fresh evidence lives under `artifacts/`; retained failing runs are not erased.
The final test report records which corrected reruns passed.

| ID | Reproduction / cause | Fix / regression |
|---|---|---|
| B01 | Extra contextual actions exceeded one wheel; no old second page existed | Explicit preview approved; Tools-only six-sector paging/dots/Tab/Shift+Tab; ContextPaging |
| B02 | Silent video advertised audio extraction based only on extension | Real asynchronous ffprobe capability cache; no decoder in wheel render; media failures/matrix |
| B03 | Tesseract/Whisper legacy argv failed in Arabic directories | Trusted fixed worker/owned ASCII-relative work paths; Unicode real OCR/speech tests |
| B04 | Initial silence filter removed audible boundary samples | Retain bounded edge silence; independently decoded silence/channel tests |
| B05 | ZIP readers infer parent directories absent as explicit entries | Round-trip compares implicit and explicit directory sets; nested/empty directory tests |
| B06 | SharpCompress TAR attribute access unsupported | .NET TarReader link/special-entry preflight; real TAR hardlink rejection |
| B07 | ICC description misclassified as a private image comment; TIFF metadata fixture unreliable | Preserve color profile, explicit supported TIFF field handling, actual WPF-tagged fixture/pixel hashes |
| B08 | PDFsharp regenerates XMP, defeating asserted metadata cleanup | Targeted qpdf JSON changes; independent metadata/text/every-page rendered comparison |
| B09 | Older AngleSharp package failed vulnerability audit | Pin 1.8.5; do not suppress NU1902; active/external HTML preflight tests |
| B10 | Worker descendants or inherited pipes could outlive host | Windows kill-on-close Job Object, deadlines/cancellation, bounded output; actual descendant PID/timeout tests |
| B11 | Installer could overwrite link targets/remove modified user content | Fresh staged extraction, path/reparse/hash ownership, preserve unrelated data, rollback/registry snapshots; file-level installer tests; full Windows upgrade pending |
| B12 | LibreOffice generated Python bytecode in component tree | Exclude generated caches from build catalog; per-job cache prefix/no-bytecode environment |
| B13 | LibreOffice OpenCL self-test touched `program/opencl` while verifying native files | Disable OpenCL/OpenGL in private profile/environment; retain checksum enforcement; fresh document matrix rerun |
| B14 | Complete valid SDK Office packages place core properties outside `docProps/core.xml` | Resolve property parts via content-type Overrides **and Defaults**; independent Open XML schema/properties/body hash tests |
| B15 | Short speech window's model interval exceeded actual media duration | Intersect actual model intervals with real duration, omit padded-only intervals, disclose adjustment; real Arabic WAV test |
| B16 | Tiny misspelled the Arabic reference; no false accuracy pass | Add pinned local multilingual Base model, prefer it when installed; keep accuracy limitations and actual Arabic/English tests |
| B17 | Lossless AVIF→PNG privacy fallback compared differently packed 8-bit/1-bit raw samples | Canonical eight-bit RGBA comparison; actual palette PNG pixels were unchanged; AVIF/GPS regression and independent Pillow decode |
| B18 | Removing profiles could reserialize cached GPS/XMP/IPTC properties as PNG text | Remove selected property namespaces, suppress generated timestamps, inspect physical PNG metadata keywords; real GPS/XMP/IPTC and selective-cleanup regression |
| B19 | A high-bit-depth privacy rewrite through Q8 could reduce precision | Refuse unsafe >8-bit re-encoding; actual 16-bit TIFF regression; byte-preserving JPEG/normal PNG path remains available |
| B20 | Clean-PC OCR/ONNX/Whisper could rely on developer-installed VC runtimes | Pin/carry unmodified app-local CRT closure with terms, including separate Whisper exe folder; actual pack imports/engines and PE import audit |
| B21 | SDK default would bundle old .NET 10.0.2 | Pin self-contained runtime 10.0.13 from Microsoft's 2026-10-13 security release, without a system install; repeat the entire release acceptance using that self-contained runtime |

Two new test-fixture defects were corrected independently: an open ZIP entry during
fixture construction, and an incomplete PPTX fixture missing master/layout/theme.
These were test construction errors, not claims that failed outputs were valid.
The self-contained release run also exposed a fixture-directory collision: the
pack installer reused the directory where a previous safety test deliberately
left a modified model. The product correctly refused overwrite. Full pack
acceptance now uses `installed-packs-full`, and the affected import/engine test is
rerun separately; original failed run results are retained.
