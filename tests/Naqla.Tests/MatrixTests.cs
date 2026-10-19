using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using ImageMagick;
using Naqla.Core;

internal static partial class Program
{
    private static readonly List<object> PairResults = [];
    private static void FreshConversionMatrix()
    {
        var matrix = Path.Combine(folder, "conversion-matrix"); Directory.CreateDirectory(matrix);
        var baseImage = CreatePng("matrix-source.png");
        void Pair(string source, string target)
        {
            var input = FileClassifier.Inspect(source); var clock = Stopwatch.StartNew(); var before = SHA256.HashData(File.ReadAllBytes(source));
            try
            {
                var result = Service.ConvertAsync(input, target).GetAwaiter().GetResult(); var validation = "";
                if (input.Category == FileCategory.Image && target != "PDF")
                {
                    using var image = new MagickImage(result.OutputPath); Check(image.Width == 32 && image.Height == 32, "Matrix image size");
                    var expected = target switch { "PNG" => MagickFormat.Png, "JPG" => MagickFormat.Jpeg, "WEBP" => MagickFormat.WebP, "AVIF" => MagickFormat.Avif, "TIFF" => MagickFormat.Tiff, "HEIC" => MagickFormat.Heic, _ => throw new Exception("Unknown matrix format") };
                    Check(image.Format == expected, "Actual codec mismatch"); validation = "Independent image decode, codec, dimensions";
                }
                else if (input.Category == FileCategory.Image) { using var pdf = UglyToad.PdfPig.PdfDocument.Open(result.OutputPath); Check(pdf.NumberOfPages == 1, "Matrix image PDF count"); validation = "PdfPig page count and production PDFium full-page validation"; }
                else if (input.Category is FileCategory.Audio or FileCategory.Video)
                {
                    var args = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "media-engine", "ffprobe.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    foreach (var arg in new[] { "-v", "error", "-show_entries", "format=format_name,duration:stream=codec_type,codec_name,width,height,channels,sample_rate", "-of", "json", result.OutputPath }) args.ArgumentList.Add(arg);
                    using var process = Process.Start(args)!; var text = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd(); process.WaitForExit(); Check(process.ExitCode == 0 && error.Length == 0, "Independent ffprobe rejected output");
                    using var json = JsonDocument.Parse(text); var streams = json.RootElement.GetProperty("streams").EnumerateArray().ToArray();
                    var audio = target is "MP3" or "M4A" or "WAV" or "FLAC" or "AIFF" or "OPUS";
                    var codec = target switch { "MP3" => "mp3", "M4A" => "aac", "WAV" => "pcm_s16le", "FLAC" => "flac", "AIFF" => "pcm_s16be", "OPUS" => "opus", "MP4" or "MOV" => "h264", "WEBM" => "vp9", "GIF" => "gif", _ => "" };
                    Check(streams.Any(s => s.GetProperty("codec_type").GetString() == (audio ? "audio" : "video") && s.GetProperty("codec_name").GetString() == codec), "Actual encoded stream codec mismatch");
                    if (audio) Check(!streams.Any(s => s.GetProperty("codec_type").GetString() == "video"), "Audio output retained video");
                    var duration = double.Parse(json.RootElement.GetProperty("format").GetProperty("duration").GetString()!, System.Globalization.CultureInfo.InvariantCulture); Check(duration is > 1.7 and < 2.5, "Media duration mismatch");
                    Engine("-v", "error", "-nostdin", "-xerror", "-i", result.OutputPath, "-map", "0:v?", "-map", "0:a?", "-f", "null", "-"); validation = "Independent ffprobe codec/track/duration + every output frame/sample decoded";
                }
                else
                {
                    if (target is "PNG" or "JPG")
                    {
                        var files = Directory.Exists(result.OutputPath) ? Directory.GetFiles(result.OutputPath) : new[] { result.OutputPath }; Check(files.Length == 3, "PDF output page count"); foreach (var page in files) { using var image = new MagickImage(page); Check(image.Width > 0 && image.Height > 0 && image.Format == (target == "PNG" ? MagickFormat.Png : MagickFormat.Jpeg), "PDF raster decode"); }
                    }
                    else if (target == "DOCX") { using var zip = ZipFile.OpenRead(result.OutputPath); using var read = zip.GetEntry("word/document.xml")!.Open(); var doc = XDocument.Load(read); Check(doc.ToString().Contains("PAGE-3"), "PDF DOCX text missing"); }
                    else { var text = File.ReadAllText(result.OutputPath); Check(text.Contains("PAGE-3") && (target != "RTF" || text.StartsWith(@"{\rtf")), "PDF extracted text missing"); }
                    validation = "All page rasters decoded or native text independently read from output package/text";
                }
                Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "Original hash changed");
                PairResults.Add(new { Category = input.Category.ToString(), Source = input.Extension, Destination = target, Handler = "LocalConversionService.ConvertAsync", Status = "PASSED", Validation = validation, OriginalHashPreserved = true, Milliseconds = clock.ElapsedMilliseconds, PhysicalWindows = "MANUAL PENDING" });
            }
            catch (Exception error) { PairResults.Add(new { Category = input.Category.ToString(), Source = input.Extension, Destination = target, Handler = "LocalConversionService.ConvertAsync", Status = "FAILED", Error = error.GetType().Name, ErrorCode = error.HResult, Milliseconds = clock.ElapsedMilliseconds, PhysicalWindows = "MANUAL PENDING" }); }
            File.WriteAllText(Path.Combine(folder, "conversion-matrix.json"), JsonSerializer.Serialize(PairResults, new JsonSerializerOptions { WriteIndented = true }));
        }
        var imageSources = new Dictionary<string, string>();
        foreach (var extension in new[] { "PNG", "JPG", "JPEG", "WEBP", "BMP", "TIFF", "AVIF", "HEIC" })
        {
            var path = Path.Combine(matrix, "صورة." + extension.ToLowerInvariant());
            if (extension == "PNG") File.Copy(baseImage, path);
            else if (extension == "BMP") { using var image = new MagickImage(baseImage); image.BackgroundColor = MagickColors.White; image.Alpha(AlphaAction.Remove); image.Write(path, MagickFormat.Bmp); }
            else { var encoded = ConvertFile(baseImage, extension == "JPEG" ? "JPG" : extension); File.Copy(encoded.OutputPath, path); }
            imageSources[extension] = path;
        }
        foreach (var (extension, source) in imageSources) foreach (var target in new[] { "PNG", "JPG", "WEBP", "PDF", "AVIF", "TIFF", "HEIC" }.Where(x => x != (extension == "JPEG" ? "JPG" : extension))) Pair(source, target);
        foreach (var extension in new[] { "MP4", "MOV", "MKV", "WEBM", "AVI" })
        {
            var path = Path.Combine(matrix, "فيديو-" + extension + "." + extension.ToLowerInvariant());
            if (extension == "MP4") File.Copy(MediaSource(), path);
            else if (extension is "MOV" or "WEBM") File.Copy(ConvertFile(MediaSource(), extension).OutputPath, path);
            else Engine("-v", "error", "-nostdin", "-n", "-i", MediaSource(), "-c:v", "mpeg4", "-c:a", "libmp3lame", path);
            foreach (var target in new[] { "MP4", "MOV", "M4A", "MP3", "GIF", "WEBM" }.Where(x => x != extension)) Pair(path, target);
        }
        var baseAudio = Path.Combine(matrix, "audio-source.wav"); Engine("-v", "error", "-nostdin", "-n", "-i", MediaSource(), "-vn", "-c:a", "pcm_s16le", baseAudio);
        foreach (var extension in new[] { "MP3", "M4A", "WAV", "FLAC", "AIFF", "OPUS" })
        {
            var path = Path.Combine(matrix, "صوت-" + extension + "." + extension.ToLowerInvariant()); File.Copy(extension == "WAV" ? baseAudio : ConvertFile(baseAudio, extension).OutputPath, path);
            foreach (var target in new[] { "MP3", "M4A", "WAV", "FLAC", "AIFF", "OPUS" }.Where(x => x != extension)) Pair(path, target);
        }
        var pdf = PdfFixture("matrix-native-text.pdf", 3, true, true); foreach (var target in new[] { "PNG", "JPG", "TXT", "DOCX", "RTF" }) Pair(pdf, target);
        Check(!PairResults.Any(x => JsonSerializer.Serialize(x).Contains("\"Status\":\"FAILED\"")), "Fresh conversion matrix has failures; inspect conversion-matrix.json");
    }
}
