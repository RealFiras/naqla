# Dependencies and offline packs

Source checkout: native DLLs/models/generated engine trees are excluded. Follow
[BUILDING.md](BUILDING.md) to distinguish a shell build, native preparation and
maintainer-only packaging. Naqla's MIT license does not relicense dependencies.

The app is Windows x64/.NET 10 self-contained. No target-PC SDK, Python, Node,
installed Office, cloud API or model download is required. Developer build/fetch
steps use network access explicitly; normal file processing does not.
Self-contained builds pin **10.0.13**, the security release published 2026-10-13
in [Microsoft's release metadata](https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json).
The development PC's installed 10.0.2 runtime/SDK is not modified.

| Dependency | Pinned version / role | License / distribution |
|---|---|---|
| Magick.NET-Q8-x64 | 15.0.0, image codecs | Apache-2.0 and native notices; bundled |
| PDFtoImage / PDFium | 5.4.0, real PDF previews/rendering | MIT wrapper; PDFium BSD-family notices; bundled |
| PdfPig | 0.1.16, independent PDF parsing/text | Apache-2.0; bundled |
| PDFsharp-WPF | **6.2.4 exact**, vector-preserving PDF toolkit | MIT at this pinned version; later licensing must be reviewed |
| SharpCompress | 0.50.5, independent archive reader | MIT; bundled |
| Tesseract wrapper/native | NuGet 5.2.0 | Apache-2.0; Leptonica BSD; English/Arabic data separate |
| ONNX Runtime CPU | 1.30.0 | MIT plus ThirdPartyNotices; bundled runtime, model separate |
| AngleSharp | 1.8.5, HTML parser only | MIT per the package/repository license; no resource loader |
| FFmpeg | 9.0.2 locked engine | Separate local exe; GPL build/license/source references in engine NOTICE |
| libheif / x265 closure | Exact `scripts/heif-engine.lock.json` | LGPL/GPL and dependent notices in engine; no PATH edits |
| whisper.cpp CPU | v1.9.5 / binary release b5454, exact ZIP hash | MIT; separate `speech` pack |
| Whisper multilingual Base/Tiny | Exact GGML SHA-256 pins in service/catalog | MIT model notices; separate pack, Base preferred |
| Tesseract eng/ara fast data | commit 87416418657359cb625c412a48b6e1d6d41c29bd | Apache-2.0; `ocr` pack |
| U2NetP | Exact rembg asset hash / 320×320 CPU model | U-2-Net Apache-2.0; `background` pack |
| 7-Zip standalone 7za | 26.04 exact upstream archive hash | LGPL-2.1/BSD notices; `archives` pack |
| qpdf | 12.4.2 mingw64 exact upstream ZIP hash | Apache-2.0; GNU runtime exception and winpthreads notices; `pdf-metadata` pack |
| LibreOffice x64 | 26.2.6.3, pinned official MSI SHA-256 | MPL-2.0 and bundled component notices; separate `office` pack |
| Open XML SDK | 3.3.0 | MIT, **tests only**, independent DOCX/XLSX/PPTX schema validation |
| Microsoft x64 VC runtime | Ten exact MSI-derived DLL hashes in `assets/native-crt/sources.json` | App-local only, no system install; full Microsoft terms included. External redistribution requires applicable licensed Visual Studio rights/review. |

LibreOffice is extracted by readonly MSI table/cabinet inspection, **never by running
MSI installation/custom actions**. App-local official x64 CRT files accompany it.
First use verifies the native component; unchanged verified trees are cached with
change invalidation. Private profiles disable macro execution, update checks,
external links, Python cache writes in the engine, OpenCL and OpenGL self-tests.
The optional Java AWT embedding bridge in the full LibreOffice tree is not a Naqla
operation and requires a JDK if used independently. Advertised Writer exports do
not invoke it or require installed Java; clean-PC acceptance is still pending.

## Build reproducibility

`fetch-optional-components.py` fetches pinned upstream assets, records sources and
hashes; `extract-office-component.py` requires the pinned official MSI and a local
7-Zip extractor. `build-component-catalog.py` emits the embedded exact-file catalog
and **six separate** local ZIP packs. Generated Python bytecode is excluded.

Import via tray → Local components / plugins. Packs may be kept on a USB drive.
No install hooks, arbitrary extra files or mismatched catalog versions are allowed.
All pack checksums and byte sizes are recorded in the final release inventory.
Remove a pack from the same UI; modified/unrelated files are retained.

Licenses shipped in `licenses/`, engine notices and optional pack license files
must accompany their binaries. This is a local development release. Before external
redistribution, verify the complete corresponding-source/notice/relink obligations
of each actual GPL/LGPL/MPL build and codec patent requirements for the intended
distribution. Upstream download URLs alone are not represented as a universal
substitute for those obligations. See [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md).
