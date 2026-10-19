using System.IO;
using System.Text;

namespace Naqla.Conversion;

/// <summary>Streaming raster PDF writer; one image per A4 page, no source-sized image queue.</summary>
internal static class ImagePdf
{
    internal sealed record Page(byte[] Jpeg, int Width, int Height);
    public static void Write(string path, byte[] jpeg, int width, int height) =>
        WritePages(path, 1, _ => new(jpeg, width, height), CancellationToken.None);

    public static void WritePages(string path, int count, Func<int, Page> readPage, CancellationToken token)
    {
        if (count is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(count));
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        var positions = new List<long> { 0 };
        void Text(string value) => stream.Write(Encoding.ASCII.GetBytes(value));
        void Object(int number, string body) { positions.Add(stream.Position); Text($"{number} 0 obj\n{body}\nendobj\n"); }
        Text("%PDF-1.4\n");
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        var kids = string.Join(" ", Enumerable.Range(0, count).Select(i => $"{3 + i * 3} 0 R"));
        Object(2, $"<< /Type /Pages /Kids [{kids}] /Count {count} >>");
        for (var i = 0; i < count; i++)
        {
            token.ThrowIfCancellationRequested();
            var page = readPage(i);
            if (page.Width < 1 || page.Height < 1 || page.Jpeg.Length == 0) throw new InvalidDataException("Invalid image page.");
            var pageWidth = page.Width > page.Height ? 842 : 595;
            var pageHeight = page.Width > page.Height ? 595 : 842;
            var scale = Math.Min(pageWidth / (double)page.Width, pageHeight / (double)page.Height);
            var drawWidth = page.Width * scale; var drawHeight = page.Height * scale;
            var number = 3 + i * 3;
            Object(number, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidth} {pageHeight}] /Resources << /XObject << /Im0 {number + 1} 0 R >> >> /Contents {number + 2} 0 R >>");
            positions.Add(stream.Position);
            Text($"{number + 1} 0 obj\n<< /Type /XObject /Subtype /Image /Width {page.Width} /Height {page.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {page.Jpeg.Length} >>\nstream\n");
            stream.Write(page.Jpeg); Text("\nendstream\nendobj\n");
            var content = FormattableString.Invariant($"q {drawWidth:0.########} 0 0 {drawHeight:0.########} {(pageWidth - drawWidth) / 2:0.########} {(pageHeight - drawHeight) / 2:0.########} cm /Im0 Do Q\n");
            Object(number + 2, $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream");
        }
        token.ThrowIfCancellationRequested();
        var xref = stream.Position;
        Text($"xref\n0 {positions.Count}\n0000000000 65535 f \n");
        foreach (var position in positions.Skip(1)) Text(FormattableString.Invariant($"{position:0000000000} 00000 n \n"));
        Text($"trailer\n<< /Size {positions.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }
}
