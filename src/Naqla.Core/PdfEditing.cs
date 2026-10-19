using System.Globalization;

namespace Naqla.Core;

public enum PdfTool { Reorder, Extract, Delete, Merge, Numbers, Watermark, Rotate }
public enum PdfPosition { TopLeft, TopCenter, TopRight, Center, BottomLeft, BottomCenter, BottomRight }
public static class PdfToolIds
{
    public static string Id(PdfTool tool) => "PDF" + tool.ToString().ToUpperInvariant();
    public static bool TryParse(string id, out PdfTool tool) => Enum.TryParse(id.StartsWith("PDF", StringComparison.Ordinal) ? id[3..] : "", true, out tool) && Enum.IsDefined(tool) && Id(tool) == id;
    public static string Label(PdfTool tool) => tool switch
    {
        PdfTool.Reorder => "ترتيب الصفحات", PdfTool.Extract => "استخراج صفحات", PdfTool.Delete => "حذف صفحات",
        PdfTool.Merge => "دمج PDF", PdfTool.Numbers => "ترقيم الصفحات", PdfTool.Watermark => "علامة مائية", _ => "تدوير صفحات"
    };
}
public sealed record PdfPageInfo(int Number, double Width, double Height, int Rotation);
public sealed record PdfSource(DraggedFile File, string Sha256, IReadOnlyList<PdfPageInfo> Pages);
public sealed record PdfSession(IReadOnlyList<PdfSource> Sources)
{
    public int PageCount => Sources.Sum(x => x.Pages.Count);
}
public sealed record PdfNumbering(int Start = 1, double Size = 12, string Color = "#202020",
    string Font = "Segoe UI", PdfPosition Position = PdfPosition.BottomCenter, bool ArabicDigits = false);
public sealed record PdfWatermark(string Text = "نقلة", byte[]? Image = null, double Opacity = .25,
    double Scale = .6, double Rotation = -35, PdfPosition Position = PdfPosition.Center,
    string Font = "Segoe UI", string Color = "#707070");

