using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImageMagick;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Naqla.Core;
using Tesseract;

namespace Naqla.Conversion;

/// <summary>Optional, local, hash-pinned data packs. There is no HTTP client or automatic download path.</summary>
public sealed class LocalModelService(string componentDirectory, string mediaDirectory)
{
    private static readonly IReadOnlyDictionary<string, string> Hashes = new Dictionary<string, string>
    {
        ["u2netp.onnx"] = "309c8469258dda742793dce0ebea8e6dd393174f89934733ecc8b14c76f4ddd8",
        ["tessdata/eng.traineddata"] = "7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2",
        ["tessdata/ara.traineddata"] = "e3206d3dc87fd50c24a0fb9f01838615911d25168f4e64415244b67d2bb3e729",
        ["ggml-tiny.bin"] = "be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21"
        , ["ggml-base.bin"] = "60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe"
    };
    public bool HasOcr => File.Exists(Path.Combine(componentDirectory, "tessdata", "eng.traineddata")) && File.Exists(Path.Combine(componentDirectory, "tessdata", "ara.traineddata"));
    public bool HasBackgroundModel => File.Exists(Path.Combine(componentDirectory, "u2netp.onnx"));
    public bool HasSpeech => (File.Exists(Path.Combine(componentDirectory, "ggml-base.bin")) || File.Exists(Path.Combine(componentDirectory, "ggml-tiny.bin"))) && File.Exists(Path.Combine(componentDirectory, "whisper", "Release", "whisper-cli.exe"));
    private string Verified(string relative)
    {
        var path = Path.Combine(componentDirectory, relative);
        if (!File.Exists(path)) throw new NotSupportedException("Install the matching local optional model pack first; Naqla never downloads models automatically.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(Hashes[relative], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The local model pack checksum is invalid.");
        return path;
    }
    public Task<OcrResult> RecognizeAsync(DraggedFile file, string language = "eng+ara", CancellationToken token = default) => Task.Run(async () =>
    {
        if (language is not ("eng" or "ara" or "eng+ara")) throw new ArgumentException("Choose English, Arabic, or English + Arabic.");
        foreach (var name in language.Split('+')) Verified("tessdata/" + name + ".traineddata");
        token.ThrowIfCancellationRequested();
        using var sourceLock = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var pages = new List<string>(); var confidence = 0f;
        using var work = new OutputDirectory(Path.Combine(componentDirectory, "ocr-work.png"), "work");
        Directory.CreateDirectory(Path.Combine(work.Temporary, "tessdata"));
        foreach (var name in language.Split('+')) File.Copy(Verified("tessdata/" + name + ".traineddata"), Path.Combine(work.Temporary, "tessdata", name + ".traineddata"));
        async Task RecognizePage(byte[] png)
        {
            await File.WriteAllBytesAsync(Path.Combine(work.Temporary, "input.png"), png, token).ConfigureAwait(false);
            var resultPath = Path.Combine(work.Temporary, "result.json"); if (File.Exists(resultPath)) File.Delete(resultPath);
            await LocalProcess.RunCapturedAsync(Path.Combine(AppContext.BaseDirectory, "Naqla.App.exe"), ["--internal-ocr", language], token, 0, 0, workingDirectory: work.Temporary, timeout: TimeSpan.FromMinutes(5)).ConfigureAwait(false);
            if (!File.Exists(resultPath) || new FileInfo(resultPath).Length > 4_194_304) throw new InvalidDataException("OCR produced a missing or oversized response.");
            var result = JsonSerializer.Deserialize<OcrResult>(await File.ReadAllTextAsync(resultPath, token).ConfigureAwait(false)) ?? throw new InvalidDataException("The OCR worker returned an invalid result.");
            if (!float.IsFinite(result.Confidence) || result.Confidence is < 0 or > 1) throw new InvalidDataException("Invalid OCR confidence.");
            pages.Add(result.Text); confidence += result.Confidence;
        }
        if (file.Extension == "PDF")
        {
            using var inspected = UglyToad.PdfPig.PdfDocument.Open(sourceLock);
            if (inspected.IsEncrypted || inspected.NumberOfPages > 100) throw new NotSupportedException("OCR requires an unencrypted PDF of at most 100 pages.");
            for (var i = 0; i < inspected.NumberOfPages; i++)
            {
                token.ThrowIfCancellationRequested(); sourceLock.Position = 0;
                byte[] png;
                lock (PdfConversions.PdfiumGate)
                {
                    var sizes = PDFtoImage.Conversion.GetPageSizes(sourceLock, leaveOpen: true).ToArray();
                    if (sizes[i].Width * sizes[i].Height * Math.Pow(200 / 72d, 2) > RasterImages.MaximumPixels) throw new NotSupportedException("The scanned page exceeds 40 MP at OCR resolution.");
                    sourceLock.Position = 0; using var bitmap = PDFtoImage.Conversion.ToImage(sourceLock, page: i, leaveOpen: true, options: new(Dpi: 200));
                    using var encoded = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100); png = encoded.ToArray();
                }
                await RecognizePage(png).ConfigureAwait(false);
            }
        }
        else
        {
            using var frames = RasterImages.Open(file); var image = frames[0];
            await RecognizePage(image.ToByteArray(MagickFormat.Png)).ConfigureAwait(false);
        }
        token.ThrowIfCancellationRequested();
        return new OcrResult(string.Join("\n\n", pages), confidence / pages.Count, pages.Count, language);
    }, token);

    public Task<ImagePreview> RemoveBackgroundPreviewAsync(DraggedFile file, CancellationToken token = default) => Task.Run(() =>
    {
        var model = Verified("u2netp.onnx"); token.ThrowIfCancellationRequested();
        using var sourceLock = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var frames = RasterImages.Open(file); var image = frames[0];
        if (image.GetColorProfile() is not null) image.TransformColorSpace(ColorProfiles.SRGB); else image.ColorSpace = ColorSpace.sRGB;
        using var scaled = image.Clone(); scaled.Alpha(AlphaAction.Remove);
        // U2NetP expects the source stretched to 320x320, RGB / max, then ImageNet channel normalization.
        scaled.Resize(new MagickGeometry(320, 320) { IgnoreAspectRatio = true });
        var rgb = scaled.ToByteArray(MagickFormat.Rgb); var divisor = Math.Max(1, (int)rgb.Max()) / 255f;
        var input = new DenseTensor<float>(new[] { 1, 3, 320, 320 }); float[] means = [.485f, .456f, .406f], std = [.229f, .224f, .225f];
        for (var p = 0; p < 320 * 320; p++) for (var c = 0; c < 3; c++) input[0, c, p / 320, p % 320] = (rgb[p * 3 + c] / 255f / divisor - means[c]) / std[c];
        using var options = new SessionOptions { IntraOpNumThreads = 2, InterOpNumThreads = 1, GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
        using var session = new InferenceSession(model, options);
        using var run = new RunOptions(); using var registration = token.Register(() => run.Terminate = true);
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> completed;
        try { completed = session.Run([NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(), input)], session.OutputMetadata.Keys.ToArray(), run); }
        catch (OnnxRuntimeException) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        using var result = completed;
        token.ThrowIfCancellationRequested(); var values = result.First().AsTensor<float>().ToArray();
        if (values.Length != 320 * 320 || values.Any(x => !float.IsFinite(x))) throw new InvalidDataException("The segmentation model returned an invalid mask.");
        var min = values.Min(); var range = values.Max() - min;
        if (range < .000001) throw new InvalidDataException("No usable foreground mask was detected.");
        var bytes = values.Select(x => (byte)Math.Clamp(Math.Round((x - min) / range * 255), 0, 255)).ToArray();
        using var maskStream = new MemoryStream(bytes, false);
        using var mask = new MagickImage(maskStream, new MagickReadSettings { Format = MagickFormat.Gray, Width = 320, Height = 320, Depth = 8 });
        mask.Resize(new MagickGeometry(image.Width, image.Height) { IgnoreAspectRatio = true });
        // Preserve existing source transparency by multiplying its alpha by the predicted mask.
        image.Alpha(AlphaAction.Set); using var alpha = image.Clone(); alpha.Alpha(AlphaAction.Extract); alpha.Composite(mask, CompositeOperator.Multiply);
        image.Composite(alpha, CompositeOperator.CopyAlpha); image.Strip(); image.Density = new Density(96);
        token.ThrowIfCancellationRequested(); return new ImagePreview(image.ToByteArray(MagickFormat.Png), (int)image.Width, (int)image.Height);
    }, token);

    public async Task<ConversionResult> SaveBackgroundAsync(DraggedFile file, CancellationToken token = default)
    {
        var preview = await RemoveBackgroundPreviewAsync(file, token).ConfigureAwait(false);
        return await SaveBackgroundPreviewAsync(file, preview, token).ConfigureAwait(false);
    }
    public async Task<ConversionResult> SaveBackgroundPreviewAsync(DraggedFile file, ImagePreview preview, CancellationToken token = default)
    {
        if (preview.Png.Length > 268_435_456 || preview.Width < 1 || preview.Height < 1 || (long)preview.Width * preview.Height > RasterImages.MaximumPixels) throw new ArgumentException("Invalid background-removal preview.");
        using var output = new OutputFile(file.FullPath, "PNG", " transparent");
        await File.WriteAllBytesAsync(output.Temporary, preview.Png, token).ConfigureAwait(false);
        RasterImages.Verify(output.Temporary, "PNG", (uint)preview.Width, (uint)preview.Height);
        using var decoded = new MagickImage(output.Temporary); if (!decoded.HasAlpha) throw new InvalidDataException("The result has no alpha channel.");
        return new(output.Publish(token), preview.Width, preview.Height);
    }
    public async Task<ConversionResult> SaveOcrAsync(DraggedFile file, OcrResult result, CancellationToken token = default)
    {
        if (!result.HasText) throw new InvalidDataException("No readable text was detected. No empty success file was created.");
        using var output = new OutputFile(file.FullPath, "TXT", " OCR");
        await File.WriteAllTextAsync(output.Temporary, result.Text, new UTF8Encoding(false, true), token).ConfigureAwait(false);
        if (await File.ReadAllTextAsync(output.Temporary, new UTF8Encoding(false, true), token).ConfigureAwait(false) != result.Text) throw new InvalidDataException("OCR text verification failed.");
        return new(output.Publish(token), 0, 0, result.Pages);
    }

    public async Task<SubtitleDocument> TranscribeAsync(DraggedFile file, string language = "auto", CancellationToken token = default)
    {
        if (language is not ("auto" or "ar" or "en")) throw new ArgumentException("Choose Arabic, English, or automatic language detection.");
        var model = Verified(File.Exists(Path.Combine(componentDirectory, "ggml-base.bin")) ? "ggml-base.bin" : "ggml-tiny.bin");
        var executable = Path.Combine(componentDirectory, "whisper", "Release", "whisper-cli.exe");
        if (!File.Exists(executable)) throw new NotSupportedException("The local whisper.cpp CPU component is missing.");
        await Task.Run(() => ComponentStore.VerifyNativeDirectory("speech", Path.GetDirectoryName(executable)!, token), token).ConfigureAwait(false);
        using var sourceLock = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var media = new AdvancedMediaService(mediaDirectory); var info = await media.InspectAsync(file, token).ConfigureAwait(false);
        if (!info.HasAudio || info.Duration <= 0 || info.Duration > 7200) throw new NotSupportedException("Subtitles require an audio track of at most two hours.");
        using var work = new OutputDirectory(Path.Combine(componentDirectory, "speech-work.wav"), "work"); var wav = Path.Combine(work.Temporary, "input.wav"); var prefix = Path.Combine(work.Temporary, "transcript");
        await LocalProcess.RunAsync(Path.Combine(mediaDirectory, "ffmpeg.exe"), ["-v", "error", "-nostdin", "-n", "-xerror", "-protocol_whitelist", "file,pipe", "-i", file.FullPath, "-map", "0:a:0", "-vn", "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", wav], token).ConfigureAwait(false);
        // A silent track must not turn into model hallucinations.
        using (var pcm = File.OpenRead(wav))
        { var data = new byte[8192]; long nonzero = 0; int n; while ((n = await pcm.ReadAsync(data, token).ConfigureAwait(false)) > 0) for (var i = 0; i + 1 < n; i += 2) if (Math.Abs((int)BitConverter.ToInt16(data, i)) > 64) nonzero++; if (nonzero < 1600) throw new InvalidDataException("No audible speech was detected in this track."); }
        await LocalProcess.RunCapturedAsync(executable, ["-m", Path.GetFileName(model), "-f", Path.GetRelativePath(componentDirectory, wav), "-l", language, "-t", "2", "-p", "1", "-ng", "-oj", "-of", Path.GetRelativePath(componentDirectory, prefix), "-np"], token, 0, 0, workingDirectory: componentDirectory, timeout: TimeSpan.FromHours(2)).ConfigureAwait(false);
        var jsonPath = prefix + ".json"; if (!File.Exists(jsonPath) || new FileInfo(jsonPath).Length > 16_777_216) throw new InvalidDataException("Missing or oversized subtitle result.");
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(jsonPath, token).ConfigureAwait(false)); var cues = new List<SubtitleCue>(); var clipped = false;
        foreach (var item in json.RootElement.GetProperty("transcription").EnumerateArray())
        {
            var offsets = item.GetProperty("offsets"); var start = offsets.GetProperty("from").GetDouble() / 1000; var end = offsets.GetProperty("to").GetDouble() / 1000;
            var text = item.GetProperty("text").GetString()?.Trim() ?? "";
            if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start) throw new InvalidDataException("The speech model returned invalid segment timing.");
            // Whisper pads short audio windows. Intersect its intervals with the real media timeline,
            // and explicitly report this adjustment; never publish cues in the padded silence.
            if (start >= info.Duration) { clipped = true; continue; }
            if (end > info.Duration) { clipped = true; end = info.Duration; }
            if (!string.IsNullOrWhiteSpace(text)) cues.Add(new(start, end, text));
        }
        var detected = json.RootElement.TryGetProperty("result", out var result) && result.TryGetProperty("language", out var lang) ? lang.GetString() ?? language : language;
        var document = new SubtitleDocument(cues.AsReadOnly(), info.Duration, detected, clipped ? new[] { "Model segments extended into padded audio. Timestamps were clipped to the actual track; review word boundaries." } : null); document.Validate(); return document;
    }
    public async Task<ConversionResult> SaveSubtitlesAsync(DraggedFile source, SubtitleDocument document, bool webVtt, CancellationToken token = default)
    {
        var text = document.Encode(webVtt); var format = webVtt ? "VTT" : "SRT";
        using var output = new OutputFile(source.FullPath, format);
        await File.WriteAllTextAsync(output.Temporary, text, new UTF8Encoding(false, true), token).ConfigureAwait(false);
        if (await File.ReadAllTextAsync(output.Temporary, token).ConfigureAwait(false) != text) throw new InvalidDataException("Subtitle output validation failed.");
        var bytes = new FileInfo(output.Temporary).Length;
        return new(output.Publish(token), 0, 0, document.Cues.Count, Report: new(source.Size, bytes, "Local speech recognition; accuracy and timing require review. " + string.Join(" ", document.Notes ?? [])));
    }
}
