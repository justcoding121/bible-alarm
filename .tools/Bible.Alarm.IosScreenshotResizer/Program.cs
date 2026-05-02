#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace Bible.Alarm.IosScreenshotResizer;

/// <summary>
/// Resizes images under .docs/screenshots/iOS to Apple App Store required dimensions.
/// </summary>
static class Program
{
    private static readonly (int W, int H)[] IPhoneSizes =
    {
        (1242, 2688), (2688, 1242),
        (1284, 2778), (2778, 1284),
    };

    private static readonly (int W, int H)[] IPadSizes =
    {
        (2064, 2752), (2752, 2064),
        (2048, 2732), (2732, 2048),
    };

    /// <summary>
    /// CarPlay PNG: steepen alpha after blur (1 = no change). Typical 2–3 for a crisp transparent boundary.
    /// </summary>
    private const float CarPlayAlphaSteepen = 2.35f;

    /// <summary>
    /// Separable Gaussian kernel (sigma ~0.85) for alpha-only blur before steepening.
    /// </summary>
    private static readonly float[] CarPlayAlphaBlurKernel5 =
    {
        0.061359f, 0.244771f, 0.387744f, 0.244771f, 0.061359f,
    };

    internal static int Main(string[] args)
    {
        var folder = args.Length > 0 ? args[0].Trim() : null;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            Console.Error.WriteLine("Usage: Bible.Alarm.IosScreenshotResizer <path-to-iOS-screenshots-folder>");
            Console.Error.WriteLine("Example: Bible.Alarm.IosScreenshotResizer \"C:\\Work\\Repositories\\bible-alarm\\docs\\screenshots\\iOS\"");
            return 1;
        }

        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };
        var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f)))
            .ToList();

        if (files.Count == 0)
        {
            Console.WriteLine("No .jpg/.jpeg/.png files found under: " + folder);
            return 0;
        }

        Console.WriteLine($"Found {files.Count} image(s). Resizing to closest App Store dimensions...");
        var ok = 0;
        foreach (var path in files)
        {
            try
            {
                ResizeToClosestStoreSize(path, folder);
                ok++;
                Console.WriteLine($"  OK: {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: {path} - {ex.Message}");
            }
        }

        Console.WriteLine($"Done. Processed {ok}/{files.Count} images.");
        return ok == files.Count ? 0 : 1;
    }

    static void ResizeToClosestStoreSize(string imagePath, string rootFolder)
    {
        var relative = Path.GetRelativePath(rootFolder, imagePath);
        var useIpad = relative.StartsWith("tablet", StringComparison.OrdinalIgnoreCase);
        var isCarplay = relative.StartsWith("carplay", StringComparison.OrdinalIgnoreCase);
        var targets = useIpad ? IPadSizes : IPhoneSizes;

        var ext = Path.GetExtension(imagePath);
        var isCarplayPng = isCarplay && ext.Equals(".png", StringComparison.OrdinalIgnoreCase);

        if (isCarplayPng)
        {
            using var rgba = Image.Load<Rgba32>(imagePath);
            if (HasTransparencyForRefine(rgba))
            {
                RefineCarPlayTransparentEdges(rgba);
            }

            ApplyResizeAndSave(rgba, imagePath, ext, targets, isCarplay: true);
            return;
        }

        using var image = Image.Load(imagePath);
        ApplyResizeAndSave(image, imagePath, ext, targets, isCarplay: isCarplay);
    }

    static void ApplyResizeAndSave(Image image, string imagePath, string ext, (int W, int H)[] targets, bool isCarplay)
    {
        var (w, h) = (image.Width, image.Height);
        var aspect = (double)w / h;

        var best = targets[0];
        var bestDiff = double.MaxValue;
        foreach (var t in targets)
        {
            var targetAspect = (double)t.W / t.H;
            var diff = Math.Abs(aspect - targetAspect);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                best = t;
            }
        }

        var padColor = ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            ? Color.Black
            : Color.Transparent;

        var resizeOptions = new ResizeOptions
        {
            Size = new Size(best.W, best.H),
            Mode = ResizeMode.Pad,
            PadColor = padColor,
            Sampler = isCarplay ? KnownResamplers.MitchellNetravali : KnownResamplers.Lanczos3,
        };

        image.Mutate(ctx => ctx.Resize(resizeOptions));
        image.Save(imagePath);
    }

    static bool HasTransparencyForRefine(Image<Rgba32> image)
    {
        var anyLowAlpha = false;
        var anyHighAlpha = false;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var a = row[x].A;
                    if (a < 250)
                    {
                        anyLowAlpha = true;
                    }

                    if (a > 5)
                    {
                        anyHighAlpha = true;
                    }
                }
            }
        });

        return anyLowAlpha && anyHighAlpha;
    }

    /// <summary>
    /// Smooths the alpha silhouette (reduces stair-steps) then steepens the ramp for a clean transparent-to-opaque edge.
    /// Keeps straight RGB; clears to transparent when alpha becomes 0.
    /// </summary>
    static void RefineCarPlayTransparentEdges(Image<Rgba32> image)
    {
        var w = image.Width;
        var h = image.Height;
        var n = w * h;
        var pixels = new Rgba32[n];
        image.CopyPixelDataTo(pixels);

        var a0 = new float[n];
        for (var i = 0; i < n; i++)
        {
            a0[i] = pixels[i].A / 255f;
        }

        var aTmp = new float[n];
        var aBlurred = new float[n];
        ConvolveSeparable(a0, aTmp, aBlurred, w, h, CarPlayAlphaBlurKernel5);

        var steepen = CarPlayAlphaSteepen;
        for (var i = 0; i < n; i++)
        {
            var t = (aBlurred[i] - 0.5f) * steepen + 0.5f;
            if (t < 0f)
            {
                t = 0f;
            }
            else if (t > 1f)
            {
                t = 1f;
            }

            var na = (byte)MathF.Round(t * 255f);
            var p = pixels[i];
            if (na == 0)
            {
                pixels[i] = default;
                continue;
            }

            pixels[i] = new Rgba32(p.R, p.G, p.B, na);
        }

        WriteRgbaPixels(image, pixels);
    }

    static void WriteRgbaPixels(Image<Rgba32> image, Rgba32[] pixels)
    {
        var w = image.Width;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var offset = y * w;
                for (var x = 0; x < w; x++)
                {
                    row[x] = pixels[offset + x];
                }
            }
        });
    }

    static void ConvolveSeparable(float[] src, float[] tmp, float[] dst, int w, int h, float[] kernel)
    {
        ConvolveHorizontal(src, tmp, w, h, kernel);
        ConvolveVertical(tmp, dst, w, h, kernel);
    }

    static void ConvolveHorizontal(float[] src, float[] dst, int w, int h, float[] kernel)
    {
        var k2 = kernel.Length / 2;
        for (var y = 0; y < h; y++)
        {
            var rowOff = y * w;
            for (var x = 0; x < w; x++)
            {
                float sum = 0;
                for (var k = 0; k < kernel.Length; k++)
                {
                    var sx = x + k - k2;
                    if (sx < 0)
                    {
                        sx = 0;
                    }
                    else if (sx >= w)
                    {
                        sx = w - 1;
                    }

                    sum += kernel[k] * src[rowOff + sx];
                }

                dst[rowOff + x] = sum;
            }
        }
    }

    static void ConvolveVertical(float[] src, float[] dst, int w, int h, float[] kernel)
    {
        var k2 = kernel.Length / 2;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                float sum = 0;
                for (var k = 0; k < kernel.Length; k++)
                {
                    var sy = y + k - k2;
                    if (sy < 0)
                    {
                        sy = 0;
                    }
                    else if (sy >= h)
                    {
                        sy = h - 1;
                    }

                    sum += kernel[k] * src[sy * w + x];
                }

                dst[y * w + x] = sum;
            }
        }
    }
}
