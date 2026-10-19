using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Naqla.Core;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace Naqla.App;

/// <summary>Explicit Crop action editor; never shown on launch or during ordinary conversions.</summary>
internal sealed class CropWindow : Window
{
    private readonly ImagePreview preview;
    private readonly Grid surface;
    private readonly Canvas overlay;
    private readonly Rectangle marquee;
    private readonly Image photograph;
    private readonly TextBox[] fields;
    private readonly TextBlock status;
    private readonly TextBlock heading;
    private Point? anchor;
    private Rect imageBounds;
    private readonly ComboBox aspect = new() { ItemsSource = new[] { "حر", "1:1", "4:3", "16:9", "9:16" }, SelectedIndex = 0, Width = 90, Margin = new Thickness(8, 0, 8, 0) };
    private readonly Slider zoom = new() { Minimum = 1, Maximum = 4, Value = 1, Width = 130, SmallChange = .1, LargeChange = .5, Margin = new Thickness(8, 0, 8, 0) };
    private readonly List<Rectangle> handles = [];
    private CropRectangle? dragOriginal;
    private int handle = -1;
    private bool moving;
    private Point pan;
    private Point? panStart;
    private Point originalPan;
    private double? Ratio => aspect.SelectedIndex switch { 1 => 1, 2 => 4d / 3, 3 => 16d / 9, 4 => 9d / 16, _ => null };
    public CropRectangle Selection { get; private set; }

