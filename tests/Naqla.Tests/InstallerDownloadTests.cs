using System.Diagnostics;
using System.IO;
using Naqla.Setup;

internal static partial class Program
{
    private static void InstallerNetworkFetch()
    {
        var package = EngineDownloads.Lock().packages.Last(x => x.name.EndsWith("-zlib", StringComparison.Ordinal));
        var cache = Path.Combine(folder, "fresh-network-cache");
        var archive = EngineDownloads.Fetch(package.url, package.sha256, cache, true, default).GetAwaiter().GetResult();
        Check(InstallationFiles.Hash(archive).Equals(package.sha256, StringComparison.OrdinalIgnoreCase), "Actual network download checksum differs");
        var cached = EngineDownloads.Fetch(package.url, package.sha256, cache, false, default).GetAwaiter().GetResult();
        Check(cached == archive && !Directory.GetFiles(cache, "*.partial").Any(), "Verified cache reuse or partial cleanup failed");
    }
    private static void RunInstallerDownloadTests()
    {
        Run("Installer cache refuses corrupt bytes, untrusted origins and cancellation without network", () =>
        {
            var cache = Path.Combine(folder, "bad-cache"); Directory.CreateDirectory(cache);
            File.WriteAllText(Path.Combine(cache, "ffmpeg-9.0.2-essentials_build.zip"), "Disposable invalid download");
            foreach (var uri in new[] { "https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip", "https://example.invalid/engine.zip" })
            {
                var refused = false;
                try { EngineDownloads.Fetch(uri, EngineDownloads.MediaHash, cache, false, default).GetAwaiter().GetResult(); }
                catch (InvalidDataException) { refused = true; }
                Check(refused, "Unverified download accepted");
            }
            var cancelled = false;
            try { EngineDownloads.Fetch("https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip", EngineDownloads.MediaHash, cache, false, new CancellationToken(true)).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && !Directory.GetFiles(cache, "*.partial").Any(), "Cancelled download left a partial file");
        });
        Run("Real installer extracts pinned FFmpeg and Zstandard HEIF packages and executes native engines", () =>
        {
            var repository = Path.GetFullPath(Path.Combine(Components, "../.."));
            var stage = Path.Combine(folder, "محركات التثبيت");
            EngineDownloads.Prepare(stage, Path.Combine(repository, "artifacts/downloads"), false, null).GetAwaiter().GetResult();
            foreach (var file in EngineDownloads.Lock().files)
                Check(InstallationFiles.Hash(Path.Combine(stage, "heif-engine", file.name)).Equals(file.sha256, StringComparison.OrdinalIgnoreCase), "Extracted native checksum differs");
            foreach (var item in new[] { ("media-engine", "ffmpeg.exe", "-version"), ("media-engine", "ffprobe.exe", "-version"), ("heif-engine", "heif-enc.exe", "--list-encoders") })
            {
                using var process = Process.Start(new ProcessStartInfo(Path.Combine(stage, item.Item1, item.Item2), item.Item3) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                Check(process.WaitForExit(30000) && process.ExitCode == 0, "Installed native engine failed"); stdout.GetAwaiter().GetResult(); stderr.GetAwaiter().GetResult();
            }
            Check(File.Exists(Path.Combine(stage, "heif-engine/licenses/x265/COPYING")) && File.Exists(Path.Combine(stage, "media-engine/LICENSE")), "Missing engine licenses");
            File.WriteAllText(Path.Combine(folder, "installer-engines-verified.txt"), "Actual pinned archives, hashes, licenses and native processes passed. Not an Explorer gesture test.");
        });
    }
}
