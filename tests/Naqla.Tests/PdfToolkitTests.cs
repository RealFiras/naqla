using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ImageMagick;
using Naqla.App;
using Naqla.Conversion;
using Naqla.Core;
using Naqla.Wheel;
using Naqla.Windows;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

internal static partial class Program
{
    private static readonly PdfToolkitService PdfTools = new();
    private static void RunPdfToolkitTests()
    {
        Run("PDF page ranges validate Arabic digits, bounds, duplicates and ordered extraction", PdfRanges);
        Run("Single-page PDF real preview/extract/rotate and vector content remain valid", PdfSingle);
        Run("PDF reorder retains every page exactly once and preserves text/vector streams", PdfReorder);
        Run("PDF extraction respects requested order and separate pages publish atomically", PdfExtract);
        Run("PDF deletion previews/saves remaining pages and rejects deleting everything", PdfDelete);
        Run("Multiple PDFs merge in document order, keep each internal order and mixed geometry", PdfMerge);
        Run("Page numbers honor start, position, size, colors, English/Arabic fonts and selected pages", PdfNumbers);
        Run("English/Arabic shaped text watermarks render offline with alpha/rotation/scale/position", PdfTextMarks);
        Run("Image watermark retains alpha, changes selected pages only and never rasterizes originals", PdfImageMarks);
        Run("PDF selected page rotation handles existing rotations, mixed sizes and all three angles", PdfRotate);
        Run("PDF numbering/watermark placement follows visible cropped pages at all four rotations", PdfOverlayGeometry);
        Run("PDF encrypted/corrupt/forms/layers are refused without changing originals", PdfUnsafeInputs);
        Run("PDF plan validation rejects omitted/duplicate/out-of-range pages and invalid settings", PdfInvalidPlans);
        Run("Arabic PDF output filenames stay beside originals, file/directory collisions never overwrite", PdfNames);
        Run("PDF pre/in-flight cancellation, source locks and stale-preview hashes preserve originals", PdfCancelAndStale);
        Run("PDF contextual tools/real queue honor every source and aggregate merge", PdfContextAndQueue);
        Run("Native PDF editor draft renders real thumbnails, all contexts, responsive sizes and stale Save gates", PdfNativeEditor);
        Run("PDF diagnostics retain closed operations/counts, never filenames or watermark text", PdfDiagnostics);
    }
    private static string PdfFixture(string name, int count = 3, bool mixed = false, bool rotated = false)
    {
        var directory = Path.Combine(folder, "pdf-fixtures"); Directory.CreateDirectory(directory); var path = Path.Combine(directory, name);
        // Independent vector fixture writer, standard Helvetica (no distributed system font).
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        var offsets = new List<long> { 0 };
        void Text(string value) => stream.Write(Encoding.ASCII.GetBytes(value));
        void Obj(string body) { offsets.Add(stream.Position); Text($"{offsets.Count - 1} 0 obj\n{body}\nendobj\n"); }
        Text("%PDF-1.4\n"); Obj("<< /Type /Catalog /Pages 2 0 R >>");
        Obj($"<< /Type /Pages /Count {count} /Kids [{string.Join(" ", Enumerable.Range(0, count).Select(i => $"{4 + 2 * i} 0 R"))}] >>");
        Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        for (var i = 1; i <= count; i++)
        {
            var width = mixed ? 360 + i * 30 : 420; var height = mixed ? 480 + i * 20 : 560; var angle = rotated && i % 2 == 0 ? 90 : 0;
            Obj($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Rotate {angle} /Resources << /Font << /F1 3 0 R >> >> /Contents {5 + (i - 1) * 2} 0 R >>");
            var rgb = (i % 3) switch { 0 => ".392 .584 .929", 1 => "1 .498 .314", _ => ".235 .702 .443" };
            var content = $"q 0 0 0 rg BT /F1 20 Tf 36 {height - 55} Td (PAGE-{i}) Tj ET {rgb} rg 36 {height - 177} 170 95 re f .28 .24 .55 RG 2 w 36 {height - 210} m 285 {height - 260} l S .18 .31 .31 rg BT /F1 12 Tf 36 {height - 300} Td (Naqla / vector page content) Tj ET Q\n";
            Obj($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream");
        }
        var xref = stream.Position; Text($"xref\n0 {offsets.Count}\n0000000000 65535 f \n"); foreach (var offset in offsets.Skip(1)) Text($"{offset:0000000000} 00000 n \n");
        Text($"trailer\n<< /Root 1 0 R /Size {offsets.Count} >>\nstartxref\n{xref}\n%%EOF\n"); return path;
    }
    private static PdfSession PdfOpen(params string[] files) => PdfTools.OpenAsync(files.Select(FileClassifier.Inspect).ToArray()).GetAwaiter().GetResult();
    private static PdfEditPlan PdfPlan(PdfSession session, PdfTool tool, params int[] pages) => new(session, tool, pages.Length == 0 ? Enumerable.Range(1, session.PageCount).ToArray() : pages);
    private static ConversionResult PdfRun(PdfEditPlan plan) => PdfTools.ExecuteAsync(plan).GetAwaiter().GetResult();
    private static void PdfOrder(string path, params int[] expected)
    {
        using var doc = UglyToad.PdfPig.PdfDocument.Open(path);
        Check(doc.NumberOfPages == expected.Length, "Wrong real PDF page count");
        for (var i = 0; i < expected.Length; i++) { var page = doc.GetPage(i + 1); Check(page.Text.Contains($"PAGE-{expected[i]}"), "Wrong real page ordering/text"); Check(page.NumberOfImages == 0, "Vector page was flattened into an image"); }
        PdfToolkitService.Validate(path, expected.Length);
    }
    private static void PdfMustFail(Action action)
    {
        var failed = false; try { action(); } catch (Exception e) when (e is ArgumentException or InvalidDataException or NotSupportedException or IOException or OperationCanceledException or FormatException or OverflowException) { failed = true; }
        Check(failed, "Invalid PDF operation unexpectedly succeeded");
    }
    private static void PdfRanges()
    {
        Check(PdfPageRanges.Parse("1,3,5-8", 8).SequenceEqual(new[] { 1, 3, 5, 6, 7, 8 }), "Ranges");
        Check(PdfPageRanges.Parse("٣،١-٢", 3).SequenceEqual(new[] { 3, 1, 2 }), "Arabic/order");
        foreach (var text in new[] { "", "0", "4", "3-2", "1,1", "1-2,2", "1,", "1-", "-1", "1--2", "1.2", "1;2" }) PdfMustFail(() => PdfPageRanges.Parse(text, 3));
    }
    private static void PdfSingle()
    {
        var source = PdfFixture("صفحة واحدة.pdf", 1); var hash = HashSources(new[] { source }); var session = PdfOpen(source);
        var preview = PdfTools.PreviewAsync(PdfPlan(session, PdfTool.Reorder)).GetAwaiter().GetResult(); using var image = new MagickImage(preview.Png);
        Check(preview.Pages == 1 && image.Height > image.Width, "Real single-page/aspect preview");
        PdfOrder(PdfRun(PdfPlan(session, PdfTool.Extract, 1)).OutputPath, 1); PdfOrder(PdfRun(PdfPlan(session, PdfTool.Rotate, 1)).OutputPath, 1);
        PdfMustFail(() => PdfPlan(session, PdfTool.Delete, 1).Validate()); OriginalsUnchanged(hash);
    }
    private static void PdfReorder()
    {
        var source = PdfFixture("ترتيب.pdf", 4, true, true); var hash = HashSources(new[] { source }); var session = PdfOpen(source);
        var result = PdfRun(PdfPlan(session, PdfTool.Reorder, 4, 2, 1, 3)); PdfOrder(result.OutputPath, 4, 2, 1, 3);
        using var original = PdfReader.Open(source, PdfDocumentOpenMode.Import); using var output = PdfReader.Open(result.OutputPath, PdfDocumentOpenMode.Import);
        foreach (var (old, index) in new[] { (3, 0), (1, 1), (0, 2), (2, 3) })
        { Check(original.Pages[old].MediaBox == output.Pages[index].MediaBox && original.Pages[old].Rotate == output.Pages[index].Rotate, "Page geometry/rotation changed"); Check(PdfRendered(source, old).SequenceEqual(PdfRendered(result.OutputPath, index)), "Imported page pixels/content changed"); }
        OriginalsUnchanged(hash);
    }
    private static void PdfExtract()
    {
        var source = PdfFixture("استخراج.pdf", 5); var session = PdfOpen(source); var hashes = HashSources(new[] { source });
        PdfOrder(PdfRun(PdfPlan(session, PdfTool.Extract, 5, 1, 3)).OutputPath, 5, 1, 3);
        var separate = PdfRun(PdfPlan(session, PdfTool.Extract, 5, 1) with { Separate = true });
        Check(Directory.Exists(separate.OutputPath) && Directory.GetFiles(separate.OutputPath, "*.pdf").Length == 2, "Separate page folder/count");
        PdfOrder(Path.Combine(separate.OutputPath, "page-005.pdf"), 5); PdfOrder(Path.Combine(separate.OutputPath, "page-001.pdf"), 1);
        Check(PdfRun(PdfPlan(session, PdfTool.Extract, 1) with { Separate = true }).OutputPath != separate.OutputPath, "Separate directory collision"); OriginalsUnchanged(hashes);
    }
    private static void PdfDelete()
    {
        var source = PdfFixture("حذف.pdf", 4); var session = PdfOpen(source); var plan = PdfPlan(session, PdfTool.Delete, 1, 3);
        var preview = PdfTools.PreviewAsync(plan).GetAwaiter().GetResult(); Check(preview.Pages == 2, "Deletion preview shows removed pages");
        var hashes = HashSources(new[] { source }); PdfOrder(PdfRun(plan).OutputPath, 2, 4); PdfMustFail(() => PdfPlan(session, PdfTool.Delete).Validate()); OriginalsUnchanged(hashes);
    }
    private static void PdfMerge()
    {
        var a = PdfFixture("دمج أول.pdf", 2, true); var b = PdfFixture("دمج ثان.pdf", 3, true, true); var session = PdfOpen(a, b); var hashes = HashSources(new[] { a, b });
        var plan = new PdfEditPlan(session, PdfTool.Merge, [], DocumentOrder: new[] { 1, 0 }); var result = PdfRun(plan);
        PdfOrder(result.OutputPath, 1, 2, 3, 1, 2); Check(Path.GetDirectoryName(result.OutputPath) == Path.GetDirectoryName(a), "Merge not beside first dragged source");
        OriginalsUnchanged(hashes);
    }
    private static byte[] PdfRendered(string path, int page)
    {
        using var input = File.OpenRead(path); using var image = PDFtoImage.Conversion.ToImage(input, page: page, options: new(Dpi: 72)); using var png = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100); return png.ToArray();
    }
    private static int PdfChangedPixels(byte[] before, byte[] after)
    {
        using var a = new MagickImage(before); using var b = new MagickImage(after); Check(a.Width == b.Width && a.Height == b.Height, "Unexpected raster dimensions");
        var first = a.ToByteArray(MagickFormat.Rgb); var second = b.ToByteArray(MagickFormat.Rgb); var count = 0;
        for (var i = 0; i < first.Length; i += 3) if (Math.Abs(first[i] - second[i]) + Math.Abs(first[i + 1] - second[i + 1]) + Math.Abs(first[i + 2] - second[i + 2]) > 30) count++;
        return count;
    }
    private static void PdfNumbers()
    {
        var source = PdfFixture("ترقيم.pdf", 3, true, true); var session = PdfOpen(source); var hashes = HashSources(new[] { source });
        foreach (var face in new[] { "Segoe UI", "Arial", "Tahoma", "Times New Roman" })
        {
            var plan = PdfPlan(session, PdfTool.Numbers, 1, 3) with { Numbers = new(7, 22, "#AA3311", face, PdfPosition.TopRight, face == "Tahoma") };
            var result = PdfRun(plan); PdfOrder(result.OutputPath, 1, 2, 3);
            Check(PdfChangedPixels(PdfRendered(source, 0), PdfRendered(result.OutputPath, 0)) > 10, "Numbers invisible");
            Check(PdfRendered(source, 1).SequenceEqual(PdfRendered(result.OutputPath, 1)), "Unselected page changed");
        }
        foreach (var pos in Enum.GetValues<PdfPosition>())
        {
            var result = PdfRun(PdfPlan(session, PdfTool.Numbers) with { Numbers = new(42, 16, "#009040", Position: pos) });
            Check(PdfChangedPixels(PdfRendered(source, 0), PdfRendered(result.OutputPath, 0)) > 10, "Position number invisible");
        }
        var ascending = PdfRun(PdfPlan(session, PdfTool.Numbers, 1, 3) with { Numbers = new(78) });
        var reverseSelection = PdfRun(PdfPlan(session, PdfTool.Numbers, 3, 1) with { Numbers = new(78) });
        Check(PdfRendered(ascending.OutputPath, 0).SequenceEqual(PdfRendered(reverseSelection.OutputPath, 0)) && PdfRendered(ascending.OutputPath, 2).SequenceEqual(PdfRendered(reverseSelection.OutputPath, 2)), "Numbering must follow document order, not range-entry order");
        OriginalsUnchanged(hashes);
    }
    private static void PdfTextMarks()
    {
        var source = PdfFixture("علامات عربية.pdf", 2); var session = PdfOpen(source); var hashes = HashSources(new[] { source });
        foreach (var (text, name) in new[] { ("CONFIDENTIAL 2026", "english"), ("نقلة · نسخة للمراجعة", "arabic") })
        {
            var plan = PdfPlan(session, PdfTool.Watermark, 1) with { Watermark = new(text, Opacity: .35, Scale: .55, Rotation: -30, Color: "#7744CC") };
            var result = PdfRun(plan); PdfOrder(result.OutputPath, 1, 2);
            Check(PdfChangedPixels(PdfRendered(source, 0), PdfRendered(result.OutputPath, 0)) > 100, "Watermark invisible");
            Check(PdfRendered(source, 1).SequenceEqual(PdfRendered(result.OutputPath, 1)), "Unselected watermark page changed");
            File.WriteAllBytes(Path.Combine(folder, $"pdf-watermark-{name}.png"), PdfRendered(result.OutputPath, 0));
        }
        foreach (var pos in Enum.GetValues<PdfPosition>()) PdfRun(PdfPlan(session, PdfTool.Watermark) with { Watermark = new("عربي English", Opacity: .1, Scale: .2, Rotation: 0, Position: pos) });
        OriginalsUnchanged(hashes);
    }
    private static void PdfImageMarks()
    {
        var source = PdfFixture("علامة صورة.pdf", 2); var session = PdfOpen(source); var image = CreatePng("pdf-mark.png");
        var bytes = PdfTools.LoadWatermarkImageAsync(image).GetAwaiter().GetResult(); var result = PdfRun(PdfPlan(session, PdfTool.Watermark, 2) with { Watermark = new(Image: bytes, Opacity: .4, Scale: .25, Rotation: 25) });
        using var doc = UglyToad.PdfPig.PdfDocument.Open(result.OutputPath); Check(doc.NumberOfPages == 2 && doc.GetPage(2).NumberOfImages == 1 && doc.GetPage(2).Text.Contains("PAGE-2"), "Image mark/source vector missing");
        Check(PdfRendered(source, 0).SequenceEqual(PdfRendered(result.OutputPath, 0)), "Unselected image-mark page changed");
        Check(PdfChangedPixels(PdfRendered(source, 1), PdfRendered(result.OutputPath, 1)) > 20, "Image watermark invisible");
        File.WriteAllBytes(Path.Combine(folder, "pdf-watermark-image.png"), PdfRendered(result.OutputPath, 1));
    }
    private static void PdfRotate()
    {
        var source = PdfFixture("تدوير.pdf", 3, true, true); var session = PdfOpen(source); var hashes = HashSources(new[] { source });
        foreach (var angle in new[] { 90, -90, 180 })
        {
            var result = PdfRun(PdfPlan(session, PdfTool.Rotate, 2) with { Rotation = angle }); PdfOrder(result.OutputPath, 1, 2, 3);
            using var doc = PdfReader.Open(result.OutputPath, PdfDocumentOpenMode.Import);
            Check(doc.Pages[1].Rotate == ((90 + angle) % 360 + 360) % 360 && doc.Pages[0].Rotate == 0 && doc.Pages[2].Rotate == 0, "Rotation ignored existing orientation or changed other pages");
            Check(PdfRendered(source, 0).SequenceEqual(PdfRendered(result.OutputPath, 0)), "Other page pixels changed");
        }
        OriginalsUnchanged(hashes);
    }
    private static void PdfUnsafeInputs()
    {
        foreach (var ownerOnly in new[] { false, true })
        {
            var path = Path.Combine(folder, $"encrypted-{ownerOnly}.pdf"); using (var doc = new PdfDocument()) { doc.AddPage(); doc.SecuritySettings.OwnerPassword = "owner"; if (!ownerOnly) doc.SecuritySettings.UserPassword = "user"; doc.Save(path); }
            var hashes = HashSources(new[] { path }); PdfMustFail(() => PdfOpen(path)); OriginalsUnchanged(hashes);
        }
        foreach (var name in new[] { "broken.pdf", "disguised.pdf" }) { var path = Path.Combine(folder, name); File.WriteAllText(path, name.StartsWith("broken") ? "%PDF-1.7\ntruncated" : "not a PDF"); PdfMustFail(() => PdfOpen(path)); }
        foreach (var key in new[] { "/AcroForm", "/OCProperties", "/Perms" })
        { var path = Path.Combine(folder, key[1..] + ".pdf"); using (var doc = new PdfDocument()) { doc.AddPage(); doc.Internals.Catalog.Elements[key] = new PdfDictionary(doc); doc.Save(path); } PdfMustFail(() => PdfOpen(path)); }
    }
    private static void PdfOverlayGeometry()
    {
        foreach (var rotation in new[] { 0, 90, 180, 270 })
        {
            var path = Path.Combine(folder, $"crop-rotation-{rotation}.pdf");
            using (var doc = new PdfDocument())
            {
                var page = doc.AddPage(); page.Width = XUnit.FromPoint(420); page.Height = XUnit.FromPoint(560);
                page.CropBox = new PdfRectangle(new XPoint(40, 60), new XPoint(380, 500)); page.Rotate = rotation; doc.Save(path);
            }
            var session = PdfOpen(path); var result = PdfRun(PdfPlan(session, PdfTool.Numbers) with { Numbers = new(78, 24, "#9900CC", Position: PdfPosition.BottomRight) });
            using var rendered = new MagickImage(PdfRendered(result.OutputPath, 0)); var bytes = rendered.ToByteArray(MagickFormat.Rgb);
            var points = new List<(int X, int Y)>();
            for (var y = 0; y < rendered.Height; y++) for (var x = 0; x < rendered.Width; x++) { var i = ((int)y * (int)rendered.Width + (int)x) * 3; if (bytes[i] > 80 && bytes[i + 1] < 80 && bytes[i + 2] > 120) points.Add((x, y)); }
            Check(points.Count > 10 && points.Average(p => p.X) > rendered.Width * .65 && points.Average(p => p.Y) > rendered.Height * .75, $"Number not bottom-right of visible crop at rotation {rotation}");
            using var output = PdfReader.Open(result.OutputPath, PdfDocumentOpenMode.Import); Check(output.Pages[0].CropBox == new PdfRectangle(new XPoint(40, 60), new XPoint(380, 500)) && output.Pages[0].Rotate == rotation, "Overlay changed crop/rotation");
        }
    }
    private static void PdfInvalidPlans()
    {
        var session = PdfOpen(PdfFixture("bad-plans.pdf", 3));
        foreach (var pages in new[] { new[] { 1, 2 }, new[] { 1, 1, 3 }, new[] { 0, 2, 3 }, new[] { 1, 2, 4 }, Array.Empty<int>() }) PdfMustFail(() => new PdfEditPlan(session, PdfTool.Reorder, pages).Validate());
        PdfMustFail(() => (PdfPlan(session, PdfTool.Rotate) with { Rotation = 45 }).Validate());
        foreach (var n in new[] { new PdfNumbering(-1), new PdfNumbering(Size: double.NaN), new PdfNumbering(Color: "red"), new PdfNumbering(Font: "remote://font") }) PdfMustFail(() => (PdfPlan(session, PdfTool.Numbers) with { Numbers = n }).Validate());
        foreach (var mark in new[] { new PdfWatermark(""), new PdfWatermark(Opacity: 0), new PdfWatermark(Scale: 2), new PdfWatermark(Rotation: double.NaN) }) PdfMustFail(() => (PdfPlan(session, PdfTool.Watermark) with { Watermark = mark }).Validate());
        PdfMustFail(() => PdfTools.ExecuteAsync(PdfPlan(session, PdfTool.Numbers)).GetAwaiter().GetResult());
    }
    private static void PdfNames()
    {
        var source = PdfFixture("أسماء عربية — ملف.pdf", 2); var session = PdfOpen(source); var hashes = HashSources(new[] { source });
        var occupied = Path.Combine(Path.GetDirectoryName(source)!, Path.GetFileNameWithoutExtension(source) + " reorder.pdf"); File.WriteAllText(occupied, "preserve sentinel"); Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(source)!, Path.GetFileNameWithoutExtension(source) + " reorder (1).pdf"));
        var result = PdfRun(PdfPlan(session, PdfTool.Reorder)); Check(result.OutputPath.EndsWith(" (2).pdf") && File.ReadAllText(occupied) == "preserve sentinel", "Collision overwrite");
        PdfOrder(result.OutputPath, 1, 2); OriginalsUnchanged(hashes);
    }
    private static void PdfCancelAndStale()
    {
        var source = PdfFixture("cancel-pages.pdf", 8); var session = PdfOpen(source); var hashes = HashSources(new[] { source }); var plan = PdfPlan(session, PdfTool.Reorder);
        using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); PdfMustFail(() => PdfTools.ExecuteAsync(plan, token: cancel.Token).GetAwaiter().GetResult()); }
        using (var cancel = new CancellationTokenSource())
        {
            var locked = false; var progress = new InlineProgress<PdfEditProgress>(p => { if (p.Completed == 1) { try { using var writer = new FileStream(source, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); } catch (IOException) { locked = true; } cancel.Cancel(); } });
            PdfMustFail(() => PdfTools.ExecuteAsync(plan, progress, cancel.Token).GetAwaiter().GetResult()); Check(locked, "Original not locked against writers during processing");
        }
        using (var cancel = new CancellationTokenSource()) PdfMustFail(() => PdfTools.ExecuteAsync(PdfPlan(session, PdfTool.Extract, 1, 2, 3) with { Separate = true }, new InlineProgress<PdfEditProgress>(_ => cancel.Cancel()), cancel.Token).GetAwaiter().GetResult());
        Check(!Directory.Exists(Path.Combine(Path.GetDirectoryName(source)!, "cancel-pages extracted")), "Partial separate pages published"); OriginalsUnchanged(hashes); NoBatchTemps(Path.GetDirectoryName(source)!);
        File.AppendAllText(source, "\n% source changed"); PdfMustFail(() => PdfTools.PreviewAsync(plan).GetAwaiter().GetResult()); PdfMustFail(() => PdfRun(plan));
    }
    private static void PdfContextAndQueue()
    {
        var source = PdfFixture("context-a.pdf", 2); var other = PdfFixture("context-b.pdf", 1); var converter = new LocalConversionService(renamer: RenameService());
        var one = FileDragPayload.Inspect(new[] { source }); var two = FileDragPayload.Inspect(new[] { source, other });
        var single = WheelOptions.For(one, WheelMode.Tools, converter.CanExecute); Check(single.Count == 8 && single.Any(x => x.Id == "TXT") && single.Any(x => x.Id == "RENAME") && !single.Any(x => x.Id == "PDFMERGE"), "Single PDF tools/context regression");
        var multi = WheelOptions.For(two, WheelMode.Tools, converter.CanExecute); Check(multi.Select(x => x.Id).SequenceEqual(new[] { "TXT", "RENAME", "PDFMERGE" }), "Multi PDF context must expose merge with retained actions");
        var session = PdfOpen(source, other); var plan = new PdfEditPlan(session, PdfTool.Merge, [], DocumentOrder: new[] { 1, 0 });
        var result = new BatchProcessor(converter).RunAsync(new(two.Files, "PDFMERGE", Pdf: plan)).GetAwaiter().GetResult(); Check(result.Succeeded == 1 && result.Jobs.Count == 1, "PDF merge was split into unrelated jobs"); PdfOrder(result.Jobs[0].Output!.OutputPath, 1, 1, 2);
        PdfMustFail(() => converter.ConvertAsync(new ConversionRequest(one.Files, "PDFREORDER")).GetAwaiter().GetResult());
        foreach (var payload in new[] { one, two })
        {
            var wheel = new PixelWheel(converter.CanExecute); wheel.SetSelection(payload); wheel.SetMode(true, false); wheel.Measure(new Size(260, 306)); wheel.Arrange(new Rect(0, 0, 260, 306)); wheel.UpdateLayout(); SaveRender(wheel, 260, 306, $"native-pdf-wheel-{payload.Files.Count}.png");
        }
    }
    private static void PdfNativeEditor()
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var source = PdfFixture("editor-preview.pdf", 4, true, true); var session = PdfOpen(source); var converter = new LocalConversionService(); var queue = new BatchProcessor(converter);
        var editor = new PdfToolkitWindow(session, PdfTool.Reorder, PdfTools, queue); Check(!editor.CanSave, "PDF save before preview"); Pump(editor.InitializeAsync()); Check(editor.CanSave, "Real editor preview didn't enable Save: " + editor.StatusText);
        var content = (FrameworkElement)editor.Content;
        void Render(string name, int width = 968, int height = 640) { content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout(); SaveRender(content, width, height, name); }
        Render("native-pdf-editor-reorder.png"); editor.MoveTile(4, 0); Check(!editor.CanSave, "Stale reorder preview allowed Save"); Pump(editor.PreviewCurrentAsync()); Check(editor.Plan!.Pages.SequenceEqual(new[] { 4, 1, 2, 3 }), "Editor drag model failed");
        foreach (var mode in new[] { PdfTool.Extract, PdfTool.Delete, PdfTool.Numbers, PdfTool.Watermark, PdfTool.Rotate })
        {
            editor.SetTool(mode); Pump(editor.PreviewCurrentAsync()); Check(editor.CanSave, "Editor tool preview failed: " + mode + " " + editor.StatusText); Render($"native-pdf-editor-{mode.ToString().ToLowerInvariant()}.png");
        }
        editor.SetTool(PdfTool.Delete); Elements(content).OfType<TextBox>().Single(x => x.Name == "PdfRanges").Text = "1-4"; Pump(editor.PreviewCurrentAsync()); Check(!editor.CanSave, "Delete all UI enabled save");
        editor.SetTool(PdfTool.Watermark); Pump(editor.PreviewCurrentAsync()); Render("native-pdf-editor-compact.png", 688, 440);
        Render("native-pdf-editor-hidpi-layout.png");
        var scaled = new System.Windows.Media.Imaging.RenderTargetBitmap(1452, 960, 144, 144, PixelFormats.Pbgra32); scaled.Render(content); var encoded = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoded.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(scaled)); using (var output = File.Create(Path.Combine(folder, "native-pdf-editor-150percent-render.png"))) encoded.Save(output);
        editor.Close(); Pump(editor.PendingWork); Check(!editor.CanSave, "Closing PDF editor retained confirmation");
        var other = PdfFixture("editor-merge.pdf", 2); var merge = new PdfToolkitWindow(PdfOpen(source, other), PdfTool.Merge, PdfTools, queue); Pump(merge.InitializeAsync()); merge.MoveTile(1, 0); Pump(merge.PreviewCurrentAsync()); Check(merge.CanSave && merge.Plan!.DocumentOrder!.SequenceEqual(new[] { 1, 0 }), "Editor document order failed");
        var mergeContent = (FrameworkElement)merge.Content; mergeContent.Measure(new Size(968, 640)); mergeContent.Arrange(new Rect(0, 0, 968, 640)); mergeContent.UpdateLayout(); SaveRender(mergeContent, 968, 640, "native-pdf-editor-merge.png"); merge.Close();
        // Exercise the actual dialog confirmation event offscreen, not physical input.
        var confirmed = new PdfToolkitWindow(session, PdfTool.Extract, PdfTools, queue) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false };
        Pump(confirmed.InitializeAsync());
        _ = confirmed.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => Elements((DependencyObject)confirmed.Content).OfType<PixelButton>().Single(x => Equals(x.Content, "حفظ نسخة")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
        Check(confirmed.ShowDialog() == true && confirmed.AcceptedPlan is not null, "Confirmed plan was lost when the dialog closed");
        Pump(confirmed.PendingWork); PdfOrder(PdfRun(confirmed.AcceptedPlan!).OutputPath, 1);
        SynchronizationContext.SetSynchronizationContext(null);
    }
    private static void PdfDiagnostics()
    {
        var directory = Path.Combine(folder, "pdf-diagnostics"); string path;
        using (var log = new DiagnosticLog(directory))
        {
            path = log.CurrentFilePath!;
            foreach (var tool in Enum.GetValues<PdfTool>()) log.Write(DiagnosticEvent.PdfPreviewReady, new() { Operation = DiagnosticData.OperationOf(PdfToolIds.Id(tool)), Count = 3 });
            log.Write(DiagnosticEvent.PdfPreviewFailed, DiagnosticData.FromException(new IOException("SECRET-WATERMARK C:\\private\\name.pdf")));
        }
        var text = File.ReadAllText(path); Check(!text.Contains("SECRET") && !text.Contains("private") && text.Contains("PdfWatermark"), "PDF privacy regression");
    }
}
