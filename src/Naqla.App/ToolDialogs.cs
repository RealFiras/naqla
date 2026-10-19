using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Naqla.Core;
using Naqla.Conversion;

namespace Naqla.App;

/// <summary>Shared transient editor surface, never a startup window or permanent dashboard.</summary>
internal class ToolDialog : Window
{
    protected readonly StackPanel Body = new();
    protected readonly TextBlock Status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 10, 0, 10), Foreground = Brushes.Silver };
    protected readonly PixelButton Confirm = new() { Content = "إنشاء نسخة", Glyph = PixelButtonGlyph.Check, Tone = PixelButtonTone.Amber };
    protected readonly CancellationTokenSource Lifetime;
    public Task PendingWork { get; protected set; } = Task.CompletedTask;
    protected bool ClosedAlready;
    public ToolDialog(string title, CancellationToken cancellation = default)
    {
        Lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        Title = "Naqla · " + title; Width = 590; Height = 630; MinWidth = 400; MinHeight = 320;
        MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Paint("#121820"); Foreground = Paint("#F5F0E6"); FontFamily = new("Segoe UI"); FontSize = 13;
        PixelControls.Apply(Resources);
        Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = System.Windows.Markup.XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ScrollBar">
             <Setter Property="Width" Value="7"/><Setter Property="Background" Value="#17212B"/>
             <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar"><Grid Background="{TemplateBinding Background}">
              <Track x:Name="PART_Track" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Orientation="{TemplateBinding Orientation}" IsDirectionReversed="True">
               <Track.DecreaseRepeatButton><RepeatButton x:Name="Decrease" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False"/></Track.DecreaseRepeatButton>
               <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#53717C" Margin="1"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
               <Track.IncreaseRepeatButton><RepeatButton x:Name="Increase" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False"/></Track.IncreaseRepeatButton>
              </Track></Grid><ControlTemplate.Triggers><Trigger Property="Orientation" Value="Horizontal"><Setter TargetName="PART_Track" Property="IsDirectionReversed" Value="False"/><Setter TargetName="Decrease" Property="Command" Value="ScrollBar.PageLeftCommand"/><Setter TargetName="Increase" Property="Command" Value="ScrollBar.PageRightCommand"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
             <Style.Triggers><Trigger Property="Orientation" Value="Horizontal"><Setter Property="Width" Value="Auto"/><Setter Property="Height" Value="7"/></Trigger></Style.Triggers>
            </Style>
            """);
        var root = new DockPanel { Margin = new(18), FlowDirection = FlowDirection.RightToLeft, Background = Background }; Content = root;
        var heading = new TextBlock { Text = title, Foreground = Paint("#FFC35A"), FontSize = 22, Margin = new(0, 0, 0, 12) }; DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); footer.Children.Add(Status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal }; footer.Children.Add(buttons);
        buttons.Children.Add(Confirm); buttons.Children.Add(new PixelButton { Content = "إلغاء", Glyph = PixelButtonGlyph.Cancel, Tone = PixelButtonTone.Coral, IsCancel = true, Margin = new(8, 0, 0, 0) });
        root.Children.Add(new ScrollViewer { Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Closed += (_, _) => { ClosedAlready = true; Lifetime.Cancel(); _ = DisposeWhenFinishedAsync(); };
    }
    private async Task DisposeWhenFinishedAsync() { try { await PendingWork; } catch { /* operation's UI already reports failures */ } finally { Lifetime.Dispose(); } }
    internal static SolidColorBrush Paint(string value) => new((Color)ColorConverter.ConvertFromString(value));
    protected void Note(string text) => Body.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Silver, Margin = new(0, 5, 0, 10) });
    protected TextBox Field(string label, string value, bool multiline = false)
    {
        Body.Children.Add(new TextBlock { Text = label, Margin = new(0, 7, 0, 4) });
        var field = new TextBox { Text = value, Background = Paint("#1C2530"), Foreground = Foreground, BorderBrush = Paint("#415266"), Padding = new(8), MinHeight = 34, FlowDirection = FlowDirection.LeftToRight, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden };
        Body.Children.Add(field); return field;
    }
    protected ComboBox Choice(string label, params string[] values)
    {
        Body.Children.Add(new TextBlock { Text = label, Margin = new(0, 7, 0, 4) });
        var box = new ComboBox { ItemsSource = values, SelectedIndex = 0 }; Body.Children.Add(box); return box;
    }
    protected CheckBox Flag(string label, bool value = true)
    {
        var box = new CheckBox { Content = label, IsChecked = value, Foreground = Foreground, Margin = new(0, 8, 0, 4) }; Body.Children.Add(box); return box;
    }
    protected PixelButton Action(string label, Action action)
    {
        var button = new PixelButton { Content = label, Tone = PixelButtonTone.Amber, Margin = new(0, 10, 0, 4), HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => action(); Body.Children.Add(button); return button;
    }
    protected static double Number(TextBox field) => double.TryParse(field.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : throw new ArgumentException("أدخل أرقاماً صحيحة بالنقطة العشرية، والزمن بالثواني");
    internal static BitmapSource Bitmap(byte[] png)
    {
        using var stream = new MemoryStream(png); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
    }
    protected async Task WorkAsync(Func<CancellationToken, Task> operation)
    {
        Confirm.IsEnabled = false;
        Body.IsEnabled = false;
        try { await operation(Lifetime.Token); }
        catch (OperationCanceledException) { if (!ClosedAlready) Status.Text = "أُلغيت العملية؛ لم تُحفظ نتيجة"; }
        catch (Exception error) { if (!ClosedAlready) Status.Text = error.Message; }
        finally { if (!ClosedAlready) Body.IsEnabled = true; }
    }
}

internal sealed class MediaToolsWindow : ToolDialog
{
    public MediaToolOptions? Accepted { get; private set; }
    public MediaToolsWindow(DraggedFile file, MediaDetails info, string operation, int count, AdvancedMediaService service, BatchProcessor queue, CancellationToken token) : base(operation switch { "TRIM" => "قص زمني", "FRAMES" => "استخراج الإطارات", "NORMALIZE" => "تطبيع الصوت", "SILENCE" => "قص الصمت", _ => "قنوات الصوت" }, token)
    {
        Note($"{file.Extension} · {file.DisplaySize} · {info.Duration:0.###} ثانية · {info.Channels} قنوات" + (count > 1 ? $" · {count} ملفات؛ تُراجع الحدود لكل مصدر" : ""));
        var start = new TextBox(); var end = new TextBox(); var interval = new TextBox(); var amount = new TextBox(); ComboBox? selection = null, format = null;
        TextBox? integrated = null, peak = null, range = null, threshold = null, minimum = null; CheckBox? leading = null, trailing = null; ComboBox? channels = null;
        if (operation is "TRIM" or "FRAMES")
        {
            start = Field(operation == "FRAMES" ? "التوقيت / بداية النطاق (ثوانٍ)" : "البداية (ثوانٍ)", "0"); end = Field("نهاية النطاق (ثوانٍ)", info.Duration.ToString("0.########", CultureInfo.InvariantCulture));
            if (operation == "FRAMES")
            {
                selection = Choice("طريقة الاستخراج", "إطار واحد", "كل N ثانية", "عدد ثابت");
                interval = Field("الفاصل بالثواني", "5"); amount = Field("عدد الإطارات (حتى 300)", "6"); format = Choice("صيغة الصور", "PNG", "JPG");
            }
            var timeline = new Slider { Minimum = 0, Maximum = Math.Max(0, info.Duration - .001), Value = 0, Margin = new(0, 10, 0, 0), FlowDirection = FlowDirection.LeftToRight };
            Body.Children.Add(timeline); timeline.ValueChanged += (_, _) => start.Text = timeline.Value.ToString("0.###", CultureInfo.InvariantCulture);
            if (info.HasVideo)
            {
                var image = new Image { Height = 175, Stretch = Stretch.Uniform }; Body.Children.Add(image);
                Action("معاينة الإطار عند البداية", () => PendingWork = WorkAsync(async ct => { var png = await queue.RunExclusiveAsync(t => service.PreviewFrameAsync(file, Number(start), t), ct); if (!ClosedAlready) { image.Source = Bitmap(png); Status.Text = "معاينة فعلية من الملف؛ قد يُعاد الترميز عند القص لضبط التوقيت"; Confirm.IsEnabled = true; } }));
            }
            Note(operation == "FRAMES" ? "تُحفظ الصور مع تقرير التوقيت الفعلي في مجلد جديد بجانب المصدر." : "قص دقيق مع إعادة ترميز؛ لا يُدّعى أن قص النسخ السريع دقيق بين الإطارات المفتاحية.");
        }
        if (operation == "NORMALIZE")
        {
            var preset = Choice("الإعداد", "بودكاست · -16 LUFS", "كلام · -18 LUFS", "مخصص");
            integrated = Field("الشدة المتكاملة LUFS (-40 إلى -5)", "-16"); peak = Field("الذروة الحقيقية dBTP (-9 إلى 0)", "-1.5"); range = Field("النطاق LRA (1 إلى 20)", "11");
            preset.SelectionChanged += (_, _) => { if (preset.SelectedIndex < 2) { integrated.Text = preset.SelectedIndex == 0 ? "-16" : "-18"; peak.Text = "-1.5"; range.Text = "11"; } };
            Note("قياس فعلي بتمريرين loudnorm، ثم قياس مستقل للنتيجة. تُرفض النتيجة إن لم تحقق حدود القياس.");
        }
        if (operation == "SILENCE") { threshold = Field("عتبة الصمت dB (-80 إلى -10)", "-40"); minimum = Field("مدة الصمت الدنيا بالثواني", "0.15"); leading = Flag("قص الصمت في البداية"); trailing = Flag("قص الصمت في النهاية"); Note("يُقص الصمت عند الأطراف، وتبقى الوقفات داخل التسجيل."); }
        if (operation == "CHANNELS") { channels = Choice("القنوات", "أحادي Mono", "نسخ الأحادي إلى قناتين Dual mono"); Note("التحويل إلى قناتين يتطلب مصدراً أحادي القناة؛ لا يصنع مشهداً صوتياً ستيريو."); }
        Confirm.Click += (_, _) =>
        {
            try
            {
                TrimRange? trim = operation == "TRIM" ? new(Number(start), Number(end)) : null; trim?.Validate(info.Duration);
                var countValue = operation == "FRAMES" && selection!.SelectedIndex == 2 ? Number(amount) : 6;
                if (countValue != Math.Truncate(countValue)) throw new ArgumentException("عدد الإطارات يجب أن يكون صحيحاً");
                FrameSettings? frames = operation == "FRAMES" ? new((FrameSelection)selection!.SelectedIndex, Number(start), selection.SelectedIndex == 0 ? null : Number(end), selection.SelectedIndex == 1 ? Number(interval) : 5, checked((int)countValue), format!.SelectedItem.ToString()!) : null; frames?.Timestamps(info.Duration);
                Accepted = new(Trim: trim, Frames: frames, Loudness: integrated is null ? null : new(Number(integrated), Number(peak!), Number(range!)), Silence: threshold is null ? null : new(Number(threshold), Number(minimum!), leading!.IsChecked == true, trailing!.IsChecked == true), Channels: channels is null ? AudioChannels.Preserve : channels.SelectedIndex == 0 ? AudioChannels.Mono : AudioChannels.DualMonoStereo);
                Accepted.Validate(); if (Accepted.Channels == AudioChannels.DualMonoStereo && info.Channels != 1) throw new ArgumentException("المصدر ليس أحادي القناة"); DialogResult = true;
            }
            catch (Exception error) when (error is ArgumentException or OverflowException) { Status.Text = error.Message; }
        };
    }
}

internal sealed class ModelToolsWindow : ToolDialog
{
    public ModelPlan? Accepted { get; private set; }
    private sealed class CueRow
    {
        public double Start { get; set; } public double End { get; set; } public string Text { get; set; } = "";
    }
    public ModelToolsWindow(DraggedFile file, string operation, LocalModelService service, BatchProcessor queue, CancellationToken token) : base(operation switch { "OCR" => "استخراج النص محلياً", "BACKGROUND" => "إزالة الخلفية", _ => "ترجمة كلام إلى نص محلياً" }, token)
    {
        Note($"{file.Extension} · {file.DisplaySize} · لا اتصال ولا رفع للملف");
        var language = Choice("اللغة", "العربية والإنجليزية / تلقائي", "العربية", "English");
        if (operation == "BACKGROUND") language.Visibility = Visibility.Collapsed;
        var text = Field("معاينة قابلة للتحرير", "", true); text.Height = 190; text.FlowDirection = FlowDirection.RightToLeft;
        var image = new Image { Height = 240, Stretch = Stretch.Uniform, Visibility = operation == "BACKGROUND" ? Visibility.Visible : Visibility.Collapsed }; Body.Children.Add(image);
        var grid = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = true, Height = 230, Background = Paint("#1C2530"), Foreground = Foreground, RowBackground = Paint("#1C2530"), FlowDirection = FlowDirection.LeftToRight, Visibility = operation == "SUBTITLE" ? Visibility.Visible : Visibility.Collapsed };
        var header = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)); header.Setters.Add(new Setter(Control.BackgroundProperty, Paint("#263443"))); header.Setters.Add(new Setter(Control.ForegroundProperty, Foreground)); header.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6))); grid.ColumnHeaderStyle = header;
        foreach (var pair in new[] { ("Start", "بداية (ثانية)", 90d), ("End", "نهاية", 90d), ("Text", "النص", 300d) }) grid.Columns.Add(new DataGridTextColumn { Header = pair.Item2, Binding = new System.Windows.Data.Binding(pair.Item1), Width = pair.Item3 }); Body.Children.Add(grid);
        if (operation != "OCR") text.Visibility = Visibility.Collapsed;
        var output = Choice("صيغة الحفظ", operation == "SUBTITLE" ? ["SRT", "WebVTT"] : operation == "BACKGROUND" ? ["PNG شفاف"] : ["TXT · UTF-8"]);
        Note(operation == "SUBTITLE" ? "يُفضّل نموذج Base المحلي عند توفره. قد يخطئ في الأسماء واللهجات والكلام المتداخل، وخاصة العربي. راجع النص والأزمنة؛ لا تُنشأ ترجمة بين اللغات." : operation == "BACKGROUND" ? "معاينة قناع U2NetP الفعلي؛ قد تحتاج الحواف الدقيقة إلى تنقيح خارجي." : "راجع النص قبل الحفظ؛ جودة الاستخراج تعتمد على دقة الصورة والخط والتخطيط.");
        OcrResult? ocr = null; ImagePreview? mask = null; SubtitleDocument? subtitles = null; string? digest = null; List<CueRow>? rows = null;
        void Invalidate() { digest = null; Confirm.IsEnabled = false; }
        language.SelectionChanged += (_, _) => Invalidate();
        Action("إنشاء المعاينة", () => PendingWork = WorkAsync(async ct =>
        {
            Invalidate(); var lang = language.SelectedIndex;
            var result = await queue.RunExclusiveAsync(async t =>
            {
                using var source = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(source, t));
                return (hash, ocr: operation == "OCR" ? await service.RecognizeAsync(file, lang switch { 1 => "ara", 2 => "eng", _ => "eng+ara" }, t) : null,
                    mask: operation == "BACKGROUND" ? await service.RemoveBackgroundPreviewAsync(file, t) : null,
                    subtitles: operation == "SUBTITLE" ? await service.TranscribeAsync(file, lang switch { 1 => "ar", 2 => "en", _ => "auto" }, t) : null);
            }, ct);
            if (ClosedAlready) return;
            digest = result.hash; ocr = result.ocr; mask = result.mask; subtitles = result.subtitles;
            if (ocr is not null) text.Text = ocr.Text;
            if (mask is not null) image.Source = Bitmap(mask.Png);
            if (subtitles is not null) { rows = subtitles.Cues.Select(x => new CueRow { Start = x.Start, End = x.End, Text = x.Text }).ToList(); grid.ItemsSource = rows; }
            Confirm.IsEnabled = mask is not null || ocr?.HasText == true || subtitles is not null;
            Status.Text = ocr is not null ? $"{ocr.Pages} صفحات · ثقة النموذج {ocr.Confidence:P0}؛ ليست ضماناً للدقة" : "راجع المعاينة ثم أنشئ نسخة جديدة" + (subtitles?.Notes is { Count: > 0 } notes ? "\n" + string.Join("\n", notes) : "");
        }));
        if (operation == "OCR") Action("نسخ النص", () => { if (!string.IsNullOrWhiteSpace(text.Text)) Clipboard.SetText(text.Text); });
        Confirm.IsEnabled = false;
        Confirm.Click += (_, _) =>
        {
            try
            {
                if (digest is null) throw new ArgumentException("أنشئ المعاينة أولاً");
                if (operation == "OCR" && string.IsNullOrWhiteSpace(text.Text)) throw new ArgumentException("لا يوجد نص للحفظ");
                if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) || !grid.CommitEdit(DataGridEditingUnit.Row, true)) throw new ArgumentException("صحح حقول التوقيت غير الصالحة قبل الحفظ");
                if (subtitles is not null) { subtitles = subtitles with { Cues = rows!.Select(x => new SubtitleCue(x.Start, x.End, x.Text)).ToArray() }; subtitles.Validate(); subtitles.Encode(output.SelectedIndex == 1); }
                Accepted = new(digest, ocr is null ? null : ocr with { Text = text.Text }, mask, subtitles, output.SelectedIndex == 1); DialogResult = true;
            }
            catch (ArgumentException error) { Status.Text = error.Message; }
        };
    }
}

internal sealed class PrivacyToolsWindow : ToolDialog
{
    public PrivacyPlan? Accepted { get; private set; }
    public PrivacyToolsWindow(IReadOnlyList<PrivacyAnalysis> analyses, CancellationToken token) : base("تنظيف الخصوصية", token)
    {
        Note($"{analyses.Count} ملفات · إزالة بيانات وصفية محددة، وليست إخفاء هوية أو تنقيح محتوى");
        var detected = analyses.SelectMany(x => x.Detected).Distinct().ToArray(); var boxes = new Dictionary<string, CheckBox>();
        Note("الفئات المكتشفة: " + (detected.Length == 0 ? "لا توجد بيانات مدعومة للإزالة" : string.Join(" · ", detected)));
        foreach (var category in PrivacySettings.KnownCategories.Where(detected.Contains)) boxes.Add(category, Flag(category));
        foreach (var limit in analyses.SelectMany(x => x.Limits).Distinct()) Note(limit);
        var fallback = Flag("أسمح بنسخة PNG دون فقد لصيغ HEIC / AVIF عند الحاجة", false);
        Note("تُحفظ نسخة جديدة. تُفحص البيانات المتبقية ومحتويات الملف بصورة مستقلة، وتظهر تفاصيل الإزالة وحدودها في تقرير النتيجة.");
        Confirm.IsEnabled = detected.Length > 0;
        Confirm.Click += (_, _) =>
        {
            try { var settings = new PrivacySettings(boxes.Where(x => x.Value.IsChecked == true).Select(x => x.Key).ToArray(), fallback.IsChecked == true); settings.Validate(); Accepted = new(analyses, settings); DialogResult = true; }
            catch (ArgumentException error) { Status.Text = error.Message; }
        };
    }
}

internal sealed class ArchiveToolsWindow : ToolDialog
{
    public string Format { get; private set; } = "ZIP";
    public ArchiveToolsWindow(int count, bool sevenZip, bool extract, CancellationToken token) : base(extract ? "فك الأرشيف" : "إنشاء أرشيف", token)
    {
        Height = 380; Note($"{count} عناصر · تبقى الأصول كما هي");
        var format = extract ? null : Choice("الصيغة", sevenZip ? ["ZIP", "7Z", "TAR"] : ["ZIP", "TAR"]);
        Note(extract ? "الفك إلى مجلد جديد بجانب كل أرشيف. تُرفض المسارات الخارجة والروابط والتشفير وتجاوز حدود الحجم والعدد." : "تُجمع العناصر المختارة في أرشيف واحد بجانب أول عنصر. تُرفض الروابط وتعارضات الأسماء، وتُراجع محتويات الأرشيف قبل نشره.");
        Confirm.Click += (_, _) => { Format = format?.SelectedItem.ToString() ?? "ZIP"; DialogResult = true; };
    }
}
