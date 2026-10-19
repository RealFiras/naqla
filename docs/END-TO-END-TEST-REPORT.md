# End-to-end processing report — 0.8.0-development.1

Public readers: [the historical test snapshot](test-results/0.8-development.json)
contains allowlisted groups and conversion results. Paths under `artifacts/` below
refer to retained local evidence, not files distributed in this source repository.
GitHub CI checks source build/hygiene separately; it is not the full native suite.

Verified 2026-10-19T17:34:27.427125+00:00. Classification: **DEVELOPMENT READY for local testing**.
Physical Windows acceptance, clean-PC installation and disconnected networking remain
**MANUAL PENDING**. No simulated Explorer drag was used as evidence.

## Automated evidence

| Suite | Passed groups | Machine result |
|---|---|---|
| release-acceptance-10.0.13 | 122/123 | `artifacts/release-acceptance-10.0.13/results.json` |
| pack-acceptance-runtime-final | 1/1 | `artifacts/pack-acceptance-runtime-final/results.json` |

Latest acceptance sequence: **124 group executions**, **123 passing executions**,
**1 retained failed execution(s)** resolved by the documented rerun;
**0 unresolved automated failures, 0 blocked automated groups, 0 skipped automated groups**.
The 136 conversion pairs are nested output checks, not additional top-level test groups.
Physical/manual rows W01–W17 and F01–F24 remain pending rather than counted as passed.

The full self-contained .NET 10.0.13 run initially passed **122/123**.
The last pack test correctly refused to overwrite an intentionally modified model
left by another safety test in the same test directory. The fixture roots were
separated, without weakening the overwrite guard, and the full six-pack import/
engine test rerun. The latest-result set is **123/123 distinct
groups passing**, stored in `artifacts/final-verification.json`; this is not
misrepresented as one completely green command. Earlier failures remain recorded.
This does not erase failures in `final-validation-1/2`, `document-tests-3/4`,
`stress-acceptance` or earlier fixture iterations. See BUG-FIX-LOG for root causes.

## Actual conversion matrix

| Family | Source/destination pairs |
|---|---|
| Images | 49/49 |
| Video | 27/27 |
| Audio | 30/30 |
| PDF | 5/5 |
| Other documents | 25/25 |
| Total | **136/136** |

Evidence: `artifacts/release-acceptance-10.0.13/conversion-matrix.json` and
`document-matrix.json`. The tests invoke actual LocalConversionService handlers,
save/reopen outputs and check codecs, dimensions/pages/content/duration, full media
decodes and original hashes. Document fixtures contain Arabic/English, real tables,
images and multiple pages. TXT exports necessarily lose formatting; PDF DOCX/RTF
exports are native text extraction, not faithful visual reconstruction.

## Tool and safety coverage

Real filesystem rename/undo/identity/rollback/recovery; all optimizer encoders and
target bounds/impossible targets; image crop/rotation/compression/alpha/orientation;
PDF page order/vector streams/rendered content/ranges/marks/numbers/rotation;
actual 30-second frames/PTS and precise trims; independently measured two-pass audio,
silence and channel counts; OCR on real English/Arabic/mixed-script images; actual
U2NetP inference; real JFK and licensed human Arabic word transcription, with actual
SRT/WebVTT packets parsed by FFmpeg; ZIP/7Z/TAR round trips, traversal/links/bomb/
encryption/cancellation; Office schema validation before/after metadata cleaning,
real GPS/XMP/IPTC and explicit AVIF PNG fallback; original hashes/collisions/source
locks/read-only failures; local declarative plugin lifecycle/failure/restart.

Additional stress: **310 actual JPEG outputs** across 10/50/50/200-image submissions.
This is small-image repeated-job stress, not an hours-long 4K video or huge-photo benchmark.
All six actual optional ZIP packs were imported into an owned test directory and
their installed OCR/segmentation/speech/archive/qpdf/Office engines executed. Exact
catalog/hash checks include dependency and license closure. See
`artifacts/pack-acceptance-runtime-final/component-packs.json`.

The native import audit covers 309 x64 native binaries. No required static
imports are missing. LibreOffice's optional Java AWT embedding bridge references
`jawt.dll`; it is not used or advertised by Naqla's Writer export operations. This
is a static dependency check, not a clean-PC execution certificate.

## Physical and release gates

Use [MANUAL-WINDOWS-TESTS.md](MANUAL-WINDOWS-TESTS.md). Real CF_HDROP delivery, global
key transitions during OLE, display edges/DPI, Explorer restart, actual button clicks,
tray appearance, clean-account registry install/upgrade/uninstall, disconnected
networking, representative continuous Arabic speech, photos/hair edges and long
sessions still require human acceptance. Offscreen images and process smoke do not
close these gates. Optional models/backends never auto-download during processing.

Installer extraction/portable lifecycle results and exact installer hashes are kept
outside the payload in `artifacts/release-verification.json` to avoid a self-referential
installer checksum. No remote publishing, cloud processing, system service or driver
installation was performed.
