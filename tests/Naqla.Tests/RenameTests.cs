using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using Naqla.App;
using Naqla.Conversion;
using Naqla.Core;
using Naqla.Windows;

internal static partial class Program
{
    private static readonly WindowsRenameFileSystem RenameFs = new();
    private static SmartRenameService RenameService(IRenameFileSystem? fs = null) => new(fs ?? RenameFs, Path.Combine(folder, "rename-records"));
    private static string RenameSource(string group, string name, string text = "محتوى ثابت — rename does not copy")
    { var directory = BatchFolder(group); var path = Path.Combine(directory, name); File.WriteAllText(path, text); return path; }
    private static RenamePlan RenamePreview(SmartRenameService service, RenameSettings settings, params string[] paths) => service.PreviewAsync(paths.Select(FileClassifier.Inspect).ToArray(), settings).GetAwaiter().GetResult();
    private static ConversionResult RenameCommit(SmartRenameService service, RenamePlan plan, bool confirmed = true, CancellationToken token = default) => service.ExecuteAsync(plan, confirmed, token).GetAwaiter().GetResult();
    private static void NoRenameStages(string directory) => Check(!Directory.EnumerateFiles(directory, "Naqla-Rename-Recovery-*", SearchOption.AllDirectories).Any(), "Original left staged without a recovery test");

