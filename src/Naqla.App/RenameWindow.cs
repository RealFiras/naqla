using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Naqla.Core;

namespace Naqla.App;

internal sealed class RenameWindow : Window
{
    private readonly ISmartRenameService service;
    private readonly IReadOnlyList<DraggedFile> files;
    private readonly Dictionary<string, TextBox> fields = [];
    private readonly CheckBox regex, ignoreCase, cleanup, preserve, confirmation;
    private readonly ComboBox casing, dates, dateFormat;
    private readonly DataGrid preview;
    private readonly TextBlock status;
    private readonly PixelButton accept;
    private readonly DispatcherTimer debounce;
    private CancellationTokenSource? pending;
    private bool ready, closed;
    public RenamePlan? Plan { get; private set; }
    public bool CanConfirm => Plan is { IsValid: true, ChangedCount: > 0 } && confirmation.IsChecked == true && pending is null;

    public RenameWindow(ISmartRenameService service, IReadOnlyList<DraggedFile> files, RenamePlan initial, bool undo = false)
    {
        this.service = service; this.files = files;
        Title = undo ? "Naqla · تراجع عن التسمية" : "Naqla · إعادة تسمية";
        Width = 760; Height = undo ? 480 : 680; MinWidth = 590; MinHeight = 420;
        MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = Paint("#121820"); Foreground = Paint("#F5F0E6"); FontFamily = new("Segoe UI"); FontSize = 12;
        var icon = Path.Combine(AppContext.BaseDirectory, "preview", "LOGO.png"); if (File.Exists(icon)) Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(icon));
        var root = new DockPanel { Margin = new Thickness(16), Background = Background, FlowDirection = FlowDirection.RightToLeft }; Content = root;
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = undo ? "راجع الأسماء قبل التراجع" : "قبل ← بعد · تُعاد تسمية الملفات نفسها بعد تأكيدك، دون نسخ المحتوى", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Paint("#FFC35A"), Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });
        var options = new WrapPanel { Margin = new Thickness(0, 0, 0, 6), Visibility = undo ? Visibility.Collapsed : Visibility.Visible }; top.Children.Add(options);
        AddField(options, "Pattern", "القالب: {name} {n} {date}", "{name}", 220);
        AddField(options, "Find", "بحث", "", 210); AddField(options, "Replace", "استبدال", "", 210);
        AddField(options, "Prefix", "بادئة", "", 170); AddField(options, "Suffix", "لاحقة", "", 170);
        AddField(options, "Start", "بداية التسلسل", "1", 100); AddField(options, "Padding", "خانات التسلسل", "3", 95);
        casing = AddCombo(options, "حالة الأحرف", new[] { "كما هي", "أحرف صغيرة", "أحرف كبيرة", "بداية الكلمات" }, 135);
        dates = AddCombo(options, "مصدر التاريخ", new[] { "آخر تعديل", "إنشاء الملف", "اليوم" }, 130);
        dateFormat = AddCombo(options, "صيغة التاريخ", new[] { "yyyy-MM-dd", "yyyyMMdd", "yyyy-MM-dd_HHmm" }, 170);
        var flags = new WrapPanel { Visibility = undo ? Visibility.Collapsed : Visibility.Visible, Margin = new Thickness(0, 0, 0, 8) }; top.Children.Add(flags);
        regex = Flag(flags, "Regex متقدم", false); ignoreCase = Flag(flags, "بحث دون حالة الأحرف", false);
        cleanup = Flag(flags, "تنظيف الاسم", false); preserve = Flag(flags, "حفظ الامتداد", true);
        var footer = new StackPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        status = new TextBlock { Foreground = Paint("#C3C8D0"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) }; footer.Children.Add(status);
        confirmation = new CheckBox { Content = undo ? "أوافق على إعادة الأسماء السابقة المعروضة" : "أوافق على تغيير أسماء الملفات الأصلية كما تظهر في المعاينة", Foreground = Foreground, Margin = new Thickness(0, 0, 0, 10) };
        confirmation.Checked += (_, _) => RefreshConfirm(); confirmation.Unchecked += (_, _) => RefreshConfirm(); footer.Children.Add(confirmation);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal }; footer.Children.Add(buttons);
        accept = new PixelButton { Content = undo ? "تأكيد التراجع" : "تأكيد إعادة التسمية", Glyph = undo ? PixelButtonGlyph.Undo : PixelButtonGlyph.Check, Tone = PixelButtonTone.Amber, IsEnabled = false, Margin = new Thickness(0, 0, 8, 0) };
        accept.Click += (_, _) => { if (CanConfirm) DialogResult = true; };
        buttons.Children.Add(accept); buttons.Children.Add(new PixelButton { Content = "إلغاء", Glyph = PixelButtonGlyph.Cancel, Tone = PixelButtonTone.Coral, IsCancel = true });
        preview = new DataGrid { IsReadOnly = true, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column, Background = Paint("#17212B"), Foreground = Foreground, RowBackground = Paint("#17212B"), AlternatingRowBackground = Paint("#1C2834"),
            BorderBrush = Paint("#354352"), GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, HorizontalGridLinesBrush = Paint("#2B3846"), FlowDirection = FlowDirection.LeftToRight, SelectionMode = DataGridSelectionMode.Single };
        var headerStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)); headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, Paint("#263443"))); headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, Foreground)); headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8))); preview.ColumnHeaderStyle = headerStyle;
        foreach (var column in new[] { ("Before", "قبل"), ("After", "بعد"), ("Status", "الحالة") })
            preview.Columns.Add(new DataGridTextColumn { Header = column.Item2, Binding = new Binding(column.Item1), Width = column.Item1 == "Status" ? new DataGridLength(180) : new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 120 });
        root.Children.Add(preview);
        debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) }; debounce.Tick += async (_, _) => { debounce.Stop(); await RefreshPreviewAsync(); };
        Closed += (_, _) => { closed = true; debounce.Stop(); pending?.Cancel(); };
        ApplyPreview(initial); ready = !undo;
    }
    private void AddField(Panel panel, string key, string label, string value, double width)
    {
        var group = new StackPanel { Width = width, Margin = new Thickness(0, 0, 8, 7) }; panel.Children.Add(group);
        group.Children.Add(new TextBlock { Text = label, Foreground = Paint("#C3C8D0") });
        var field = new TextBox { Text = value, Padding = new Thickness(5), Background = Paint("#202C39"), Foreground = Foreground, BorderBrush = Paint("#4B5B6C"), FlowDirection = FlowDirection.LeftToRight };
        field.TextChanged += (_, _) => Changed(); fields.Add(key, field); group.Children.Add(field);
    }
    private ComboBox AddCombo(Panel panel, string label, string[] values, double width)
    {
        var group = new StackPanel { Width = width, Margin = new Thickness(0, 0, 8, 7) }; panel.Children.Add(group); group.Children.Add(new TextBlock { Text = label, Foreground = Paint("#C3C8D0") });
        var combo = new ComboBox { ItemsSource = values, SelectedIndex = 0, Padding = new Thickness(3) }; combo.SelectionChanged += (_, _) => Changed(); group.Children.Add(combo); return combo;
    }
    private CheckBox Flag(Panel panel, string label, bool value)
    { var flag = new CheckBox { Content = label, IsChecked = value, Foreground = Foreground, Margin = new Thickness(0, 0, 12, 0) }; flag.Checked += (_, _) => Changed(); flag.Unchecked += (_, _) => Changed(); panel.Children.Add(flag); return flag; }
    private void Changed()
    {
        if (!ready || closed) return;
        confirmation.IsChecked = false; confirmation.IsEnabled = false; Plan = null; pending?.Cancel(); accept.IsEnabled = false; status.Text = "جارٍ تحديث المعاينة… لا تتغير أي ملفات قبل التأكيد";
        debounce.Stop(); debounce.Start();
    }
    private async Task RefreshPreviewAsync()
    {
        var cancellation = new CancellationTokenSource(); pending = cancellation;
        try
        {
            var settings = new RenameSettings(fields["Pattern"].Text, fields["Find"].Text, fields["Replace"].Text, fields["Prefix"].Text, fields["Suffix"].Text,
                regex.IsChecked == true, ignoreCase.IsChecked == true, int.Parse(fields["Start"].Text, CultureInfo.InvariantCulture), int.Parse(fields["Padding"].Text, CultureInfo.InvariantCulture),
                (RenameCase)casing.SelectedIndex, cleanup.IsChecked == true, preserve.IsChecked == true, (RenameDateSource)dates.SelectedIndex, dateFormat.SelectedItem!.ToString()!);
            var plan = await service.PreviewAsync(files, settings, cancellation.Token);
            if (!closed && !cancellation.IsCancellationRequested) { pending = null; ApplyPreview(plan); }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException)
        { if (!closed && !cancellation.IsCancellationRequested) { Plan = null; status.Text = error.Message; } }
        finally { if (ReferenceEquals(pending, cancellation)) pending = null; cancellation.Dispose(); if (!closed) RefreshConfirm(); }
    }
    private void ApplyPreview(RenamePlan plan)
    {
        Plan = plan; preview.ItemsSource = plan.Entries;
        status.Text = !plan.IsValid ? "أصلح الأسماء والتعارضات؛ لا يُستبدل أي ملف موجود" : plan.ChangedCount == 0 ? "غيّر الإعدادات لإنشاء أسماء جديدة؛ {n} للتسلسل و{date} للتاريخ" : $"سيتغير اسم {plan.ChangedCount} من {plan.Entries.Count} ملف · تغيير الامتداد لا يحوّل المحتوى";
        RefreshConfirm();
    }
    private void RefreshConfirm()
    {
        if (confirmation is not null) confirmation.IsEnabled = Plan is { IsValid: true, ChangedCount: > 0 } && pending is null;
        if (accept is not null) accept.IsEnabled = CanConfirm;
    }
    private static SolidColorBrush Paint(string color) => new((Color)ColorConverter.ConvertFromString(color));
}
