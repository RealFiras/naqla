using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Documents;
using Naqla.Core;

namespace Naqla.App;

/// <summary>Transient settings/real measurement after Drop; shares native pixel buttons.
/// No encoder work occurs on the dispatcher, and closing waits through PendingMeasurement.</summary>
internal sealed class TargetSizeWindow : Window
{
    private readonly Func<TargetSizeSettings, IProgress<ImageOptimizationProgress>, CancellationToken, Task<ImageOptimizationPreview>> measure;
    private readonly CancellationToken operationToken;
    private readonly int count;
    private readonly TextBox amount, width, height;
    private readonly CheckBox resize;
    private readonly PixelButton unit, format, calculate, stop, save;
    private readonly TextBlock currentSize, target, result, note;
    private readonly Image image;
    private readonly Dictionary<long, PixelButton> presets = [];
    private ImageSizeUnit sizeUnit;
    private string outputFormat;
    private CancellationTokenSource? cancellation;
    private int revision;
    private bool ready, closed;
    public Task PendingMeasurement { get; private set; } = Task.CompletedTask;
    public ImageOptimizationPreview? Measurement { get; private set; }
    public TargetSizeSettings? MeasuredSettings { get; private set; }
    public bool CanSave => MeasuredSettings is not null && Measurement is not null && PendingMeasurement.IsCompleted && (Measurement.Report.Achieved || count > 1);
    public string ResultText => result.Text;

