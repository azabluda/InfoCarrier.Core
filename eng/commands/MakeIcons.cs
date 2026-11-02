// Licensed under the MIT license. See license.txt file in the project root for license information.

using SkiaSharp;

// Derive all shipped icons from docs/assets/icon-source.png. SkiaSharp supplies portable PNG
// decoding/encoding; the resampler retains the original three-lobe Lanczos filter and alpha mask.
// --output-root <directory> redirects generated assets for review without replacing shipped files.
internal static class MakeIcons
{
    private static readonly (string Path, int Canvas, int Margin)[] Targets =
    [
        ("docs/assets/icon.png", 128, 4),
        ("website/docs/assets/icons/apple-touch-icon.png", 180, 6),
        ("website/docs/assets/icons/icon-192.png", 192, 6),
        ("website/docs/assets/icons/icon-512.png", 512, 16),
        ("samples/Northwind.Client/wwwroot/icon-64.png", 64, 0)
    ];
    private const string Source = "docs/assets/icon-source.png";
    private const string Ico = "website/docs/assets/icons/favicon.ico";

    private static double Lanczos(double x)
    {
        x = Math.Abs(x);
        return x == 0 ? 1 : x >= 3 ? 0 : Math.Sin(Math.PI * x) * Math.Sin(Math.PI * x / 3) / (Math.PI * Math.PI * x * x / 3);
    }

    // Separable, normalized Lanczos with an expanded support for downsampling. Intermediate
    // pixels are clamped to bytes, matching the original two-pass, premultiplied-alpha pipeline.
    private static byte[] Resize(byte[] source, int width, int height, int outputWidth, int outputHeight)
    {
        static byte[] Pass(byte[] input, int width, int height, int size, bool horizontal)
        {
            var length = horizontal ? width : height;
            var scale = (double)length / size;
            var filterScale = Math.Max(1, scale);
            var outputWidth = horizontal ? size : width;
            var outputHeight = horizontal ? height : size;
            var output = new byte[outputWidth * outputHeight * 4];
            for (var n = 0; n < size; n++)
            {
                var center = (n + .5) * scale;
                var first = Math.Max(0, (int)(center - 3 * filterScale + .5));
                var last = Math.Min(length, (int)(center + 3 * filterScale + .5));
                var weights = Enumerable.Range(first, last - first).Select(i => Lanczos((i - center + .5) / filterScale)).ToArray();
                var sum = weights.Sum();
                var coefficients = weights.Select(w => (int)Math.Round(w / sum * (1 << 22), MidpointRounding.AwayFromZero)).ToArray();
                var otherLength = horizontal ? height : width;
                for (var other = 0; other < otherLength; other++)
                    for (var channel = 0; channel < 4; channel++)
                    {
                        long value = 1 << 21;
                        for (var i = first; i < last; i++)
                        {
                            var pixel = horizontal ? other * width + i : i * width + other;
                            value += (long)input[pixel * 4 + channel] * coefficients[i - first];
                        }
                        var pixelOut = horizontal ? other * outputWidth + n : n * outputWidth + other;
                        output[pixelOut * 4 + channel] = (byte)Math.Clamp(value >> 22, 0, 255);
                    }
            }
            return output;
        }
        return Pass(Pass(source, width, height, outputWidth, true), outputWidth, height, outputHeight, false);
    }

    private static byte[] Render(byte[] mark, int width, int height, int canvas, int margin)
    {
        var target = canvas - 2 * margin;
        var square = Math.Abs((double)width / height - 1) <= .08;
        var scale = Math.Min((double)target / width, (double)target / height);
        var w = square ? target : Math.Max(1, (int)Math.Round(width * scale));
        var h = square ? target : Math.Max(1, (int)Math.Round(height * scale));
        var resized = Resize(mark, width, height, w, h);
        using var bitmap = new SKBitmap(new SKImageInfo(canvas, canvas, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bitmap.Erase(SKColors.Transparent);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var index = (y * w + x) * 4;
                var alpha = resized[index + 3];
                byte Channel(int c)
                {
                    var unpremultiplied = alpha == 0 ? 0 : Math.Min(255, resized[index + c] * 255 / alpha);
                    return (byte)((unpremultiplied * alpha + 127) / 255);
                }
                // The original paste uses the scaled image as its own mask, including alpha.
                bitmap.SetPixel(x + (canvas - w) / 2, y + (canvas - h) / 2,
                    new SKColor(Channel(0), Channel(1), Channel(2), (byte)((alpha * alpha + 127) / 255)));
            }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    public static int Run(string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output-root"))
        { Console.Error.WriteLine("usage: MakeIcons.cs [--output-root directory]"); return 2; }
        try
        {
            var output = args.Length == 0 ? "." : args[1];
            using var codec = SKCodec.Create(Source) ?? throw new IOException("Cannot decode " + Source);
            using var source = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            if (codec.GetPixels(source.Info, source.GetPixels()) != SKCodecResult.Success) throw new IOException("Incomplete PNG source.");
            var pixels = source.Pixels;
            var visible = pixels.Select((p, i) => (p, i)).Where(item => item.p.Alpha >= 2).Select(item => item.i).ToArray();
            if (visible.Length == 0) throw new IOException("The source contains no visible artwork at alpha 2.");
            var left = visible.Min(i => i % source.Width); var right = visible.Max(i => i % source.Width);
            var top = visible.Min(i => i / source.Width); var bottom = visible.Max(i => i / source.Width);
            var width = right - left + 1; var height = bottom - top + 1;
            var mark = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var color = pixels[(y + top) * source.Width + x + left]; var index = (y * width + x) * 4;
                    mark[index] = (byte)((color.Red * color.Alpha + 127) / 255);
                    mark[index + 1] = (byte)((color.Green * color.Alpha + 127) / 255);
                    mark[index + 2] = (byte)((color.Blue * color.Alpha + 127) / 255);
                    mark[index + 3] = color.Alpha;
                }
            foreach (var (path, canvas, margin) in Targets)
            {
                var destination = Path.Combine(output, path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllBytes(destination, Render(mark, width, height, canvas, margin));
                Console.WriteLine($"{path,-48} {canvas}x{canvas}  {new FileInfo(destination).Length} bytes");
            }
            // ICO directory entries point to independently rendered, PNG-compressed 16/32/48 frames.
            var sizes = new[] { 16, 32, 48 };
            var frames = sizes.Select(n => Render(mark, width, height, n, 0)).ToArray();
            var ico = Path.Combine(output, Ico);
            using (var writer = new BinaryWriter(File.Create(ico)))
            {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)3);
                var offset = 6 + 16 * sizes.Length;
                for (var i = 0; i < sizes.Length; i++)
                {
                    writer.Write((byte)sizes[i]); writer.Write((byte)sizes[i]); writer.Write((ushort)0);
                    writer.Write((ushort)0); writer.Write((ushort)32); writer.Write(frames[i].Length); writer.Write(offset);
                    offset += frames[i].Length;
                }
                foreach (var frame in frames) writer.Write(frame);
            }
            Console.WriteLine($"{Ico,-48} 16/32/48  {new FileInfo(ico).Length} bytes");
            Console.WriteLine($"\nall from {Source}  {source.Width}x{source.Height}, mark {width}x{height} (trimmed at alpha 2)");
            return 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { Console.Error.WriteLine($"make-icons: {error.Message}"); return 1; }
    }
}
