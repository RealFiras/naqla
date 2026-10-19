using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Naqla.Conversion;
using Naqla.Wheel;
using Naqla.Windows;
using Naqla.Core;
using Forms = System.Windows.Forms;

namespace Naqla.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--internal-ocr") return OcrWorker.Run(args);
        if (args.FirstOrDefault() == "--internal-plugin") return PluginService.RunHost(args);
        if (args is ["--import-component", var componentZip])
        {
            // Explicit local maintenance command; no tray/hooks/network are started.
            try { new ComponentStore(ComponentStore.DefaultRoot).InstallAsync(Path.GetFullPath(componentZip)).GetAwaiter().GetResult(); return 0; }
            catch { return 1; }
        }
        if (args.Contains("--quit"))
        {
            try { using var existing = EventWaitHandle.OpenExisting(@"Local\Naqla.Exit.0.1"); existing.Set(); }
            catch (WaitHandleCannotBeOpenedException) { }
            return 0;
        }
        using var mutex = new Mutex(true, @"Local\Naqla.Tray.0.1", out var first);
        if (!first) return 0;
        using var diagnostics = new DiagnosticLog();
        diagnostics.Write(DiagnosticEvent.AppStarting);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => diagnostics.Write(DiagnosticEvent.UnhandledError, DiagnosticData.FromException(e.Exception));
        AppDomain.CurrentDomain.UnhandledException += (_, e) => diagnostics.Write(DiagnosticEvent.UnhandledError,
            e.ExceptionObject is Exception error ? DiagnosticData.FromException(error) : new() { Error = DiagnosticError.Other });
        using var exit = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Naqla.Exit.0.1");
        try
        {
            using var context = new TrayContext(app, diagnostics);
            var exitWait = ThreadPool.RegisterWaitForSingleObject(exit, (_, _) => app.Dispatcher.BeginInvoke(() => _ = context.RequestExitAsync()), null, Timeout.Infinite, true);
            try { app.Run(); }
            finally { exitWait.Unregister(null); }
            context.Dispose();
            diagnostics.Write(DiagnosticEvent.AppStopped);
            return 0;
        }
        catch (Exception error)
        {
            diagnostics.Write(DiagnosticEvent.AppStartupFailed, DiagnosticData.FromException(error));
            MessageBox.Show($"Naqla could not start.\n{error.Message}", "Naqla development build", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }
}

internal sealed class TrayContext : IDisposable
{
    private readonly Application app;
    private readonly DiagnosticLog diagnostics;
    private readonly Forms.NotifyIcon tray;
    private readonly System.Drawing.Icon icon;
    private readonly PixelWheel wheel;
    private readonly WheelModeState wheelMode = new();
    private readonly OverlayWindow overlay;
    private readonly OleDropSession drop;
    private readonly DragGestureMonitor gestures;
    private readonly DispatcherTimer timeout;
    private readonly DispatcherTimer releaseDelay;
    private readonly LocalConversionService converter;
    private readonly SmartRenameService renamer = new(new WindowsRenameFileSystem());
    private RenameReceipt? lastRename;
    private readonly Forms.ToolStripMenuItem undoRename;
    private readonly BatchProcessor processor;
    private BatchProgressWindow? progressWindow;
    private CancellationTokenSource? conversionCancellation;
    private readonly Forms.ToolStripMenuItem cancelConversion;
    private readonly Forms.ToolStripMenuItem showProgress;
    private bool busy, disposed, exiting;
    private Task pendingConversion = Task.CompletedTask;
    private int testMarker;
    private CancellationTokenSource? capabilityProbe;
    private Task pendingCapabilityProbe = Task.CompletedTask;

