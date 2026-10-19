using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using ImageMagick;
using Naqla.Core;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SkiaSharp;

namespace Naqla.Conversion;

/// <summary>Page imports retain source PDF streams/resources. Rasterization is only for previews/verification.</summary>
public sealed class PdfToolkitService
{
    public const long MaximumSourceBytes = 256L * 1024 * 1024;
    public Task<PdfSession> OpenAsync(IReadOnlyList<DraggedFile> files, CancellationToken token = default)
    {
        var snapshot = files.ToArray();
        return Task.Run(() =>
        {
            if (snapshot.Length is < 1 or > 20 || snapshot.Select(x => Path.GetFullPath(x.FullPath)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Length)
                throw new ArgumentException("اختر من 1 إلى 20 ملف PDF مختلفًا");
            var sources = new List<PdfSource>(); long total = 0;
            foreach (var file in snapshot)
            {
                token.ThrowIfCancellationRequested();
                using var input = OpenLocal(file.FullPath);
                total += input.Length;
                if (total > 512L * 1024 * 1024) throw new NotSupportedException("مجموع الملفات يتجاوز 512 MB");
                var hash = Hash(input, token); using var doc = Read(input);
                var pages = doc.Pages.Cast<PdfPage>().Select((p, i) => new PdfPageInfo(i + 1, p.Width.Point, p.Height.Point, p.Rotate)).ToArray();
                sources.Add(new(FileClassifier.Inspect(file.FullPath), hash, Array.AsReadOnly(pages)));
                if (sources.Sum(x => x.Pages.Count) > 200) throw new NotSupportedException("الحد الحالي 200 صفحة لكل عملية");
            }
            return new PdfSession(Array.AsReadOnly(sources.ToArray()));
        }, token);
    }

    public Task<byte[]> LoadWatermarkImageAsync(string path, CancellationToken token = default) => Task.Run(() =>
    {
        using var guard = OpenLocal(path, false);
        var file = FileClassifier.Inspect(path);
        if (file.Extension is not ("PNG" or "JPG" or "JPEG")) throw new NotSupportedException("اختر PNG أو JPG للعلامة");
        if (guard.Length > 20 * 1024 * 1024) throw new NotSupportedException("الحد 20 MB لصورة العلامة");
        token.ThrowIfCancellationRequested(); using var frames = RasterImages.Open(file); var image = frames[0];
        image.Resize(new MagickGeometry(2000, 2000) { Greater = true }); image.Strip(); image.ColorSpace = ColorSpace.sRGB;
        token.ThrowIfCancellationRequested(); return image.ToByteArray(MagickFormat.Png);
    }, token);

    public Task<PdfPreview> ThumbnailAsync(PdfSession session, int source, int page, CancellationToken token = default) => Task.Run(() =>
    {
        if (source < 0 || source >= session.Sources.Count || page < 1 || page > session.Sources[source].Pages.Count) throw new ArgumentOutOfRangeException(nameof(page));
        var info = session.Sources[source]; using var input = OpenLocal(info.File.FullPath);
        if (Hash(input, token) != info.Sha256) throw new InvalidDataException("تغير المستند بعد فتح المحرر؛ أعد سحبه");
        var png = Render(input, page - 1, 160, token);
        return new PdfPreview(png, info.Pages.Count, page, info.Pages[page - 1].Width, info.Pages[page - 1].Height);
    }, token);

    public Task<int> ThumbnailsAsync(PdfSession session, bool documentsOnly, IProgress<PdfThumbnail> progress, CancellationToken token = default) => Task.Run(() =>
    {
        // Verify/lock each source once, rather than hashing a 256 MB document per thumbnail.
        using var inputs = new Inputs(session, token); var completed = 0;
        for (var source = 0; source < session.Sources.Count; source++)
            for (var page = 0; page < (documentsOnly ? 1 : inputs.Documents[source].PageCount); page++)
            {
                token.ThrowIfCancellationRequested(); var png = Render(inputs.Streams[source], page, 160, token);
                progress.Report(new(source, page + 1, png)); completed++;
            }
        return completed;
    }, token);

    public Task<PdfPreview> PreviewAsync(PdfEditPlan plan, int page = 1, CancellationToken token = default)
    {
        plan = plan.Snapshot(); plan.Validate();
        return Task.Run(() =>
        {
            using var inputs = new Inputs(plan.Session, token);
            using var doc = Build(plan, inputs.Documents, token);
            if (page < 1 || page > doc.PageCount) throw new ArgumentOutOfRangeException(nameof(page));
            var pageCount = doc.PageCount; var width = doc.Pages[page - 1].Width.Point; var height = doc.Pages[page - 1].Height.Point;
            using var stream = new MemoryStream(); doc.Save(stream, false); stream.Position = 0;
            var png = Render(stream, page - 1, 1100, token);
            return new PdfPreview(png, pageCount, page, width, height);
        }, token);
    }

    public Task<ConversionResult> ExecuteAsync(PdfEditPlan plan, IProgress<PdfEditProgress>? progress = null, CancellationToken token = default)
    {
        plan = plan.Snapshot(); plan.Validate();
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested(); using var inputs = new Inputs(plan.Session, token);
            var source = plan.Session.Sources[0].File.FullPath;
            if (plan.Separate)
            {
                // A dedicated directory is published atomically after every page validates.
                var parent = Path.GetDirectoryName(source)!;
                var temporary = Path.Combine(parent, ".naqla-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporary);
                try
                {
                    for (var i = 0; i < plan.Pages.Count; i++)
                    {
                        token.ThrowIfCancellationRequested(); using var part = Build(plan with { Pages = new[] { plan.Pages[i] }, Separate = false }, inputs.Documents, token);
                        var output = Path.Combine(temporary, $"page-{plan.Pages[i]:000}.pdf"); part.Save(output); Validate(output, 1, token);
                        progress?.Report(new(i + 1, plan.Pages.Count));
                    }
                    for (var i = 0; i < 10000; i++)
                    {
                        token.ThrowIfCancellationRequested(); var destination = Path.Combine(parent, Path.GetFileNameWithoutExtension(source) + " extracted" + (i == 0 ? "" : $" ({i})"));
                        try { Directory.Move(temporary, destination); return new(destination, 0, 0, plan.Pages.Count); }
                        catch (IOException) when (Directory.Exists(destination) || File.Exists(destination)) { }
                    }
                    throw new IOException("No free output folder name.");
                }
                finally
                {
                    // Only this invocation's exact GUID directory, created immediately above.
                    if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
                }
            }
            using var result = Build(plan, inputs.Documents, token, progress);
            var resultCount = result.PageCount;
            using var file = new OutputFile(source, "PDF", " " + plan.Tool.ToString().ToLowerInvariant());
            token.ThrowIfCancellationRequested(); result.Save(file.Temporary); Validate(file.Temporary, resultCount, token);
            return new ConversionResult(file.Publish(token), 0, 0, resultCount);
        }, token);
    }

