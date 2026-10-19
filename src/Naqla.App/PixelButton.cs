using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Naqla.App;

internal enum PixelButtonTone { Amber, Blue, Mint, Violet, Coral }
internal enum PixelButtonGlyph { None, Hide, Cancel, Details, Folder, Check, Undo, Reset }

/// <summary>Code-native pixel surface; retains Button keyboard, automation and dialog semantics.</summary>
internal sealed class PixelButton : Button
{
    private static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(PixelButtonTone), typeof(PixelButton), new FrameworkPropertyMetadata(PixelButtonTone.Blue, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(nameof(Glyph), typeof(PixelButtonGlyph), typeof(PixelButton), new FrameworkPropertyMetadata(PixelButtonGlyph.None, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));
    public PixelButtonTone Tone { get => (PixelButtonTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public PixelButtonGlyph Glyph { get => (PixelButtonGlyph)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public PixelButton()
    {
        OverridesDefaultStyle = true; Template = new ControlTemplate(typeof(Button));
        FontFamily = new("Segoe UI"); FontSize = 12; FontWeight = FontWeights.SemiBold;
        FlowDirection = FlowDirection.LeftToRight;
        Padding = new Thickness(10, 7, 10, 7); MinHeight = 32; MinWidth = 48;
        SnapsToDevicePixels = true; UseLayoutRounding = true; FocusVisualStyle = null;
    }
    private FormattedText Label(Brush brush) => new(Content?.ToString() ?? "", CultureInfo.CurrentUICulture,
        FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    protected override Size MeasureOverride(Size constraint)
    {
        var text = Label(Brushes.Ivory);
        return new(Math.Min(constraint.Width, Math.Ceiling(text.Width + Padding.Left + Padding.Right + (Glyph == PixelButtonGlyph.None ? 6 : 23))), Math.Ceiling(text.Height + Padding.Top + Padding.Bottom + 4));
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ContentProperty || e.Property == FontSizeProperty) { InvalidateMeasure(); InvalidateVisual(); }
        if (e.Property == IsMouseOverProperty || e.Property == IsPressedProperty || e.Property == IsEnabledProperty || e.Property == IsKeyboardFocusedProperty) InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth < 8 || ActualHeight < 8) return;
        var accent = Tone switch { PixelButtonTone.Amber => "#FFC35A", PixelButtonTone.Mint => "#65DAB4", PixelButtonTone.Violet => "#B69AFF", PixelButtonTone.Coral => "#FF8390", _ => "#82C9F5" };
        var primary = Tone == PixelButtonTone.Amber;
        dc.PushOpacity(IsEnabled ? 1 : .46);
        var shape = Shape(1, 1, Math.Floor(ActualWidth - 2), Math.Floor(ActualHeight - 2));
        var top = primary ? (IsPressed ? "#D79A3B" : IsMouseOver ? "#FFD486" : "#FFC35A") : IsPressed ? "#314658" : IsMouseOver ? "#2C3A4A" : "#202B37";
        dc.DrawGeometry(new LinearGradientBrush(ColorOf(top), ColorOf(primary ? "#EAA13D" : "#17212C"), 90), new Pen(Paint(accent), 1), shape);
        dc.DrawLine(new Pen(Paint(primary ? "#FFE4AF" : accent, .35), 1), new(6, 3), new(ActualWidth - 6, 3));
        dc.DrawLine(new Pen(Paint("#080F17", .65), 2), new(6, ActualHeight - 3), new(ActualWidth - 6, ActualHeight - 3));
        var ink = Paint(primary ? "#18212B" : "#F5F0E6"); var text = Label(ink);
        var total = text.Width + (Glyph == PixelButtonGlyph.None ? 0 : 20); var start = (ActualWidth - total) / 2;
        dc.DrawText(text, new(start, Math.Floor((ActualHeight - text.Height) / 2) - (IsPressed ? 0 : 1)));
        if (Glyph != PixelButtonGlyph.None) DrawGlyph(dc, Glyph, Math.Floor(start + text.Width + 6), Math.Floor((ActualHeight - 14) / 2), primary ? ink : Paint(accent));
        if (IsKeyboardFocused) dc.DrawRectangle(null, new Pen(Paint(accent), 1) { DashStyle = DashStyles.Dot }, new Rect(5, 5, Math.Max(1, ActualWidth - 10), Math.Max(1, ActualHeight - 10)));
        dc.Pop();
    }
    private static Geometry Shape(double x, double y, double width, double height)
    {
        var right = x + width; var bottom = y + height;
        Point[] points = [new(x+4,y),new(right-4,y),new(right-4,y+2),new(right-2,y+2),new(right-2,y+4),new(right,y+4),new(right,bottom-4),new(right-2,bottom-4),new(right-2,bottom-2),new(right-4,bottom-2),new(right-4,bottom),new(x+4,bottom),new(x+4,bottom-2),new(x+2,bottom-2),new(x+2,bottom-4),new(x,bottom-4),new(x,y+4),new(x+2,y+4),new(x+2,y+2),new(x+4,y+2)];
        var geometry = new StreamGeometry(); using (var context = geometry.Open()) { context.BeginFigure(points[0], true, true); foreach (var point in points.Skip(1)) context.LineTo(point, true, false); } geometry.Freeze(); return geometry;
    }
    private static void DrawGlyph(DrawingContext dc, PixelButtonGlyph glyph, double x, double y, Brush ink)
    {
        var rows = glyph switch
        {
            PixelButtonGlyph.Hide => new[] { "0000000","0000000","0000000","0000000","1111111","1111111","0000000" },
            PixelButtonGlyph.Cancel => ["1100011","0110110","0011100","0001000","0011100","0110110","1100011"],
            PixelButtonGlyph.Folder => ["0111000","1111100","1000011","1111111","1000001","1000001","1111111"],
            PixelButtonGlyph.Check => ["0000001","0000011","0000110","1001100","1111000","0110000","0000000"],
            PixelButtonGlyph.Undo or PixelButtonGlyph.Reset => ["0010000","0111110","1110011","0110001","0010001","0000011","0011110"],
            _ => ["1101111","1100000","0000000","1101111","1100000","0000000","1101111"]
        };
        for (var row = 0; row < rows.Length; row++) for (var column = 0; column < rows[row].Length; column++) if (rows[row][column] == '1') dc.DrawRectangle(ink, null, new Rect(x + column * 2, y + row * 2, 2, 2));
    }
    private static Color ColorOf(string color) => (Color)ColorConverter.ConvertFromString(color);
    private static SolidColorBrush Paint(string color, double opacity = 1) => new(ColorOf(color)) { Opacity = opacity };
}
