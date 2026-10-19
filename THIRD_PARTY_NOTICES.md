# Naqla third-party notices

This local development build includes separately licensed components. Their
copyrights and licenses remain with their respective authors. Preserve the full
texts under `licenses/`, `media-engine/`, `heif-engine/` and each optional pack.

The dependency/version/license table is [docs/DEPENDENCIES.md](docs/DEPENDENCIES.md).
PDFsharp-WPF is deliberately pinned to MIT-licensed 6.2.4. AngleSharp 1.8.5 is MIT
according to its NuGet metadata and repository license. ONNX Runtime's complete
ThirdPartyNotices, Tesseract/Leptonica, SharpCompress and AngleSharp license texts
are included with the app. FFmpeg and libheif/x265 notices identify their exact
native builds; optional packs include model/backend and dependent runtime licenses.

Official sources:

- [PDFsharp 6.2.4](https://github.com/empira/PDFsharp/tree/v6.2.4)
- [Magick.NET](https://github.com/dlemstra/Magick.NET)
- [PDFtoImage](https://github.com/sungaila/PDFtoImage), [PDFium](https://pdfium.googlesource.com/pdfium/)
- [PdfPig](https://github.com/UglyToad/PdfPig)
- [SharpCompress](https://github.com/adamhathcock/sharpcompress)
- [Tesseract wrapper](https://github.com/charlesw/tesseract), [Leptonica](https://github.com/DanBloomberg/leptonica)
- [ONNX Runtime](https://github.com/microsoft/onnxruntime), [AngleSharp](https://github.com/AngleSharp/AngleSharp)
- [FFmpeg](https://ffmpeg.org/), [libheif](https://github.com/strukturag/libheif), [x265](https://bitbucket.org/multicoreware/x265_git/)
- [whisper.cpp](https://github.com/ggml-org/whisper.cpp), [Whisper model license](https://github.com/openai/whisper/blob/main/LICENSE)
- [U-2-Net](https://github.com/xuebinqin/U-2-Net), [tessdata_fast](https://github.com/tesseract-ocr/tessdata_fast)
- [7-Zip](https://www.7-zip.org/license.txt), [qpdf](https://github.com/qpdf/qpdf/blob/v12.4.2/LICENSE.txt)
- [LibreOffice licenses](https://www.libreoffice.org/about-us/licenses/)

Developer fixture `assets/test-fixtures/arabic-marhaba.wav` is the unmodified
Lingua Libre recording **Zinou2go**, “مرحبا”, CC BY-SA 3.0. Attribution, source URL,
license link and hash accompany it in `arabic-marhaba-source.json`. It is a test
fixture, not a model or bundled user media. The JFK fixture from whisper.cpp is
identified in the developer source inventory. Synthetic files are clearly labeled
as generated fixtures; they are not evidence of physical Explorer dragging.

No public distribution/source-compliance certification or patent license is implied
by local compilation. The final report retains external release review gates.
