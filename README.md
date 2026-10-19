<div align="center">
  <img src="docs/assets/hero.png" alt="Naqla — Drag. Press Shift. Done." width="100%">

  <br>

  **Drag a file. Press Shift. Everything you need appears around it.**

  A native Windows tray utility for supported local images, media and documents.<br>
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

## Start here

- **Using Naqla:** [English guide](docs/GETTING-STARTED.md) · [الدليل العربي](docs/GETTING-STARTED.ar.md).
- **How to install:** [Download, install, launch, update and uninstall](docs/GETTING-STARTED.md#install).
- **Trying the design:** [Live UI preview](https://realfiras.github.io/naqla/). This browser demonstration does not convert or upload files.
- **Download Windows x64:** [Naqla 0.8.0-beta.1 installer](https://github.com/RealFiras/naqla/releases/download/v0.8.0-beta.1/Naqla-Setup-0.8.0-beta.1-win-x64.exe) · [Release notes, checksums and optional packs](https://github.com/RealFiras/naqla/releases/tag/v0.8.0-beta.1).
- **Developing:** [Build guide](docs/BUILDING.md) · [Contributing](CONTRIBUTING.md).

## How it works

The public beta installer needs the official [Microsoft Visual C++ x64 runtime](https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist). With its explicit download checkbox selected, setup obtains pinned FFmpeg/HEIF engines directly from their upstream providers once. Processing afterward is offline. No SDK/Python/Node is needed on the target PC. Five optional packs are available in the release; the Office conversion pack is not distributed in this beta. [Installation details](docs/GETTING-STARTED.md#install).

| | |
|:--:|---|
| **1** | **Drag** a supported file in Explorer or on the desktop. |
| **2** | **Hold** the left mouse button and press **Shift**. |
| **3** | **Release** over a sector. Naqla processes it or opens its compact editor. |

Shift arms the overlay; only an actual OLE file payload confirms file identity. Tap **Ctrl** to toggle Convert/Tools; releasing Ctrl keeps the selected mode. In Tools, **Tab** / **Shift+Tab** change **pages**, with at most six sectors per page. Release Shift before using Tab alone. Move the pointer to select a sector; **Esc** cancels.

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

`136` real conversion pairs passed the recorded local acceptance matrix. OCR, background removal, subtitles, 7Z writing, selective PDF metadata cleanup and Office conversion require **optional local packs** — no automatic downloads. HEIC uses the separate local HEIF engine bundled in the development build; source builders prepare it explicitly.

PDF → DOCX/RTF extracts native text; it does not reconstruct the original layout. Speech/OCR/masks need review. Plugins currently support a constrained declarative API, not arbitrary third-party code. See the [limits](docs/KNOWN-LIMITATIONS.md).

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

**Requirements:** Windows 10/11 x64, Git and the .NET 10 SDK. `global.json` pins `10.0.102` with roll-forward. NuGet restore needs a connection; installed processing is local.

```powershell
git clone https://github.com/RealFiras/naqla.git
cd naqla
dotnet restore Naqla.sln
dotnet build Naqla.sln -c Release --no-restore
dotnet run --project src/Naqla.App -c Release
```

This builds and starts the tray shell and NuGet-provided features. It does **not** prepare every native engine/model or create an installer. Exit an existing Naqla instance first; the singleton otherwise keeps that older instance running.

Explicit developer fetches prepare the FFmpeg and HEIF engines:

```powershell
scripts\fetch-media-engine.ps1
scripts\fetch-heif-engine.ps1
```

For model packs, CRT requirements, full acceptance and packaging, follow [BUILDING.md](docs/BUILDING.md). The source repository excludes native redistributable DLLs and generated engines; third-party licensing is separate from Naqla's MIT source license.

## Tests

```powershell
dotnet run --project tests/Naqla.Tests -c Release -- artifacts/my-tests --release-acceptance
```

The full suite needs the prepared engines and all six component packs; it is not a clean-clone smoke command. Each run needs a fresh, owned output directory. Extended packaging and stress acceptance:

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

The [public test snapshot](docs/test-results/0.8-development.json) contains the historical groups and conversion matrix. Raw fixtures/logs/installers remain local under ignored `artifacts/`; they are not shipped in a source checkout. The [end-to-end report](docs/END-TO-END-TEST-REPORT.md) explains the retained failure and rerun. GitHub CI verifies source build and repository checks; it does not certify Explorer dragging or rerun the full local matrix.

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
| [Getting started](docs/GETTING-STARTED.md) | Install/build status, keyboard controls, optional packs and troubleshooting |
| [Contributing](CONTRIBUTING.md) | Development workflow and useful bug reports |

## License

Naqla source code is released under the [MIT License](LICENSE). Third-party components keep their own licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and [docs/DEPENDENCIES.md](docs/DEPENDENCIES.md). No public redistribution or source-compliance certification is implied.

<div align="center">
  <sub>Built for Windows. Designed to disappear until you need it.</sub>
</div>
