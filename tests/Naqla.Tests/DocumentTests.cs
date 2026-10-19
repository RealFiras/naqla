using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Naqla.Conversion;
using Naqla.Core;

internal static partial class Program
{
    private static void RunDocumentTests()
    {
        Run("Offline document real-handler matrix: Arabic/English, table/image, multipage, DOCX/PDF/TXT/RTF/HTML/ODT", DocumentMatrix);
        Run("Document active HTML/external resources/macros/corruption/cancellation are rejected", DocumentGuards);
    }
    private static LocalConversionService OfficeService() => new(officeDirectory: Path.Combine(Components, "office"));
    private static void DocumentMatrix()
    {
        var service = OfficeService(); Check(service.Documents.Available, "Optional LibreOffice component is absent");
        var directory = Path.Combine(folder, "document-matrix"); Directory.CreateDirectory(directory);
        var html = Path.Combine(directory, "مستند المصدر.html"); var png = CreatePng("document-image.png");
        File.WriteAllText(html, "<!doctype html><html lang='ar'><meta charset='utf-8'><body style='font-family:Arial'><h1>NAQLA-OFFLINE-REFERENCE</h1><p dir='rtl'>مرحبا بالعالم نقلة</p><table border='1'><tr><td>First cell</td><td>خلية عربية</td></tr><tr><td>42</td><td>84</td></tr></table><img width='80' height='80' src='data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(png)) + "'><p style='page-break-before:always'>SECOND-PAGE-REFERENCE</p></body></html>");
        var sources = new Dictionary<string, string> { ["HTML"] = html };
        foreach (var target in new[] { "DOCX", "ODT", "RTF", "TXT" })
        {
            var output = service.ConvertAsync(FileClassifier.Inspect(html), target).GetAwaiter().GetResult();
            var source = Path.Combine(directory, "source-" + target + "." + target.ToLowerInvariant()); File.Copy(output.OutputPath, source); sources[target] = source;
        }
        var rows = new List<object>();
        foreach (var (inputFormat, source) in sources)
        foreach (var target in new[] { "DOCX", "PDF", "TXT", "RTF", "HTML", "ODT" }.Where(x => x != inputFormat))
        {
            var clock = Stopwatch.StartNew(); var hash = SHA256.HashData(File.ReadAllBytes(source));
            try
            {
                var output = service.ConvertAsync(FileClassifier.Inspect(source), target).GetAwaiter().GetResult();
                if (target == "PDF")
                {
                    using var pdf = UglyToad.PdfPig.PdfDocument.Open(output.OutputPath); Check(pdf.NumberOfPages >= (inputFormat == "TXT" ? 1 : 2), "Actual multipage PDF output missing");
                    var text = string.Join(' ', pdf.GetPages().Select(x => x.Text)); Check(text.Contains("NAQLA-OFFLINE-REFERENCE") && text.Contains("SECOND-PAGE-REFERENCE"), "PDF text references missing");
                    if (inputFormat != "TXT") Check(pdf.GetPages().Sum(x => x.NumberOfImages) > 0, "Document image was lost");
                }
                else if (target is "DOCX" or "ODT")
                {
                    using var zip = ZipFile.OpenRead(output.OutputPath); using var read = zip.GetEntry(target == "DOCX" ? "word/document.xml" : "content.xml")!.Open(); var xml = XDocument.Load(read);
                    Check(xml.Root!.Value.Contains("NAQLA-OFFLINE-REFERENCE") && xml.Root.Value.Contains("مرحبا"), "Document text/Arabic missing");
                    if (inputFormat != "TXT") Check(xml.Descendants().Any(x => x.Name.LocalName is "tbl" or "table") && zip.Entries.Any(x => x.FullName.StartsWith("word/media/") || x.FullName.StartsWith("Pictures/")), "Table/image package content missing");
                }
                else if (target == "TXT") { var text = File.ReadAllText(output.OutputPath); Check(text.Contains("مرحبا") && text.Contains("SECOND-PAGE-REFERENCE"), "Actual TXT content missing"); }
                else if (target == "HTML") { var text = File.ReadAllText(Path.Combine(output.OutputPath, "index.html")); Check(text.Contains("NAQLA-OFFLINE-REFERENCE"), "Actual HTML content missing"); }
                else Check(File.ReadAllText(output.OutputPath).Contains("NAQLA-OFFLINE-REFERENCE"), "Actual RTF content missing");
                Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "Document original hash changed");
                rows.Add(new { Source = inputFormat, Destination = target, Status = "PASSED", Handler = "LocalConversionService → DocumentService → LibreOffice", Milliseconds = clock.ElapsedMilliseconds, OriginalHashPreserved = true, NativeOfficeGui = "MANUAL PENDING" });
            }
            catch (Exception error) { rows.Add(new { Source = inputFormat, Destination = target, Status = "FAILED", Error = error.GetType().Name, Detail = error.Message, Milliseconds = clock.ElapsedMilliseconds }); }
            File.WriteAllText(Path.Combine(folder, "document-matrix.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        Check(!rows.Any(x => JsonSerializer.Serialize(x).Contains("\"Status\":\"FAILED\"")), "Document conversion matrix has failures; inspect document-matrix.json");
    }
    private static void DocumentGuards()
    {
        var service = OfficeService(); var n = 0;
        foreach (var text in new[] { "<html><img src='https://example.invalid/private.png'></html>", "<html><script>alert(1)</script></html>", "<style>body{background:u\\72l(https://example.invalid/a)}</style>", "<html><input type='image' src='file:///C:/private.png'></html>" })
        {
            var path = Path.Combine(folder, $"unsafe-doc-{n++}.html"); File.WriteAllText(path, text); var rejected = false;
            try { service.ConvertAsync(FileClassifier.Inspect(path), "PDF").GetAwaiter().GetResult(); } catch (NotSupportedException) { rejected = true; } Check(rejected, "Unsafe document resource accepted");
        }
        var corrupt = Path.Combine(folder, "corrupt-document.docx"); File.WriteAllText(corrupt, "invalid ZIP document"); var corruptRejected = false;
        try { service.ConvertAsync(FileClassifier.Inspect(corrupt), "PDF").GetAwaiter().GetResult(); } catch (InvalidDataException) { corruptRejected = true; } Check(corruptRejected, "Corrupt DOCX accepted");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var cancelled = false;
        try { service.ConvertAsync(FileClassifier.Inspect(corrupt), "PDF", cancellation.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; } Check(cancelled, "Document cancellation ignored");
        Check(!Directory.GetDirectories(folder, ".naqla-*").Any(), "Document failure left temporary output");
    }
}