    public TargetSizeWindow(ImageOptimizationSource source, int count,
        Func<TargetSizeSettings, IProgress<ImageOptimizationProgress>, CancellationToken, Task<ImageOptimizationPreview>> measure, CancellationToken operationToken = default)
    {
        this.count = count; this.measure = measure; this.operationToken = operationToken; outputFormat = source.DefaultFormat;
        Title = "Naqla · حجم مستهدف"; Width = 560; Height = 690; MinWidth = 460; MinHeight = 420;
        MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
        MinWidth = Math.Min(MinWidth, MaxWidth); MinHeight = Math.Min(MinHeight, MaxHeight); Width = Math.Min(Width, MaxWidth); Height = Math.Min(Height, MaxHeight);
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = Paint("#121820"); Foreground = Paint("#F5F0E6"); FontFamily = new("Segoe UI"); FontSize = 12;
        Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), (Style)XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ScrollBar">
              <Setter Property="Width" Value="7"/><Setter Property="Background" Value="#17212B"/>
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar">
                <Grid Background="{TemplateBinding Background}"><Track x:Name="PART_Track" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Orientation="Vertical" IsDirectionReversed="True">
                  <Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False"/></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#53717C" Margin="1"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False"/></Track.IncreaseRepeatButton>
                </Track></Grid>
              </ControlTemplate></Setter.Value></Setter>
            </Style>
            """));
        var icon = Path.Combine(AppContext.BaseDirectory, "preview", "LOGO.png"); if (File.Exists(icon)) Icon = BitmapFrame.Create(new Uri(icon));
        var root = new DockPanel { Margin = new Thickness(16), Background = Background, FlowDirection = FlowDirection.RightToLeft }; Content = root;
        var footer = new StackPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        result = Label("لم يُقَس الناتج بعد. المعاينة ترمّز الصورة كاملة دون حفظ نسخة.", "#C3C8D0"); footer.Children.Add(result);
        var buttons = new WrapPanel { Margin = new Thickness(0, 9, 0, 0) }; footer.Children.Add(buttons);
        save = Button(count > 1 ? "معالجة الدفعة" : "حفظ نسخة", PixelButtonTone.Amber, PixelButtonGlyph.Check); save.IsEnabled = false;
        save.Click += (_, _) => { if (CanSave) DialogResult = true; }; buttons.Children.Add(save);
        calculate = Button("قياس الناتج", PixelButtonTone.Mint, PixelButtonGlyph.Details); calculate.Click += (_, _) => { PendingMeasurement = MeasureAsync(); }; buttons.Children.Add(calculate);
        stop = Button("إيقاف القياس", PixelButtonTone.Blue, PixelButtonGlyph.Hide); stop.IsEnabled = false; stop.Click += (_, _) => cancellation?.Cancel(); buttons.Children.Add(stop);
        var cancel = Button("إلغاء", PixelButtonTone.Coral, PixelButtonGlyph.Cancel); cancel.IsCancel = true; buttons.Children.Add(cancel);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; root.Children.Add(scroll);
        var body = new StackPanel(); scroll.Content = body;
        var heading = Label("حجم مستهدف", "#65DAB4"); heading.FontSize = 22; heading.FontWeight = FontWeights.SemiBold; body.Children.Add(heading);
        body.Children.Add(Label(count > 1 ? $"{count} صور · معاينة الصورة رقم {source.PreviewIndex + 1} فقط، والحد لكل صورة" + (source.PreviewIndex > 0 ? " · غير المقروءة تُبلّغ كفشل في الدفعة" : "") : "ترميز محلي · الأبعاد الأصلية محفوظة افتراضيًا", "#C3C8D0"));
        currentSize = Label("", "#F5F0E6"); SetCurrent(source.Bytes, source.Image.Width, source.Image.Height); body.Children.Add(currentSize);
        var choices = new WrapPanel { Margin = new Thickness(0, 10, 0, 5) }; body.Children.Add(choices);
        foreach (var bytes in TargetSizeSettings.Presets)
        {
            var preset = Button(TargetSizeSettings.Size(bytes), PixelButtonTone.Blue, PixelButtonGlyph.None); presets.Add(bytes, preset); choices.Children.Add(preset);
            preset.Click += (_, _) => { sizeUnit = bytes >= 1048576 ? ImageSizeUnit.MB : ImageSizeUnit.KB; unit!.Content = sizeUnit.ToString(); amount!.Text = (bytes / (sizeUnit == ImageSizeUnit.KB ? 1024 : 1048576)).ToString(CultureInfo.InvariantCulture); Changed(); };
        }
        var custom = new WrapPanel { Margin = new Thickness(0, 4, 0, 5) }; body.Children.Add(custom);
        custom.Children.Add(Label("مخصص", "#C3C8D0"));
        amount = Field("TargetAmount", "200", 100); custom.Children.Add(amount);
        unit = Button("KB", PixelButtonTone.Blue, PixelButtonGlyph.None); unit.MinWidth = 55; custom.Children.Add(unit);
        unit.Click += (_, _) => { sizeUnit = sizeUnit == ImageSizeUnit.KB ? ImageSizeUnit.MB : ImageSizeUnit.KB; unit.Content = sizeUnit.ToString(); Changed(); };
        format = Button(outputFormat, PixelButtonTone.Violet, PixelButtonGlyph.None); format.MinWidth = 80; format.ToolTip = "صيغة الناتج · المرمّزات المحلية المتاحة"; custom.Children.Add(format);
        format.Click += (_, _) =>
        {
            var menu = new ContextMenu { FlowDirection = FlowDirection.LeftToRight };
            foreach (var available in source.Formats)
            {
                var entry = new MenuItem { Header = available, IsCheckable = true, IsChecked = outputFormat == available };
                entry.Click += (_, _) => { outputFormat = available; format.Content = available; Changed(); }; menu.Items.Add(entry);
            }
            format.ContextMenu = menu; menu.PlacementTarget = format; menu.IsOpen = true;
        };
        target = Label("", "#FFC35A"); body.Children.Add(target);
        resize = new CheckBox { Content = "تصغير اختياري ضمن حدود · نفس النسبة ودون تكبير", Foreground = Foreground, Margin = new Thickness(0, 10, 0, 5) }; body.Children.Add(resize);
        var dimensions = new WrapPanel(); body.Children.Add(dimensions);
        dimensions.Children.Add(Label("أقصى عرض", "#C3C8D0")); width = Field("MaximumWidth", source.Image.Width.ToString(CultureInfo.InvariantCulture), 78); dimensions.Children.Add(width);
        dimensions.Children.Add(Label("أقصى ارتفاع", "#C3C8D0")); height = Field("MaximumHeight", source.Image.Height.ToString(CultureInfo.InvariantCulture), 78); dimensions.Children.Add(height);
        width.IsEnabled = height.IsEnabled = false;
        resize.Checked += (_, _) => { width.IsEnabled = height.IsEnabled = true; Changed(); };
        resize.Unchecked += (_, _) => { width.IsEnabled = height.IsEnabled = false; Changed(); };
        note = Label("", "#C3C8D0"); note.FontSize = 11; note.Margin = new Thickness(0, 7, 0, 8); body.Children.Add(note);
        image = new Image { Height = 170, Stretch = Stretch.Uniform, Source = Bitmap(source.Image.Png) };
        body.Children.Add(new Border { Background = Paint("#202B37"), BorderBrush = Paint("#354352"), BorderThickness = new Thickness(1), Padding = new Thickness(8), Child = image });
        body.Children.Add(Label("العرض مصغّر للمعاينة؛ القياس للملف الكامل. تُحفظ نسخة بجانب الأصل.", "#C3C8D0"));
        Closed += (_, _) => { closed = true; cancellation?.Cancel(); };
        ready = true; Changed();
    }
    private TargetSizeSettings ReadSettings()
    {
        var settings = new TargetSizeSettings(TargetSizeSettings.ParseBytes(amount.Text, sizeUnit), outputFormat,
            resize.IsChecked == true ? new(int.Parse(width.Text, CultureInfo.InvariantCulture), int.Parse(height.Text, CultureInfo.InvariantCulture)) : null);
        settings.Validate(); return settings;
    }
    private void Changed()
    {
        if (!ready || closed) return;
        revision++; cancellation?.Cancel(); Measurement = null; MeasuredSettings = null; save.IsEnabled = false;
        result.Foreground = Paint("#C3C8D0"); result.Text = "الإعدادات تغيرت؛ أعد القياس. لا يُحفظ شيء أثناء المعاينة.";
        foreach (var entry in presets) entry.Value.Tone = PixelButtonTone.Blue;
        try
        {
            var settings = ReadSettings(); target.Inlines.Clear(); Part(target, "الحد: "); Part(target, $"{TargetSizeSettings.Size(settings.TargetBytes)} · {settings.TargetBytes:N0} B", true);
            foreach (var entry in presets) entry.Value.Tone = entry.Key == settings.TargetBytes ? PixelButtonTone.Mint : PixelButtonTone.Blue;
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException) { target.Text = error.Message; }
        note.Text = outputFormat is "PNG" or "TIFF" ? "ضغط دون فقد للبكسلات المحوّلة. قد يستحيل الحد دون تصغير أو صيغة أخرى." :
            outputFormat == "JPG" ? "أعلى جودة ترميز تحقق الحد. الشفافية تصبح بيضاء في JPG." : "أعلى جودة ترميز تحقق الحد مع الاحتفاظ بالشفافية. القياس قد يستغرق وقتًا.";
    }
    public Task StartMeasurementAsync() => PendingMeasurement = MeasureAsync();
    private async Task MeasureAsync()
    {
        if (cancellation is not null || closed) return;
        TargetSizeSettings settings;
        try { settings = ReadSettings(); }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException) { result.Text = error.Message; return; }
        var generation = revision;
        using var current = CancellationTokenSource.CreateLinkedTokenSource(operationToken); cancellation = current;
        calculate.IsEnabled = false; stop.IsEnabled = true; save.IsEnabled = false;
        Measurement = null; MeasuredSettings = null; result.Text = "جارٍ الترميز والقياس الفعلي…";
        var observer = new Progress<ImageOptimizationProgress>(state =>
        {
            if (!closed && generation == revision && ReferenceEquals(cancellation, current) && !current.IsCancellationRequested)
                result.Text = $"قياس {state.Attempts} · {(state.Quality.HasValue ? $"جودة {state.Quality}" : "دون فقد")} · أقل حجم جُرّب {TargetSizeSettings.Size(state.BestBytes)}";
        });
        try
        {
            var measured = await measure(settings, observer, current.Token);
            if (closed || current.IsCancellationRequested || revision != generation) return;
            ApplyMeasurement(settings, measured);
        }
        catch (OperationCanceledException) { if (!closed && generation == revision) result.Text = "أُلغي القياس؛ لم تُحفظ نسخة."; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or ImageMagick.MagickException)
        { if (!closed && generation == revision) { result.Foreground = Paint("#FF8390"); result.Text = "تعذر القياس المحلي. تحقق من الصورة والإعدادات والصلاحيات."; } }
        finally
        {
            cancellation = null;
            if (!closed) { calculate.IsEnabled = true; stop.IsEnabled = false; _ = Dispatcher.BeginInvoke(() => save.IsEnabled = CanSave); }
        }
    }
    private void ApplyMeasurement(TargetSizeSettings settings, ImageOptimizationPreview measured)
    {
        Measurement = measured; MeasuredSettings = settings;
        image.Source = Bitmap(measured.Image.Png);
        var report = measured.Report;
        SetCurrent(report.OriginalBytes, report.SourceWidth, report.SourceHeight);
        result.Foreground = Paint(report.Achieved ? "#65DAB4" : "#FF8390");
        result.Inlines.Clear();
        Part(result, report.Achieved ? "القياس يحقق الحد. " : "الحد غير قابل للتحقيق بهذه الإعدادات؛ لم تُحفظ نسخة. ");
        Part(result, $"{report.Format} · {report.Width}×{report.Height}", true);
        result.Inlines.Add(new LineBreak()); Part(result, "الناتج: "); Part(result, $"{TargetSizeSettings.Size(report.ResultBytes)} ({report.ResultBytes:N0} B)", true);
        Part(result, " · الحد: "); Part(result, TargetSizeSettings.Size(report.TargetBytes), true);
        result.Inlines.Add(new LineBreak());
        if (report.Quality is { } quality) { Part(result, "جودة المرمّز: "); Part(result, $"{quality}/100", true); }
        else Part(result, "ضغط دون فقد");
        Part(result, " · ترميزات: "); Part(result, report.Attempts.ToString(CultureInfo.InvariantCulture), true);
        if (report.ResultBytes > report.OriginalBytes) Part(result, "\nالناتج المقاس أكبر من الأصل رغم مقارنته بالحد.");
        if (count > 1) Part(result, "\nالدفعة تُقاس لكل صورة؛ ما يتجاوز الحد يفشل منفردًا.");
        Part(result, "\nالحفظ يعيد الترميز والتحقق من الحد.");
    }
    private TextBox Field(string name, string value, double size)
    {
        var field = new TextBox { Name = name, Text = value, Width = size, Padding = new Thickness(5), Margin = new Thickness(4, 0, 4, 0), Background = Paint("#202C39"), Foreground = Foreground, BorderBrush = Paint("#4B5B6C"), FlowDirection = FlowDirection.LeftToRight };
        field.TextChanged += (_, _) => Changed(); return field;
    }
    private static PixelButton Button(string text, PixelButtonTone tone, PixelButtonGlyph glyph) => new() { Content = text, Tone = tone, Glyph = glyph, Margin = new Thickness(0, 0, 5, 5) };
    private static TextBlock Label(string text, string color) => new() { Text = text, Foreground = Paint(color), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 3) };
    private static BitmapImage Bitmap(byte[] png)
    {
        using var stream = new MemoryStream(png); var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
    }
    private static SolidColorBrush Paint(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private void SetCurrent(long bytes, int imageWidth, int imageHeight)
    {
        currentSize.Inlines.Clear(); Part(currentSize, "الحالي: "); Part(currentSize, $"{TargetSizeSettings.Size(bytes)} ({bytes:N0} B) · {imageWidth}×{imageHeight}", true);
    }
    private static void Part(TextBlock block, string text, bool leftToRight = false) => block.Inlines.Add(new Run(text) { FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft });
}
