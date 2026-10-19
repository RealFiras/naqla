# Development limitations — 0.8

- Actual Explorer/Desktop drag, Tab during OLE, live RTL entry, tray clicks, DPI,
  clean-PC install/upgrade/uninstall and physically disconnected networking are
  **MANUAL PENDING**, even where backend tests pass.
- Windows 10/11 x64, ordinary user integrity. Virtual files, remote URLs, elevated
  Explorer and reparse/cloud-placeholder files are not accepted. Folder metadata
  does not recursively calculate size during drag; its hub says size is unknown.
- Images use Q8/single-frame decoding and a 40 MP limit. This is not a lossless
  high-bit-depth or animated-image editor. JPG uses white for transparency.
- Privacy's JPEG/ordinary PNG byte rewrites preserve compressed image payloads;
  metadata operations that would re-encode an image deeper than 8 bits are refused
  instead of silently reducing precision. The wheel accepts its recognized source
  types; unsupported extensions do not acquire generic rename/plugin actions.
- Target-size lossless formats can have impossible targets. Failed measurements
  are reported without publishing a falsely successful optimized file.
- Batch settings are applied per source; incompatible dimensions/ranges/channels
  fail that member honestly. OCR/background/subtitle review is single-source.
- Frame extraction is capped at 300 frames, with actual FFmpeg timestamps. It can
  decode repeatedly from the beginning and be slow on long media. Accurate trim
  re-encodes; fast keyframe copy is not falsely advertised as exact.
- Two-pass audio normalization validates measured output. Silence/no usable signal
  or an unattainable loudness/peak combination can fail. Dual-mono is not spatial stereo.
- OCR accuracy varies with fonts, scans and layout. U2NetP masks need photo/edge
  review. Whisper Base/Tiny may hallucinate or mishear dialects/names/overlapping
  speech. Model intervals are clipped to actual duration with a disclosed note;
  timestamps and words still require review. No cloud or automatic model downloads.
- PDF toolkit limits 20 sources/200 pages and refuses encrypted, signed,
  interactive/form or layered documents where preservation cannot be guaranteed.
  Existing page vectors are imported. New shaped Arabic text is drawn as outlines
  and is not searchable text. PDF → DOCX/RTF reconstructs native text only;
  layout, tables and images are not rebuilt. OCR is an explicit separate action.
- The optional LibreOffice component is large (~1.5 GB installed). It starts only
  for a document job, with an isolated profile, macros/updates/links disabled.
  Complex formatting/fonts/fields can differ. HTML with active content or external
  resources, RTF fields/objects and OOXML macro/OLE/ActiveX packages are refused.
- Privacy Clean removes supported selected metadata, not visible personal content,
  tracked changes, comments in document bodies, PDF revisions or all proprietary
  container fields. Essential color/orientation structure remains. HEIC/AVIF needs
  explicit lossless PNG fallback consent. The result report lists retained limits.
- Archives: ZIP/7Z/TAR, no passwords/encrypted/split/link entries. Defaults: 10,000
  entries, 1 GB expanded, 512 MB per file, expansion ratio ≤1000, safe Windows paths.
  Conservative limits can reject legitimate very compressible archives.
- Plugins are constrained declarative SHA-256/text-statistics operations in trusted
  separate hosts. No arbitrary community code, online marketplace or OS security
  sandbox. General-purpose third-party executable plugins are intentionally unsupported.
- Process jobs impose a 1.5 GB per-process memory limit, four descendants and
  deadlines. They do not deny filesystem/network access. Same-user malicious code
  can modify ordinary user-owned files; hash checks are not an OS trust boundary.
- Development binaries are unsigned. Public release, code signing, representative
  long-session/hardware validation and redistribution/source-compliance review
  remain release gates; this package is for local development testing.
