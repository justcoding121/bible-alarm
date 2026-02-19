#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Bible.Alarm.IosScreenshotResizer;

/// <summary>
/// Resizes images under .docs/screenshots/iOS to Apple App Store required dimensions.
/// </summary>
class Program
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

    static int Main(string[] args)
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
        var targets = useIpad ? IPadSizes : IPhoneSizes;

        using var image = Image.Load(imagePath);
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

        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(best.W, best.H),
            Mode = ResizeMode.Pad,
        }));

        image.Save(imagePath);
    }
}
