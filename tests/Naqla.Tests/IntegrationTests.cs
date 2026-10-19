using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using Naqla.App;
using Naqla.Conversion;
using Naqla.Core;
using Naqla.Setup;
using Naqla.Wheel;

internal static partial class Program
{
    private static void RunIntegrationTests()
    {
        Run("Approved contextual Tools pagination wraps/clamps, six visible sectors and unchanged Convert/Ctrl", ContextPaging);
        Run("Local exact-catalog component import, modification rejection, cancellation and safe uninstall", ComponentLifecycle);
        Run("Local child jobs enforce deadlines, kill descendants and leave the parent usable", ProcessLifecycle);
        Run("Installer staged ownership preserves user files, refuses traversal and retains modified content", InstallerSafety);
        Run("New contextual native editors render offscreen with shared pixel controls and gated model save", NewEditorRenders);
        Run("Folder OLE payload metadata and real aggregate archive dispatch preserve sources", FolderDispatch);
        Run("Reviewed model dispatch rejects stale sources and retains editable actual OCR output", ModelDispatch);
        Run("Archive actual ZIP symlinks, TAR hardlinks, expansion bombs and mid-creation cancellation fail safely", ArchiveSecurityExtras);
        Run("Optional document engine with job/integrity guard converts real Arabic text and keeps source", DocumentIntegritySmoke);
    }
    private static LocalConversionService ExtendedService() => new(componentDirectory: Components, officeDirectory: Path.Combine(Components, "office"), pluginDirectory: Path.Combine(folder, "integration-plugins"));
    private static void ContextPaging()
    {
        var file = CreatePng("paged-tools.png"); var payload = FileDragPayload.Inspect([file]); var service = ExtendedService();
        var all = service.ContextOptions(payload, WheelMode.Tools); Check(new[] { "OCR", "BACKGROUND", "PRIVACY", "ARCHIVE", "TARGETSIZE", "CROP" }.All(id => all.Any(x => x.Id == id)), "Extended contextual tools are not registered");
        var wheel = new PixelWheel(service.CanOffer, service.ContextOptions); wheel.SetSelection(payload); wheel.SetMode(true, false);
        Check(wheel.OptionsCount == all.Count && wheel.VisibleOptionsCount <= 6 && wheel.PageCount >= 2, "Paging counts mismatch");
        var pages = wheel.PageCount; wheel.ChangePage(-1, false); Check(wheel.CurrentPage == pages - 1, "Backward wrap"); wheel.ChangePage(1, false); Check(wheel.CurrentPage == 0, "Forward wrap");
        wheel.Measure(new Size(260, 306)); wheel.Arrange(new Rect(0, 0, 260, 306)); wheel.UpdateLayout(); SaveRender(wheel, 260, 306, "native-tools-page-1.png");
        wheel.ChangePage(1, false); wheel.UpdateLayout(); SaveRender(wheel, 260, 306, "native-tools-page-2.png");
        wheel.SetMode(false, false); Check(wheel.VisibleOptionsCount == service.ContextOptions(payload, WheelMode.Convert).Count, "Convert was paginated");
        wheel.Reset(); Check(wheel.CurrentPage == 0 && wheel.Mode == WheelMode.Convert, "Per-drag page/mode reset");
        var state = new WheelPaging(); state.Update(19); Check(state.Pages == 4, "Page ceiling"); state.Turn(-1); state.Update(2); Check(state.Page == 0 && state.Pages == 1 && !state.Turn(1), "Page clamp");
        foreach (var id in new[] { "OCR", "BACKGROUND", "TRIM", "SUBTITLE", "PRIVACY", "ARCHIVE", "PLUGIN:private.name:action" }) Check(DiagnosticData.OperationOf(id) != DiagnosticOperation.Convert, "Untyped operation diagnostic");
    }
    private static string MakeComponentPack(string feature)
    {
        var catalogPath = Path.GetFullPath(Path.Combine(Components, "../../assets/component-catalog.json"));
        var catalog = JsonSerializer.Deserialize<Dictionary<string, ComponentFile>>(File.ReadAllText(catalogPath))!;
        var path = Path.Combine(folder, "pack-" + feature + ".zip"); using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var writer = new StreamWriter(zip.CreateEntry("component.json").Open())) writer.Write(JsonSerializer.Serialize(new { SchemaVersion = 1, Feature = feature, CatalogVersion = ComponentStore.CatalogVersion }));
        foreach (var pair in catalog.Where(x => x.Value.Feature == feature))
        {
            var source = pair.Key.StartsWith("component-licenses/") ? feature == "archives" ? Path.Combine(Components, "sevenzip", "License.txt") : Path.Combine(Components, "licenses", feature == "background" ? "u2net-Apache-2.0.txt" : "tessdata-Apache-2.0.txt") : Path.Combine(Components, pair.Key);
            zip.CreateEntryFromFile(source, pair.Key);
        }
        return path;
    }
    private static void ComponentLifecycle()
    {
        var path = MakeComponentPack("background"); var root = Path.Combine(folder, "installed-components"); var store = new ComponentStore(root);
        store.InstallAsync(path).GetAwaiter().GetResult(); Check(store.Status().Single(x => x.Feature == "background").Installed, "Installed pack not available");
        var unrelated = Path.Combine(root, "my-local-note.txt"); File.WriteAllText(unrelated, "preserve");
        var model = Path.Combine(root, "u2netp.onnx"); File.AppendAllText(model, "modified"); var failed = false;
        try { store.InstallAsync(path).GetAwaiter().GetResult(); } catch (IOException) { failed = true; } Check(failed, "Modified component silently overwritten");
        store.Uninstall("background"); Check(File.Exists(model) && File.ReadAllText(unrelated) == "preserve" && !store.Status().Single(x => x.Feature == "background").Installed, "Uninstall deleted modified/user content");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); failed = false;
        try { new ComponentStore(Path.Combine(folder, "cancelled-pack")).InstallAsync(path, token: cancellation.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { failed = true; } Check(failed, "Component cancellation ignored");
        var invalid = Path.Combine(folder, "bad-component.zip"); using (var zip = ZipFile.Open(invalid, ZipArchiveMode.Create)) { using (var writer = new StreamWriter(zip.CreateEntry("component.json").Open())) writer.Write("{}"); zip.CreateEntry("../escape"); }
        failed = false; try { store.InstallAsync(invalid).GetAwaiter().GetResult(); } catch (Exception e) when (e is InvalidDataException or NotSupportedException) { failed = true; } Check(failed, "Invalid local component accepted");
        var nativeRoot = Path.Combine(folder, "verified-native"); var nativeStore = new ComponentStore(nativeRoot); nativeStore.InstallAsync(MakeComponentPack("archives")).GetAwaiter().GetResult();
        var nativeDirectory = Path.Combine(nativeRoot, "sevenzip", "x64"); ComponentStore.VerifyNativeDirectory("archives", nativeDirectory, default);
        File.AppendAllText(Path.Combine(nativeDirectory, "7za.exe"), "tampered"); Task.Delay(150).GetAwaiter().GetResult(); failed = false;
        try { ComponentStore.VerifyNativeDirectory("archives", nativeDirectory, default); } catch (InvalidDataException) { failed = true; } Check(failed, "Native verification cache accepted a modified executable");
    }
    private static void ProcessLifecycle()
    {
        var watch = Stopwatch.StartNew(); var timedOut = false;
        try { LocalProcess.RunCapturedAsync(Environment.ProcessPath!, ["--test-job-child"], default, timeout: TimeSpan.FromMilliseconds(350)).GetAwaiter().GetResult(); } catch (TimeoutException) { timedOut = true; }
        Check(timedOut && watch.Elapsed < TimeSpan.FromSeconds(10), "Worker deadline did not terminate promptly");
        var pidFile = Path.Combine(folder, "owned-child-pid.txt"); LocalProcess.RunCapturedAsync(Environment.ProcessPath!, ["--test-job-parent", pidFile], default, timeout: TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        var pid = int.Parse(File.ReadAllText(pidFile)); bool alive; try { using var process = Process.GetProcessById(pid); alive = !process.HasExited; } catch (ArgumentException) { alive = false; }
        Check(!alive, "Job left a descendant running after host completion");
        var ffprobe = Path.Combine(AppContext.BaseDirectory, "media-engine", "ffprobe.exe"); var result = LocalProcess.RunCapturedAsync(ffprobe, ["-version"], default).GetAwaiter().GetResult(); Check(result.Output.Contains("ffprobe version"), "Parent unusable after worker timeout");
    }
    private static void InstallerSafety()
    {
        var root = Path.Combine(folder, "installer-fixture"); Directory.CreateDirectory(root); var owned = Path.Combine(root, "app.dll"); File.WriteAllText(owned, "owned"); InstallationFiles.WriteManifest(root, ["app.dll"]);
        File.WriteAllText(Path.Combine(root, "user.txt"), "personal"); var stage = Path.Combine(folder, "installer-next"); Directory.CreateDirectory(stage); File.WriteAllText(Path.Combine(stage, "app.dll"), "next");
        InstallationFiles.PreserveUnowned(root, stage); Check(File.ReadAllText(Path.Combine(stage, "user.txt")) == "personal", "Upgrade lost user file");
        InstallationFiles.RemoveKnown(root); Check(!File.Exists(owned) && File.ReadAllText(Path.Combine(root, "user.txt")) == "personal", "Removal exceeded ownership");
        var modified = Path.Combine(folder, "installer-modified"); Directory.CreateDirectory(modified); var path = Path.Combine(modified, "app.dll"); File.WriteAllText(path, "initial"); InstallationFiles.WriteManifest(modified, ["app.dll"]); File.WriteAllText(path, "modified"); InstallationFiles.RemoveKnown(modified); Check(File.ReadAllText(path) == "modified", "Modified file deleted");
        foreach (var name in new[] { "../escape", "C:/escape", "CON.txt", "COM¹.txt", "CONIN$", "a:ads", "trailing. ", "x/../../y" }) { var rejected = false; try { InstallationFiles.Target(root, name); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Unsafe installer path accepted"); }
        using var memory = new MemoryStream(); using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true)) { using var text = new StreamWriter(zip.CreateEntry("../escape.txt").Open()); text.Write("bad"); } memory.Position = 0;
        var failed = false; try { InstallationFiles.Extract(memory, Path.Combine(folder, "bad-installer-stage")); } catch (InvalidDataException) { failed = true; } Check(failed && !File.Exists(Path.Combine(folder, "escape.txt")), "Installer extraction traversal");
    }
    private static void NewEditorRenders()
    {
        var png = CreatePng("ui-source.png"); var file = FileClassifier.Inspect(png); var service = ExtendedService(); var queue = new BatchProcessor(service);
        void Render(ToolDialog window, string name)
        {
            var content = (FrameworkElement)window.Content; content.Measure(new Size(554, 580)); content.Arrange(new Rect(0, 0, 554, 580)); content.UpdateLayout(); SaveRender(content, 554, 580, name); window.Close(); Pump(window.PendingWork);
        }
        Render(new ModelToolsWindow(file, "OCR", service.Models, queue, default), "native-ocr-editor.png");
        Render(new ModelToolsWindow(file, "BACKGROUND", service.Models, queue, default), "native-background-editor.png");
        Render(new ModelToolsWindow(file, "SUBTITLE", service.Models, queue, default), "native-subtitle-editor.png");
        var analysis = new PrivacyAnalysis(file, "sha-test-only", ["EXIF", "XMP"], ["Not anonymization"]);
        Render(new PrivacyToolsWindow([analysis], default), "native-privacy-editor.png"); Render(new ArchiveToolsWindow(3, true, false, default), "native-archive-editor.png");
        var info = new MediaDetails(true, true, "mp4", "h264", "aac", 30, 640, 360, 30, 2, 48000, 0);
        foreach (var tool in new[] { "TRIM", "FRAMES", "NORMALIZE", "SILENCE", "CHANNELS" }) Render(new MediaToolsWindow(file, info, tool, 1, service.AdvancedMedia, queue, default), "native-" + tool.ToLowerInvariant() + "-editor.png");
        File.WriteAllText(Path.Combine(folder, "native-preview-scope.txt"), "Offscreen WPF layout only; real native clicks, DPI, OLE and Arabic entry remain manual acceptance.");
    }
    private static void FolderDispatch()
    {
        var root = Path.Combine(folder, "مجلد فعلي"); Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "نص.txt"), "original");
        var payload = FileDragPayload.Inspect([root]); Check(payload.IsValid && payload.Files[0].Category == FileCategory.Folder && payload.PreviewFile.SizeUnknown, "Real folder payload classification");
        var service = ExtendedService(); Check(service.ContextOptions(payload, WheelMode.Convert).Count == 0 && service.ContextOptions(payload, WheelMode.Tools).Any(x => x.Id == "ARCHIVE"), "Folder context");
        var output = new BatchProcessor(service).RunAsync(new(payload.Files, "ARCHIVE", ArchiveFormat: "ZIP")).GetAwaiter().GetResult(); Check(output.Succeeded == 1 && File.Exists(output.Jobs[0].Output!.OutputPath), "Aggregate folder operation failed");
        using var zip = ZipFile.OpenRead(output.Jobs[0].Output!.OutputPath); Check(zip.Entries.Any(x => x.FullName.EndsWith("نص.txt")) && File.ReadAllText(Path.Combine(root, "نص.txt")) == "original", "Folder archive content");
    }
    private static void ModelDispatch()
    {
        var path = CreatePng("reviewed-source.png"); var file = FileClassifier.Inspect(path); var service = ExtendedService(); var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var text = new OcrResult("Reviewed العربية English", .85f, 1, "eng+ara");
        var output = service.ConvertAsync(new([file], "OCR", Model: new(hash, Ocr: text))).GetAwaiter().GetResult(); Check(File.ReadAllText(output.OutputPath) == text.Text, "Reviewed OCR edit not saved");
        File.AppendAllText(path, "changed"); var rejected = false; try { service.ConvertAsync(new([file], "OCR", Model: new(hash, Ocr: text))).GetAwaiter().GetResult(); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Stale model preview accepted");
    }
    private static void ArchiveSecurityExtras()
    {
        var link = Path.Combine(folder, "symlink.zip"); using (var zip = ZipFile.Open(link, ZipArchiveMode.Create)) { var entry = zip.CreateEntry("link"); entry.ExternalAttributes = unchecked((int)0xa1ff0000); using var w = new StreamWriter(entry.Open()); w.Write("../outside"); }
        var hardlink = Path.Combine(folder, "hardlink.tar"); using (var stream = File.Create(hardlink)) using (var tar = new System.Formats.Tar.TarWriter(stream)) tar.WriteEntry(new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.HardLink, "hardlink") { LinkName = "../outside" });
        var bomb = Path.Combine(folder, "expansion-bomb.zip"); using (var zip = ZipFile.Open(bomb, ZipArchiveMode.Create)) { using var stream = zip.CreateEntry("large.txt", CompressionLevel.SmallestSize).Open(); stream.Write(new byte[4 * 1024 * 1024]); }
        foreach (var path in new[] { link, hardlink, bomb }) { var failed = false; try { Archives().ExtractAsync(FileClassifier.Inspect(path)).GetAwaiter().GetResult(); } catch (Exception error) when (error is InvalidDataException or NotSupportedException) { failed = true; } Check(failed, "Unsafe actual archive accepted: " + Path.GetFileName(path)); }
        var large = Path.Combine(folder, "cancel-archive.bin"); using (var stream = File.Create(large)) stream.SetLength(128 * 1024 * 1024);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(30)); var cancelled = false;
        try { Archives().CreateAsync([large], "ZIP", cts.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && new FileInfo(large).Length == 128 * 1024 * 1024 && !Directory.GetDirectories(folder, ".naqla-*").Any(), "Mid-creation cancellation/source/temp cleanup");
        foreach (var name in new[] { "COM¹.txt", "LPT².txt", "CONOUT$" }) { var rejected = false; try { ArchiveService.SafeRelativePath(name); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Windows device alias accepted"); }
        var encryptedRoot = Path.Combine(folder, "encrypted-fixture"); Directory.CreateDirectory(encryptedRoot); File.WriteAllText(Path.Combine(encryptedRoot, "input.txt"), "Disposable encryption fixture");
        LocalProcess.RunAsync(Path.Combine(Components, "sevenzip/x64/7za.exe"), ["a", "-tzip", "-pNaqlaFixtureOnly", "-mem=AES256", "protected.zip", "input.txt"], default, workingDirectory: encryptedRoot).GetAwaiter().GetResult();
        var refused = false; try { Archives().ExtractAsync(FileClassifier.Inspect(Path.Combine(encryptedRoot, "protected.zip"))).GetAwaiter().GetResult(); } catch (NotSupportedException) { refused = true; } Check(refused, "Actual AES encrypted archive accepted");
    }
    private static void DocumentIntegritySmoke()
    {
        var path = Path.Combine(folder, "نص محمي.txt"); File.WriteAllText(path, "Offline document integrity guard\nمرحبا نقلة"); var before = SHA256.HashData(File.ReadAllBytes(path));
        var result = ExtendedService().ConvertAsync(FileClassifier.Inspect(path), "DOCX").GetAwaiter().GetResult();
        using var zip = ZipFile.OpenRead(result.OutputPath); using var xml = zip.GetEntry("word/document.xml")!.Open(); var text = System.Xml.Linq.XDocument.Load(xml).Root!.Value;
        Check(text.Contains("مرحبا") && text.Contains("integrity") && before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Job/integrity document regression");
    }
}