    private static PdfDocument Build(PdfEditPlan plan, IReadOnlyList<PdfDocument> inputs, CancellationToken token, IProgress<PdfEditProgress>? progress = null)
    {
        var doc = new PdfDocument();
        try
        {
            var refs = plan.Tool == PdfTool.Merge
                ? plan.DocumentOrder!.SelectMany(d => Enumerable.Range(1, inputs[d].PageCount).Select(p => (Doc: d, Page: p))).ToArray()
                : plan.OutputPages().Select(p => (Doc: 0, Page: p)).ToArray();
            var numberIndex = 0;
            for (var i = 0; i < refs.Length; i++)
            {
                token.ThrowIfCancellationRequested(); var reference = refs[i]; var page = doc.AddPage(inputs[reference.Doc].Pages[reference.Page - 1]);
                if (plan.Pages.Contains(reference.Page))
                {
                    if (plan.Tool == PdfTool.Rotate) page.Rotate = ((page.Rotate + plan.Rotation) % 360 + 360) % 360;
                    else if (plan.Tool == PdfTool.Numbers) Number(page, plan.Numbers!, plan.Numbers!.Start + numberIndex++);
                    else if (plan.Tool == PdfTool.Watermark) Watermark(page, plan.Watermark!);
                }
                progress?.Report(new(i + 1, refs.Length));
            }
            return doc;
        }
        catch { doc.Dispose(); throw; }
    }

