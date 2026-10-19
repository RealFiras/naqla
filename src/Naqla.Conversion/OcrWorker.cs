using System.IO;
using System.Text.Json;
using Naqla.Core;
using Tesseract;

namespace Naqla.Conversion;

/// <summary>Fixed-function worker. Relative ASCII names avoid legacy native ANSI-path handling.</summary>
public static class OcrWorker
{
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length != 2 || args[0] != "--internal-ocr" || args[1] is not ("eng" or "ara" or "eng+ara")) return 2;
            using var engine = new TesseractEngine("tessdata", args[1], EngineMode.LstmOnly);
            engine.SetVariable("user_defined_dpi", "300");
            using var pix = Pix.LoadFromMemory(File.ReadAllBytes("input.png")); using var page = engine.Process(pix, PageSegMode.Auto);
            var result = new OcrResult(page.GetText().Trim(), page.GetMeanConfidence(), 1, args[1]);
            File.WriteAllText("result.json", JsonSerializer.Serialize(result)); return 0;
        }
        catch { return 3; } // No private OCR text or source paths on stderr / diagnostics.
    }
}
