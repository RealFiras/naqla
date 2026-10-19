using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Naqla.Core;

namespace Naqla.App;

/// <summary>Transient local job UI, created only after an accepted drop. Hiding never cancels work.</summary>
internal sealed class BatchProgressWindow : Window
{
    private readonly TextBlock status, counts;
    private readonly ProgressBar progress;
    private readonly Button cancel, details, folders, dismiss;
    private readonly ListBox failures;
    private BatchResult? result;
    private bool exiting;
    public event Action? CancelRequested;
    public string StatusText => status.Text;
    public double CompletedPercent => progress.Value;

    public BatchProgressWindow()
    {
        Title = "Naqla · تقدم المعالجة"; Width = 350; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStyle = WindowStyle.None; AllowsTransparency = true;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        Background = Brushes.Transparent; Foreground = Paint("#F5F0E6"); FontFamily = new("Segoe UI"); FontSize = 13;
        var icon = Path.Combine(AppContext.BaseDirectory, "preview", "LOGO.png");
        if (File.Exists(icon)) Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(icon));
        var root = new StackPanel { Margin = new Thickness(16), FlowDirection = FlowDirection.RightToLeft };
        Content = new Border { Child = root, Background = Paint("#121820"), BorderBrush = Paint("#6B5437"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2) };
        var heading = new TextBlock { Text = "نقلة", FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = Paint("#FFB347") }; root.Children.Add(heading);
        heading.MouseLeftButtonDown += (_, _) => DragMove();
        status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 9, 0, 6) }; root.Children.Add(status);
        progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 9, Foreground = Paint("#FFB347"), Background = Paint("#26313E"), BorderThickness = new Thickness(0) }; root.Children.Add(progress);
        counts = new TextBlock { FontSize = 11, Foreground = Paint("#C3C8D0"), Margin = new Thickness(0, 7, 0, 9), TextWrapping = TextWrapping.Wrap }; root.Children.Add(counts);
        var buttons = new WrapPanel(); root.Children.Add(buttons);
        cancel = Button("إلغاء", buttons, PixelButtonTone.Coral, PixelButtonGlyph.Cancel); cancel.Click += (_, _) => { cancel.IsEnabled = false; status.Text = "جارٍ الإلغاء وإتمام الاستعادة/التنظيف…"; CancelRequested?.Invoke(); };
        dismiss = Button("إخفاء", buttons, PixelButtonTone.Blue, PixelButtonGlyph.Hide); dismiss.Click += (_, _) => Hide();
        details = Button("تفاصيل", buttons, PixelButtonTone.Violet, PixelButtonGlyph.Details); details.IsEnabled = false;
        folders = Button("النتائج", buttons, PixelButtonTone.Mint, PixelButtonGlyph.Folder); folders.ToolTip = "فتح مجلدات النتائج"; folders.IsEnabled = false;
        failures = new ListBox { MaxHeight = 220, Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed,
            Background = Paint("#1C2530"), Foreground = Foreground, BorderThickness = new Thickness(0) }; root.Children.Add(failures);
        details.Click += (_, _) => { failures.Visibility = failures.IsVisible ? Visibility.Collapsed : Visibility.Visible; };
        folders.Click += (_, _) => OpenFolders();
        var copy = Button("نسخ التقرير", buttons, PixelButtonTone.Violet, PixelButtonGlyph.Details);
        copy.Click += (_, _) => { if (failures.Items.Count > 0) Clipboard.SetText(string.Join("\n\n", failures.Items.OfType<TextBlock>().Select(x => x.Text))); };
        Loaded += (_, _) => { var area = SystemParameters.WorkArea; Left = Math.Max(area.Left, area.Right - ActualWidth - 18); Top = Math.Max(area.Top, area.Bottom - ActualHeight - 18); };
        SizeChanged += (_, _) => { if (IsLoaded) Top = Math.Clamp(Top, SystemParameters.WorkArea.Top, Math.Max(SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Bottom - ActualHeight)); };
        Closing += OnClosing;
    }
    public void Begin(int total, string label, bool renaming = false)
    {
        result = null; failures.Items.Clear(); failures.Visibility = Visibility.Collapsed;
        details.IsEnabled = false; folders.IsEnabled = false; cancel.Visibility = Visibility.Visible; cancel.IsEnabled = true; dismiss.Content = "إخفاء";
        status.Text = $"{label} · 0 / {total}"; counts.Text = renaming ? "تتغير أسماء الملفات نفسها بعد التأكيد؛ لا تُنسخ محتوياتها" : "النتائج تُحفظ بجانب كل ملف أصلي"; progress.Value = 0;
        Show();
    }
    public void Update(BatchProgress state)
    {
        if (result is not null || exiting) return;
        progress.Value = state.Percent;
        status.Text = state.Current > 0 ? $"معالجة {state.Current} / {state.Total} · اكتمل {state.Completed}" : $"اكتمل {state.Completed} / {state.Total}";
        counts.Text = $"نجح {state.Succeeded} · فشل {state.Failed} · {state.Percent}% من المهام اكتملت";
    }
    public void Finish(BatchResult value, bool undo = false)
    {
        result = value; progress.Value = value.Jobs.Count == 0 ? 0 : 100d * value.Completed / value.Jobs.Count;
        cancel.Visibility = Visibility.Collapsed; dismiss.Content = "إغلاق";
        status.Text = value.Cancelled > 0 ? $"أُلغي الباقي · نجح {value.Succeeded} وفشل {value.Failed}" : $"اكتملت المعالجة · نجح {value.Succeeded} وفشل {value.Failed}";
        counts.Text = value.Cancelled > 0 ? $"توقف {value.Cancelled} من {value.Jobs.Count} مهام · النتائج الناجحة محفوظة" : "الأصول محفوظة؛ افتح النتائج أو راجع تفاصيل أي فشل";
        var renamed = value.Jobs.Select(x => x.Output?.Rename).FirstOrDefault(x => x is not null);
        if (renamed is not null)
        {
            status.Text = undo ? $"استعيدت أسماء {renamed.Files.Count} ملف" : $"أُعيدت تسمية {renamed.Files.Count} ملف";
            counts.Text = undo ? "استعيدت الأسماء السابقة؛ محتوى الملفات محفوظ." : "تغيرت الأسماء فقط؛ المحتوى محفوظ. التراجع من قائمة الأيقونة.";
        }
        if (value.Jobs.Any(x => x.Failure?.Kind == JobFailureKind.RenameRecovery))
        { status.Text = "تحتاج بعض الأسماء استردادًا؛ الملفات لم تُحذف"; counts.Text = "افتح التفاصيل أو Rename recovery records من الأيقونة"; }
        foreach (var job in value.Jobs.Where(x => x.Outcome != JobOutcome.Succeeded || x.Output?.Optimization is not null || x.Output?.Report is not null || x.Output?.Privacy is not null))
        {
            // Full source location is local UI only: distinguish duplicate names in different folders.
            var names = job.Sources.Count == 1 ? job.Sources[0].FullPath : $"{job.Sources.Count} ملفات";
            var message = job.Output?.Optimization is { } optimized ? $"تحقق الحد · {optimized.Description}\n{optimized.ResultBytes:N0} B / {optimized.TargetBytes:N0} B · جودة {optimized.Quality?.ToString() ?? "دون فقد"}" :
                job.Output?.Report is { } report ? report.Description : job.Outcome == JobOutcome.Cancelled ? "أُلغيت؛ لا ناتج منشور لهذه المهمة" : job.Failure?.Message;
            if (job.Output?.Report is { } measured) message += $"\nالحجم: {measured.SourceBytes:N0} → {measured.ResultBytes:N0} B · إعادة ترميز: {measured.Reencoded}";
            if (job.Output?.Privacy is { } privacy) message += $"\nأُزيل: {string.Join("، ", privacy.Removed)}\nبقي: {string.Join("، ", privacy.Retained)}\nحدود: {string.Join("؛ ", privacy.Limitations)}\nSHA-256 الأصل: {privacy.SourceSha256}\nSHA-256 الناتج: {privacy.OutputSha256}";
            failures.Items.Add(new TextBlock { Text = $"{names}\n{message}", TextWrapping = TextWrapping.Wrap, MaxWidth = 280, Margin = new Thickness(3) });
        }
        details.IsEnabled = failures.Items.Count > 0; folders.IsEnabled = value.OutputDirectories.Count > 0;
    }
    public void Reopen() { Show(); Activate(); }
    public void CloseForExit() { exiting = true; Close(); }
    private void OpenFolders()
    {
        if (result is null) return;
        var menu = new ContextMenu();
        foreach (var directory in result.OutputDirectories)
        {
            var entry = new MenuItem { Header = directory, FlowDirection = FlowDirection.LeftToRight };
            entry.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true }); }
                catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception) { MessageBox.Show("تعذر فتح مجلد النتائج", "Naqla"); }
            };
            menu.Items.Add(entry);
        }
        folders.ContextMenu = menu; menu.PlacementTarget = folders; menu.IsOpen = true;
    }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!exiting) { e.Cancel = true; Hide(); } }
    private static SolidColorBrush Paint(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private static Button Button(string label, Panel parent, PixelButtonTone tone, PixelButtonGlyph glyph)
    {
        var button = new PixelButton { Content = label, Tone = tone, Glyph = glyph, Margin = new Thickness(0, 0, 5, 4) };
        parent.Children.Add(button); return button;
    }
}
