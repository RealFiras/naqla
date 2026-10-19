using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows.Threading;
using Naqla.Conversion;
using Naqla.Core;

namespace Naqla.App;

/// <summary>Compact three-column editor. Changes here retain the existing navigation.</summary>
internal sealed class PdfToolkitWindow : Window
{
    private readonly PdfSession session;
    private readonly PdfToolkitService service;
    private readonly BatchProcessor queue;
    private readonly IDiagnostics diagnostics;
    private readonly CancellationTokenSource lifetime;
    private CancellationTokenSource? previewCancellation;
    private Task pending = Task.CompletedTask;
    private bool ready, changing, initialized, closed, rebuilding;
    private int version, previewPage = 1;
    private readonly List<int> order;
    private readonly Dictionary<int, BitmapSource> thumbnails = [];
    private readonly Dictionary<int, Image> thumbnailViews = [];
    private readonly ComboBox tool = Choice("PdfTool", [], 0);
    private readonly ListBox tiles = new() { Background = Paint("#121820"), BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly Image main = new() { Stretch = Stretch.Uniform, Margin = new Thickness(14) };
    private readonly TextBlock status = Label("جارٍ فتح المستند…"), counter = Label(""), summary = Label(""), toolHint = Label("");
    private readonly StackPanel settings = new() { FlowDirection = FlowDirection.RightToLeft };
    private readonly TextBox ranges = Field("PdfRanges", "1"), start = Field("PdfStart", "1"), size = Field("PdfFontSize", "12"), color = Field("PdfColor", "#707070"), watermarkText = Field("PdfWatermarkText", "نقلة · نسخة للمراجعة");
    private readonly ComboBox font = Choice("PdfFont", ["Segoe UI", "Arial", "Tahoma", "Times New Roman"], 0);
    private readonly ComboBox position = Choice("PdfPosition", ["أعلى اليسار", "أعلى الوسط", "أعلى اليمين", "الوسط", "أسفل اليسار", "أسفل الوسط", "أسفل اليمين"], 5);
    private readonly ComboBox rotation = Choice("PdfRotation", ["90° مع الساعة", "90° عكس الساعة", "180°"], 0);
    private readonly ComboBox markType = Choice("PdfWatermarkType", ["نص", "صورة PNG / JPG"], 0);
    private readonly CheckBox all = Tick("كل الصفحات", true), separate = Tick("تصدير كل صفحة في PDF منفصل", false), arabicDigits = Tick("أرقام عربية ١٢٣", false);
    private readonly Slider opacity = SliderOf("PdfOpacity", 5, 100, 25), scale = SliderOf("PdfScale", 5, 100, 55), angle = SliderOf("PdfAngle", -180, 180, -30);
    private readonly PixelButton save = ButtonOf("حفظ نسخة", PixelButtonTone.Amber, PixelButtonGlyph.Check), measure = ButtonOf("معاينة", PixelButtonTone.Blue, PixelButtonGlyph.Details);
    private readonly List<PdfTool> available;
    private byte[]? watermarkImage;
    private string? dragged;
    private Point dragStart;
    private readonly Guid dragOwner = Guid.NewGuid();
    private readonly ScrollViewer pageViewport = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, PanningMode = PanningMode.Both };
    private readonly TextBlock zoomLabel = Label("100%");
    private readonly DispatcherTimer autoPreview = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private double zoom = 1;
    private int fitMode; // 0 fit page, 1 fit width, 2 explicit percentage
    private Point? panStart;
    private Point panOffset;
    private readonly StackPanel moveControls = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 8) };
    public PdfEditPlan? Plan { get; private set; }
    public PdfEditPlan? AcceptedPlan { get; private set; }
    public bool CanSave => Plan is not null && save.IsEnabled && !closed;
    public Task PendingWork => pending;
    public string StatusText => status.Text;
    public PdfTool ActiveTool => available[Math.Max(0, tool.SelectedIndex)];

    public PdfToolkitWindow(PdfSession session, PdfTool initial, PdfToolkitService service, BatchProcessor queue, CancellationToken token = default, IDiagnostics? diagnostics = null)
    {
        this.session = session; this.service = service; this.queue = queue; this.diagnostics = diagnostics ?? NullDiagnostics.Instance; lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        available = session.Sources.Count > 1 ? [PdfTool.Merge] : [PdfTool.Reorder, PdfTool.Extract, PdfTool.Delete, PdfTool.Numbers, PdfTool.Watermark, PdfTool.Rotate];
        order = Enumerable.Range(session.Sources.Count > 1 ? 0 : 1, session.Sources.Count > 1 ? session.Sources.Count : session.PageCount).ToList();
        Title = "Naqla · أدوات PDF"; Width = Math.Min(1000, SystemParameters.WorkArea.Width); Height = Math.Min(720, SystemParameters.WorkArea.Height);
        MinWidth = Math.Min(720, Width); MinHeight = Math.Min(500, Height); MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = Paint("#121820"); Foreground = Paint("#F5F0E6");
        FontFamily = new("Segoe UI"); FontSize = 13; UseLayoutRounding = true; SnapsToDevicePixels = true;
        Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), (Style)XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ScrollBar">
              <Setter Property="Width" Value="7"/><Setter Property="Background" Value="#17212B"/>
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar">
                <Grid Background="{TemplateBinding Background}"><Track x:Name="PART_Track" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Orientation="{TemplateBinding Orientation}" IsDirectionReversed="True">
                  <Track.DecreaseRepeatButton><RepeatButton x:Name="Decrease" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False"/></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#53717C" Margin="1"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton x:Name="Increase" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False"/></Track.IncreaseRepeatButton>
                </Track></Grid>
                <ControlTemplate.Triggers><Trigger Property="Orientation" Value="Horizontal"><Setter TargetName="PART_Track" Property="IsDirectionReversed" Value="False"/><Setter TargetName="Decrease" Property="Command" Value="ScrollBar.PageLeftCommand"/><Setter TargetName="Increase" Property="Command" Value="ScrollBar.PageRightCommand"/></Trigger></ControlTemplate.Triggers>
              </ControlTemplate></Setter.Value></Setter>
              <Style.Triggers><Trigger Property="Orientation" Value="Horizontal"><Setter Property="Width" Value="Auto"/><Setter Property="Height" Value="7"/></Trigger></Style.Triggers>
            </Style>
            """));
        Resources.Add(typeof(Slider), (Style)XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Slider">
              <Setter Property="MinHeight" Value="24"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Slider">
                <Grid><Border Background="#344454" Height="4"/><Track x:Name="PART_Track" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Orientation="Horizontal">
                  <Track.DecreaseRepeatButton><RepeatButton Command="Slider.DecreaseLarge" Opacity="0" Focusable="False"/></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb Width="12" Height="18"><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#FFC35A" BorderBrush="#FFE4AF" BorderThickness="1"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton Command="Slider.IncreaseLarge" Opacity="0" Focusable="False"/></Track.IncreaseRepeatButton>
                </Track></Grid>
              </ControlTemplate></Setter.Value></Setter>
            </Style>
            """));
        var icon = Path.Combine(AppContext.BaseDirectory, "preview", "LOGO.png"); if (File.Exists(icon)) Icon = BitmapFrame.Create(new Uri(icon));
        Resources.Add(typeof(ComboBox), (Style)XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ComboBox">
              <Setter Property="Foreground" Value="#F5F0E6"/><Setter Property="Background" Value="#1C2530"/>
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid>
                <ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen,Mode=TwoWay,RelativeSource={RelativeSource TemplatedParent}}">
                  <ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border Background="#1C2530" BorderBrush="#415266" BorderThickness="1"/></ControlTemplate></ToggleButton.Template>
                </ToggleButton><ContentPresenter IsHitTestVisible="False" Margin="10,7,22,7" Content="{TemplateBinding SelectionBoxItem}"/>
                <TextBlock IsHitTestVisible="False" Text="▾" HorizontalAlignment="Right" Margin="0,5,8,0" Foreground="#FFC35A"/>
                <Popup IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" AllowsTransparency="True"><Border Background="#1C2530" BorderBrush="#FFC35A" BorderThickness="1" MinWidth="{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}"><ScrollViewer MaxHeight="270"><ItemsPresenter/></ScrollViewer></Border></Popup>
              </Grid></ControlTemplate></Setter.Value></Setter>
            </Style>
            """));
        var root = new Grid { Margin = new Thickness(16), Background = Paint("#121820") }; root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); Content = root;
        var header = new Grid { Margin = new Thickness(0, 0, 0, 14) }; header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); root.Children.Add(header);
        var brand = new StackPanel(); brand.Children.Add(new TextBlock { Text = "NAQLA  /  PDF", FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = Paint("#FFC35A") });
        brand.Children.Add(new TextBlock { Text = "معاينة فعلية · حفظ نسخة جديدة", Foreground = Paint("#9DAFC1"), FontSize = 11, Margin = new Thickness(0, 3, 0, 0) }); header.Children.Add(brand);
        tool.ItemsSource = available.Select(PdfToolIds.Label).ToArray(); tool.Width = 220; tool.FlowDirection = FlowDirection.RightToLeft; tool.SelectedIndex = Math.Max(0, available.IndexOf(initial)); Grid.SetColumn(tool, 1); header.Children.Add(tool);
        var body = new Grid(); Grid.SetRow(body, 1); root.Children.Add(body); body.ColumnDefinitions.Add(new() { Width = new GridLength(172) }); body.ColumnDefinitions.Add(new()); body.ColumnDefinitions.Add(new() { Width = new GridLength(238) });
        var sidebar = new DockPanel(); Grid.SetColumn(sidebar, 0); body.Children.Add(sidebar);
        var sideHeader = new StackPanel(); sideHeader.Children.Add(Label(session.Sources.Count > 1 ? "المستندات" : "الصفحات"));
        var up = ButtonOf("↑", PixelButtonTone.Blue); var down = ButtonOf("↓", PixelButtonTone.Blue); up.ToolTip = "نقل للأعلى"; down.ToolTip = "نقل للأسفل";
        up.Click += (_, _) => MoveSelected(-1); down.Click += (_, _) => MoveSelected(1); moveControls.Children.Add(up); moveControls.Children.Add(down); sideHeader.Children.Add(moveControls);
        DockPanel.SetDock(sideHeader, Dock.Top); sidebar.Children.Add(sideHeader); sidebar.Children.Add(tiles);
        ScrollViewer.SetHorizontalScrollBarVisibility(tiles, ScrollBarVisibility.Disabled); tiles.AllowDrop = true;
        tiles.SelectionChanged += (_, _) =>
        {
            if (!initialized || rebuilding || !ready || closed || tiles.SelectedItem is not ListBoxItem { Tag: int key }) return;
            try
            {
                var plan = CurrentPlan(); plan.Validate();
                var index = plan.Tool == PdfTool.Merge ? order.TakeWhile(x => x != key).Sum(x => session.Sources[x].Pages.Count) : plan.OutputPages().ToList().IndexOf(key);
                if (index >= 0) { previewPage = index + 1; Track(PreviewCurrentAsync()); }
            }
            catch (ArgumentException) { }
        };
        tiles.PreviewMouseLeftButtonDown += (_, e) =>
        {
            dragStart = e.GetPosition(tiles); var item = Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject); dragged = item?.Tag?.ToString();
        };
        tiles.PreviewMouseMove += (_, e) =>
        {
            var now = e.GetPosition(tiles);
            if (ready && ActiveTool is PdfTool.Reorder or PdfTool.Merge && e.LeftButton == MouseButtonState.Pressed && dragged is not null && (Math.Abs(now.X - dragStart.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(now.Y - dragStart.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                var data = new DataObject("Naqla.Pdf.Page", $"{dragOwner:N}:{dragged}"); dragged = null; DragDrop.DoDragDrop(tiles, data, DragDropEffects.Move);
            }
        };
        tiles.DragOver += (_, e) => { e.Effects = OwnDrag(e.Data) && ActiveTool is PdfTool.Reorder or PdfTool.Merge ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
        tiles.Drop += (_, e) =>
        {
            if (!OwnDrag(e.Data) || ActiveTool is not (PdfTool.Reorder or PdfTool.Merge)) return;
            var target = Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject); if (target?.Tag is not int to) return;
            var text = (string)e.Data.GetData("Naqla.Pdf.Page"); var from = int.Parse(text.Split(':')[1], CultureInfo.InvariantCulture);
            var index = order.IndexOf(to); if (e.GetPosition(target).Y > target.ActualHeight / 2) index++;
            MoveTile(from, index); e.Handled = true;
        };
        var center = new Grid { Margin = new Thickness(12, 0, 12, 0) }; Grid.SetColumn(center, 1); body.Children.Add(center);
        center.RowDefinitions.Add(new() { Height = GridLength.Auto }); center.RowDefinitions.Add(new()); center.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var viewing = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
        var fitPage = ButtonOf("صفحة", PixelButtonTone.Blue); var fitWidth = ButtonOf("عرض", PixelButtonTone.Blue);
        var minus = ButtonOf("−", PixelButtonTone.Blue); var plus = ButtonOf("+", PixelButtonTone.Blue);
        fitPage.Click += (_, _) => { fitMode = 0; UpdateZoom(); }; fitWidth.Click += (_, _) => { fitMode = 1; UpdateZoom(); };
        minus.Click += (_, _) => ChangeZoom(.8); plus.Click += (_, _) => ChangeZoom(1.25);
        zoomLabel.VerticalAlignment = VerticalAlignment.Center; zoomLabel.Margin = new Thickness(5);
        foreach (var control in new FrameworkElement[] { fitPage, fitWidth, minus, zoomLabel, plus }) viewing.Children.Add(control);
        center.Children.Add(viewing);
        main.Stretch = Stretch.Fill; main.HorizontalAlignment = HorizontalAlignment.Left; main.VerticalAlignment = VerticalAlignment.Top;
        pageViewport.Content = main; pageViewport.SizeChanged += (_, _) => UpdateZoom();
        var pageFrame = new Border { Background = Paint("#0B1119"), BorderBrush = Paint("#2A394A"), BorderThickness = new Thickness(1), Child = pageViewport };
        Grid.SetRow(pageFrame, 1); center.Children.Add(pageFrame);
        pageViewport.PreviewMouseWheel += (_, e) => { if (Keyboard.Modifiers == ModifierKeys.Control) { ChangeZoom(e.Delta > 0 ? 1.15 : 1 / 1.15); e.Handled = true; } };
        pageViewport.PreviewMouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { panStart = e.GetPosition(pageViewport); panOffset = new(pageViewport.HorizontalOffset, pageViewport.VerticalOffset); pageViewport.CaptureMouse(); e.Handled = true; } };
        pageViewport.PreviewMouseMove += (_, e) => { if (panStart is Point from && e.MiddleButton == MouseButtonState.Pressed) { var at = e.GetPosition(pageViewport); pageViewport.ScrollToHorizontalOffset(panOffset.X + from.X - at.X); pageViewport.ScrollToVerticalOffset(panOffset.Y + from.Y - at.Y); } };
        pageViewport.PreviewMouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { panStart = null; pageViewport.ReleaseMouseCapture(); } };
        var pager = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 9, 0, 0) }; Grid.SetRow(pager, 2); center.Children.Add(pager);
        var previous = ButtonOf("‹", PixelButtonTone.Blue); var next = ButtonOf("›", PixelButtonTone.Blue); previous.ToolTip = "الصفحة السابقة في الناتج"; next.ToolTip = "الصفحة التالية في الناتج";
        previous.Click += (_, _) => ChangePreviewPage(-1); next.Click += (_, _) => ChangePreviewPage(1); pager.Children.Add(previous); counter.Margin = new Thickness(10, 8, 10, 0); counter.FlowDirection = FlowDirection.LeftToRight; pager.Children.Add(counter); pager.Children.Add(next);
        var scroller = new ScrollViewer { Content = settings, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetColumn(scroller, 2); body.Children.Add(scroller);
        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) }; Grid.SetRow(footer, 2); root.Children.Add(footer);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(buttons, Dock.Right); footer.Children.Add(buttons);
        var cancel = ButtonOf("إلغاء", PixelButtonTone.Coral, PixelButtonGlyph.Cancel); cancel.IsCancel = true; cancel.Click += (_, _) => Close(); measure.Click += (_, _) => Track(PreviewCurrentAsync());
        save.IsDefault = true; save.IsEnabled = false; save.Click += (_, _) => { if (CanSave) { AcceptedPlan = Plan!.Snapshot(); DialogResult = true; } };
        buttons.Children.Add(cancel); buttons.Children.Add(measure); buttons.Children.Add(save); status.Margin = new Thickness(0, 5, 12, 0); status.FontSize = 11; footer.Children.Add(status);
        foreach (var box in new[] { ranges, start, size, color, watermarkText }) box.TextChanged += (_, _) => { Edited(); if (box == ranges && ready && !changing) RebuildTiles(); };
        foreach (var choice in new[] { font, position, rotation, markType }) choice.SelectionChanged += (_, _) => { if (ready && !changing) { Edited(); if (choice == markType) BuildSettings(); } };
        foreach (var tick in new[] { all, separate, arabicDigits }) { tick.Checked += (_, _) => { Edited(); if (tick == all && ready && !changing) RebuildTiles(); }; tick.Unchecked += (_, _) => { Edited(); if (tick == all && ready && !changing) RebuildTiles(); }; }
        foreach (var slider in new[] { opacity, scale, angle }) slider.ValueChanged += (_, _) => Edited();
        tool.SelectionChanged += (_, _) => { if (ready && !changing) SetTool(ActiveTool); };
        ready = true; SetTool(ActiveTool); Loaded += (_, _) => Track(InitializeAsync());
        autoPreview.Tick += (_, _) => { autoPreview.Stop(); if (initialized && !closed) Track(PreviewCurrentAsync()); };
        Closed += (_, _) => { closed = true; autoPreview.Stop(); Plan = null; save.IsEnabled = false; lifetime.Cancel(); previewCancellation?.Cancel(); _ = pending.ContinueWith(_ => lifetime.Dispose(), TaskScheduler.Default); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }
    private void ChangeZoom(double factor) { fitMode = 2; zoom = Math.Clamp(zoom * factor, .1, 4); UpdateZoom(); }
    private void UpdateZoom()
    {
        if (main.Source is not BitmapSource bitmap || pageViewport.ViewportWidth <= 0 || pageViewport.ViewportHeight <= 0) return;
        var width = bitmap.PixelWidth; var height = bitmap.PixelHeight;
        if (fitMode != 2) zoom = Math.Min((pageViewport.ViewportWidth - 30) / width, fitMode == 0 ? (pageViewport.ViewportHeight - 30) / height : double.MaxValue);
        zoom = Math.Clamp(zoom, .05, 4); main.Width = width * zoom; main.Height = height * zoom;
        zoomLabel.Text = $"{zoom * 100:0}%";
    }
    private bool OwnDrag(IDataObject data) => data.GetDataPresent("Naqla.Pdf.Page") && data.GetData("Naqla.Pdf.Page") is string text && text.StartsWith(dragOwner.ToString("N") + ":", StringComparison.Ordinal);
    private static T? Ancestor<T>(DependencyObject? node) where T : DependencyObject
    { while (node is not null) { if (node is T found) return found; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node); } return null; }
    private void Track(Task task) { pending = Task.WhenAll(pending, task); }
    public async Task InitializeAsync()
    {
        if (initialized) return; initialized = true;
        try
        {
            var completed = 0;
            var observer = new Progress<PdfThumbnail>(thumbnail =>
            {
                if (closed) return; var key = session.Sources.Count > 1 ? thumbnail.Source : thumbnail.Page;
                thumbnails[key] = Bitmap(thumbnail.Png); if (thumbnailViews.TryGetValue(key, out var view)) view.Source = thumbnails[key];
                status.Text = $"معاينات فعلية {++completed} / {order.Count}";
            });
            await queue.RunExclusiveAsync(ct => service.ThumbnailsAsync(session, session.Sources.Count > 1, observer, ct), lifetime.Token);
            await PreviewCurrentAsync();
        }
        catch (OperationCanceledException) { if (!closed) status.Text = "أُلغيت المعاينة"; }
        catch (Exception error) { if (!closed) status.Text = SafeError(error); }
    }
    public void SetTool(PdfTool value)
    {
        if (!available.Contains(value)) throw new ArgumentException("هذه العملية لا تناسب عدد المستندات");
        changing = true; tool.SelectedIndex = available.IndexOf(value); order.Sort(); previewPage = 1;
        ranges.Text = value is PdfTool.Extract or PdfTool.Delete or PdfTool.Rotate ? "1" : $"1-{session.PageCount}";
        all.IsChecked = true; color.Text = value == PdfTool.Numbers ? "#202020" : "#707070";
        position.SelectedIndex = value == PdfTool.Watermark ? 3 : 5; changing = false;
        BuildSettings(); RebuildTiles(); Edited();
    }
    private void BuildSettings()
    {
        settings.Children.Clear(); var current = ActiveTool;
        moveControls.Visibility = current is PdfTool.Reorder or PdfTool.Merge ? Visibility.Visible : Visibility.Collapsed;
        ranges.IsEnabled = !(current is PdfTool.Numbers or PdfTool.Watermark && all.IsChecked == true);
        toolHint.Text = current switch
        {
            PdfTool.Reorder => "اسحب المصغّرات لترتيب الصفحات. الأسهم بديل بالسحب ولوحة المفاتيح.",
            PdfTool.Extract => "اختر الصفحات من المصغّرات أو اكتب نطاقًا. ترتيب النطاق هو ترتيب التصدير.",
            PdfTool.Delete => "الصفحات المحددة للحذف تظهر باهتة. المعاينة تعرض الصفحات المتبقية.",
            PdfTool.Merge => "اسحب المستندات لترتيب الدمج. يبقى ترتيب الصفحات داخل كل مستند كما هو.",
            PdfTool.Numbers => "ترقيم متسلسل على الصفحات المختارة فقط. عاين النتيجة قبل الحفظ.",
            PdfTool.Watermark => "نص عربي متصل أو صورة. المعاينة من نفس محرك الحفظ.",
            _ => "دوّر الصفحات المختارة فقط؛ باقي الصفحات تبقى كما هي."
        };
        settings.Children.Add(TitleOf(PdfToolIds.Label(current))); settings.Children.Add(toolHint);
        if (current is not (PdfTool.Reorder or PdfTool.Merge))
        {
            if (current is PdfTool.Numbers or PdfTool.Watermark) settings.Children.Add(all);
            Add("الصفحات · مثال 1,3,5-8", ranges);
        }
        if (current == PdfTool.Extract) settings.Children.Add(separate);
        if (current == PdfTool.Rotate) Add("الدوران", rotation);
        if (current == PdfTool.Numbers) { Add("يبدأ من", start); Add("حجم الخط · نقطة", size); settings.Children.Add(arabicDigits); }
        if (current == PdfTool.Watermark)
        {
            Add("نوع العلامة", markType);
            if (markType.SelectedIndex == 0) Add("النص", watermarkText);
            else
            {
                var image = ButtonOf(watermarkImage is null ? "اختيار صورة" : "تغيير الصورة ✓", PixelButtonTone.Blue, PixelButtonGlyph.Folder);
                image.Click += (_, _) => Track(ChooseImageAsync()); settings.Children.Add(image);
            }
            Add("الشفافية · %", opacity); Add("الحجم داخل الصفحة · %", scale); Add("الدوران · درجة", angle);
        }
        if (current is PdfTool.Numbers or PdfTool.Watermark) { Add("الخط", font); Add("اللون · #RRGGBB", color); Add("الموضع", position); }
        settings.Children.Add(new Border { Height = 1, Background = Paint("#344454"), Margin = new Thickness(0, 18, 0, 10) });
        settings.Children.Add(summary); settings.Children.Add(Label("يُحفظ PDF جديد بجانب أول مستند. الأصل محفوظ."));
    }
    private void Add(string label, FrameworkElement value)
    {
        var heading = new DockPanel { Margin = new Thickness(0, 13, 0, 5), FlowDirection = FlowDirection.LeftToRight };
        if (value is Slider slider)
        {
            var number = new TextBlock { Foreground = Paint("#FFC35A"), FontSize = 11, FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(0, 0, 10, 0) };
            number.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = slider, StringFormat = "{0:0}" }); DockPanel.SetDock(number, Dock.Left); heading.Children.Add(number);
        }
        heading.Children.Add(new TextBlock { Text = label, Foreground = Paint("#A7B9CA"), FontSize = 11, FlowDirection = FlowDirection.RightToLeft }); settings.Children.Add(heading);
        settings.Children.Add(value);
    }
    private async Task ChooseImageAsync()
    {
        var chooser = new Microsoft.Win32.OpenFileDialog { Filter = "PNG / JPG|*.png;*.jpg;*.jpeg", CheckFileExists = true };
        if (chooser.ShowDialog(this) != true) return;
        try { watermarkImage = await queue.RunExclusiveAsync(ct => service.LoadWatermarkImageAsync(chooser.FileName, ct), lifetime.Token); Edited(); BuildSettings(); }
        catch (OperationCanceledException) { }
        catch (Exception error) { status.Text = SafeError(error); }
    }
    private void Edited()
    {
        if (!ready || changing || closed) return;
        ranges.IsEnabled = !(ActiveTool is PdfTool.Numbers or PdfTool.Watermark && all.IsChecked == true);
        version++; previewCancellation?.Cancel(); Plan = null; save.IsEnabled = false; status.Foreground = Paint("#A7B9CA");
        autoPreview.Stop();
        try { var plan = CurrentPlan(); plan.Validate(); summary.Text = $"{(plan.Tool == PdfTool.Merge ? session.PageCount : plan.OutputPages().Count)} صفحة في الناتج"; status.Text = "تحديث المعاينة…"; if (initialized) autoPreview.Start(); }
        catch (Exception error) { summary.Text = "تحقق من الاختيار"; status.Text = SafeError(error); }
    }
    private PdfEditPlan CurrentPlan()
    {
        var current = ActiveTool;
        var selected = current == PdfTool.Merge ? Array.Empty<int>() : current == PdfTool.Reorder ? order.ToArray() : current is PdfTool.Numbers or PdfTool.Watermark && all.IsChecked == true ? Enumerable.Range(1, session.PageCount).ToArray() : PdfPageRanges.Parse(ranges.Text, session.PageCount);
        var number = current == PdfTool.Numbers ? new PdfNumbering(int.Parse(start.Text, CultureInfo.InvariantCulture), double.Parse(size.Text, CultureInfo.InvariantCulture), color.Text, font.SelectedItem?.ToString() ?? "Segoe UI", (PdfPosition)position.SelectedIndex, arabicDigits.IsChecked == true) : null;
        var watermark = current == PdfTool.Watermark ? new PdfWatermark(watermarkText.Text, markType.SelectedIndex == 1 ? watermarkImage ?? throw new ArgumentException("اختر صورة العلامة") : null, opacity.Value / 100, scale.Value / 100, angle.Value, (PdfPosition)position.SelectedIndex, font.SelectedItem?.ToString() ?? "Segoe UI", color.Text) : null;
        return new(session, current, selected, current == PdfTool.Extract && separate.IsChecked == true, order.ToArray(), number, watermark, rotation.SelectedIndex switch { 1 => -90, 2 => 180, _ => 90 });
    }
    private void RebuildTiles()
    {
        rebuilding = true;
        var selectedItem = tiles.SelectedItem as ListBoxItem; var selectedKey = selectedItem?.Tag;
        tiles.Items.Clear(); thumbnailViews.Clear(); HashSet<int> chosen = [];
        try { chosen = PdfPageRanges.Parse(ranges.Text, session.PageCount).ToHashSet(); } catch (ArgumentException) { }
        foreach (var key in order)
        {
            var document = session.Sources.Count > 1; var editable = ActiveTool is not (PdfTool.Reorder or PdfTool.Merge) && !(ActiveTool is PdfTool.Numbers or PdfTool.Watermark && all.IsChecked == true);
            var tile = new StackPanel { Margin = new Thickness(7, 7, 7, 5) };
            var image = new Image { Source = thumbnails.GetValueOrDefault(key), Height = 108, Stretch = Stretch.Uniform };
            thumbnailViews[key] = image;
            if (ActiveTool == PdfTool.Delete && chosen.Contains(key)) image.Opacity = .3;
            tile.Children.Add(image);
            var caption = document ? Path.GetFileName(session.Sources[key].File.FullPath) : $"{order.IndexOf(key) + 1:00}  /  صفحة {key}";
            var label = new TextBlock { Text = caption, FontSize = 11, Foreground = Paint("#F5F0E6"), TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 5, 0, 0), FlowDirection = FlowDirection.RightToLeft };
            if (editable)
            {
                var check = Tick(caption, chosen.Contains(key)); check.HorizontalAlignment = HorizontalAlignment.Center;
                check.Click += (_, _) =>
                {
                    var pages = new HashSet<int>(); try { pages = PdfPageRanges.Parse(ranges.Text, session.PageCount).ToHashSet(); } catch (ArgumentException) { }
                    if (check.IsChecked == true) pages.Add(key); else pages.Remove(key);
                    ranges.Text = string.Join(",", order.Where(pages.Contains)); RebuildTiles();
                }; tile.Children.Add(check);
            }
            else tile.Children.Add(label);
            if (document) tile.Children.Add(Label($"{session.Sources[key].Pages.Count} صفحة"));
            var item = new ListBoxItem { Content = new Border { Child = tile, Background = Paint("#1C2530"), BorderBrush = Paint("#344454"), BorderThickness = new Thickness(1) }, Tag = key, Padding = new Thickness(0), Margin = new Thickness(0, 0, 5, 7), Foreground = Paint("#F5F0E6") };
            tiles.Items.Add(item); if (Equals(selectedKey, key)) item.IsSelected = true;
        }
        rebuilding = false;
    }
    public void MoveTile(int key, int insertionIndex)
    {
        if (ActiveTool is not (PdfTool.Reorder or PdfTool.Merge) || !order.Contains(key)) throw new ArgumentException("يمكن السحب في وضع الترتيب أو الدمج");
        var old = order.IndexOf(key); insertionIndex = Math.Clamp(insertionIndex, 0, order.Count); order.RemoveAt(old); if (old < insertionIndex) insertionIndex--; order.Insert(insertionIndex, key);
        Edited(); RebuildTiles(); var item = tiles.Items.Cast<ListBoxItem>().First(x => Equals(x.Tag, key)); item.IsSelected = true; tiles.ScrollIntoView(item);
    }
    private void MoveSelected(int delta)
    {
        if (ActiveTool is not (PdfTool.Reorder or PdfTool.Merge) || tiles.SelectedItem is not ListBoxItem { Tag: int key }) return;
        var current = order.IndexOf(key); var next = current + delta; if (next < 0 || next >= order.Count) return;
        MoveTile(key, delta > 0 ? next + 1 : next);
    }
    private void ChangePreviewPage(int delta)
    {
        if (closed) return;
        try { var plan = CurrentPlan(); plan.Validate(); var count = plan.Tool == PdfTool.Merge ? session.PageCount : plan.OutputPages().Count; previewPage = Math.Clamp(previewPage + delta, 1, count); Track(PreviewCurrentAsync()); }
        catch (Exception error) { status.Text = SafeError(error); }
    }
    public async Task PreviewCurrentAsync()
    {
        if (closed) return; previewCancellation?.Cancel(); var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); previewCancellation = cancellation;
        var ticket = version; Plan = null; save.IsEnabled = false; measure.IsEnabled = false; status.Text = "جارٍ تجهيز معاينة فعلية…";
        try
        {
            var plan = CurrentPlan().Snapshot(); plan.Validate(); var count = plan.Tool == PdfTool.Merge ? session.PageCount : plan.OutputPages().Count; previewPage = Math.Clamp(previewPage, 1, count);
            diagnostics.Write(DiagnosticEvent.PdfPreviewStarted, new() { Operation = DiagnosticData.OperationOf(PdfToolIds.Id(plan.Tool)), Count = count });
            var result = await queue.RunExclusiveAsync(ct => service.PreviewAsync(plan, previewPage, ct), cancellation.Token);
            if (closed || ticket != version || cancellation.IsCancellationRequested) return;
            main.Source = Bitmap(result.Png); UpdateZoom(); counter.Text = $"{result.Page:00} / {result.Pages:00}"; summary.Text = $"{result.Pages} صفحة في الناتج";
            status.Text = plan.Separate ? "معاينة صحيحة · كل صفحة بملف داخل مجلد جديد" : "معاينة صحيحة · جاهز لحفظ نسخة"; status.Foreground = Paint("#65DAB4"); Plan = plan; save.IsEnabled = true;
            diagnostics.Write(DiagnosticEvent.PdfPreviewReady, new() { Operation = DiagnosticData.OperationOf(PdfToolIds.Id(plan.Tool)), Count = result.Pages });
        }
        catch (OperationCanceledException) { diagnostics.Write(DiagnosticEvent.PdfPreviewCancelled, new() { Operation = DiagnosticData.OperationOf(PdfToolIds.Id(ActiveTool)) }); if (!closed && ticket == version) status.Text = "أُلغيت المعاينة"; }
        catch (Exception error) { diagnostics.Write(DiagnosticEvent.PdfPreviewFailed, DiagnosticData.FromException(error) with { Operation = DiagnosticData.OperationOf(PdfToolIds.Id(ActiveTool)) }); if (!closed && ticket == version) { status.Text = SafeError(error); status.Foreground = Paint("#FF8390"); } }
        finally { cancellation.Dispose(); if (ReferenceEquals(previewCancellation, cancellation)) { previewCancellation = null; if (!closed) measure.IsEnabled = true; } }
    }
    private static string SafeError(Exception error) => error switch
    {
        ArgumentException or FormatException or OverflowException => error is ArgumentException ? error.Message.Split(" (Parameter", StringSplitOptions.None)[0] : "تحقق من الأرقام والنطاقات",
        NotSupportedException => error.Message,
        _ => "تعذر تجهيز PDF؛ تحقق من صلاحية الملف والقرص ثم أعد فتحه"
    };
    private static BitmapSource Bitmap(byte[] data) { using var stream = new MemoryStream(data); var result = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad); result.Freeze(); return result; }
    private static SolidColorBrush Paint(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static TextBlock Label(string text) => new() { Text = text, Foreground = Paint("#9DAFC1"), FontSize = 11, TextWrapping = TextWrapping.Wrap, FlowDirection = FlowDirection.RightToLeft, Margin = new Thickness(0, 4, 0, 4) };
    private static TextBlock TitleOf(string text) => new() { Text = text, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Paint("#FFC35A"), Margin = new Thickness(0, 0, 0, 8) };
    private static TextBox Field(string name, string value) => new() { Name = name, Text = value, Background = Paint("#1C2530"), Foreground = Paint("#F5F0E6"), BorderBrush = Paint("#415266"), CaretBrush = Paint("#FFC35A"), Padding = new Thickness(7), FlowDirection = name == "PdfWatermarkText" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight };
    private static ComboBox Choice(string name, string[] options, int selected) => new() { Name = name, ItemsSource = options, SelectedIndex = selected, MinHeight = 32, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private static CheckBox Tick(string text, bool check) => new() { Content = text, IsChecked = check, Foreground = Paint("#F5F0E6"), Margin = new Thickness(0, 9, 0, 3), FontSize = 11, FlowDirection = FlowDirection.RightToLeft };
    private static Slider SliderOf(string name, double min, double max, double value) => new() { Name = name, Minimum = min, Maximum = max, Value = value, IsSnapToTickEnabled = true, TickFrequency = 1, AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.BottomRight, Margin = new Thickness(0, 4, 0, 4) };
    private static PixelButton ButtonOf(string text, PixelButtonTone tone, PixelButtonGlyph glyph = PixelButtonGlyph.None) => new() { Content = text, Tone = tone, Glyph = glyph, Margin = new Thickness(0, 0, 6, 0) };
}
