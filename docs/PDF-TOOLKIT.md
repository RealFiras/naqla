# Advanced PDF Toolkit — 0.7.0-pdfpreview.1

Functional development editor and offline processing backends. **The editor's visual
design/navigation is a draft awaiting explicit approval.** No Feature 4 work has started.
The Phase 3 request authorizes this increment; it does not establish acceptance of any
earlier physical desktop test. The existing wheel renderer, palette, circle/capsule,
Drag + Shift, Ctrl toggle, pixel buttons and earlier editors remain in place.

## Open and save

Drag a real PDF from Explorer/Desktop, hold the left button, press Shift, tap Ctrl,
then release over ترتيب / استخراج / حذف / ترقيم / علامة مائية / تدوير. These are
contextual registrations in the existing wheel; TXT and Rename remain available.
With multiple PDFs, Tools offers دمج PDF plus existing text/Rename actions. Each tool
opens the same compact native editor. The operation selector switches single-document
tools; a multi-document editor handles Merge. No secondary wheel navigation is added.

Thumbnail images are rendered from the actual PDFs. Select a thumbnail to view its
output page. Drag thumbnails/documents to reorder; ↑/↓ are an accessible alternative
after selecting an item. A changed selection/setting/order invalidates Save. Click
**معاينة** to generate the real prospective output and render it with PDFium. Use the
page arrows to review the result. **حفظ نسخة** explicitly confirms that measured plan;
processing then runs on the shared asynchronous queue. Cancel/Escape closes without
publishing. Tray cancellation/Exit also cancels and waits for owned cleanup.

Each source is read-locked while processing. Its SHA-256 must still match the opened
session, so changed originals require reopening. Outputs are beside the first dragged
source: `name reorder.pdf`, `name extract.pdf`, `name delete.pdf`, `name merge.pdf`,
`name numbers.pdf`, `name watermark.pdf` or `name rotate.pdf`. File/directory collisions
use ` (1)`, ` (2)`, etc.; neither originals nor existing outputs are overwritten.

## Operations

| Tool | Actual behavior |
| --- | --- |
| ترتيب الصفحات | A complete one-use permutation of all source pages. Nothing disappears when reordering. |
| استخراج صفحات | Check thumbnails or enter `1,3,5-8` (Arabic digits/commas accepted). The entered order is retained. One PDF by default. Separate mode validates every selected page, then atomically publishes a new `name extracted` folder containing `page-001.pdf`, etc. |
| حذف صفحات | Checked pages are dimmed for deletion. The main preview shows remaining pages/count. Removing every page is rejected and Save remains disabled. |
| دمج PDF | Reorder the dragged documents; internal page order of each document stays intact. One output beside the first dragged source, regardless of the chosen merge order. |
| ترقيم الصفحات | Start 0–1,000,000; 6–144 pt, `#RRGGBB` color, seven positions, four local fonts. Optional Arabic digits. All or selected pages; numbering advances in document order on selected pages only. |
| علامة مائية | Shaped Arabic/English/mixed text (up to 500 characters) or local PNG/JPG. Opacity, proportional size, rotation, seven positions and all/selected pages. An oversized rotated mark is rejected with an actionable error instead of silently clipping it. |
| تدوير صفحات | Selected pages +90° clockwise, −90° counterclockwise or 180°, added to existing rotation. Other pages, page content and boxes remain intact. |

Numbers and text marks use Windows WPF shaping/BiDi and **vector glyph outlines**.
This preserves connected Arabic without rasterizing a page or redistributing font
files. Added outline text is not searchable/selectable text. Original page text remains
text. Segoe UI, Arial, Tahoma and Times New Roman are local installed Windows fonts.
Positions follow the displayed CropBox and all four existing page rotations; MediaBox,
CropBox, different page sizes and original rotations are preserved.

Only the watermark image is normalized (sRGB, oriented, stripped, at most 2000×2000).
Its existing alpha and selected opacity are retained through an explicit RGBA PNG/soft
mask; original PDF image/vector resources are imported rather than replaced by bitmaps.

## Validation, limits and licensing

Every published PDF is reopened by independent PdfPig, its exact expected page count
checked, and every output page decoded by PDFium. Separate output folders remain private
until every member passes. Cancellation removes only files owned by this invocation.
Native parsing/rendering/save calls cannot be interrupted inside the call; tokens are
checked between reads/pages/stages. A large native call can delay cleanup/Exit.

Current limits: 1–20 distinct local documents, 200 total pages, 256 MB per source and
512 MB total; page dimensions 1–14,400 pt. Watermark input is PNG/JPG, ≤20 MB and existing
40 MP image limits, normalized to ≤4 MP. Fully local/available files only: network drives,
UNC and offline/recall cloud placeholders are refused, not deliberately hydrated.
Encrypted PDFs (including owner-only/empty-user-password encryption), corrupted PDFs,
interactive forms, signature permission dictionaries and optional-content layers are
refused safely. There is no password entry or document repair workflow in this phase.

This is a page-content editor, not a PDF/A/UA preservation or document-navigation editor.
Document-level bookmarks, tags, page labels and metadata are not promised to survive the
new document import; internal annotation destinations need manual checking. No OCR or
scanned-text editing is implied. Originals always remain available.

