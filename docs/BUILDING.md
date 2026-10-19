# Building Naqla from a source checkout

[Project](../README.md) · [User guide](GETTING-STARTED.md) · [Contributing](../CONTRIBUTING.md)

## 1. Build the application shell

Use Windows 10/11 x64, Git and the **.NET 10 SDK**. `global.json` selects 10.0.102
with `latestFeature` roll-forward. NuGet restore requires network access. The
installed application never calls these developer fetch scripts.

```powershell
git clone https://github.com/RealFiras/naqla.git
cd naqla
dotnet restore Naqla.sln
dotnet build Naqla.sln -c Release --no-restore
dotnet run --project src/Naqla.App -c Release
```

No main window is expected. Check the tray, including hidden icons. Exit any older
Naqla instance first; the singleton keeps the existing instance instead of replacing
it. Stop from the tray, or gracefully request exit:

```powershell
dotnet run --project src/Naqla.App -c Release -- --quit
```

This builds the source/NuGet features; it does not supply every external engine or
model. Missing capabilities stay unavailable. It also does not create a working
installer payload: compiling `Naqla.Setup` alone is not packaging.

## 2. Prepare base native engines when needed

Run these explicit, pinned developer downloads from the repository root:

```powershell
& .\scripts\fetch-media-engine.ps1
& .\scripts\fetch-heif-engine.ps1
```

They populate `artifacts/media-engine` and `artifacts/heif-engine`, with hashes,
licenses and notices. Rebuild after fetching so the app copies them. Do not put
random FFmpeg/libheif binaries on PATH or claim they match the verified engine.
HEIC requires HEIF; media conversions require FFmpeg/ffprobe.

## 3. Optional components: explicit maintainer preparation

Python **3.11+** is needed for these scripts, not for the installed application.
`fetch-optional-components.py` uses only the standard library and fetches speech
models/runtime, OCR data, U2NetP, the 7-Zip extra archive, fixtures and license texts:

```powershell
py -3 scripts/fetch-optional-components.py
```

This is **not** a complete one-command pack builder. The following local inputs
must also be prepared, preserving their upstream licenses:

| Input | Required location / preparation |
|---|---|
| 7za | Extract the pinned `artifacts/optional-components/7z2604-extra.7z` with 7-Zip into `artifacts/optional-components/sevenzip`; retain `x64/7za.exe` and `License.txt` |
| qpdf | Obtain the official **12.4.2 mingw64** archive from [upstream releases](https://github.com/qpdf/qpdf/releases/tag/v12.4.2), extract under `artifacts/optional-components/qpdf/qpdf-12.4.2-mingw64`; required bytes must match the checked-in catalog |
| Office | Obtain the official **LibreOffice 26.2.6.3 Windows x64 MSI**, save as `artifacts/LibreOffice-fast.msi`; SHA-256 must be `f9877032fd908beb9c0ddf06df4af5c2e85f419c42e14876c4cce5aae5fb2660` |
| Office extraction | Requires local `C:\Program Files\7-Zip\7z.exe`; run `py -3 scripts/extract-office-component.py`. It reads MSI tables/cabinets without installing Office or running MSI actions |
| App-local CRT | Review the Microsoft terms and your redistribution rights, then run `py -3 scripts/prepare-app-crt.py` against the pinned extracted Office tree. It copies exact DLLs into ignored `assets/native-crt` and the separate Whisper executable folder |

Native CRT DLLs, installers, model binaries and the generated engine trees are
deliberately **not part of the open-source checkout**. License/provenance manifests
remain tracked. Keeping the files local does not by itself grant redistribution rights.

The complete expected file catalog is `assets/component-catalog.json`. Before
building ZIP packs, ensure every required file/license and model has the expected
bytes. The pack builder **recomputes** that catalog from local files; do not treat
recomputing it as proof of provenance or casually replace it with arbitrary bytes.
Run only after reviewing the pinned inputs, and review any resulting catalog diff:

```powershell
py -3 scripts/build-component-catalog.py
py -3 scripts/build-sample-plugins.py
```

Import matching ZIPs from `artifacts/component-packs` through tray → **Local
components / plugins…**. Plugin examples install disabled and need explicit consent.
For licensing details see [DEPENDENCIES.md](DEPENDENCIES.md) and
[THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md). Large Office imports may take minutes.

## 4. Tests and their prerequisites

Repository/CI checks need Git and Python, with no models:

```powershell
py -3 scripts/verify-public-repository.py
dotnet build Naqla.sln -c Release
```

Once engines and all optional components are prepared, run full local acceptance
in a **new, owned** directory. Tests create and modify disposable fixtures; never
point them at a directory containing user documents or previous test state.

```powershell
dotnet run --project tests/Naqla.Tests -c Release -- artifacts/acceptance-new --release-acceptance
```

Other supported focused modes include `--plugins-only`, `--privacy-only`,
`--archives-only`, `--models-only`, `--documents-only`, `--matrix-only`,
`--integration-only`, `--stress-only` and `--component-packs-only`. Each has its own
native dependencies; none is advertised as a full dependency-free unit suite.
`--component-packs-only` needs the six actual ZIP packs, not just raw model files.

Tests write JSON evidence to the supplied directory. The historical
[0.8 snapshot](test-results/0.8-development.json) is not evidence that your checkout
has just passed. Use [MANUAL-WINDOWS-TESTS.md](MANUAL-WINDOWS-TESTS.md) for actual
Explorer/OLE, keys, tray, DPI and installer acceptance. Do not mark unexecuted rows passed.

## 5. Local development packaging

The maintainer release workflow needs all reviewed native inputs plus Python packages
`psutil`, `pefile` and `markdown` for resource sampling, PE inspection and HTML docs.
Use a developer virtual environment if installing them. It runs real acceptance
before packaging; it can take substantial time/disk space. Exit Naqla first:

```powershell
& .\scripts\build.ps1
& .\scripts\verify-diagnostic-package.ps1
py -3 scripts/write-release-verification.py
```

The installer is `artifacts/Naqla-Dev-Setup-win-x64.exe`. Self-contained builds pin
.NET **10.0.13**. `package-development.ps1` without `-RunAcceptance` is for a
maintainer who already has a current passing source inventory/report; a clean clone
will correctly fail that gate. Generated reports/packaging scripts are maintainer
tools and may replace local development summaries; they are not the public README editor.

Do not publish the generated installer or optional packs until the applicable
GPL/LGPL/MPL/Microsoft notices, corresponding-source/relink requirements and
redistribution rights have been reviewed. The MIT license covers Naqla's own code.
This repository currently publishes **source and design previews**, not a stable binary release.
