# Naqla feature audit — 0.8 development

The current evidence and complete 180-row matrix are in
[FINAL-FEATURE-AUDIT.md](docs/FINAL-FEATURE-AUDIT.md). Backend fixture verification
and physical Windows acceptance are separate; this is not a public stable release.

| Requested feature | Current implementation | Remaining acceptance |
|---|---|---|
| Smart Rename | Actual confirmed file rename, preview/transforms/collisions/undo/rollback | Physical editor/drag testing |
| Target Size | Actual iterative encoding, measured strict target, presets/custom/resize/batch | Physical editor/drag testing |
| PDF Toolkit | Seven real operations, thumbnails, same compact three-column editor, fit/zoom/pan | Visual review and physical/DPI testing |
| Frame Extraction | Real FFmpeg frames/PTS, timestamp/every-N/count/range, PNG/JPG | Long/high-resolution and physical timeline testing |
| Audio Normalizer | Measured two-pass loudnorm, peak limits, edge silence, channel tools | Representative long recordings and settings UI |
| Subtitles | Real local Base/Tiny speech recognition, Arabic/English, editable cues, SRT/VTT | Continuous/dialect/mixed speech accuracy review |
| Privacy Clean | Real image/PDF/Office/media cleanup, selected categories, independent verification/report | Representative personal files; not content anonymization |
| Plugins | Versioned local declarative API, consent/lifecycle, separate trusted host, two examples | Broader executable plugin API unsupported; native crash/long-session review |

[Acceptance evidence](docs/END-TO-END-TEST-REPORT.md) · [Limitations](docs/KNOWN-LIMITATIONS.md)
· [Windows checklist](docs/MANUAL-WINDOWS-TESTS.md).