    private static void RenameTransforms()
    {
        var source = RenameSource("rename-transforms", "draft_12.TXT"); File.SetLastWriteTimeUtc(source, new DateTime(2020, 5, 6, 13, 14, 0, DateTimeKind.Utc));
        var service = RenameService(); var hash = SHA256.HashData(File.ReadAllBytes(source));
        var plan = RenamePreview(service, new(Pattern: "{n}-{name}-{date}", Find: @"draft_(\d+)", Replace: "final-$1", Prefix: "مشروع-", Suffix: "_done", Regex: true, Start: 7, Padding: 4, Case: RenameCase.Upper), source);
        Check(plan.IsValid && plan.Entries[0].After == "مشروع-0007-FINAL-12-2020-05-06_DONE.TXT", "Regex/groups/prefix/suffix/padding/case/date/extension transform");
        var literal = RenamePreview(service, new(Find: "DRAFT", Replace: "تقرير", IgnoreCase: true, Pattern: "{name}"), source);
        Check(literal.Entries[0].After == "تقرير_12.TXT", "Literal case-insensitive replacement");
        var cleanup = RenamePreview(service, new(Prefix: " bad:  ", Suffix: "...  ", Cleanup: true, Case: RenameCase.Lower), source);
        Check(cleanup.IsValid && cleanup.Entries[0].After == "bad_ draft_12.TXT", "Cleanup/whitespace/trailing punctuation");
        var full = RenamePreview(service, new(Pattern: "NEW.data", PreserveExtension: false), source);
        Check(full.IsValid && full.Entries[0].After == "NEW.data", "Explicit full-name editing ignored");
        var title = RenamePreview(service, new(Pattern: "sample TITLE", Case: RenameCase.Title), source); Check(title.Entries[0].After == "Sample Title.TXT", "Title case");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))) && Directory.GetFiles(Path.GetDirectoryName(source)!).Length == 1, "Preview changed files");
    }
    private static void RenameValidation()
    {
        var a = RenameSource("rename-validation", "a.txt"); var b = RenameSource("rename-validation", "b.txt"); var service = RenameService();
        foreach (var name in new[] { "CON", "AUX", "COM¹", "LPT9", "x/y", "x:y", "bad.", "bad ", "", "{unknown}", new string('x', 250) })
        {
            var plan = RenamePreview(service, new(Pattern: name), a);
            // Dot/space before a preserved extension is legal; validate the full name directly too.
            if (name is "bad." or "bad ") { var rejected = false; try { SmartRenameService.ValidateLeaf(name); } catch (ArgumentException) { rejected = true; } Check(rejected, "Trailing character accepted"); }
            else Check(!plan.IsValid, "Invalid/reserved template accepted: " + name);
        }
        var duplicate = RenamePreview(service, new(Pattern: "same"), a, b); Check(!duplicate.IsValid && duplicate.Entries.All(x => x.Error is not null), "Duplicate target names accepted");
        var occupied = RenamePreview(service, new(Pattern: "b"), a); Check(!occupied.IsValid, "Existing unrelated target accepted");
        var duplicateSource = RenamePreview(service, new(Pattern: "{n}"), a, a); Check(!duplicateSource.IsValid, "Duplicate source accepted");
        var changed = RenamePreview(service, new(Prefix: "new-"), a);
        var escaped = new RenamePlan(new[] { changed.Entries[0] with { TargetPath = Path.Combine(folder, "outside.txt") } });
        var failed = false; try { RenameCommit(service, escaped); } catch (ArgumentException) { failed = true; }
        Check(failed && File.Exists(a) && !File.Exists(Path.Combine(folder, "outside.txt")), "Rename escaped source directory");
    }
    private static void RenameRealAndUndo()
    {
        var source = RenameSource("rename-real", "ملف أصلي.txt");
        File.WriteAllText(source + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
        var before = RenameFs.Capture(source); var hash = SHA256.HashData(File.ReadAllBytes(source)); var service = RenameService();
        var plan = RenamePreview(service, new(Prefix: "جديد-"), source);
        var denied = false; try { RenameCommit(service, plan, false); } catch (ArgumentException) { denied = true; }
        Check(denied && File.Exists(source), "Rename executed without explicit confirmation");
        var output = RenameCommit(service, plan);
        Check(!File.Exists(source) && File.Exists(output.OutputPath) && output.Rename!.Files.Count == 1, "Source was copied instead of renamed");
        Check(RenameFs.Capture(output.OutputPath) == before && hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(output.OutputPath))), "File identity/content changed");
        Check(File.ReadAllText(output.OutputPath + ":Zone.Identifier").Contains("ZoneId=3"), "Rename lost alternate stream/MOTW");
        var undo = service.PreviewUndoAsync(output.Rename!).GetAwaiter().GetResult(); Check(undo.IsValid, "Safe undo preview rejected"); RenameCommit(service, undo);
        Check(File.Exists(source) && !File.Exists(output.OutputPath) && RenameFs.Capture(source) == before && hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "Undo did not restore original identity/name");
        NoRenameStages(Path.GetDirectoryName(source)!);
    }
    private static void RenameBatchAndSwap()
    {
        var a = RenameSource("rename-batch", "A.txt", "first"); var b = RenameSource("rename-batch", "B.txt", "second"); var service = RenameService();
        File.SetCreationTimeUtc(a, new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        File.SetCreationTimeUtc(b, new DateTime(2021, 3, 4, 0, 0, 0, DateTimeKind.Utc));
        var stateA = RenameFs.Capture(a); var stateB = RenameFs.Capture(b);
        var swap = new RenamePlan(new[] { new RenameEntry(FileClassifier.Inspect(a), stateA, b), new RenameEntry(FileClassifier.Inspect(b), stateB, a) });
        var result = RenameCommit(service, swap);
        Check(File.ReadAllText(a) == "second" && File.ReadAllText(b) == "first" && RenameFs.Capture(b) == stateA && RenameFs.Capture(a) == stateB, "Swap lost a source");
        RenameCommit(service, service.PreviewUndoAsync(result.Rename!).GetAwaiter().GetResult());
        var caseOnly = RenamePreview(service, new(Case: RenameCase.Lower), a); RenameCommit(service, caseOnly);
        Check(Directory.GetFiles(Path.GetDirectoryName(a)!).Any(x => Path.GetFileName(x) == "a.txt"), "Case-only rename failed");
        var secondDirectory = RenameSource("rename-other-directory", "a.txt");
        var numbered = RenamePreview(service, new(Pattern: "{n}-{name}", Start: 8, Padding: 2), Path.Combine(Path.GetDirectoryName(a)!, "a.txt"), secondDirectory);
        var batch = RenameCommit(service, numbered);
        Check(batch.Items == 2 && batch.OutputPaths!.Count == 2 && batch.OutputPaths.Select(Path.GetDirectoryName).Distinct().Count() == 2, "Batch names/source folders wrong");
        NoRenameStages(Path.GetDirectoryName(a)!); NoRenameStages(Path.GetDirectoryName(secondDirectory)!);
    }
    private static void RenameStaleAndUndoGuards()
    {
        var source = RenameSource("rename-stale", "source.txt"); var service = RenameService();
        var plan = RenamePreview(service, new(Prefix: "new-"), source); File.AppendAllText(source, " changed");
        var failed = false; try { RenameCommit(service, plan); } catch (IOException) { failed = true; }
        Check(failed && File.Exists(source) && !File.Exists(plan.Entries[0].TargetPath), "Stale preview renamed changed file");
        var output = RenameCommit(service, RenamePreview(service, new(Prefix: "new-"), source));
        File.WriteAllText(source, "unrelated target occupant");
        Check(!service.PreviewUndoAsync(output.Rename!).GetAwaiter().GetResult().IsValid, "Undo would replace an unrelated file");
        File.Delete(source); File.AppendAllText(output.OutputPath, " edited");
        Check(!service.PreviewUndoAsync(output.Rename!).GetAwaiter().GetResult().IsValid, "Undo accepted a modified file");
        NoRenameStages(Path.GetDirectoryName(source)!);
    }
    private static void RenameLockAndCancellation()
    {
        var source = RenameSource("rename-lock", "source.txt"); var service = RenameService(); var plan = RenamePreview(service, new(Prefix: "new-"), source);
        using (var blocked = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failed = false; try { RenameCommit(service, plan); } catch (System.ComponentModel.Win32Exception) { failed = true; }
            Check(failed && File.Exists(source), "Locked source mutated");
        }
        using var cancellation = new CancellationTokenSource();
        var wrapped = new RenameFaultFileSystem((_, _) => cancellation.Cancel()); var cancellable = RenameService(wrapped);
        var hash = SHA256.HashData(File.ReadAllBytes(source)); var cancelled = false;
        try { RenameCommit(cancellable, plan, token: cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && File.Exists(source) && hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "Mid-stage cancellation did not restore source");
        NoRenameStages(Path.GetDirectoryName(source)!);
    }
    private static void RenameRollback()
    {
        var a = RenameSource("rename-rollback", "a.txt", "A"); var b = RenameSource("rename-rollback", "b.txt", "B"); var triggered = false;
        var wrapped = new RenameFaultFileSystem((_, leaf) => { if (leaf == "new-b.txt" && !triggered) { triggered = true; throw new IOException("injected commit failure"); } }, beforeMove: true);
        var service = RenameService(wrapped); var plan = RenamePreview(service, new(Prefix: "new-"), a, b);
        var failed = false; try { RenameCommit(service, plan); } catch (IOException) { failed = true; }
        Check(failed && triggered && File.ReadAllText(a) == "A" && File.ReadAllText(b) == "B", "Commit failure did not roll back all names");
        NoRenameStages(Path.GetDirectoryName(a)!);
    }
    private static void RenameRecoveryCollision()
    {
        var source = RenameSource("rename-recovery", "source.txt", "ORIGINAL"); using var cancellation = new CancellationTokenSource(); var once = false;
        var wrapped = new RenameFaultFileSystem((_, leaf) => { if (leaf.StartsWith("Naqla-Rename-Recovery-", StringComparison.Ordinal) && !once) { once = true; File.WriteAllText(source, "unrelated newcomer"); cancellation.Cancel(); } });
        var service = RenameService(wrapped); var plan = RenamePreview(service, new(Prefix: "new-"), source);
        RenameRecoveryException? failure = null; try { RenameCommit(service, plan, token: cancellation.Token); } catch (RenameRecoveryException error) { failure = error; }
        var staged = Directory.GetFiles(Path.GetDirectoryName(source)!, "Naqla-Rename-Recovery-*");
        Check(failure is not null && staged.Length == 1 && File.ReadAllText(staged[0]) == "ORIGINAL" && File.ReadAllText(source) == "unrelated newcomer", "Blocked rollback overwrote/deleted user data");
        var reported = JobFailure.FromException(failure!);
        Check(reported.Kind == JobFailureKind.RenameRecovery && File.Exists(reported.LocalRecoveryRecordPath), "Recovery failure lost its local recovery map");
        Check(Directory.GetFiles(Path.Combine(folder, "rename-records"), "rename-*.json").Any(), "Recovery map missing");
        // Restore only this test's fixtures, after proving the service preserved both objects.
        File.Delete(source); File.Move(staged[0], source);
    }
    private static void RenameRegexBound()
    {
        var source = RenameSource("rename-regex", new string('a', 180) + "!.txt"); var service = RenameService(); var clock = Stopwatch.StartNew();
        var plan = RenamePreview(service, new(Find: "^(a+)+$", Replace: "x", Regex: true), source);
        Check(clock.ElapsedMilliseconds < 2000 && File.Exists(source), "Advanced regex was unbounded or mutated input");
        var invalid = false; try { RenamePreview(service, new(Find: "[", Regex: true), source); } catch (ArgumentException) { invalid = true; }
        Check(invalid && (plan.IsValid || plan.Entries[0].Error!.Contains("100 ms")), "Regex validation/timeout result missing");
    }
    private static void RenameQueueAndCapabilities()
    {
        var first = RenameSource("rename-queue", "first.txt"); var second = RenameSource("rename-queue", "second.txt"); var renamer = RenameService(); var converter = new LocalConversionService(renamer: renamer);
        var payload = FileDragPayload.Inspect(new[] { first, second }); Check(WheelOptions.For(payload, WheelMode.Tools, converter.CanExecute).Select(x => x.Id).SequenceEqual(new[] { "RENAME" }), "Supported text batch rename unavailable");
        var plan = RenamePreview(renamer, new(Prefix: "changed-"), first, second);
        var result = new BatchProcessor(converter).RunAsync(new(payload.Files, "RENAME", Rename: plan, RenameConfirmed: true)).GetAwaiter().GetResult();
        Check(result.Succeeded == 1 && result.Jobs[0].Output!.Items == 2 && !File.Exists(first) && !File.Exists(second), "Queue treated rename as copying/per-file independent jobs");
        var png = CreatePng("rename-wheel-one.png");
        var images = FileDragPayload.Inspect(new[] { png, CreatePng("rename-wheel-two.png") });
        Check(WheelOptions.For(images, WheelMode.Tools, converter.CanExecute).Any(x => x.Id == "RENAME") && WheelOptions.For(images, WheelMode.Tools, converter.CanExecute).Count == 7, "Image batch tools lost rename/optimizer");
        var wheel = new Naqla.Wheel.PixelWheel(converter.CanExecute); wheel.SetSelection(images); wheel.SetMode(true, false); wheel.Measure(new Size(260, 306)); wheel.Arrange(new Rect(0, 0, 260, 306)); wheel.UpdateLayout(); SaveRender(wheel, 260, 306, "native-rename-wheel.png");
    }
    private static void PixelButtonsAndRenameEditor()
    {
        _ = Application.Current ?? new Application();
        var panel = new StackPanel { Background = new SolidColorBrush(Color.FromRgb(18, 24, 32)), Margin = new Thickness(0) };
        foreach (var enabled in new[] { true, false })
        {
            var row = new WrapPanel { Margin = new Thickness(12) }; panel.Children.Add(row);
            foreach (var setting in new[] { ("إخفاء", PixelButtonTone.Blue, PixelButtonGlyph.Hide), ("إلغاء", PixelButtonTone.Coral, PixelButtonGlyph.Cancel), ("تفاصيل", PixelButtonTone.Violet, PixelButtonGlyph.Details), ("النتائج", PixelButtonTone.Mint, PixelButtonGlyph.Folder), ("تأكيد", PixelButtonTone.Amber, PixelButtonGlyph.Check) })
            {
                var button = new PixelButton { Content = setting.Item1, Tone = setting.Item2, Glyph = setting.Item3, IsEnabled = enabled, Margin = new Thickness(3) }; row.Children.Add(button);
                Check(button.Focusable && new ButtonAutomationPeer(button).GetName() == setting.Item1, "Pixel surface lost keyboard/automation semantics");
            }
        }
        panel.Measure(new Size(480, double.PositiveInfinity)); panel.Arrange(new Rect(0, 0, 480, panel.DesiredSize.Height)); panel.UpdateLayout(); SaveRender(panel, 480, (int)Math.Ceiling(panel.DesiredSize.Height), "native-pixel-buttons.png");
        var path = RenameSource("rename-editor", "معاينة.txt"); var service = RenameService(); var files = new[] { FileClassifier.Inspect(path) }; var plan = RenamePreview(service, new(Prefix: "جديد-"), path);
        var window = new RenameWindow(service, files, plan); var content = (FrameworkElement)window.Content;
        Check(!window.CanConfirm && File.Exists(path), "Editor enabled rename without confirmation");
        content.Measure(new Size(728, 600)); content.Arrange(new Rect(0, 0, 728, 600)); content.UpdateLayout(); SaveRender(content, 728, 600, "native-rename-editor.png");
        var dock = (DockPanel)content; var footer = (StackPanel)dock.Children[1]; var consent = (CheckBox)footer.Children[1];
        consent.IsChecked = true; Check(window.CanConfirm, "Valid preview with explicit consent cannot be accepted");
        var options = (WrapPanel)((StackPanel)dock.Children[0]).Children[1];
        ((TextBox)((StackPanel)options.Children[3]).Children[1]).Text = "edited-";
        Check(!window.CanConfirm && !consent.IsEnabled && consent.IsChecked == false && window.Plan is null, "Changing settings retained consent for a stale preview");
        window.Close();
        var renamed = RenameCommit(service, plan);
        var restored = RenameCommit(service, service.PreviewUndoAsync(renamed.Rename!).GetAwaiter().GetResult());
        var progress = new BatchProgressWindow();
        progress.Finish(new(new[] { new BatchItemResult(0, files, JobOutcome.Succeeded, restored) }), undo: true);
        Check(progress.StatusText == "استعيدت أسماء 1 ملف" && File.Exists(path), "Undo result did not report the restored names");
        var resultContent = (FrameworkElement)progress.Content;
        resultContent.Measure(new Size(350, 600)); resultContent.Arrange(new Rect(0, 0, 350, resultContent.DesiredSize.Height)); resultContent.UpdateLayout();
        SaveRender(resultContent, 350, (int)Math.Ceiling(resultContent.DesiredSize.Height), "native-rename-undo-result.png"); progress.CloseForExit();
    }
    private sealed class RenameFaultFileSystem(Action<string, string> callback, bool beforeMove = false) : IRenameFileSystem
    {
        // Failure/cancellation injection wraps real Windows moves. It is not Explorer acceptance.
        public RenameFileState Capture(string path) => RenameFs.Capture(path);
        public IRenameFileHandle Lock(string path) => new Wrapper(RenameFs.Lock(path), callback, beforeMove);
        private sealed class Wrapper(IRenameFileHandle actual, Action<string, string> callback, bool before) : IRenameFileHandle
        {
            public RenameFileState State => actual.State; public string CurrentPath => actual.CurrentPath;
            public void RenameToLeaf(string filename) { if (before) callback(CurrentPath, filename); actual.RenameToLeaf(filename); if (!before) callback(CurrentPath, filename); }
            public void Dispose() => actual.Dispose();
        }
    }
}
