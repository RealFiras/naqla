using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Naqla.Core;

namespace Naqla.Wheel;

/// <summary>Native renderer of the approved Pixel-Modern design. No drag or codec dependencies.</summary>
public sealed class PixelWheel : FrameworkElement
{
    public const double CanvasWidth = 260, CanvasHeight = 306, Center = 130, Outer = 120, Inner = 51;
    private readonly Func<DraggedFile, string, bool> canExecute;
    private readonly Func<FileDragPayload, WheelMode, IReadOnlyList<WheelOption>>? optionProvider;
    private readonly WheelPaging paging = new();
    private DraggedFile? file;
    private FileDragPayload? payload;
    private IReadOnlyList<WheelOption> options = [];
    private IReadOnlyList<WheelOption> allOptions = [];
    private WheelMode mode;
    private int selected = -1;
    private static readonly Dictionary<string, BitmapSource> Icons = [];
    private static readonly Typeface Font = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Rect Capsule = new(28, 266, 204, 34);
    private static readonly DependencyProperty TransitionProperty = DependencyProperty.Register(nameof(Transition), typeof(double), typeof(PixelWheel), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty SelectorProperty = DependencyProperty.Register(nameof(Selector), typeof(double), typeof(PixelWheel), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Transition { get => (double)GetValue(TransitionProperty); set => SetValue(TransitionProperty, value); }
    public double Selector { get => (double)GetValue(SelectorProperty); set => SetValue(SelectorProperty, value); }
    public WheelOption? Selection => selected >= 0 && selected < options.Count ? options[selected] : null;
    public WheelMode Mode => mode;
    public int OptionsCount => allOptions.Count;
    public int VisibleOptionsCount => options.Count;
    public int PageCount => mode == WheelMode.Tools ? paging.Pages : 1;
    public int CurrentPage => mode == WheelMode.Tools ? paging.Page : 0;
    public string HubLabel => payload is { Files.Count: > 1 } ? $"{payload.Files.Count} ملف" : file?.Extension == "JPEG" ? "JPG" : file?.Extension ?? "";
    public PixelWheel(Func<DraggedFile, string, bool> canExecute, Func<FileDragPayload, WheelMode, IReadOnlyList<WheelOption>>? optionProvider = null)
    {
        this.canExecute = canExecute;
        this.optionProvider = optionProvider;
        Width = CanvasWidth; Height = CanvasHeight;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        SnapsToDevicePixels = true;
    }
    public void SetFile(DraggedFile value) { payload = null; file = value; RefreshOptions(); }
    public void SetSelection(FileDragPayload value) { payload = value; file = value.PreviewFile; RefreshOptions(); }
    public void SetMode(bool tools, bool animate = true)
    {
        var next = tools ? WheelMode.Tools : WheelMode.Convert;
        if (mode == next) return;
        mode = next; RefreshOptions();
        if (!animate) { BeginAnimation(TransitionProperty, null); Transition = 1; BeginAnimation(SelectorProperty, null); Selector = mode == WheelMode.Tools ? 0 : 1; return; }
        BeginAnimation(TransitionProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        BeginAnimation(SelectorProperty, new DoubleAnimation(mode == WheelMode.Tools ? 0 : 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
    public void Reset()
    {
        file = null; payload = null; options = []; allOptions = []; paging.Reset(); selected = -1; mode = WheelMode.Convert;
        BeginAnimation(TransitionProperty, null); Transition = 1;
        BeginAnimation(SelectorProperty, null); Selector = 1;
        InvalidateVisual();
    }
    public void Open()
    {
        var scale = new ScaleTransform(1, 1, Center, Center); RenderTransform = scale;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var animation = new DoubleAnimation(.94, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation); scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        BeginAnimation(OpacityProperty, new DoubleAnimation(.08, 1, TimeSpan.FromMilliseconds(160)));
    }
    private void RefreshOptions()
    {
        allOptions = payload is not null ? optionProvider?.Invoke(payload, mode) ?? WheelOptions.For(payload, mode, canExecute) : file is null ? [] : WheelOptions.For(file, mode, canExecute);
        options = paging.Visible(allOptions, mode); selected = -1; InvalidateVisual();
    }
    public bool ChangePage(int direction, bool animate = true)
    {
        if (mode != WheelMode.Tools || !paging.Turn(direction)) return false;
        options = paging.Visible(allOptions, mode); selected = -1; InvalidateVisual();
        if (animate) BeginAnimation(TransitionProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        return true;
    }
    public void Track(Point point)
    {
        var next = RadialHitTest.Sector(point.X - Center, point.Y - Center, Inner, Outer, options.Count);
        if (next != selected) { selected = next; InvalidateVisual(); }
    }
    protected override HitTestResult? HitTestCore(PointHitTestParameters parameters)
    {
        var p = parameters.HitPoint;
        return (p - new Point(Center, Center)).Length <= Outer + 3 || Capsule.Contains(p) ? new PointHitTestResult(this, p) : null;
    }
    protected override void OnRender(DrawingContext dc)
    {
        var theme = Theme.For(file?.Category, mode);
        // All pixels outside the wheel/capsule are genuinely transparent; no panel or rectangular shadow.
        dc.DrawEllipse(Brush("#0b0f16"), new Pen(Brush("#070a10"), 3), new(Center, Center), Outer + 2, Outer + 2);
        dc.PushOpacity(Transition);
        if (options.Count == 0)
            dc.DrawEllipse(Gradient(theme.Surface, theme.Dark), null, new(Center, Center), Outer, Outer);
        for (var i = 0; i < options.Count; i++)
        {
            var active = selected == i;
            var start = -90 - 180d / options.Count + i * 360d / options.Count;
            var geometry = Sector(start, 360d / options.Count);
            dc.DrawGeometry(active ? Gradient(theme.Bright, theme.Accent, theme.Highlight) : Gradient(theme.Surface, theme.Dark),
                new Pen(Brush(active ? theme.Bright : "#090e16"), active ? 1.1 : 1.4), geometry);
            var radians = (-90 + i * 360d / options.Count) * Math.PI / 180;
            var x = Center + 85 * Math.Cos(radians); var y = Center + 85 * Math.Sin(radians);
            Icon(dc, OptionIcon(options[i].Id), x - 12, y - 23, 24);
            Text(dc, options[i].Label, x, y + 5, options[i].Id == "STRIP" ? 9.5 : 11.5, active ? "#221925" : "#f5f0e6");
        }
        dc.Pop();
        dc.DrawGeometry(null, new Pen(Brush(theme.Accent, 190), 1.1), SteppedCircle(Outer));
        // Hub never moves or fades during mode switching.
        dc.DrawEllipse(Gradient("#17202a", "#0b111a"), new Pen(Brush(theme.Accent), 1.1), new(Center, Center), Inner, Inner);
        dc.DrawEllipse(null, new Pen(Brush("#ffffff", 25), .6), new(Center, Center), Inner - 3, Inner - 3);
        Icon(dc, HubIcon(file), Center - 17, Center - 35, 34);
        Text(dc, HubLabel, Center, Center + 4, 15, "#f5f0e6");
        if (file is not null) Text(dc, file.DisplaySize, Center, Center + 24, 10, "#c3c8d0");
        if (mode == WheelMode.Tools && paging.Pages > 1)
            for (var p = 0; p < paging.Pages; p++) dc.DrawRectangle(Brush(p == paging.Page ? theme.Accent : "#627483"), null, new Rect(Center + (p - (paging.Pages - 1) / 2d) * 8 - 1.5, Center + 43, 3, 3));
        DrawCapsule(dc, theme);
    }
    private void DrawCapsule(DrawingContext dc, Theme theme)
    {
        dc.DrawRoundedRectangle(Gradient("#26313d", "#121a25", 215), new Pen(Brush(theme.Accent, 110), .8), Capsule, 12, 12);
        dc.DrawLine(new Pen(Brush("#ffffff", 45), .7), new(41, 267.5), new(218, 267.5));
        var x = 31 + 100 * Math.Clamp(Selector, 0, 1);
        dc.DrawRoundedRectangle(Gradient(theme.Accent, theme.Highlight, 210), new Pen(Brush(theme.Bright, 160), .7), new Rect(x, 269, 98, 28), 9, 9);
        Icon(dc, "mode-tools", 45, 277, 14); Icon(dc, "mode-convert", 146, 277, 14);
        Text(dc, "أدوات", 83, 273.5, 12, mode == WheelMode.Tools ? "#101822" : theme.Bright);
        Text(dc, "تحويل", 185, 273.5, 12, mode == WheelMode.Convert ? "#101822" : theme.Bright);
    }
    private static string HubIcon(DraggedFile? value) => value?.Category switch
    {
        FileCategory.Video => "video", FileCategory.Audio => "music", FileCategory.Document => "paper-PDF", FileCategory.Archive or FileCategory.Folder => "merge", _ => "image"
    };
    private static string OptionIcon(string id) => id switch
    {
        "COMPRESS" => "compress", "CROP" => "crop", "ROTATE" => "rotate", "STRIP" => "privacy", "MERGE" => "merge", "MUTE" => "mute",
        "PDFREORDER" or "PDFMERGE" => "merge", "PDFEXTRACT" => "paper-PDF", "PDFDELETE" => "crop", "PDFNUMBERS" => "paper-TXT", "PDFWATERMARK" => "privacy", "PDFROTATE" => "rotate",
        "TARGETSIZE" or "ARCHIVE" => "compress", "RENAME" or "OCR" or "SUBTITLE" => "paper-TXT", "WEBM" => "paper-WebM",
        "TRIM" or "FRAMES" => "crop", "NORMALIZE" or "SILENCE" or "CHANNELS" => "music", "BACKGROUND" or "PRIVACY" => "privacy", "EXTRACT" => "merge", _ => id.StartsWith("PLUGIN:") ? "mode-tools" : "paper-" + id
    };
    private static void Icon(DrawingContext dc, string key, double x, double y, double size)
    {
        if (key == "mute")
        {
            Icon(dc, "music", x, y, size);
            var pixel = size / 8;
            for (var i = 1; i <= 6; i++)
            {
                dc.DrawRectangle(Brush("#102b26"), null, new Rect(x + i * pixel - 1, y + i * pixel - 1, pixel + 2, pixel + 2));
                dc.DrawRectangle(Brush("#d4f7e8"), null, new Rect(x + i * pixel, y + i * pixel, pixel, pixel));
            }
            return;
        }
        if (!Icons.TryGetValue(key, out var icon))
        {
            icon = new BitmapImage(new Uri($"pack://application:,,,/Naqla.Wheel;component/Assets/{key}.png"));
            icon.Freeze(); Icons[key] = icon;
        }
        dc.DrawImage(icon, new Rect(x, y, size, size));
    }
    private void Text(DrawingContext dc, string text, double x, double y, double size, string color)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Font, size, Brush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, new Point(x - formatted.Width / 2, y));
    }
    private static SolidColorBrush Brush(string hex, byte alpha = 255)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex); color.A = alpha;
        var brush = new SolidColorBrush(color); brush.Freeze(); return brush;
    }
    private static LinearGradientBrush Gradient(string first, string last, byte alpha = 255) => Gradient(first, first, last, alpha);
    private static LinearGradientBrush Gradient(string first, string middle, string last, byte alpha = 255)
    {
        Color C(string hex) { var c = (Color)ColorConverter.ConvertFromString(hex); c.A = alpha; return c; }
        var brush = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(.65, 1), GradientStops = [new(C(first), 0), new(C(middle), .3), new(C(last), 1)] };
        brush.Freeze(); return brush;
    }
    private static Geometry Sector(double start, double sweep)
    {
        if (sweep >= 360)
        {
            var ring = new CombinedGeometry(GeometryCombineMode.Exclude, new EllipseGeometry(new Point(Center, Center), Outer, Outer), new EllipseGeometry(new Point(Center, Center), Inner, Inner));
            ring.Freeze(); return ring;
        }
        Point P(double r, double degrees) => new(Center + r * Math.Cos(degrees * Math.PI / 180), Center + r * Math.Sin(degrees * Math.PI / 180));
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(P(Inner, start), true, true); g.LineTo(P(Outer, start), true, false);
            g.ArcTo(P(Outer, start + sweep), new(Outer, Outer), 0, sweep > 180, SweepDirection.Clockwise, true, false);
            g.LineTo(P(Inner, start + sweep), true, false);
            g.ArcTo(P(Inner, start), new(Inner, Inner), 0, sweep > 180, SweepDirection.Counterclockwise, true, false);
        }
        geometry.Freeze(); return geometry;
    }
    private static StreamGeometry SteppedCircle(double radius)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            for (var angle = 0d; angle < 360; angle += 1.5)
            {
                var rad = angle * Math.PI / 180;
                var p = new Point(Math.Round((Center + radius * Math.Cos(rad)) / 2) * 2, Math.Round((Center + radius * Math.Sin(rad)) / 2) * 2);
                if (angle == 0) g.BeginFigure(p, false, true); else g.LineTo(p, true, false);
            }
        }
        geometry.Freeze(); return geometry;
    }
    private sealed record Theme(string Accent, string Bright, string Dark, string Surface, string Highlight)
    {
        public static Theme For(FileCategory? category, WheelMode mode) => mode == WheelMode.Tools
            ? new("#55d6b0", "#c5ffe7", "#143730", "#19332f", "#2a9c83")
            : category switch
            {
                FileCategory.Video => new("#ff6274", "#ffc0bc", "#341b25", "#2a202a", "#ee3f58"),
                FileCategory.Audio => new("#aa6bff", "#e7c9ff", "#261b3d", "#242139", "#8b3ef3"),
                FileCategory.Document => new("#31b2ff", "#c1edff", "#122b40", "#192b3d", "#008ce8"),
                _ => new("#ffb347", "#ffe3a0", "#332719", "#25252a", "#f49a20")
            };
    }
}
