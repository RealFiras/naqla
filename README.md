<div align="center">
  <img src="docs/assets/hero.png" alt="Naqla — Drag. Press Shift. Done." width="100%">

  <br>

  **Drag a file. Press Shift. Everything you need appears around it.**

  A native Windows tray utility that turns any local file into a radial command hub.<br>
  No main window. No uploads. No telemetry.

  <br>

  ![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011%20x64-0078D4?style=flat-square&logo=windows11&logoColor=white)
  ![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet&logoColor=white)
  ![Version](https://img.shields.io/badge/version-0.8%20development-FFB347?style=flat-square)
  ![License](https://img.shields.io/badge/license-MIT-55D6B0?style=flat-square)
  ![Offline](https://img.shields.io/badge/processing-100%25%20local-111827?style=flat-square)

  <br>

  [Live UI preview](https://realfiras.github.io/naqla/) · [العربية](README.ar.md) · [Feature audit](docs/FINAL-FEATURE-AUDIT.md) · [Limitations](docs/KNOWN-LIMITATIONS.md) · [Security](docs/SECURITY-REVIEW.md)
</div>

> [!WARNING]
> **Development build 0.8.** Real local processing and automated acceptance evidence are included, but physical Windows acceptance and clean-PC installation are still pending. This is not a public stable release.

## How it works

| | |
|:--:|---|
| **1** | **Drag** a supported file in Explorer or on the desktop. |
| **2** | **Hold** the left mouse button and press **Shift**. |
| **3** | **Release** over a sector. Naqla processes it or opens its compact editor. |

The wheel is drawn from the real OLE drag payload, never from a guessed path. Tap **Ctrl** for Tools; press **Tab** / **Shift+Tab** to move between sectors; **Esc** cancels.

## One hub, every file type

<img src="docs/assets/wheel-family.png" alt="Naqla wheel for images, video, audio, PDF and tools" width="100%">

> The same wheel, restyled per family. The hub shows the real type and size of the file you are holding.

<details>
<summary><b>Full state sheet</b> — convert, hover, tools and paging</summary>
<br>
<img src="docs/assets/wheel-states.png" alt="Naqla wheel states, hover states and tools pages" width="100%">
</details>

## What it does

| Category | Capabilities |
|---|---|
| **Images** | JPG · PNG · WEBP · AVIF · TIFF · HEIC · PDF conversion, crop, rotate, compress, target-size optimizer |
| **Video** | MP4 · MOV · WebM · GIF · MP3/M4A extraction, frame extraction, trim, subtitles |
| **Audio** | MP3 · M4A · WAV · FLAC · AIFF · OPUS, two-pass loudness normalization, silence detection, channel tools |
| **PDF** | Merge, extract, delete, reorder, rotate, watermark, page numbers; export to PNG/JPG/TXT/DOCX/RTF |
| **Documents** | DOCX · RTF · TXT ↔ PDF and native text extraction |
| **Files** | Smart Rename with preview/undo, ZIP/7Z/TAR, Privacy Clean |

`136` real conversion pairs are covered by the acceptance matrix. HEIC, OCR, background removal, subtitles, 7Z writing, qpdf metadata cleanup and Office export are **optional local packs** — no automatic downloads.

## Tools stay out of the way

<img src="docs/assets/tool-editors.png" alt="Naqla compact tool editors for frames, trim, normalize, subtitles, OCR and privacy" width="100%">

> Small, contextual editors open only when an action needs parameters. There is no dashboard and no startup window.

## A real PDF toolkit

<p align="center"><img src="docs/assets/pdf-toolkit.png" alt="Naqla PDF toolkit with page thumbnails and live preview" width="62%"></p>

> Seven vector-preserving operations with thumbnails, zoom, fit and pan. Existing page vectors are imported; new Arabic text is drawn as outlines.

## Local by design

| | |
|---|---|
| **OLE, not guesses** | Real drag data from Explorer, with native hooks during the gesture. |
| **Offline processing** | The installed app never uploads files or downloads models. |
| **Safe outputs** | New names beside the source. Smart Rename changes originals only after explicit confirmation. |
| **Honest progress** | Per-file failures and cancellation; completed outputs are preserved. |
| **Quiet diagnostics** | Typed local logs without paths, names or file content. |

## Architecture

| Project | Responsibility |
|---|---|
| `Naqla.App` | WPF shell, tray lifecycle, compact editors, progress windows |
| `Naqla.Wheel` | Custom pixel radial wheel, per-family themes, six-sector Tools pages |
| `Naqla.Windows` | OLE drag session, global key hooks, native interop, tray |
| `Naqla.Conversion` | FFmpeg, Magick.NET, PDFium/PDFsharp, Tesseract, ONNX, archives, plugins |
| `Naqla.Core` | Contracts, validation, batching, diagnostics, wheel state |
| `Naqla.Setup` | Per-user installer and startup preference |
| `Naqla.Tests` | Release acceptance, conversion matrix, stress and safety suites |

## Build from source

**Requirements:** Windows 10/11 x64 and the .NET 10 SDK. `global.json` pins `10.0.102` with roll-forward.

```powershell
git clone https://github.com/RealFiras/naqla.git
cd naqla
dotnet build Naqla.sln -c Release
dotnet run --project src/Naqla.App -c Release
```

Optional engines are developer-only fetches; normal processing never needs the network:

```powershell
scripts\fetch-media-engine.ps1
scripts\fetch-heif-engine.ps1
py -3 scripts\fetch-optional-components.py
```

Office and qpdf packs require their own pinned inputs. See [dependencies](docs/DEPENDENCIES.md).

## Tests

```powershell
dotnet run --project tests/Naqla.Tests -c Release -- artifacts/my-tests --release-acceptance
```

Each run needs a fresh, owned output directory. Extended packaging and stress acceptance:

```powershell
dotnet run --project tests/Naqla.Tests -c Release -- artifacts/stress-tests --stress-only
dotnet run --project tests/Naqla.Tests -c Release -- artifacts/pack-tests --component-packs-only
```

## Status

| Gate | Result |
|---|---|
| Conversion matrix | **136/136** pairs |
| Automated acceptance | **123/123** distinct groups after a documented fixture-isolation rerun |
| Physical Windows / DPI / tray | **Manual pending** |
| Clean-PC install / upgrade / uninstall | **Manual pending** |
| Disconnected-network run | **Manual pending** |

The full evidence and every retained failure are in the [end-to-end report](docs/END-TO-END-TEST-REPORT.md). Known boundaries are in [KNOWN-LIMITATIONS.md](docs/KNOWN-LIMITATIONS.md).

## Documentation

| | |
|---|---|
| [Feature audit](docs/FINAL-FEATURE-AUDIT.md) | Complete 180-row scope and acceptance matrix |
| [Conversions](docs/CONVERSIONS.md) | Supported source/target pairs |
| [Smart Rename](docs/SMART-RENAME.md) | Preview, transforms, collisions, undo |
| [Target Size](docs/TARGET-SIZE.md) | Iterative encoding and strict targets |
| [PDF Toolkit](docs/PDF-TOOLKIT.md) | Seven operations and preservation rules |
| [Plugins](docs/PLUGINS.md) | Local declarative plugin API |
| [Security review](docs/SECURITY-REVIEW.md) | Threat model and process boundaries |
| [Dependencies](docs/DEPENDENCIES.md) | Pinned engines, licenses and offline packs |
| [Manual tests](docs/MANUAL-WINDOWS-TESTS.md) | Physical Windows acceptance checklist |

## License

Naqla source code is released under the [MIT License](LICENSE). Third-party components keep their own licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and [docs/DEPENDENCIES.md](docs/DEPENDENCIES.md). No public redistribution or source-compliance certification is implied.

<div align="center">
  <sub>Built for Windows. Designed to disappear until you need it.</sub>
</div>
