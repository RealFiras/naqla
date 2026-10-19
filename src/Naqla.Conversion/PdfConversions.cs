using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using ImageMagick;
using Naqla.Core;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Naqla.Conversion;

internal static class PdfConversions
{
    internal static readonly object PdfiumGate = new();
    public static ConversionResult Convert(DraggedFile file, string target, CancellationToken token, int dpi = 144)
    {
        if (dpi is < 72 or > 300) throw new ArgumentException("اختر دقة PDF من 72 إلى 300 DPI");
        using var document = PdfDocument.Open(file.FullPath);
        if (document.NumberOfPages is < 1 or > 200) throw new NotSupportedException("PDF conversion is limited to 200 pages in this development build.");
        if (target is "PNG" or "JPG") return RenderPages(file, target, document, token, dpi);
        var pages = new List<string>();
        foreach (var page in document.GetPages()) { token.ThrowIfCancellationRequested(); pages.Add(ContentOrderTextExtractor.GetText(page)); }
        if (pages.All(string.IsNullOrWhiteSpace)) throw new NotSupportedException("This PDF has no extractable text. Install the local OCR pack and choose OCR in Tools for scanned pages.");
        var text = string.Join("\n\f\n", pages);
        using var output = new OutputFile(file.FullPath, target);
        if (target == "TXT") File.WriteAllText(output.Temporary, text, new UTF8Encoding(false));
        else if (target == "RTF") File.WriteAllText(output.Temporary, Rtf(pages), Encoding.ASCII);
        else WriteDocx(output.Temporary, pages);
        if (target == "DOCX")
        {
            using var archive = ZipFile.OpenRead(output.Temporary);
            using var content = archive.GetEntry("word/document.xml")!.Open();
            if (XDocument.Load(content).Root is null) throw new InvalidDataException("DOCX verification failed.");
        }
        else if (new FileInfo(output.Temporary).Length == 0) throw new InvalidDataException("Text output verification failed.");
        var bytes = new FileInfo(output.Temporary).Length;
        return new(output.Publish(token), 0, 0, pages.Count, Report: new(file.Size, bytes, "Native PDF text extraction. Original layout, images and tables are not reconstructed; scanned pages require the separate local OCR tool."));
    }
    private static ConversionResult RenderPages(DraggedFile file, string target, PdfDocument document, CancellationToken token, int dpi)
    {
        foreach (var page in document.GetPages())
            if (page.Width * page.Height * Math.Pow(dpi / 72d, 2) > 40_000_000) throw new NotSupportedException("A PDF page exceeds the 40-megapixel rendering limit at this resolution.");
        if (document.NumberOfPages == 1)
        {
            using var output = new OutputFile(file.FullPath, target);
            Render(file.FullPath, output.Temporary, target, 0, token, dpi);
            using var check = new MagickImage(output.Temporary);
            return new(output.Publish(token), (int)check.Width, (int)check.Height);
        }
        var directory = Path.GetDirectoryName(file.FullPath)!;
        var temporary = Path.Combine(directory, $".naqla-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            for (var i = 0; i < document.NumberOfPages; i++)
            {
                var path = Path.Combine(temporary, $"page-{i + 1:000}.{target.ToLowerInvariant()}");
                Render(file.FullPath, path, target, i, token, dpi);
                using var check = new MagickImage(path);
                if (check.Width == 0) throw new InvalidDataException("Page decode verification failed.");
            }
            var stem = Path.GetFileNameWithoutExtension(file.FullPath) + " " + target;
            for (var i = 0; i < 10000; i++)
            {
                token.ThrowIfCancellationRequested();
                var destination = Path.Combine(directory, stem + (i == 0 ? "" : $" ({i})"));
                try { Directory.Move(temporary, destination); return new(destination, 0, 0, document.NumberOfPages); }
                catch (IOException) when (Directory.Exists(destination) || File.Exists(destination)) { }
            }
            throw new IOException("No available output folder name was found.");
        }
        finally
        {
            // This exact random directory was created by this call; never enumerate source siblings for cleanup.
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }
    private static void Render(string source, string destination, string target, int page, CancellationToken token, int dpi)
    {
        token.ThrowIfCancellationRequested();
        lock (PdfiumGate)
        {
            token.ThrowIfCancellationRequested();
            using var input = File.OpenRead(source);
            if (target == "PNG") PDFtoImage.Conversion.SavePng(destination, input, page: page, options: new(Dpi: dpi));
            else PDFtoImage.Conversion.SaveJpeg(destination, input, page: page, options: new(Dpi: dpi));
        }
        token.ThrowIfCancellationRequested();
    }
    internal static void Verify(string path, int expectedPages = 1, CancellationToken token = default)
    {
        using var document = PdfDocument.Open(path);
        if (document.NumberOfPages != expectedPages) throw new InvalidDataException("PDF verification failed.");
        lock (PdfiumGate)
        {
            using var input = File.OpenRead(path);
            var count = 0;
            foreach (var bitmap in PDFtoImage.Conversion.ToImages(input, options: new(Dpi: 72)))
            {
                using (bitmap)
                {
                    token.ThrowIfCancellationRequested();
                    if (bitmap.Width == 0) throw new InvalidDataException("PDF raster verification failed.");
                    count++;
                }
            }
            if (count != expectedPages) throw new InvalidDataException("PDF page decode verification failed.");
        }
    }
    private static string Rtf(IEnumerable<string> pages)
    {
        var builder = new StringBuilder(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\uc1 ");
        var first = true;
        foreach (var page in pages)
        {
            if (!first) builder.Append(@"\page "); first = false;
            foreach (var c in page)
                if (c is '\\' or '{' or '}') builder.Append('\\').Append(c);
                else if (c == '\n') builder.Append(@"\par ");
                else if (c == '\r') { }
                else if (c > 127) builder.Append(@"\u").Append(unchecked((short)c).ToString(CultureInfo.InvariantCulture)).Append('?');
                else builder.Append(c);
        }
        return builder.Append('}').ToString();
    }
    private static void WriteDocx(string path, IEnumerable<string> pages)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);
        void Entry(string name, string text) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(text); }
        Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        Entry("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var body = new XElement(w + "body"); var first = true;
        foreach (var page in pages)
        {
            if (!first) body.Add(new XElement(w + "p", new XElement(w + "r", new XElement(w + "br", new XAttribute(w + "type", "page"))))); first = false;
            foreach (var line in page.Replace("\r", "").Split('\n')) body.Add(new XElement(w + "p", new XElement(w + "r", new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), line))));
        }
        Entry("word/document.xml", new XDocument(new XElement(w + "document", new XAttribute(XNamespace.Xmlns + "w", w), body)).ToString());
    }
}
