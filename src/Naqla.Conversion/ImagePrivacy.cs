using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using ImageMagick;
using Naqla.Core;

namespace Naqla.Conversion;

internal static class ImagePrivacy
{
    public static string[] Categories(DraggedFile file)
    {
        using var frames = RasterImages.Open(file); var image = frames[0]; var found = new HashSet<string>();
        foreach (var name in image.ProfileNames)
        {
            if (name.Equals("exif", StringComparison.OrdinalIgnoreCase) && image.GetExifProfile()?.Values.Any(x => !x.Tag.Equals(ExifTag.Orientation)) == true) found.Add("EXIF");
            if (name.Equals("xmp", StringComparison.OrdinalIgnoreCase)) found.Add("XMP");
            if (name.Equals("iptc", StringComparison.OrdinalIgnoreCase) || name.Equals("8bim", StringComparison.OrdinalIgnoreCase)) found.Add("IPTC");
        }
        if (image.AttributeNames.Any(x => x.Equals("comment", StringComparison.OrdinalIgnoreCase) || x.Equals("description", StringComparison.OrdinalIgnoreCase))) found.Add("Comments");
        if (image.AttributeNames.Any(IsTiffExif)) found.Add("EXIF");
        foreach (var attribute in image.AttributeNames) { var category = AttributeCategory(attribute); if (category != "") found.Add(category); }
        if (file.Extension == "PNG") foreach (var category in PngCategories(file.FullPath)) found.Add(category);
        return found.Order().ToArray();
    }
    public static void Clean(DraggedFile source, string output, string format, PrivacySettings settings, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var info = new MagickImage(); info.Ping(source.FullPath);
        var byteRewrite = source.Extension is "JPG" or "JPEG" || source.Extension == "PNG" && (int)info.Orientation is 0 or 1;
        if (!byteRewrite && info.Depth > 8) throw new NotSupportedException("This metadata operation would re-encode an image deeper than 8 bits through the Q8 engine. No output was saved; use a high-bit-depth metadata tool for this file.");
        if (source.Extension is "JPG" or "JPEG") CleanJpeg(source.FullPath, output, settings);
        else if (source.Extension == "PNG" && (int)new MagickImageInfo(source.FullPath).Orientation is 0 or 1) CleanPng(source.FullPath, output, settings);
        else
        {
            using var frames = RasterImages.Open(source); var image = frames[0];
            foreach (var name in image.ProfileNames.ToArray())
            {
                var category = name.ToLowerInvariant() switch { "exif" => "EXIF", "xmp" => "XMP", "iptc" or "8bim" => "IPTC", _ => "" };
                if (settings.Removes(category)) image.RemoveProfile(name);
            }
            if (settings.Removes("Comments")) foreach (var attribute in image.AttributeNames.Where(x => x.Equals("comment", StringComparison.OrdinalIgnoreCase) || x.Equals("description", StringComparison.OrdinalIgnoreCase)).ToArray()) image.RemoveAttribute(attribute);
            if (settings.Removes("EXIF")) foreach (var attribute in image.AttributeNames.Where(IsTiffExif).ToArray()) image.RemoveAttribute(attribute);
            // Profile removal alone can leave cached exif:/xmp:/iptc: properties, which PNG
            // would serialize as text. Remove the selected derived properties as well.
            foreach (var attribute in image.AttributeNames.Where(x => settings.Removes(AttributeCategory(x)) || settings.Removes("EXIF") && x.StartsWith("exif:", StringComparison.OrdinalIgnoreCase)).ToArray()) image.RemoveAttribute(attribute);
            if (format == "PNG") image.Settings.SetDefine(MagickFormat.Png, "exclude-chunk", "date,time");
            image.Settings.SetDefine(MagickFormat.WebP, "lossless", true); image.Settings.Compression = CompressionMethod.Zip;
            image.Write(output, LocalConversionService.ImageFormat(format));
        }
        token.ThrowIfCancellationRequested();
        // Decode independently of the byte-rewriter and compare oriented pixel content, alpha and dimensions.
        using var before = RasterImages.Open(source); using var after = RasterImages.Open(FileClassifier.Inspect(output));
        // A lossless palette PNG may choose a one-bit depth; raw RGBA writers inherit
        // that packing. Compare canonical eight-bit samples, not differently packed bytes.
        before[0].Depth = 8; after[0].Depth = 8;
        if (before[0].Width != after[0].Width || before[0].Height != after[0].Height || !SHA256.HashData(before[0].ToByteArray(MagickFormat.Rgba)).SequenceEqual(SHA256.HashData(after[0].ToByteArray(MagickFormat.Rgba)))) throw new InvalidDataException("Privacy cleanup changed decoded image pixels/orientation/alpha.");
        var remaining = Categories(FileClassifier.Inspect(output)).Intersect(settings.Categories).ToArray();
        if (remaining.Length != 0) throw new InvalidDataException("Targeted image metadata remains after cleaning: " + string.Join(", ", remaining));
        // JPEG may deliberately retain one essential orientation EXIF tag; its rebuilt segment contains no personal fields.
    }
    private static bool IsTiffExif(string name) => name.ToLowerInvariant() is "tiff:make" or "tiff:model" or "tiff:artist" or "tiff:copyright" or "tiff:software" or "tiff:datetime" or "tiff:hostcomputer";
    private static string AttributeCategory(string name) => name.ToLowerInvariant() switch
    {
        var value when value.StartsWith("exif:") && value != "exif:orientation" => "EXIF",
        var value when value.StartsWith("xmp:") => "XMP",
        var value when value.StartsWith("iptc:") || value.StartsWith("8bim:") => "IPTC", _ => ""
    };
    private static string PngCategory(string type, ReadOnlySpan<byte> payload)
    {
        if (type == "eXIf") return "EXIF"; if (type == "tIME") return "Dates";
        if (type is not ("tEXt" or "zTXt" or "iTXt")) return "";
        var end = payload.IndexOf((byte)0); if (end is < 1 or > 79) throw new InvalidDataException("Invalid PNG text keyword.");
        var name = System.Text.Encoding.ASCII.GetString(payload[..end]).ToLowerInvariant();
        return name.StartsWith("xml:com.adobe.xmp") || name.StartsWith("xmp:") ? "XMP" : name.StartsWith("exif:") || name == "raw profile type exif" ? "EXIF" : name.StartsWith("iptc:") || name.StartsWith("8bim:") || name is "raw profile type iptc" or "raw profile type 8bim" ? "IPTC" : name.StartsWith("date:") ? "Dates" : "Comments";
    }
    private static string[] PngCategories(string source)
    {
        using var read = File.OpenRead(source); read.Position = 8; var result = new HashSet<string>(); var header = new byte[8];
        while (read.Position < read.Length)
        {
            read.ReadExactly(header); var size = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
            if (size > 67_108_864 || size + 4L > read.Length - read.Position) throw new InvalidDataException("Invalid PNG chunk length.");
            var type = System.Text.Encoding.ASCII.GetString(header, 4, 4); var small = new byte[(int)Math.Min(size, 80)]; read.ReadExactly(small); read.Seek(size - small.Length + 4L, SeekOrigin.Current);
            var category = PngCategory(type, small); if (category != "") result.Add(category); if (type == "IEND") break;
        }
        return result.ToArray();
    }
    private static void CleanJpeg(string source, string output, PrivacySettings settings)
    {
        var data = File.ReadAllBytes(source); if (data.Length < 4 || data[0] != 0xff || data[1] != 0xd8) throw new InvalidDataException("Invalid JPEG header.");
        using var write = new FileStream(output, FileMode.CreateNew, FileAccess.Write); write.Write(data, 0, 2); var offset = 2;
        while (offset < data.Length)
        {
            var begin = offset; if (data[offset++] != 0xff) throw new InvalidDataException("Malformed JPEG marker."); while (offset < data.Length && data[offset] == 0xff) offset++;
            if (offset >= data.Length) throw new InvalidDataException("Truncated JPEG marker."); var marker = data[offset++];
            if (marker == 0xda || marker == 0xd9) { write.Write(data, begin, data.Length - begin); return; }
            if (marker is 0x01 or >= 0xd0 and <= 0xd7) { write.Write(data, begin, offset - begin); continue; }
            if (offset + 2 > data.Length) throw new InvalidDataException("Truncated JPEG length."); var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2)); if (length < 2 || offset + length > data.Length) throw new InvalidDataException("Invalid JPEG segment length.");
            var payload = data.AsSpan(offset + 2, length - 2); var exif = marker == 0xe1 && payload.StartsWith("Exif\0\0"u8); var xmp = marker == 0xe1 && payload.StartsWith("http://ns.adobe.com/xap/1.0/"u8);
            var skip = exif && settings.Removes("EXIF") || xmp && settings.Removes("XMP") || marker == 0xed && settings.Removes("IPTC") || marker == 0xfe && settings.Removes("Comments");
            if (marker == 0xe0 && payload.Length > 14 && payload.StartsWith("JFIF\0"u8) && settings.Removes("EXIF"))
            {
                write.Write(new byte[] { 0xff, 0xe0, 0, 16 }); var jfif = payload[..14].ToArray(); jfif[12] = jfif[13] = 0; write.Write(jfif); offset += length; continue;
            }
            if (skip && exif)
            {
                using var image = new MagickImage(source); var orientation = (int)image.Orientation;
                if (orientation is >= 2 and <= 8)
                {
                    byte[] minimal = [0xff, 0xe1, 0, 34, (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0, (byte)'I', (byte)'I', 42, 0, 8, 0, 0, 0, 1, 0, 0x12, 1, 3, 0, 1, 0, 0, 0, (byte)orientation, 0, 0, 0, 0, 0, 0, 0];
                    write.Write(minimal);
                }
            }
            if (!skip) write.Write(data, begin, offset + length - begin); offset += length;
        }
        throw new InvalidDataException("JPEG scan marker missing.");
    }
    private static void CleanPng(string source, string output, PrivacySettings settings)
    {
        using var read = File.OpenRead(source); using var write = new FileStream(output, FileMode.CreateNew, FileAccess.Write); Span<byte> signature = stackalloc byte[8]; read.ReadExactly(signature); if (!signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw new InvalidDataException("Invalid PNG signature."); write.Write(signature);
        var header = new byte[8]; var crc = new byte[4]; var ended = false;
        while (read.Position < read.Length)
        {
            read.ReadExactly(header); var size = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4)); if (size > 67_108_864 || size + 4L > read.Length - read.Position) throw new InvalidDataException("Invalid PNG chunk length.");
            var payload = new byte[size]; read.ReadExactly(payload); read.ReadExactly(crc); var type = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            var skip = settings.Removes(PngCategory(type, payload));
            if (!skip) { write.Write(header); write.Write(payload); write.Write(crc); }
            if (type == "IEND") { ended = true; break; }
        }
        if (!ended || read.Position != read.Length) throw new InvalidDataException("PNG has missing end marker or trailing data.");
    }
}
