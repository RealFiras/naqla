using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using SharpCompress.Readers;

namespace Naqla.Setup;

// Explicit installer-only downloads. Never referenced by the tray application.
internal static class EngineDownloads
{
    internal const string MediaHash = "60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba";
    private const string MediaUrl = "https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip";
    internal sealed record Package(string name, string url, string sha256);
    internal sealed record NativeFile(string name, string sha256);
    internal sealed record HeifLock(Package[] packages, NativeFile[] files);
    internal static HeifLock Lock()
    {
        using var read = Assembly.GetExecutingAssembly().GetManifestResourceStream("Naqla.HeifLock.json") ?? throw new InvalidDataException("Missing engine catalog.");
        return JsonSerializer.Deserialize<HeifLock>(read) ?? throw new InvalidDataException("Invalid engine catalog.");
    }
    internal static string[] MissingRuntime()
    {
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return new[] { "msvcp140.dll", "msvcp140_1.dll", "msvcp140_2.dll", "vcruntime140.dll", "vcruntime140_1.dll" }.Where(x => !File.Exists(Path.Combine(system, x))).ToArray();
    }
    internal static async Task<string> Fetch(string url, string sha256, string cache, bool allowNetwork, CancellationToken token)
    {
        var uri = new Uri(url);
        if (uri.Scheme != "https" || uri.Host is not ("www.gyan.dev" or "mirror.msys2.org")) throw new InvalidDataException("Untrusted download origin.");
        InstallationFiles.GuardDirectory(cache); Directory.CreateDirectory(cache);
        var name = Path.GetFileName(uri.AbsolutePath); var target = InstallationFiles.Target(cache, name);
        foreach (var candidate in new[] { target, Path.Combine(cache, "heif", name), Path.Combine(cache, "ffmpeg-9.0.2-essentials.zip") })
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(candidate)) continue;
            InstallationFiles.GuardDirectory(Path.GetDirectoryName(candidate)!);
            if ((File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0) throw new IOException("Download cache files must not be links.");
            if (InstallationFiles.Hash(candidate).Equals(sha256, StringComparison.OrdinalIgnoreCase)) return candidate;
        }
        if (!allowNetwork) throw new InvalidDataException("The requested verified engine is not available in the local cache.");
        var partial = InstallationFiles.Target(cache, name + "." + Guid.NewGuid().ToString("N") + ".partial");
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromMinutes(8));
            using var client = new HttpClient(); client.DefaultRequestHeaders.UserAgent.ParseAdd("Naqla-Installer/0.8");
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 536_870_912) throw new InvalidDataException("Engine download exceeds the size limit.");
            await using (var input = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false))
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[131072]; long bytes = 0; int count;
                while ((count = await input.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) != 0)
                { bytes += count; if (bytes > 536_870_912) throw new InvalidDataException("Engine download exceeds the size limit."); await output.WriteAsync(buffer.AsMemory(0, count), deadline.Token).ConfigureAwait(false); }
            }
            if (!InstallationFiles.Hash(partial).Equals(sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Downloaded engine checksum mismatch.");
            // Replace only the cache file, never an application or user document.
            File.Move(partial, target, overwrite: true); return target;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
    internal static async Task Prepare(string stage, string cache, bool allowNetwork, IProgress<string>? progress, CancellationToken token = default)
    {
        InstallationFiles.GuardDirectory(stage);
        var media = Path.Combine(stage, "media-engine"); Directory.CreateDirectory(media);
        if (!File.Exists(Path.Combine(media, "ffmpeg.exe")) || !File.Exists(Path.Combine(media, "ffprobe.exe")))
        {
            progress?.Report("Downloading verified FFmpeg (one-time setup)…");
            var archive = await Fetch(MediaUrl, MediaHash, cache, allowNetwork, token).ConfigureAwait(false);
            using var zip = ZipFile.OpenRead(archive);
            foreach (var name in new[] { "ffmpeg.exe", "ffprobe.exe", "LICENSE" })
            {
                token.ThrowIfCancellationRequested(); var entries = zip.Entries.Where(x => x.Name == name).ToArray();
                if (entries.Length != 1 || entries[0].Length > 268_435_456) throw new InvalidDataException("Unexpected media engine archive.");
                using var input = entries[0].Open(); using var output = new FileStream(InstallationFiles.Target(media, name), FileMode.CreateNew); input.CopyTo(output);
            }
            File.WriteAllText(Path.Combine(media, "NOTICE.txt"), "FFmpeg 9.0.2, unmodified essentials build downloaded explicitly from Gyan during setup.\n" + MediaUrl + "\nSHA256: " + MediaHash + "\nGPLv3; see LICENSE. Source/build information: https://www.gyan.dev/ffmpeg/builds/ and https://github.com/GyanD/codexffmpeg\nNo runtime download or file upload.\n");
        }
        var manifest = Lock(); var heif = Path.Combine(stage, "heif-engine"); Directory.CreateDirectory(heif);
        var wanted = manifest.files.ToDictionary(x => "ucrt64/bin/" + x.name, StringComparer.Ordinal);
        if (!manifest.files.All(x => File.Exists(Path.Combine(heif, x.name)) && InstallationFiles.Hash(Path.Combine(heif, x.name)).Equals(x.sha256, StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var package in manifest.packages)
            {
                token.ThrowIfCancellationRequested(); progress?.Report("Preparing HEIF: " + package.name.Replace("mingw-w64-ucrt-x86_64-", "") + "…");
                var archive = await Fetch(package.url, package.sha256, cache, allowNetwork, token).ConfigureAwait(false);
                using var input = File.OpenRead(archive); using var reader = ReaderFactory.OpenReader(input);
                var entries = 0;
                while (reader.MoveToNextEntry())
                {
                    token.ThrowIfCancellationRequested(); if (++entries > 50000) throw new InvalidDataException("Too many package entries.");
                    var entry = reader.Entry; if (entry.IsDirectory) continue;
                    var key = (entry.Key ?? "").Replace('\\', '/'); string? relative = null;
                    if (wanted.TryGetValue(key, out var native)) relative = native.name;
                    else if (key.StartsWith("ucrt64/share/licenses/", StringComparison.Ordinal)) relative = "licenses/" + key[22..];
                    if (relative is null) continue;
                    if (entry.LinkTarget is not null || entry.Size is < 0 or > 134_217_728) throw new InvalidDataException("Unexpected engine package entry.");
                    var destination = InstallationFiles.Target(heif, relative); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    using var output = new FileStream(destination, FileMode.CreateNew); using var data = reader.OpenEntryStream();
                    CopyBounded(data, output, token);
                }
            }
            foreach (var file in manifest.files)
                if (!File.Exists(Path.Combine(heif, file.name)) || !InstallationFiles.Hash(Path.Combine(heif, file.name)).Equals(file.sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("HEIF native engine checksum mismatch.");
            var notice = InstallationFiles.Target(heif, "licenses/x265/COPYING"); Directory.CreateDirectory(Path.GetDirectoryName(notice)!);
            using var license = Assembly.GetExecutingAssembly().GetManifestResourceStream("Naqla.X265License") ?? throw new InvalidDataException("Missing x265 notice.");
            using (var output = File.Create(notice)) license.CopyTo(output);
            using (var catalog = Assembly.GetExecutingAssembly().GetManifestResourceStream("Naqla.HeifLock.json")!)
            using (var output = File.Create(Path.Combine(heif, "packages.lock.json"))) catalog.CopyTo(output);
            File.WriteAllText(Path.Combine(heif, "NOTICE.txt"), "Unmodified MSYS2 UCRT64 HEIF tools fetched explicitly from official packages during setup.\nIncludes LGPL/GPL components; see licenses and package source URLs in the source repository's scripts/heif-engine.lock.json.\nhttps://github.com/msys2/MINGW-packages\nNo runtime downloads or uploads.\n");
        }
        progress?.Report("Verified conversion engines ready.");
    }
    private static void CopyBounded(Stream input, Stream output, CancellationToken token)
    {
        var buffer = new byte[131072]; long bytes = 0; int count;
        while ((count = input.Read(buffer)) != 0) { token.ThrowIfCancellationRequested(); bytes += count; if (bytes > 134_217_728) throw new InvalidDataException("Expanded engine file is too large."); output.Write(buffer, 0, count); }
    }
}