    public CropWindow(ImagePreview preview)
    {
        this.preview = preview;
        Selection = new(0, 0, preview.Width, preview.Height);
        Title = "Naqla · قص الصورة"; Width = 820; Height = 660; MinWidth = 560; MinHeight = 430;
        MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(18, 24, 32)); Foreground = Brushes.Ivory;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        PixelControls.Apply(Resources);
        var icon = Path.Combine(AppContext.BaseDirectory, "preview", "LOGO.png");
        if (File.Exists(icon)) Icon = BitmapFrame.Create(new Uri(icon));
        var root = new DockPanel { Margin = new Thickness(18) }; Content = root;
        heading = new TextBlock { Text = "اسحب لتحديد منطقة القص · الإحداثيات بالبكسل بعد تطبيق اتجاه الصورة", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12), FlowDirection = FlowDirection.RightToLeft };
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var viewing = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
        viewing.Children.Add(new TextBlock { Text = "النسبة", VerticalAlignment = VerticalAlignment.Center }); viewing.Children.Add(aspect);
        viewing.Children.Add(new TextBlock { Text = "تكبير", VerticalAlignment = VerticalAlignment.Center }); viewing.Children.Add(zoom);
        var fit = new PixelButton { Content = "ملاءمة", Tone = PixelButtonTone.Blue }; viewing.Children.Add(fit); fit.Click += (_, _) => { pan = new(); zoom.Value = 1; DrawSelection(); };
        DockPanel.SetDock(viewing, Dock.Top); root.Children.Add(viewing);
        var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var coordinates = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        fields = new TextBox[4];
        for (var i = 0; i < 4; i++)
        {
            var group = new StackPanel { Margin = new Thickness(7, 0, 7, 0) };
            group.Children.Add(new TextBlock { Text = new[] { "X", "Y", "Width", "Height" }[i], Foreground = Brushes.Silver });
            fields[i] = new TextBox { Width = 90, Padding = new Thickness(5), Margin = new Thickness(0, 3, 0, 0), Background = new SolidColorBrush(Color.FromRgb(28, 37, 48)), Foreground = Brushes.Ivory, BorderBrush = Brushes.SlateGray };
            fields[i].LostKeyboardFocus += (_, _) => ApplyFields(); group.Children.Add(fields[i]); coordinates.Children.Add(group);
        }
        footer.Children.Add(coordinates);
        status = new TextBlock { TextAlignment = TextAlignment.Center, Foreground = Brushes.Silver, Margin = new Thickness(0, 8, 0, 8) }; footer.Children.Add(status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var reset = new PixelButton { Content = "إعادة التحديد", Glyph = PixelButtonGlyph.Reset, Tone = PixelButtonTone.Blue, Margin = new Thickness(0, 0, 8, 0) };
        reset.Click += (_, _) => { Selection = new(0, 0, preview.Width, preview.Height); UpdateFields(); DrawSelection(); };
        var cancel = new PixelButton { Content = "إلغاء", IsCancel = true, Tone = PixelButtonTone.Coral, Glyph = PixelButtonGlyph.Cancel, Margin = new Thickness(0, 0, 8, 0) };
        var save = new PixelButton { Content = "حفظ نسخة مقصوصة", IsDefault = true, Tone = PixelButtonTone.Amber, Glyph = PixelButtonGlyph.Check };
        save.Click += (_, _) => { if (ApplyFields()) DialogResult = true; };
        buttons.Children.Add(reset); buttons.Children.Add(cancel); buttons.Children.Add(save); footer.Children.Add(buttons);
        using var input = new MemoryStream(preview.Png, false);
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = input; bitmap.EndInit(); bitmap.Freeze();
        surface = new Grid { Background = new SolidColorBrush(Color.FromRgb(10, 15, 22)), ClipToBounds = true };
        photograph = new Image { Source = bitmap, Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        surface.Children.Add(photograph);
        overlay = new Canvas { Background = Brushes.Transparent, Cursor = Cursors.Cross }; surface.Children.Add(overlay);
        marquee = new Rectangle { Stroke = new SolidColorBrush(Color.FromRgb(255, 179, 71)), StrokeThickness = 1.5, Fill = new SolidColorBrush(Color.FromArgb(38, 255, 179, 71)), IsHitTestVisible = false };
        overlay.Children.Add(marquee);
        for (var i = 0; i < 4; i++) { var corner = new Rectangle { Width = 8, Height = 8, Fill = Brushes.Ivory, Stroke = Brushes.DarkOrange, StrokeThickness = 1, IsHitTestVisible = false }; handles.Add(corner); overlay.Children.Add(corner); }
        root.Children.Add(surface);
        zoom.ValueChanged += (_, _) => DrawSelection();
        aspect.SelectionChanged += (_, _) =>
        {
            if (Ratio is double ratio) { var width = Math.Min(Selection.Width, (int)(Selection.Height * ratio)); var height = Math.Min(Selection.Height, (int)(width / ratio)); Selection = new(Selection.X, Selection.Y, Math.Max(1, width), Math.Max(1, height)); UpdateFields(); DrawSelection(); }
        };
        surface.SizeChanged += (_, _) => DrawSelection();
        overlay.MouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { panStart = e.GetPosition(overlay); originalPan = pan; overlay.CaptureMouse(); e.Handled = true; } };
        overlay.MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { panStart = null; overlay.ReleaseMouseCapture(); e.Handled = true; } };
        overlay.MouseLeftButtonDown += (_, e) =>
        {
            if (imageBounds.Width <= 0) return;
            var at = e.GetPosition(overlay); handle = -1;
            for (var i = 0; i < handles.Count; i++) if (new Rect(Canvas.GetLeft(handles[i]) - 4, Canvas.GetTop(handles[i]) - 4, 16, 16).Contains(at)) { handle = i; break; }
            var pixels = PixelPoint(at); moving = handle < 0 && Selection.Width < preview.Width && new Rect(Selection.X, Selection.Y, Selection.Width, Selection.Height).Contains(pixels);
            dragOriginal = Selection; anchor = handle >= 0 || moving ? at : Clamp(at); overlay.CaptureMouse(); e.Handled = true;
        };
        overlay.MouseMove += (_, e) =>
        {
            if (panStart is Point panFrom && e.MiddleButton == MouseButtonState.Pressed) { var at = e.GetPosition(overlay); pan = new(originalPan.X + at.X - panFrom.X, originalPan.Y + at.Y - panFrom.Y); DrawSelection(); return; }
            if (anchor is not { } start || imageBounds.Width <= 0) return;
            var end = Clamp(e.GetPosition(overlay));
            if (moving && dragOriginal is { } original)
            {
                var from = PixelPoint(start); var to = PixelPoint(end);
                Selection = original with { X = Math.Clamp(original.X + (int)Math.Round(to.X - from.X), 0, preview.Width - original.Width), Y = Math.Clamp(original.Y + (int)Math.Round(to.Y - from.Y), 0, preview.Height - original.Height) };
            }
            else
            {
                if (handle >= 0 && dragOriginal is { } old) start = ViewPoint(new(handle is 0 or 2 ? old.X + old.Width : old.X, handle is 0 or 1 ? old.Y + old.Height : old.Y));
                if (Ratio is double ratio)
                {
                    var dx = end.X - start.X; var dy = end.Y - start.Y; var width = Math.Min(Math.Abs(dx), Math.Abs(dy) * ratio);
                    end = Clamp(new(start.X + Math.Sign(dx) * width, start.Y + Math.Sign(dy) * width / ratio));
                }
                Selection = CropSelection.FromNormalized((start.X - imageBounds.X) / imageBounds.Width, (start.Y - imageBounds.Y) / imageBounds.Height,
                (end.X - imageBounds.X) / imageBounds.Width, (end.Y - imageBounds.Y) / imageBounds.Height, preview.Width, preview.Height);
            }
            UpdateFields(); DrawSelection();
        };
        overlay.MouseLeftButtonUp += (_, e) => { anchor = null; overlay.ReleaseMouseCapture(); e.Handled = true; };
        overlay.LostMouseCapture += (_, _) => anchor = null;
        UpdateFields();
    }
    private Point PixelPoint(Point point) => new((point.X - imageBounds.X) * preview.Width / imageBounds.Width, (point.Y - imageBounds.Y) * preview.Height / imageBounds.Height);
    private Point ViewPoint(Point point) => new(imageBounds.X + point.X * imageBounds.Width / preview.Width, imageBounds.Y + point.Y * imageBounds.Height / preview.Height);
    public void SetBatchContext(int count) => heading.Text = $"معاينة أول صورة من {count} · يُطبق مستطيل البكسل نفسه على كل صورة؛ الحدود غير المناسبة تُسجّل كفشل منفصل";
    private Point Clamp(Point point) => new(Math.Clamp(point.X, imageBounds.Left, imageBounds.Right), Math.Clamp(point.Y, imageBounds.Top, imageBounds.Bottom));
    private void UpdateFields()
    {
        var values = new[] { Selection.X, Selection.Y, Selection.Width, Selection.Height };
        for (var i = 0; i < fields.Length; i++) fields[i].Text = values[i].ToString(CultureInfo.InvariantCulture);
        status.Text = $"{Selection.Width} × {Selection.Height} / {preview.Width} × {preview.Height}";
    }
    private bool ApplyFields()
    {
        try
        {
            var values = fields.Select(x => int.Parse(x.Text, CultureInfo.InvariantCulture)).ToArray();
            var crop = new CropRectangle(values[0], values[1], values[2], values[3]); crop.Validate(preview.Width, preview.Height);
            Selection = crop; UpdateFields(); DrawSelection(); return true;
        }
        catch (Exception error) when (error is FormatException or OverflowException or ArgumentOutOfRangeException)
        { status.Text = "أدخل منطقة صحيحة داخل حدود الصورة"; return false; }
    }
    private void DrawSelection()
    {
        if (surface.ActualWidth <= 0 || surface.ActualHeight <= 0) return;
        var scale = Math.Min(surface.ActualWidth / preview.Width, surface.ActualHeight / preview.Height) * zoom.Value;
        pan = new(Math.Clamp(pan.X, -Math.Max(0, (preview.Width * scale - surface.ActualWidth) / 2), Math.Max(0, (preview.Width * scale - surface.ActualWidth) / 2)), Math.Clamp(pan.Y, -Math.Max(0, (preview.Height * scale - surface.ActualHeight) / 2), Math.Max(0, (preview.Height * scale - surface.ActualHeight) / 2)));
        imageBounds = new((surface.ActualWidth - preview.Width * scale) / 2 + pan.X, (surface.ActualHeight - preview.Height * scale) / 2 + pan.Y, preview.Width * scale, preview.Height * scale);
        // Use the original aspect ratio even when thumbnail pixel dimensions were rounded.
        photograph.Width = imageBounds.Width; photograph.Height = imageBounds.Height; photograph.Margin = new Thickness(imageBounds.X, imageBounds.Y, 0, 0);
        Canvas.SetLeft(marquee, imageBounds.X + Selection.X * scale); Canvas.SetTop(marquee, imageBounds.Y + Selection.Y * scale);
        marquee.Width = Selection.Width * scale; marquee.Height = Selection.Height * scale;
        for (var i = 0; i < handles.Count; i++)
        {
            var point = ViewPoint(new(Selection.X + (i is 1 or 3 ? Selection.Width : 0), Selection.Y + (i >= 2 ? Selection.Height : 0)));
            Canvas.SetLeft(handles[i], point.X - 4); Canvas.SetTop(handles[i], point.Y - 4);
        }
    }
}