    public TrayContext(Application app, DiagnosticLog diagnostics)
    {
        this.app = app;
        this.diagnostics = diagnostics;
        converter = new(renamer: renamer); processor = new(converter);
        wheel = new(converter.CanOffer, converter.ContextOptions);
        overlay = new(wheel, diagnostics, PixelWheel.CanvasWidth, PixelWheel.CanvasHeight, PixelWheel.Center);
        drop = new(wheel, diagnostics) { Selection = () => busy ? null : wheel.Selection };
        timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        timeout.Tick += (_, _) => { timeout.Stop(); if (!drop.HasFile) { diagnostics.Write(DiagnosticEvent.OverlayUnconfirmedTimeout); CloseOverlay(DiagnosticReason.NoOleConfirmation); } };
        releaseDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        releaseDelay.Tick += (_, _) => CloseOverlay(DiagnosticReason.MouseReleased);
        gestures = new(app.Dispatcher, diagnostics);
        gestures.Candidate += point =>
        {
            var leftDown = DragGestureMonitor.IsLeftButtonDown;
            if (busy || exiting || overlay.IsVisible || !leftDown)
            {
                diagnostics.Write(DiagnosticEvent.CandidateIgnored, new() { Reason = busy ? DiagnosticReason.Busy : exiting ? DiagnosticReason.Exiting : overlay.IsVisible ? DiagnosticReason.AlreadyVisible : DiagnosticReason.LeftNotDown });
                return;
            }
            wheel.Reset(); drop.Reset(); wheelMode.Reset(DragGestureMonitor.IsControlButtonDown);
            diagnostics.Write(DiagnosticEvent.OverlayShowRequested);
            try { overlay.ShowNear(point); wheel.Open(); timeout.Start(); }
            catch (Exception error) { diagnostics.Write(DiagnosticEvent.OverlayFailed, DiagnosticData.FromException(error)); CloseOverlay(DiagnosticReason.NativeCallFailed); }
        };
        gestures.ControlChanged += down =>
        {
            if (!overlay.IsVisible || !wheelMode.ControlChanged(down)) return;
            wheel.SetMode(wheelMode.Mode == WheelMode.Tools);
            UpdatePageNavigation();
            diagnostics.Write(DiagnosticEvent.ModeChanged, new() { Tools = wheel.Mode == WheelMode.Tools, Count = wheel.OptionsCount });
        };
        gestures.Cancelled += () => CloseOverlay(DiagnosticReason.Escape);
        gestures.PageRequested += direction =>
        {
            if (!busy && overlay.IsVisible && drop.HasFile && wheel.Mode == WheelMode.Tools)
            {
                wheel.ChangePage(direction);
                diagnostics.Write(DiagnosticEvent.ToolsPageChanged, new() { Page = wheel.CurrentPage + 1, Count = wheel.PageCount, ToolOptionsCount = wheel.OptionsCount });
            }
            UpdatePageNavigation();
        };
        gestures.Released += () => { if (overlay.IsVisible) releaseDelay.Start(); };
        drop.Entered += payload =>
        {
            timeout.Stop(); wheel.SetSelection(payload);
            capabilityProbe?.Cancel(); capabilityProbe?.Dispose();
            capabilityProbe = new();
            pendingCapabilityProbe = RefreshCapabilitiesAsync(payload, capabilityProbe.Token);
            var conversions = converter.ContextOptions(payload, WheelMode.Convert).Count;
            var tools = converter.ContextOptions(payload, WheelMode.Tools).Count;
            UpdatePageNavigation();
            diagnostics.Write(DiagnosticEvent.SelectionAnalyzed, new() { Count = payload.Files.Count, Enabled = payload.IsValid, ConversionOptionsCount = conversions, ToolOptionsCount = tools });
            if (conversions == 0 && tools == 0)
            {
                CloseOverlay(DiagnosticReason.NoCommonAction);
                tray!.ShowBalloonTip(5000, "Naqla", payload.IsValid ? "لا توجد عملية مشتركة لهذه الملفات" : "بعض الملفات غير مدعومة أو لا يمكن قراءتها؛ لم يتم تخطيها", Forms.ToolTipIcon.Info);
            }
            else if (conversions == 0)
                tray!.ShowBalloonTip(4000, "Naqla", "لا توجد صيغة مشتركة؛ اضغط Ctrl للأدوات المشتركة", Forms.ToolTipIcon.Info);
        };
        drop.Moved += wheel.Track;
        drop.Left += () => CloseOverlay(DiagnosticReason.OleEnded);
        drop.Accepted += StartOperation;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add($"Naqla · {DiagnosticLog.Build}").Enabled = false;
        menu.Items.Add("Drag a file, then press Shift").Enabled = false;
        menu.Items.Add("Wheel design preview…", null, (_, _) => OpenPreview());
        menu.Items.Add("Manual testing guide…", null, (_, _) => OpenTestingGuide());
        menu.Items.Add("Local components / plugins…", null, async (_, _) => await OpenExtensionsAsync());
        cancelConversion = new Forms.ToolStripMenuItem("Cancel current conversion") { Enabled = false };
        cancelConversion.Click += (_, _) => conversionCancellation?.Cancel();
        menu.Items.Add(cancelConversion);
        showProgress = new Forms.ToolStripMenuItem("Show current / recent results…") { Enabled = false };
        showProgress.Click += (_, _) => progressWindow?.Reopen(); menu.Items.Add(showProgress);
        undoRename = new Forms.ToolStripMenuItem("Undo last rename…") { Enabled = false };
        undoRename.Click += async (_, _) => await RequestUndoAsync(); menu.Items.Add(undoRename);
        menu.Items.Add("Rename recovery records…", null, (_, _) =>
        {
            if (Directory.Exists(renamer.RecoveryDirectoryPath)) Process.Start(new ProcessStartInfo(renamer.RecoveryDirectoryPath) { UseShellExecute = true });
            else MessageBox.Show("لا توجد خرائط استرداد محلية. التراجع المعتاد متاح للجلسة الحالية من القائمة.", "Naqla");
        });
        var diagnosticMenu = new Forms.ToolStripMenuItem("Diagnostics");
        var logging = new Forms.ToolStripMenuItem("Local logging enabled") { CheckOnClick = true, Checked = diagnostics.Enabled };
        logging.CheckedChanged += (_, _) => diagnostics.Enabled = logging.Checked;
        diagnosticMenu.DropDownItems.Add(logging);
        diagnosticMenu.DropDownItems.Add("Mark test start", null, (_, _) =>
        {
            if (!diagnostics.Enabled || !diagnostics.Available || diagnostics.LimitReached)
            { MessageBox.Show(diagnostics.StatusText(), "Naqla · Diagnostics"); return; }
            diagnostics.Write(DiagnosticEvent.TestMarked, new() { Count = ++testMarker });
            tray!.ShowBalloonTip(3000, $"Naqla · Test {testMarker}", "Drag a local file, then press Shift. Escape cancels.", Forms.ToolTipIcon.Info);
        });
        diagnosticMenu.DropDownItems.Add("Show diagnostic status…", null, (_, _) => MessageBox.Show(diagnostics.StatusText(), "Naqla · Diagnostics"));
        diagnosticMenu.DropDownItems.Add("Open log folder", null, (_, _) =>
        {
            if (Directory.Exists(diagnostics.DirectoryPath)) Process.Start(new ProcessStartInfo(diagnostics.DirectoryPath) { UseShellExecute = true });
            else MessageBox.Show(diagnostics.StatusText(), "Naqla · Diagnostics");
        });
        menu.Items.Add(diagnosticMenu);
        var startup = new Forms.ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = IsStartupEnabled() };
        startup.CheckedChanged += (_, _) =>
        {
            try { SetStartup(startup.Checked); diagnostics.Write(DiagnosticEvent.StartupSettingChanged, new() { Enabled = startup.Checked }); }
            catch (Exception error) { diagnostics.Write(DiagnosticEvent.StartupSettingChanged, DiagnosticData.FromException(error)); tray!.ShowBalloonTip(5000, "Naqla · Startup setting failed", error.Message, Forms.ToolTipIcon.Error); }
        };
        menu.Items.Add(startup);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, async (_, _) => await RequestExitAsync());
        icon = CreateIcon();
        tray = new Forms.NotifyIcon { Icon = icon, Text = "Naqla — drag + Shift", ContextMenuStrip = menu, Visible = true };
        diagnostics.Write(DiagnosticEvent.TrayVisibleRequested, new() { Visible = tray.Visible });
        diagnostics.Write(DiagnosticEvent.AppReady);
        // NotifyIcon recreates its Shell_NotifyIcon registration on TaskbarCreated after Explorer restarts.
        app.SessionEnding += (_, _) => { Dispose(); };
    }
    private async Task RefreshCapabilitiesAsync(FileDragPayload payload, CancellationToken token)
    {
        try
        {
            await converter.ProbeCapabilitiesAsync(payload.Files, token);
            if (!token.IsCancellationRequested && overlay.IsVisible && !busy) { wheel.SetSelection(payload); UpdatePageNavigation(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { diagnostics.Write(DiagnosticEvent.UnhandledError, DiagnosticData.FromException(error)); }
    }
    private void StartOperation(FileDragPayload payload, WheelOption option)
    {
        CloseOverlay(DiagnosticReason.AcceptedDrop); busy = true;
        cancelConversion.Enabled = true; tray.Text = $"Naqla · {option.Label}";
        var cancellation = new CancellationTokenSource(); conversionCancellation = cancellation;
        // Track the entire crop-preview/editor/conversion workflow, so Exit can cancel and await cleanup.
        pendingConversion = RunOperationAsync(payload, option, cancellation);
    }
    private async Task RunOperationAsync(FileDragPayload payload, WheelOption option, CancellationTokenSource cancellation)
    {
        // Always finish the source OLE Drop callback before an editor can open.
        await Task.Yield();
        using (cancellation)
        {
            diagnostics.Write(DiagnosticEvent.ConversionStarted, DiagnosticData.FromFile(payload.PreviewFile) with
            { Target = DiagnosticData.FormatOf(option.Id), Operation = DiagnosticData.OperationOf(option.Id), Count = payload.Files.Count });
            try
            {
                CropRectangle? crop = null;
                RenamePlan? rename = null;
                TargetSizeSettings? optimization = null;
                PdfEditPlan? pdf = null;
                ImageToolOptions? imageOptions = null;
                MediaToolOptions? mediaOptions = null;
                ModelPlan? model = null;
                PrivacyPlan? privacy = null;
                string? archiveFormat = null;
                var pdfDpi = 144;
                var target = option.Id;
                if (target is "TRIM" or "FRAMES" or "NORMALIZE" or "SILENCE" or "CHANNELS")
                {
                    var info = await processor.RunExclusiveAsync(ct => converter.AdvancedMedia.InspectAsync(payload.Files[0], ct), cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    var editor = new MediaToolsWindow(payload.Files[0], info, target, payload.Files.Count, converter.AdvancedMedia, processor, cancellation.Token);
                    if (!await ShowToolAsync(editor, cancellation.Token) || editor.Accepted is null) throw new OperationCanceledException(cancellation.Token);
                    mediaOptions = editor.Accepted;
                }
                if (target is "BACKGROUND" or "OCR" or "SUBTITLE")
                {
                    var editor = new ModelToolsWindow(payload.Files[0], target, converter.Models, processor, cancellation.Token);
                    if (!await ShowToolAsync(editor, cancellation.Token) || editor.Accepted is null) throw new OperationCanceledException(cancellation.Token);
                    model = editor.Accepted;
                }
                if (target == "PRIVACY")
                {
                    var analyses = await processor.RunExclusiveAsync(async ct =>
                    {
                        var list = new List<PrivacyAnalysis>(); foreach (var file in payload.Files) list.Add(await converter.Privacy.AnalyzeAsync(file, ct)); return list;
                    }, cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    var editor = new PrivacyToolsWindow(analyses, cancellation.Token);
                    if (!await ShowToolAsync(editor, cancellation.Token) || editor.Accepted is null) throw new OperationCanceledException(cancellation.Token);
                    privacy = editor.Accepted;
                }
                if (target is "ARCHIVE" or "EXTRACT")
                {
                    var editor = new ArchiveToolsWindow(payload.Files.Count, converter.Archives.SevenZipAvailable, target == "EXTRACT", cancellation.Token);
                    if (!await ShowToolAsync(editor, cancellation.Token)) throw new OperationCanceledException(cancellation.Token);
                    archiveFormat = editor.Format;
                }
                if (target is "ROTATE" or "COMPRESS" || payload.Files[0].Extension == "PDF" && target is "PNG" or "JPG")
                {
                    var settings = new ProcessingSettingsWindow(payload.Files[0], target, payload.Files.Count);
                    using var registration = cancellation.Token.Register(() => app.Dispatcher.BeginInvoke(() => { if (settings.IsVisible) settings.Close(); }));
                    if (settings.ShowDialog() != true) throw new OperationCanceledException(cancellation.Token);
                    imageOptions = settings.ImageOptions; mediaOptions = settings.MediaOptions; pdfDpi = settings.Dpi;
                }
                if (PdfToolIds.TryParse(option.Id, out var pdfTool))
                {
                    var session = await processor.RunExclusiveAsync(ct => converter.PdfToolkit.OpenAsync(payload.Files, ct), cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    var editor = new PdfToolkitWindow(session, pdfTool, converter.PdfToolkit, processor, cancellation.Token, diagnostics);
                    using var registration = cancellation.Token.Register(() => app.Dispatcher.BeginInvoke(() => { if (editor.IsVisible) editor.Close(); }));
                    diagnostics.Write(DiagnosticEvent.PdfEditorOpened, new() { Operation = DiagnosticData.OperationOf(option.Id), Count = session.PageCount });
                    diagnostics.Write(DiagnosticEvent.OperationInputRequested, new() { Operation = DiagnosticData.OperationOf(option.Id), Count = payload.Files.Count });
                    var accepted = editor.ShowDialog() == true;
                    await editor.PendingWork; cancellation.Token.ThrowIfCancellationRequested();
                    if (!accepted || editor.AcceptedPlan is null) throw new OperationCanceledException(cancellation.Token);
                    pdf = editor.AcceptedPlan; target = PdfToolIds.Id(pdf.Tool);
                    diagnostics.Write(DiagnosticEvent.OperationInputAccepted, new() { Operation = DiagnosticData.OperationOf(target), Count = pdf.Tool == PdfTool.Merge ? session.PageCount : pdf.OutputPages().Count });
                }
                if (option.Id == "TARGETSIZE")
                {
                    var source = await processor.RunExclusiveAsync(ct => converter.GetOptimizationSourceAsync(payload.Files, ct), cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    var editor = new TargetSizeWindow(source, payload.Files.Count, async (settings, progress, ct) =>
                    {
                        diagnostics.Write(DiagnosticEvent.OptimizationPreviewStarted, new() { Operation = DiagnosticOperation.TargetSize, TargetBytes = settings.TargetBytes, Target = DiagnosticData.FormatOf(settings.Format) });
                        try
                        {
                            var measured = await processor.RunExclusiveAsync(inner => converter.PreviewOptimizationAsync(payload.Files[source.PreviewIndex], settings, progress, inner), ct);
                            diagnostics.Write(DiagnosticEvent.OptimizationPreviewMeasured, new() { Operation = DiagnosticOperation.TargetSize, TargetBytes = measured.Report.TargetBytes, ResultBytes = measured.Report.ResultBytes, Succeeded = measured.Report.Achieved });
                            return measured;
                        }
                        catch (OperationCanceledException) { diagnostics.Write(DiagnosticEvent.OptimizationPreviewCancelled, new() { Operation = DiagnosticOperation.TargetSize }); throw; }
                        catch (Exception error) { diagnostics.Write(DiagnosticEvent.OptimizationPreviewFailed, DiagnosticData.FromException(error) with { Operation = DiagnosticOperation.TargetSize }); throw; }
                    }, cancellation.Token);
                    using var registration = cancellation.Token.Register(() => app.Dispatcher.BeginInvoke(() => { if (editor.IsVisible) editor.Close(); }));
                    diagnostics.Write(DiagnosticEvent.OperationInputRequested, new() { Operation = DiagnosticOperation.TargetSize, Count = payload.Files.Count });
                    var accepted = editor.ShowDialog() == true;
                    await editor.PendingMeasurement;
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (!accepted || !editor.CanSave) throw new OperationCanceledException(cancellation.Token);
                    optimization = editor.MeasuredSettings!;
                    diagnostics.Write(DiagnosticEvent.OperationInputAccepted, new() { Operation = DiagnosticOperation.TargetSize, Count = payload.Files.Count, TargetBytes = optimization.TargetBytes, Target = DiagnosticData.FormatOf(optimization.Format) });
                }
                if (option.Id == "CROP")
                {
                    var preview = await converter.GetImagePreviewAsync(payload.Files[0], cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    var editor = new CropWindow(preview);
                    if (payload.Files.Count > 1) editor.SetBatchContext(payload.Files.Count);
                    using var registration = cancellation.Token.Register(() => app.Dispatcher.BeginInvoke(() => { if (editor.IsVisible) editor.Close(); }));
                    diagnostics.Write(DiagnosticEvent.OperationInputRequested, new() { Operation = DiagnosticOperation.Crop });
                    if (editor.ShowDialog() != true) throw new OperationCanceledException(cancellation.Token);
                    crop = editor.Selection;
                    diagnostics.Write(DiagnosticEvent.OperationInputAccepted, new() { Operation = DiagnosticOperation.Crop, Width = crop.Width, Height = crop.Height });
                }
                if (option.Id == "RENAME")
                {
                    var initial = await renamer.PreviewAsync(payload.Files, new(), cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    var editor = new RenameWindow(renamer, payload.Files, initial);
                    using var registration = cancellation.Token.Register(() => app.Dispatcher.BeginInvoke(() => { if (editor.IsVisible) editor.Close(); }));
                    diagnostics.Write(DiagnosticEvent.OperationInputRequested, new() { Operation = DiagnosticOperation.Rename, Count = payload.Files.Count });
                    if (editor.ShowDialog() != true || !editor.CanConfirm) throw new OperationCanceledException(cancellation.Token);
                    rename = editor.Plan!;
                    diagnostics.Write(DiagnosticEvent.OperationInputAccepted, new() { Operation = DiagnosticOperation.Rename, Count = rename.ChangedCount });
                }
                progressWindow ??= CreateProgressWindow(); showProgress.Enabled = true;
                progressWindow.Begin(option.Id is "MERGE" or "RENAME" or "ARCHIVE" || pdf is not null ? 1 : payload.Files.Count, pdf is null ? option.Label : PdfToolIds.Label(pdf.Tool), option.Id == "RENAME");
                var observer = new Progress<BatchProgress>(state =>
                {
                    if (disposed) return;
                    progressWindow?.Update(state);
                    diagnostics.Write(DiagnosticEvent.BatchProgressChanged, new() { Count = state.Total, CompletedCount = state.Completed, FailedCount = state.Failed });
                    if (state.LastFinished is { } item)
                    {
                        if (item.Outcome == JobOutcome.Succeeded)
                            diagnostics.Write(DiagnosticEvent.ConversionSucceeded, new() { Format = DiagnosticData.FormatOf(Path.GetExtension(item.Output!.OutputPath).TrimStart('.')), Operation = DiagnosticData.OperationOf(target), Width = item.Output.Width, Height = item.Output.Height, Count = item.Output.Items, TargetBytes = item.Output.Optimization?.TargetBytes, ResultBytes = item.Output.Optimization?.ResultBytes });
                        else if (item.Outcome == JobOutcome.Failed)
                            diagnostics.Write(DiagnosticEvent.ConversionFailed, new() { Operation = DiagnosticData.OperationOf(target), Error = item.Failure!.Kind switch { JobFailureKind.PermissionDenied => DiagnosticError.Unauthorized, JobFailureKind.Unsupported => DiagnosticError.NotSupported, JobFailureKind.IO or JobFailureKind.InvalidInput or JobFailureKind.ImageTargetNotMet => DiagnosticError.IO, _ => DiagnosticError.Other }, ErrorCode = item.Failure.ErrorCode, TargetBytes = item.Failure.Optimization?.TargetBytes, ResultBytes = item.Failure.Optimization?.ResultBytes });
                    }
                });
                var result = await processor.RunAsync(new(payload.Files, target, crop, rename, rename is not null, optimization, pdf, imageOptions, mediaOptions, pdfDpi, model, privacy, archiveFormat), observer, cancellation.Token);
                progressWindow.Finish(result);
                var renamed = result.Jobs.Select(x => x.Output?.Rename).FirstOrDefault(x => x is not null);
                if (renamed is not null) { lastRename = renamed; undoRename.Enabled = true; }
                diagnostics.Write(DiagnosticEvent.BatchFinished, new() { Count = result.Jobs.Count, CompletedCount = result.Completed, FailedCount = result.Failed, CancelledCount = result.Cancelled });
                if (result.Cancelled > 0) diagnostics.Write(DiagnosticEvent.ConversionCancelled);
                diagnostics.Write(DiagnosticEvent.NotificationRequested);
                if (!disposed && !exiting) tray.ShowBalloonTip(5000, "Naqla", $"نجح {result.Succeeded} · فشل {result.Failed} · أُلغي {result.Cancelled}. التفاصيل من الأيقونة.", result.Failed > 0 ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);
            }
            catch (OperationCanceledException)
            {
                diagnostics.Write(DiagnosticEvent.ConversionCancelled);
                if (!disposed && !exiting) tray.ShowBalloonTip(3000, "Naqla · Cancelled", "The original files were preserved.", Forms.ToolTipIcon.Info);
            }
            catch (Exception error)
            {
                diagnostics.Write(DiagnosticEvent.ConversionFailed, DiagnosticData.FromException(error));
                if (!disposed && !exiting) tray.ShowBalloonTip(7000, "Naqla · Conversion failed", error.Message, Forms.ToolTipIcon.Error);
            }
            finally
            {
                busy = false; conversionCancellation = null;
                if (!disposed) { cancelConversion.Enabled = false; tray.Text = "Naqla — drag + Shift"; }
            }
        }
    }
    private async Task<bool> ShowToolAsync(ToolDialog editor, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var registration = token.Register(() => app.Dispatcher.BeginInvoke(() => { if (editor.IsVisible) editor.Close(); }));
        var accepted = editor.ShowDialog() == true;
        await editor.PendingWork; token.ThrowIfCancellationRequested(); return accepted;
    }
    private async Task OpenExtensionsAsync()
    {
        if (busy || exiting) return;
        CloseOverlay(); busy = true; cancelConversion.Enabled = true;
        using var cancellation = new CancellationTokenSource(); conversionCancellation = cancellation;
        async Task Work()
        {
            try { await ShowToolAsync(new ExtensionsWindow(converter, processor, cancellation.Token), cancellation.Token); }
            catch (OperationCanceledException) { }
            catch (Exception error) { diagnostics.Write(DiagnosticEvent.UnhandledError, DiagnosticData.FromException(error)); if (!exiting) MessageBox.Show(error.Message, "Naqla"); }
            finally { busy = false; conversionCancellation = null; if (!disposed) cancelConversion.Enabled = false; }
        }
        pendingConversion = Work(); await pendingConversion;
    }
    private async Task RequestUndoAsync()
    {
        if (busy || exiting || lastRename is null) return;
        busy = true; cancelConversion.Enabled = true;
        using var cancellation = new CancellationTokenSource(); conversionCancellation = cancellation;
        pendingConversion = RunUndoAsync(lastRename, cancellation.Token);
        try { await pendingConversion; }
        finally { busy = false; conversionCancellation = null; if (!disposed) { cancelConversion.Enabled = false; tray.Text = "Naqla — drag + Shift"; } }
    }
    private async Task RunUndoAsync(RenameReceipt receipt, CancellationToken token)
    {
        IReadOnlyList<DraggedFile> undoFiles = [];
        var executing = false;
        try
        {
            var plan = await renamer.PreviewUndoAsync(receipt, token);
            token.ThrowIfCancellationRequested();
            var files = plan.Entries.Select(x => x.Source).ToArray();
            undoFiles = files;
            var editor = new RenameWindow(renamer, files, plan, undo: true);
            using var registration = token.Register(() => app.Dispatcher.BeginInvoke(() => { if (editor.IsVisible) editor.Close(); }));
            diagnostics.Write(DiagnosticEvent.OperationInputRequested, new() { Operation = DiagnosticOperation.UndoRename, Count = files.Length });
            if (editor.ShowDialog() != true || !editor.CanConfirm) throw new OperationCanceledException(token);
            diagnostics.Write(DiagnosticEvent.OperationInputAccepted, new() { Operation = DiagnosticOperation.UndoRename, Count = plan.ChangedCount });
            progressWindow ??= CreateProgressWindow(); showProgress.Enabled = true; progressWindow.Begin(1, "استعادة الأسماء", true);
            executing = true;
            var output = await processor.RunExclusiveAsync(ct => renamer.ExecuteAsync(plan, true, ct), token);
            progressWindow.Finish(new(new[] { new BatchItemResult(0, files, JobOutcome.Succeeded, output) }), undo: true);
            lastRename = null; undoRename.Enabled = false;
            diagnostics.Write(DiagnosticEvent.ConversionSucceeded, new() { Operation = DiagnosticOperation.UndoRename, Count = output.Items });
            if (!exiting && !disposed) tray.ShowBalloonTip(4000, "Naqla", $"استعيدت أسماء {output.Items} ملف", Forms.ToolTipIcon.Info);
        }
        catch (OperationCanceledException)
        {
            diagnostics.Write(DiagnosticEvent.ConversionCancelled, new() { Operation = DiagnosticOperation.UndoRename });
            if (executing) progressWindow?.Finish(new(new[] { new BatchItemResult(0, undoFiles, JobOutcome.Cancelled) }));
        }
        catch (Exception error)
        {
            diagnostics.Write(DiagnosticEvent.ConversionFailed, DiagnosticData.FromException(error) with { Operation = DiagnosticOperation.UndoRename });
            if (executing) progressWindow?.Finish(new(new[] { new BatchItemResult(0, undoFiles, JobOutcome.Failed, Failure: JobFailure.FromException(error)) }));
            if (!disposed && !exiting) tray.ShowBalloonTip(7000, "Naqla · Undo failed", error.Message, Forms.ToolTipIcon.Error);
        }
    }
    private BatchProgressWindow CreateProgressWindow()
    {
        var window = new BatchProgressWindow();
        window.CancelRequested += () => conversionCancellation?.Cancel();
        return window;
    }
    private void UpdatePageNavigation() => gestures.PageNavigationEnabled = !busy && overlay.IsVisible && drop.HasFile && wheel.Mode == WheelMode.Tools && wheel.PageCount > 1;
    private void CloseOverlay(DiagnosticReason reason = DiagnosticReason.None)
    {
        gestures.PageNavigationEnabled = false;
        capabilityProbe?.Cancel();
        timeout.Stop(); releaseDelay.Stop();
        drop.Reset();
        if (overlay.IsVisible) { overlay.Hide(); diagnostics.Write(DiagnosticEvent.OverlayClosed, new() { Reason = reason }); }
    }
    public async Task RequestExitAsync()
    {
        if (exiting) return;
        exiting = true; diagnostics.Write(DiagnosticEvent.AppExitRequested, new() { Busy = busy }); CloseOverlay(DiagnosticReason.Exit);
        conversionCancellation?.Cancel();
        await pendingCapabilityProbe;
        try { await pendingConversion; } catch (Exception) { /* failure already reported */ }
        Dispose(); app.Shutdown();
    }
    private static void OpenPreview()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "preview", "index.html");
        if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    private static void OpenTestingGuide()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TESTING.html");
        if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("Naqla") is string value && value == $"\"{Environment.ProcessPath}\"";
    }
    private static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("Naqla", $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue("Naqla", false);
    }
    private static System.Drawing.Icon CreateIcon()
    {
        using var resource = typeof(Program).Assembly.GetManifestResourceStream("Naqla.Brand.Icon")
            ?? throw new InvalidOperationException("The Naqla logo icon resource is missing.");
        using var branded = new System.Drawing.Icon(resource, 32, 32);
        return (System.Drawing.Icon)branded.Clone();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        conversionCancellation?.Cancel();
        CloseOverlay(DiagnosticReason.Exit); gestures.Dispose(); drop.Dispose(); overlay.Close();
        progressWindow?.CloseForExit();
        ComponentStore.ReleaseVerificationHandles();
        tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); icon.Dispose();
        diagnostics.Write(DiagnosticEvent.TrayRemoved);
    }
}