PDFsharp-WPF is pinned to **exactly 6.2.4**, whose [tagged license is MIT](https://github.com/empira/PDFsharp/blob/v6.2.4/LICENSE)
and whose [package metadata declares MIT](https://www.nuget.org/packages/PDFsharp-WPF/6.2.4).
Its license/copyright notice and Microsoft dependency licenses/notices are bundled under
`licenses/`. Do not upgrade blindly: [the current licensing page](https://docs.pdfsharp.net/General/License/License.html)
documents different terms beginning with 6.5. Existing PDFtoImage/PDFium/PdfPig notices
remain bundled. No runtime dependency download, cloud API, telemetry or network client
is introduced. Development NuGet restore uses the network; installed processing does not.
The package's unused WPFonts (embedded Segoe WP demo fonts), Snippets, Quality, Charting,
BarCodes and Cryptography/signing assemblies are excluded from build/publish. Only the
needed WPF/System/Shared PDFsharp libraries are redistributed; native editor fonts come
from Windows. The package verifier checks those exclusions as well as licensing files.

## Automated verification and draft previews

Eighteen focused groups in `tests/Naqla.Tests/PdfToolkitTests.cs` cover genuine single/
multi-page vector PDFs, exact ordering/counts, extraction/separate/delete/merge, numbering,
Arabic/English/image marks, all rotations/visible cropped positions, mixed sizes,
encryption/corruption, Arabic filenames, collisions, source hashes/locks, cancellation,
stale sessions, contextual queue dispatch and native editor Save gates. Reordered and
unselected pages are also compared by actual decoded pixels; original text remains
extractable and vector fixtures never acquire whole-page raster images.

Run `dotnet run --project tests/Naqla.Tests -c Release -- artifacts/pdf-tests --pdf-only`
into a **new** result directory. Full regression is run by `scripts/build.ps1` and
includes earlier image/media conversions, Smart Rename, optimizer, Ctrl state and wheel
rendering. A rendered 150% image is layout evidence, not physical high-DPI input evidence.

[Interactive design gallery](../preview/pdf-toolkit.html) switches real native-rendered
states at 100% size. These are WPF renderer artifacts, not physical desktop screenshots.
The approved [wheel design preview](../preview/index.html) remains separate and unchanged.

## Manual Windows acceptance — still required

Use disposable local PDFs. The package's `testing/pdf-fixtures/` (source repo:
`assets/pdf-test-files/`) contains actual vector `source-a.pdf` (4 mixed/rotated pages),
`source-b.pdf` (2 pages), an encrypted rejection sample, a transparent watermark and
`sources.json` with source SHA-256 hashes. Copy the folder to a writable test location.
No SDK is required. The fixture manifest explicitly does not claim Explorer acceptance.

1. Check tray build **0.7.0-pdfpreview.1**, mark a diagnostic attempt. Use actual Desktop
   and Explorer drags → Shift → Ctrl → a PDF tool. Confirm ordinary drag, Ctrl return and
   Escape still behave normally. Do not use a picker to substitute for the source drag.
2. Reorder A by dragging page 4 before 1, then try arrows: preview/save order **4,1,2,3**,
   count **4**. Open the new PDF in a local viewer; zoom into text/lines at 800%.
3. Extract `1,3-4`: count **3**, order **1,3,4**. Separate mode creates three PDFs in a
   new folder. Try `0`, `9`, `3-2`, `1,1`, `1,` and empty input: all rejected.
4. Delete `1,3`: remaining **2,4**, count **2**. Mark all four: Save must remain disabled.
5. Drag A+B together → Merge, move B before A: **6 pages**, labels **1,2,1,2,3,4** and
   original per-page size/rotation. Output remains beside first dragged A.
6. Number selected `1,3`, start **7**: page 1 gets 7, page 3 gets 8; 2/4 unchanged.
   Test each font, Arabic digits, size/color and top/bottom positions. Inspect every
   preview page, including already rotated pages. Repeat on a cropped PDF.
7. Watermark `نقلة · نسخة للمراجعة` and mixed Arabic/English. Test opacity/size/rotation/
   positions, all vs selected pages, then transparent PNG and JPG. Check connected Arabic,
   alpha and unchanged unselected content. Edit a field: remeasurement is required.
8. Rotate selected pages through +90/−90/180. Check accumulated rotation and all other
   page content. Rename a test source to an Arabic filename and repeat an operation.
9. Repeat Save with existing names and with an existing directory at the desired PDF
   name. Existing occupants survive and numbering advances. Compare original SHA-256
   using `Get-FileHash -LiteralPath '<source>' -Algorithm SHA256` against the copied manifest.
10. Cancel an editor/preview, cancel long saving, and Exit. No unconfirmed output or
    `.naqla-*` partial remains; original hashes match. Change a source after opening:
    preview/save must refuse it and require reopening.
11. Try encrypted/corrupt PDFs and a form/layer PDF: fail safely without an output.
    Test 100/125/150% actual DPI, resizing, screen edges, keyboard Tab/Space and monitors.
12. Repeat image conversions/tools, Smart Rename and Target Size from their guides;
    check singleton/tray Exit and Explorer restart. Test deliberately disconnected
    networking separately. None of these physical checks is inferred from automated tests.

Return the selected operation, ranges/settings, expected/actual count/order, time/attempt
marker, Windows/DPI and cropped editor/result screenshots. Include the matching local
`session-*.jsonl` from `%LOCALAPPDATA%\Naqla\Diagnostics`. Logs record typed Pdf* operations,
preview stages and numeric counts only; never names, paths, watermark text/image bytes or
raw exception messages. Hide private names in screenshots; original files are unnecessary.

**Explicit approval of the editor design/navigation and actual desktop validation remain
pending. Feature 4 stays unstarted until Phase 3 is completed and validated.**
