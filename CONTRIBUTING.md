# Contributing to Naqla

Thanks for helping improve a local, offline Windows utility. Arabic and English
issues/documentation are welcome. Start with [BUILDING.md](docs/BUILDING.md) and
the [current scope](docs/FINAL-FEATURE-AUDIT.md).

## Work on a change

1. Fork the repository and create a focused branch from `main`.
2. Build on Windows x64 with the .NET 10 SDK. Do not check in `bin/`, `obj/`,
   `artifacts/`, native DLLs, models, installers, credentials or personal test files.
3. Preserve the approved wheel, native pixel buttons, Drag + Shift, Ctrl toggle,
   Tools paging and tray lifecycle. Discuss a significant navigation/design change
   before replacing those interactions.
4. Keep conversion backends separate from UI. Preserve originals and collision-safe
   sibling output; Smart Rename performs actual confirmed renaming.
5. Run checks appropriate to the change and report exactly what ran. Processing
   tests, offscreen renders and source builds do not prove actual Explorer dragging.
6. Open a pull request with the problem, resulting behavior, validation and limitations.

## Tests

Use a new owned directory under `artifacts/` for each test run; the tests create and
modify disposable fixtures. Full acceptance requires separately prepared native
engines/models. See [BUILDING.md](docs/BUILDING.md). For a physical Windows result,
use [MANUAL-WINDOWS-TESTS.md](docs/MANUAL-WINDOWS-TESTS.md) and record the actual OS/DPI,
source type, gesture and observed result. Mark unexecuted checks pending.

Repository checks run with Python 3.11+:

```powershell
py -3 scripts/verify-public-repository.py
```

The GitHub workflow checks the clean source build and public repository hygiene;
it does not run all native acceptance suites or grant a release certification.

## Reports and licenses

Use the issue templates for reproducible failures and feature proposals. Do not
attach private files, unredacted screenshots or rename recovery records. Report
vulnerabilities privately according to [SECURITY.md](SECURITY.md).

Naqla's own code is MIT. Keep dependency notices, fixture attribution and exact
version/license reviews. Do not add a binary/model without reviewing its provenance,
license and redistribution requirements. Runtime processing must remain offline;
developer downloads must be explicit.