/// <summary>One-based page numbers. Order is significant for reorder/extract; never silently deduplicated.</summary>
public sealed record PdfEditPlan(PdfSession Session, PdfTool Tool, IReadOnlyList<int> Pages,
    bool Separate = false, IReadOnlyList<int>? DocumentOrder = null, PdfNumbering? Numbers = null,
    PdfWatermark? Watermark = null, int Rotation = 90)
{
    public PdfEditPlan Snapshot() => this with
    {
        Pages = Array.AsReadOnly(Pages.ToArray()), DocumentOrder = DocumentOrder is null ? null : Array.AsReadOnly(DocumentOrder.ToArray()),
        Watermark = Watermark is null ? null : Watermark with { Image = Watermark.Image?.ToArray() },
        Session = new(Array.AsReadOnly(Session.Sources.Select(x => x with { Pages = Array.AsReadOnly(x.Pages.ToArray()) }).ToArray()))
    };
    public void Validate()
    {
        if (!Enum.IsDefined(Tool) || Session.Sources.Count is < 1 or > 20 || Session.PageCount is < 1 or > 200)
            throw new ArgumentException("اختر حتى 20 مستندًا ومجموع 200 صفحة");
        if (Tool == PdfTool.Merge)
        {
            if (Session.Sources.Count < 2 || DocumentOrder is null || !DocumentOrder.Order().SequenceEqual(Enumerable.Range(0, Session.Sources.Count)))
                throw new ArgumentException("ترتيب الدمج يجب أن يحتوي كل المستندات مرة واحدة");
            if (Separate) throw new ArgumentException("التصدير المنفصل للاستخراج فقط");
            return;
        }
        if (Session.Sources.Count != 1) throw new ArgumentException("اسحب PDF واحدًا للتحرير، أو عدة مستندات للدمج");
        var count = Session.PageCount;
        if (Pages.Count < 1 || Pages.Distinct().Count() != Pages.Count || Pages.Any(x => x < 1 || x > count))
            throw new ArgumentException("اختر صفحات صحيحة دون تكرار");
        if (Tool == PdfTool.Reorder && !Pages.Order().SequenceEqual(Enumerable.Range(1, count)))
            throw new ArgumentException("الترتيب يجب أن يحتفظ بكل الصفحات مرة واحدة");
        if (Tool == PdfTool.Delete && Pages.Count == count) throw new ArgumentException("لا يمكن حذف كل الصفحات");
        if (Separate && Tool != PdfTool.Extract) throw new ArgumentException("التصدير المنفصل للاستخراج فقط");
        if (Tool == PdfTool.Rotate && Rotation is not (90 or -90 or 180)) throw new ArgumentException("اختر ربع دورة أو نصف دورة");
        if (Tool == PdfTool.Numbers)
        {
            var n = Numbers ?? throw new ArgumentException("إعدادات الترقيم مطلوبة");
            if (n.Start < 0 || n.Start > 1_000_000 || !double.IsFinite(n.Size) || n.Size is < 6 or > 144 || !Enum.IsDefined(n.Position))
                throw new ArgumentException("تحقق من بداية الترقيم والحجم والموضع");
            ValidateTextStyle(n.Font, n.Color);
        }
        if (Tool == PdfTool.Watermark)
        {
            var w = Watermark ?? throw new ArgumentException("إعدادات العلامة مطلوبة");
            if (w.Image is null && (string.IsNullOrWhiteSpace(w.Text) || w.Text.Length > 500 || w.Text.Any(char.IsControl)))
                throw new ArgumentException("أدخل نصًا من 1 إلى 500 حرف دون أسطر جديدة");
            if (w.Image is not null && (w.Image.Length < 1 || w.Image.Length > 20 * 1024 * 1024)) throw new ArgumentException("صورة العلامة أكبر من الحد 20 MB");
            if (!double.IsFinite(w.Opacity) || w.Opacity is <= 0 or > 1 || !double.IsFinite(w.Scale) || w.Scale is < .05 or > 1 || !double.IsFinite(w.Rotation) || w.Rotation is < -180 or > 180 || !Enum.IsDefined(w.Position))
                throw new ArgumentException("تحقق من شفافية العلامة وحجمها ودورانها");
            ValidateTextStyle(w.Font, w.Color);
        }
    }
    private static void ValidateTextStyle(string font, string color)
    {
        if (font is not ("Segoe UI" or "Arial" or "Tahoma" or "Times New Roman")) throw new ArgumentException("اختر خطًا من القائمة المحلية");
        if (color.Length != 7 || color[0] != '#' || !int.TryParse(color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            throw new ArgumentException("اللون بصيغة #RRGGBB");
    }
    public IReadOnlyList<int> OutputPages() => Tool switch
    {
        PdfTool.Reorder or PdfTool.Extract => Pages,
        PdfTool.Delete => Enumerable.Range(1, Session.PageCount).Except(Pages).ToArray(),
        _ => Enumerable.Range(1, Session.PageCount).ToArray()
    };
}
public sealed record PdfPreview(byte[] Png, int Pages, int Page, double Width, double Height);
public sealed record PdfThumbnail(int Source, int Page, byte[] Png);
public sealed record PdfEditProgress(int Completed, int Total);

public static class PdfPageRanges
{
    public static int[] Parse(string text, int count)
    {
        if (count is < 1 or > 200 || string.IsNullOrWhiteSpace(text) || text.Length > 2000) throw new ArgumentException("أدخل صفحات مثل 1,3,5-8");
        text = string.Concat(text.Select(c => c is >= '\u0660' and <= '\u0669' ? (char)('0' + c - '\u0660') : c is >= '\u06F0' and <= '\u06F9' ? (char)('0' + c - '\u06F0') : c)).Replace('،', ',');
        var result = new List<int>();
        foreach (var group in text.Split(','))
        {
            var range = group.Trim().Split('-');
            if (range.Length is < 1 or > 2 || !int.TryParse(range[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var first)) throw new ArgumentException("صيغة نطاق غير صحيحة");
            var last = first;
            if (range.Length == 2 && !int.TryParse(range[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out last)) throw new ArgumentException("نهاية النطاق غير صحيحة");
            if (first < 1 || last < first || last > count) throw new ArgumentException("النطاق خارج المستند أو معكوس");
            for (var page = first; page <= last; page++) if (result.Contains(page)) throw new ArgumentException("لا تكرر الصفحات"); else result.Add(page);
        }
        return result.ToArray();
    }
}
