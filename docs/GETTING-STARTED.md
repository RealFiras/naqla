# Using Naqla 0.8

[العربية](GETTING-STARTED.ar.md) · [Project](../README.md) · [Build instructions](BUILDING.md)

Naqla is a Windows 10/11 **x64** tray utility. Version 0.8 is a development build;
actual Explorer/Desktop, DPI and clean-PC acceptance remain pending.

## Get a runnable copy

Download the [Windows x64 beta installer](https://github.com/RealFiras/naqla/releases/download/v0.8.0-beta.1/Naqla-Setup-0.8.0-beta.1-win-x64.exe).
The [release page](https://github.com/RealFiras/naqla/releases/tag/v0.8.0-beta.1)
contains checksums, notes and five optional packs. This is a **pre-release**, not
a stable certification. GitHub's **Code → Download ZIP** contains source; developers
can follow [BUILDING.md](BUILDING.md).

<a id="install"></a>

## Install step by step

Naqla is a standalone Windows application. Setup downloads selected conversion
engines only after you explicitly choose to install them; processing never uploads
files or downloads engines/models in the background.

1. Check that the PC runs **Windows 10 or 11, x64**.
2. Open the [official Releases page](https://github.com/RealFiras/naqla/releases).
   Read the version notes and development/stable label. If no installer appears
   under **Assets**, that release does not provide a ready-to-install package.
3. Download **`Naqla-Setup-0.8.0-beta.1-win-x64.exe`** from Assets.
   **Source code (zip)** and **Source code (tar.gz)** contain source, not an installer.
   Compare its SHA-256 with the published checksum. Development packages are unsigned
   and Windows may show a warning; verify the source and checksum before proceeding.
4. The native tools need Microsoft's current **Visual C++ x64 Redistributable**.
   If setup reports it missing, use its official Microsoft link, install that runtime,
   then reopen Naqla setup. This is not the .NET SDK. Runtime installation may require
   Windows administrator approval; Naqla itself installs per user.
5. Open the installer as your ordinary user. Keep **Launch Naqla after installation**
   checked to start immediately. **Start Naqla with Windows** controls automatic startup.
   Keep **Download FFmpeg / HEIF once from official sources (internet)** checked for
   media and HEIC conversion. It needs a connection during setup; the downloaded
   archives and native files are hash-checked. Unchecking it leaves those actions unavailable.
6. Click **Install development build** and wait for the completion message.
7. Find the tray icon beside the clock or under hidden icons. No main window is
   expected. Follow **First conversion** below to try a disposable PNG.

Installation is per user under `%LOCALAPPDATA%\Programs\Naqla`. The self-contained
package needs no SDK, Python, Node or administrator rights on the target PC. If you
exited or unchecked launch after installation, press **Win+R** and run:

```text
"%LOCALAPPDATA%\Programs\Naqla\Naqla.App.exe"
```

To change startup later, right-click the tray icon and toggle **Start with Windows**.
Optional components are a separate step below; PNG → JPG does not require their import.

## Update and uninstall

Finish active work and choose **Exit** from the tray, then run the new matching
installer with the same Windows user account.

To remove Naqla, open **Settings → Apps → Installed apps** on Windows 11 or
**Apps & features** on Windows 10. Find **Naqla (beta)** and
choose **Uninstall**. Removal is designed to preserve original and converted files;
actual clean-account install/upgrade/uninstall acceptance remains pending in the
[manual checklist](MANUAL-WINDOWS-TESTS.md).

## First conversion

1. Launch Naqla. No main window opens. Find the Naqla icon in the system tray;
   Windows may put it under **Show hidden icons**.
2. Start with a disposable **local PNG** on the desktop or in Explorer. Keep the
   left mouse button held and drag a short distance.
3. While still dragging, press **Shift**. The wheel should appear near the pointer;
   file identity is confirmed only when Explorer delivers real OLE file data.
4. Move over **JPG**, then release the mouse. The action runs or opens its compact
   editor when settings are required.
5. Open the result beside the PNG. The source should be untouched. Transparent
   pixels get a white background in JPG. Existing names get a safe numbered suffix.

This describes intended behavior, not a claim that the physical gesture has already
been accepted on every Windows desktop. The browser [preview](https://realfiras.github.io/naqla/)
is a design demonstration; it cannot perform this workflow.

## Keys and cancellation

| Input | Effect in the native app |
|---|---|
| Drag + Shift | Arm/show the contextual wheel |
| Ctrl down once | Toggle Convert ↔ Tools; releasing Ctrl retains the mode |
| Ctrl down again | Return to the other mode |
| Tab in Tools | Next page, up to six sectors per page; release Shift first |
| Shift+Tab in Tools | Previous page |
| Pointer over a sector + mouse release | Select/drop the actual file for that action |
| Escape | Cancel the wheel without processing |
| Cancel in an editor/job | Cancel that work; already completed batch outputs remain |

Available destinations depend on source type and installed local capabilities.
The current format is hidden when no meaningful same-format action exists.

## Optional tools

From the tray, open **Local components / plugins…** and explicitly import a matching
local ZIP pack built for this exact release. No model or pack downloads automatically.
Download the five published packs from [release Assets](https://github.com/RealFiras/naqla/releases/tag/v0.8.0-beta.1).
The Office row below describes a backend requiring a separately prepared pack;
**no Office pack is distributed in this beta**, so Office conversions remain unavailable
in an ordinary public installation. Do not mix older private development packs with public ones.

| Pack | Adds |
|---|---|
| `ocr` | English/Arabic OCR data; image/scanned-PDF text recognition |
| `background` | U2NetP background-removal model |
| `speech` | whisper.cpp and multilingual Base/Tiny models for SRT/WebVTT |
| `archives` | 7Z writing using 7za; ZIP/TAR use built-in backends |
| `pdf-metadata` | Selective PDF metadata cleanup using qpdf |
| `office` | Local Office document conversion using LibreOffice |

HEIC uses the local HEIF engine obtained by the explicit setup download. Source
builders must prepare it. Renaming a random ZIP will
not turn it into a compatible pack: catalog version and exact hashes are checked.
Office is large and its first import/integrity check can take several minutes.

## Files, batches and limits

- Outputs use new collision-safe names beside the source. Work in a writable folder.
- **Smart Rename changes the actual names after explicit confirmation**. Review
  Before → After; undo can be refused if files changed or names became occupied.
- For Merge Images → PDF, select the images together and drag the selection.
  For Merge PDFs, drag the selected PDFs together and confirm document order.
- Compatible actions can process a selection as a batch; failures are reported per
  file. OCR/background/subtitle review is single-source.
- PDF → DOCX/RTF is native text extraction, not a layout-preserving reconstruction.
  OCR/transcription/background edges need human review.
- Plugin API 1 supports local declarative operations such as SHA-256 and text
  statistics; it does not execute arbitrary community binaries or provide an OS sandbox.

Read [KNOWN-LIMITATIONS.md](KNOWN-LIMITATIONS.md) before relying on a workflow.

## Nothing happens when I press Shift

Confirm the process is running, keep the left button down, and drag a supported,
fully local file from Explorer/Desktop. A normal-integrity app cannot capture every
elevated, virtual, cloud-placeholder or non-Explorer drag. Exit an old Naqla instance
before launching a newly built one.

Use the tray's diagnostics and mark the attempt. Logs stay under
`%LOCALAPPDATA%\Naqla\Diagnostics`; they omit paths, filenames and contents.
Follow [the manual checklist](MANUAL-WINDOWS-TESTS.md) to distinguish keyboard hook,
overlay and real OLE delivery problems. Return the build/Windows version, extension,
approximate size, key order, marked session and a cropped recording. Remove personal
details from screenshots; **never publish rename recovery records or private files**.
Use [a bug report](https://github.com/RealFiras/naqla/issues/new?template=bug_report.yml)
for non-security problems and [SECURITY.md](../SECURITY.md) for vulnerabilities.

Exit from the tray when finished. Closing the wheel or an editor does not exit the app.