    private static void Number(PdfPage page, PdfNumbering numbers, int value)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        if (numbers.ArabicDigits) text = string.Concat(text.Select(c => (char)('\u0660' + c - '0')));
        using var gfx = Overlay(page, out var viewport);
        DrawText(gfx, viewport, text, numbers.Font, numbers.Size, numbers.Color, 1, 0, numbers.Position, null);
    }
    private static void Watermark(PdfPage page, PdfWatermark mark)
    {
        using var gfx = Overlay(page, out var viewport);
        if (mark.Image is null) DrawText(gfx, viewport, mark.Text, mark.Font, 100, mark.Color, mark.Opacity, mark.Rotation, mark.Position, mark.Scale);
        else
        {
            // Only the watermark image is normalized; source PDF page streams stay vector.
            using var decoded = new MagickImage(mark.Image, MagickFormat.Png);
            if ((long)decoded.Width * decoded.Height > 4_000_000) throw new NotSupportedException("صورة العلامة كبيرة جدًا");
            decoded.Alpha(AlphaAction.Set); decoded.Evaluate(Channels.Alpha, EvaluateOperator.Multiply, mark.Opacity);
            // PDFsharp's indexed PNG path cannot represent partial palette alpha reliably.
            // Force real 8-bit RGBA with a soft mask, including transparent colored pixels.
            using var data = new MemoryStream(decoded.ToByteArray(MagickFormat.Png32)); using var image = XImage.FromStream(data);
            var factor = Math.Min((viewport.Width - 48) * mark.Scale / image.PixelWidth, (viewport.Height - 48) * mark.Scale / image.PixelHeight);
            var width = image.PixelWidth * factor; var height = image.PixelHeight * factor;
            var center = Placement(viewport, width, height, mark.Rotation, mark.Position);
            gfx.TranslateTransform(center.X, center.Y); gfx.RotateTransform(mark.Rotation); gfx.DrawImage(image, -width / 2, -height / 2, width, height);
        }
    }
    private static XGraphics Overlay(PdfPage page, out XSize viewport)
    {
        // PDFsharp appends in unrotated media coordinates. Map the visible CropBox
        // (including nonzero origins) and /Rotate into top-left displayed coordinates.
        var media = page.MediaBox; var crop = page.HasCropBox ? page.CropBox : media;
        var left = Math.Max(media.X1, crop.X1); var right = Math.Min(media.X2, crop.X2);
        var bottom = Math.Max(media.Y1, crop.Y1); var top = Math.Min(media.Y2, crop.Y2);
        var width = right - left; var height = top - bottom; var rotation = ((page.Rotate % 360) + 360) % 360;
        if (width <= 0 || height <= 0) throw new NotSupportedException("حدود الصفحة المرئية غير صحيحة");
        viewport = rotation is 90 or 270 ? new(height, width) : new(width, height);
        var h = page.Height.Point;
        var matrix = rotation switch
        {
            90 => new XMatrix(0, -1, 1, 0, left, h - bottom),
            180 => new XMatrix(-1, 0, 0, -1, right, h - bottom),
            270 => new XMatrix(0, 1, -1, 0, right, h - top),
            _ => new XMatrix(1, 0, 0, 1, left, h - top)
        };
        var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append); gfx.MultiplyTransform(matrix); return gfx;
    }
    private static void DrawText(XGraphics gfx, XSize viewport, string text, string font, double size, string color, double opacity, double rotation, PdfPosition position, double? scale)
    {
        // WPF supplies shaping, ligatures and BiDi. Vector glyph outlines avoid PDFsharp's
        // simple-script DrawString limitations without embedding/distributing system fonts.
        var rtl = text.Any(c => c is >= '\u0590' and <= '\u08FF');
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo(rtl ? "ar" : "en"), rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            new Typeface(font), size, Brushes.Black, 1);
        var geometry = formatted.BuildGeometry(new Point(0, 0)); var bounds = geometry.Bounds;
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) throw new ArgumentException("تعذر تشكيل النص بهذا الخط");
        var factor = scale is null ? 1 : Math.Min((viewport.Width - 48) * scale.Value / bounds.Width, (viewport.Height - 48) * scale.Value / bounds.Height);
        var width = bounds.Width * factor; var height = bounds.Height * factor;
        var center = Placement(viewport, width, height, rotation, position);
        gfx.TranslateTransform(center.X, center.Y); gfx.RotateTransform(rotation); gfx.ScaleTransform(factor);
        gfx.TranslateTransform(-bounds.X - bounds.Width / 2, -bounds.Y - bounds.Height / 2);
        var rgb = uint.Parse(color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var brush = new XSolidBrush(XColor.FromArgb((int)Math.Round(opacity * 255), (int)(rgb >> 16), (int)((rgb >> 8) & 255), (int)(rgb & 255)));
        gfx.DrawPath(brush, new XGraphicsPath(PathGeometry.CreateFromGeometry(geometry)));
    }
    private static XPoint Placement(XSize viewport, double width, double height, double rotation, PdfPosition position)
    {
        var angle = rotation * Math.PI / 180;
        var halfWidth = (Math.Abs(width * Math.Cos(angle)) + Math.Abs(height * Math.Sin(angle))) / 2;
        var halfHeight = (Math.Abs(width * Math.Sin(angle)) + Math.Abs(height * Math.Cos(angle))) / 2;
        var pageWidth = viewport.Width; var pageHeight = viewport.Height;
        if (halfWidth * 2 > pageWidth - 12 || halfHeight * 2 > pageHeight - 12) throw new ArgumentException("العلامة أو الترقيم أكبر من الصفحة؛ قلل الحجم أو الدوران");
        return new(position is PdfPosition.TopLeft or PdfPosition.BottomLeft ? 24 + halfWidth : position is PdfPosition.TopRight or PdfPosition.BottomRight ? pageWidth - 24 - halfWidth : pageWidth / 2,
            position is PdfPosition.TopLeft or PdfPosition.TopCenter or PdfPosition.TopRight ? 24 + halfHeight : position == PdfPosition.Center ? pageHeight / 2 : pageHeight - 24 - halfHeight);
    }
    private static FileStream OpenLocal(string path, bool pdf = true)
    {
        var file = FileClassifier.Inspect(path); var info = new FileInfo(file.FullPath);
        var drive = new DriveInfo(Path.GetPathRoot(file.FullPath)!);
        if (drive.DriveType == DriveType.Network || (info.Attributes & (FileAttributes.Offline | (FileAttributes)0x00400000 | (FileAttributes)0x00040000)) != 0)
            throw new NotSupportedException("استخدم ملفات محلية متاحة بالكامل");
        if (pdf && file.Extension != "PDF") throw new ArgumentException("اختر PDF فقط");
        if (file.Size is < 1 or > MaximumSourceBytes) throw new NotSupportedException("الحد 256 MB للمستند");
        return new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
    private static string Hash(Stream input, CancellationToken token)
    {
        input.Position = 0; using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[81920]; int read;
        while ((read = input.Read(buffer)) > 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, read); }
        input.Position = 0; return System.Convert.ToHexString(hash.GetHashAndReset());
    }
    private static PdfDocument Read(Stream input)
    {
        PdfDocument? doc = null;
        try
        {
            // PDFsharp decrypts an empty-user-password import and clears its encryption
            // flag. Inspect independently first so owner-only encryption is refused too.
            try
            {
                input.Position = 0; using var inspected = UglyToad.PdfPig.PdfDocument.Open(input);
                if (inspected.IsEncrypted) throw new NotSupportedException("المستند مشفر؛ استخدم نسخة غير مشفرة");
            }
            catch (NotSupportedException) { throw; }
            catch (Exception error) when (error is not (OutOfMemoryException or OperationCanceledException))
            { throw new InvalidDataException("تعذر فتح PDF؛ قد يكون مشفرًا أو تالفًا", error); }
            input.Position = 0;
            doc = PdfReader.Open(input, PdfDocumentOpenMode.Import);
            if (doc.SecuritySettings.IsEncrypted) throw new NotSupportedException("المستندات المشفرة غير مدعومة؛ افتح نسخة غير مشفرة أولًا");
            if (doc.PageCount is < 1 or > 200) throw new NotSupportedException("الحد 1–200 صفحة");
            var catalog = doc.Internals.Catalog;
            if (catalog.Elements.ContainsKey("/AcroForm") || catalog.Elements.ContainsKey("/Perms") || catalog.Elements.ContainsKey("/OCProperties"))
                throw new NotSupportedException("النماذج التفاعلية والتوقيعات والطبقات تحتاج مسار حفظ متخصص؛ الأصل محفوظ");
            foreach (var page in doc.Pages)
                if (!double.IsFinite(page.Width.Point) || !double.IsFinite(page.Height.Point) || page.Width.Point is < 1 or > 14400 || page.Height.Point is < 1 or > 14400 || page.Rotate % 90 != 0)
                    throw new NotSupportedException("أبعاد أو دوران صفحة غير مدعوم");
            return doc;
        }
        catch (PdfReaderException error) { doc?.Dispose(); throw new InvalidDataException("تعذر فتح PDF؛ قد يكون مشفرًا أو تالفًا", error); }
        catch { doc?.Dispose(); throw; }
    }
    private sealed class Inputs : IDisposable
    {
        public List<FileStream> Streams { get; } = [];
        public List<PdfDocument> Documents { get; } = [];
        public Inputs(PdfSession session, CancellationToken token)
        {
            try
            {
                foreach (var source in session.Sources)
                {
                    token.ThrowIfCancellationRequested(); var stream = OpenLocal(source.File.FullPath); Streams.Add(stream);
                    if (Hash(stream, token) != source.Sha256) throw new InvalidDataException("تغير المستند بعد المعاينة؛ أعد فتحه");
                    var doc = Read(stream); Documents.Add(doc);
                    if (doc.PageCount != source.Pages.Count) throw new InvalidDataException("تغير عدد الصفحات");
                }
            }
            catch { Dispose(); throw; }
        }
        public void Dispose() { foreach (var doc in Documents) doc.Dispose(); foreach (var stream in Streams) stream.Dispose(); }
    }
    private static byte[] Render(Stream input, int page, int edge, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        lock (PdfConversions.PdfiumGate)
        {
            token.ThrowIfCancellationRequested(); input.Position = 0;
            var sizes = PDFtoImage.Conversion.GetPageSizes(input, leaveOpen: true).ToArray();
            var size = sizes[page]; var factor = edge / Math.Max(size.Width, size.Height); input.Position = 0;
            using var bitmap = PDFtoImage.Conversion.ToImage(input, page: page, leaveOpen: true, options: new(Width: Math.Max(1, (int)Math.Round(size.Width * factor)), Height: Math.Max(1, (int)Math.Round(size.Height * factor))));
            using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100); token.ThrowIfCancellationRequested(); return encoded.ToArray();
        }
    }
    public static void Validate(string path, int expected, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); using var parsed = UglyToad.PdfPig.PdfDocument.Open(path);
        if (parsed.NumberOfPages != expected) throw new InvalidDataException("عدد الصفحات الناتج غير صحيح");
        using var check = File.OpenRead(path);
        lock (PdfConversions.PdfiumGate)
        {
            if (PDFtoImage.Conversion.GetPageCount(check, leaveOpen: true) != expected) throw new InvalidDataException("فشل فحص PDF المستقل");
            for (var page = 0; page < expected; page++)
            {
                token.ThrowIfCancellationRequested(); check.Position = 0;
                using var bitmap = PDFtoImage.Conversion.ToImage(check, page: page, leaveOpen: true, options: new(Width: 192, Height: 192));
                if (bitmap.Width < 1 || bitmap.Height < 1) throw new InvalidDataException("تعذر فك صفحة ناتجة");
            }
        }
        token.ThrowIfCancellationRequested();
    }
}
