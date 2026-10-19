using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Naqla.Conversion;
using Naqla.Core;

internal static partial class Program
{
    private static void RunPluginTests()
    {
        Run("Declarative plugins install disabled; explicit enable, context, change invalidation and uninstall", PluginLifecycle);
        Run("Separate trusted plugin host computes real SHA256/text statistics and preserves source", PluginProcessing);
        Run("Plugin API/capability/executable/path/duplicate packages are refused without execution", PluginPackages);
        Run("Plugin worker failure/cancellation keeps parent usable and cleans owned work", PluginFailures);
    }
    private static string PluginPackage(string name, string operation, string[] extensions, int api = 1, string[]? capabilities = null)
    {
        var path = Path.Combine(folder, name + ".naqla-plugin.zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create); using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open());
        writer.Write(JsonSerializer.Serialize(new PluginManifest(1, name, name, "1.0.0", api, capabilities ?? ["read-selection", "write-result"], [new("action", "اختبار", operation, extensions)]))); return path;
    }
    private static void PluginLifecycle()
    {
        var root = Path.Combine(folder, "plugins-lifecycle"); var service = new PluginService(root);
        var package = PluginPackage("local.hash", "sha256", ["TXT"]); var installed = service.InstallAsync(package).GetAwaiter().GetResult(); Check(!installed.Enabled && service.List().Count == 1, "Install must be disabled");
        var source = new DraggedFile("local.txt", "TXT", 1, FileCategory.Document); Check(service.ActionsFor(source).Count == 0, "Disabled plugin registered actions");
        service.SetEnabled("local.hash", true); Check(service.ActionsFor(source).Single().Id == "PLUGIN:local.hash:action", "Enabled contextual registration missing");
        Check(service.ActionsFor(source with { Extension = "PNG" }).Count == 0, "Plugin context mismatch");
        File.WriteAllText(Path.Combine(root, "local.hash", "README.md"), "Package changed after approval"); Check(service.List().Single().Changed && service.ActionsFor(source).Count == 0, "Changed package approval remained active");
        service.SetEnabled("local.hash", true); service.SetEnabled("local.hash", false); Check(service.ActionsFor(source).Count == 0, "Disable ignored");
        service.Uninstall("local.hash"); Check(service.List().Count == 0 && !Directory.Exists(Path.Combine(root, "local.hash")), "Uninstall failed");
    }
    private static void PluginProcessing()
    {
        var service = new PluginService(Path.Combine(folder, "plugins-processing")); var source = Path.Combine(folder, "نص الإضافة.txt"); File.WriteAllText(source, "مرحبا Naqla\nlocal plugins"); var hash = SHA256.HashData(File.ReadAllBytes(source));
        foreach (var operation in new[] { "sha256", "text-statistics" })
        {
            var id = "sample." + (operation == "sha256" ? "hash" : "stats"); var path = PluginPackage(id, operation, ["TXT"]); service.InstallAsync(path).GetAwaiter().GetResult(); service.SetEnabled(id, true);
            var result = service.ExecuteAsync(FileClassifier.Inspect(source), $"PLUGIN:{id}:action").GetAwaiter().GetResult();
            using var json = JsonDocument.Parse(File.ReadAllText(result.OutputPath));
            if (operation == "sha256") Check(json.RootElement.GetProperty("Value").GetString() == Convert.ToHexString(hash), "Real SHA256 mismatch");
            else Check(json.RootElement.GetProperty("Words").GetInt32() == 4 && json.RootElement.GetProperty("Lines").GetInt32() == 2, "Real text statistics mismatch");
        }
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "Plugin modified source");
    }
    private static void PluginPackages()
    {
        var service = new PluginService(Path.Combine(folder, "plugins-unsafe")); var paths = new List<string>();
        paths.Add(PluginPackage("bad.api", "sha256", ["TXT"], 2)); paths.Add(PluginPackage("bad.command", "execute-shell", ["TXT"])); paths.Add(PluginPackage("bad.network", "sha256", ["TXT"], capabilities: ["read-selection", "network"]));
        var malicious = Path.Combine(folder, "traversal-plugin.zip"); ZipFixture(malicious, "../escape.json"); paths.Add(malicious);
        var code = Path.Combine(folder, "executable-plugin.zip"); ZipFixture(code, "plugin.exe"); paths.Add(code);
        foreach (var path in paths)
        {
            var failed = false; try { service.InstallAsync(path).GetAwaiter().GetResult(); } catch (Exception e) when (e is InvalidDataException or NotSupportedException or ArgumentException) { failed = true; }
            Check(failed, "Unsafe plugin installed");
        }
        Check(service.List().Count == 0, "Unsafe plugin actions registered");
    }
    private static void PluginFailures()
    {
        var service = new PluginService(Path.Combine(folder, "plugins-failures")); var package = PluginPackage("safe.stats", "text-statistics", ["TXT"]); service.InstallAsync(package).GetAwaiter().GetResult(); service.SetEnabled("safe.stats", true);
        var invalid = Path.Combine(folder, "invalid-utf8.txt"); File.WriteAllBytes(invalid, [0xff, 0xff, 0, 0]); var failed = false;
        try { service.ExecuteAsync(FileClassifier.Inspect(invalid), "PLUGIN:safe.stats:action").GetAwaiter().GetResult(); } catch (InvalidDataException) { failed = true; } Check(failed, "Host invalid UTF8 should fail");
        File.WriteAllText(invalid, "host can restart"); var success = service.ExecuteAsync(FileClassifier.Inspect(invalid), "PLUGIN:safe.stats:action").GetAwaiter().GetResult(); Check(File.Exists(success.OutputPath), "Host did not recover on next job");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var cancelled = false;
        try { service.ExecuteAsync(FileClassifier.Inspect(invalid), "PLUGIN:safe.stats:action", cancellation.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; } Check(cancelled, "Plugin cancellation ignored");
        Check(!Directory.GetDirectories(folder, ".naqla-*").Any(), "Plugin worker left temporary data");
    }
}
