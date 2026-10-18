using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Aligns the three NPOT game textures to BC7 blocks without resampling their artwork.
/// Padding is added above/right of the original bottom-left pixel region only.
/// </summary>
public static class SealTexturePacking
{
    private sealed class Source
    {
        public string Path;
        public int Width, Height;
        public bool ExtendEdges;

        public Source(string path, int width, int height, bool extendEdges)
        {
            Path = path; Width = width; Height = height; ExtendEdges = extendEdges;
        }
    }

    private static readonly Source[] Sources =
    {
        new Source("Assets/Resources/Characters/ArcticBand/background.png", 1672, 941, true),
        new Source("Assets/Resources/Characters/ArcticBand/seals.png", 1774, 887, false),
        new Source("Assets/Resources/UI/SealPaper/seal-ui-atlas.png", 1254, 1254, false)
    };

    [MenuItem("Tools/Seal Rhythm/Pack Texture Blocks")]
    public static void Pack()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before packing texture source files.");

        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
        string folder = System.IO.Path.Combine(root, "Logs", "SealValidation", "Optimization");
        string backupFolder = System.IO.Path.Combine(folder, "source-art");
        Directory.CreateDirectory(backupFolder);
        var report = new StringBuilder();
        report.AppendLine("Seal texture block packing");
        report.AppendLine("Original pixels keep their bottom-left coordinates. No scaling or repainting.");
        report.AppendLine("BC7, full resolution, no mipmaps, NPOT.None, maximum size 2048.");
        report.AppendLine("Active build target: " + EditorUserBuildSettings.activeBuildTarget);
        report.AppendLine();
        long originalBytes = 0, packedBytes = 0;
        try
        {
            foreach (var source in Sources)
            {
                PackOne(source, root, backupFolder, report, out long original, out long packed);
                originalBytes += original;
                packedBytes += packed;
            }
            report.AppendLine("Original RGBA32 bytes (three textures): " + originalBytes);
            report.AppendLine("BC7 block bytes (three textures): " + packedBytes);
            report.AppendLine("Texture data reduction, before player packaging: " + (originalBytes - packedBytes));
            report.AppendLine("PASS: every original RGBA byte, including alpha, matches the first-run source backup.");
            Debug.Log(report.ToString());
        }
        catch (Exception exception)
        {
            report.AppendLine("FAILED: " + exception.Message);
            throw;
        }
        finally
        {
            File.WriteAllText(System.IO.Path.Combine(folder, "texture-packing.txt"), report.ToString(), Encoding.UTF8);
        }
    }

    private static void PackOne(Source source, string root, string backupFolder, StringBuilder report,
        out long originalBytes, out long packedBytes)
    {
        string path = System.IO.Path.Combine(root, source.Path);
        string backup = System.IO.Path.Combine(backupFolder, System.IO.Path.GetFileName(source.Path));
        byte[] sourcePng = File.ReadAllBytes(path);
        var original = Decode(sourcePng);
        Texture2D baseline = null;
        Texture2D padded = null;
        try
        {
            int width = original.width, height = original.height;
            int targetWidth = (source.Width + 3) & ~3;
            int targetHeight = (source.Height + 3) & ~3;
            Require((width == source.Width && height == source.Height) ||
                (width == targetWidth && height == targetHeight),
                source.Path + ": unexpected source size " + width + "x" + height);

            // Never replace the first-run PNG or its original import settings on later runs.
            if (!File.Exists(backup)) File.Copy(path, backup);
            if (File.Exists(path + ".meta") && !File.Exists(backup + ".meta"))
                File.Copy(path + ".meta", backup + ".meta");

            baseline = Decode(File.ReadAllBytes(backup));
            Require(baseline.width >= source.Width && baseline.height >= source.Height,
                source.Path + ": original backup is smaller than its logical artwork region");
            var pixels = original.GetPixels32();
            var baselinePixels = baseline.GetPixels32();
            CompareRegion(pixels, width, baselinePixels, baseline.width, source.Width, source.Height, source.Path);

            bool changed = width != targetWidth || height != targetHeight;
            if (changed)
            {
                // Color32[] starts transparent. Atlas padding must remain transparent black.
                var targetPixels = new Color32[targetWidth * targetHeight];
                for (int y = 0; y < height; ++y)
                    Array.Copy(pixels, y * width, targetPixels, y * targetWidth, width);

                if (source.ExtendEdges)
                {
                    for (int y = 0; y < height; ++y)
                    for (int x = width; x < targetWidth; ++x)
                        targetPixels[y * targetWidth + x] = pixels[y * width + width - 1];
                    for (int y = height; y < targetHeight; ++y)
                        Array.Copy(targetPixels, (height - 1) * targetWidth, targetPixels, y * targetWidth, targetWidth);
                }

                padded = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
                padded.SetPixels32(targetPixels);
                padded.Apply(false, false);
                byte[] packedPng = padded.EncodeToPNG();
                VerifyPng(packedPng, source, targetWidth, targetHeight, baselinePixels, baseline.width);
                File.WriteAllBytes(path, packedPng);
            }
            // Decode the on-disk PNG as well; validation does not depend on lossy GPU compression.
            VerifyPng(File.ReadAllBytes(path), source, targetWidth, targetHeight, baselinePixels, baseline.width);

            AssetDatabase.ImportAsset(source.Path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(source.Path) as TextureImporter;
            Require(importer != null, "Missing TextureImporter for " + source.Path);
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            importer.crunchedCompression = false;
            if (source.ExtendEdges) importer.spriteImportMode = SpriteImportMode.Single;
            else importer.alphaIsTransparency = true;

            var platform = importer.GetPlatformTextureSettings("Standalone");
            platform.name = "Standalone";
            platform.overridden = true;
            platform.maxTextureSize = 2048;
            platform.format = TextureImporterFormat.BC7;
            platform.textureCompression = TextureImporterCompression.CompressedHQ;
            platform.compressionQuality = 100;
            platform.crunchedCompression = false;
            importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();

            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(source.Path);
            Require(imported != null, "Imported texture missing: " + source.Path);
            Require(imported.width == targetWidth && imported.height == targetHeight,
                source.Path + ": importer changed the padded source dimensions");
            originalBytes = (long)source.Width * source.Height * 4;
            packedBytes = (long)(targetWidth / 4) * (targetHeight / 4) * 16;
            report.AppendLine(source.Path);
            report.AppendLine("  Original logical region: " + source.Width + "x" + source.Height +
                "; this run: " + width + "x" + height + " -> " + imported.width + "x" + imported.height +
                (changed ? " (padded above/right)" : " (already aligned; PNG unchanged)"));
            report.AppendLine("  Format: " + imported.format + "; original RGBA32 bytes: " + originalBytes +
                "; theoretical BC7 bytes: " + packedBytes);
            report.AppendLine("  RGBA/alpha source comparison: PASS; padding: " +
                (source.ExtendEdges ? "nearest edge pixels" : "transparent black") + "; backup: " + backup);
            Require(imported.format == TextureFormat.BC7,
                source.Path + ": expected BC7, got " + imported.format + ". Select Windows Standalone and retry.");
            report.AppendLine();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
            if (baseline != null) UnityEngine.Object.DestroyImmediate(baseline);
            if (padded != null) UnityEngine.Object.DestroyImmediate(padded);
        }
    }

    private static Texture2D Decode(byte[] png)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (texture.LoadImage(png, false)) return texture;
        UnityEngine.Object.DestroyImmediate(texture);
        throw new InvalidOperationException("Cannot decode source PNG.");
    }

    private static void VerifyPng(byte[] png, Source source, int width, int height,
        Color32[] original, int originalWidth)
    {
        var decoded = Decode(png);
        try
        {
            Require(decoded.width == width && decoded.height == height, source.Path + ": PNG dimensions changed");
            var pixels = decoded.GetPixels32();
            CompareRegion(pixels, width, original, originalWidth, source.Width, source.Height, source.Path);
            for (int y = 0; y < height; ++y)
            for (int x = 0; x < width; ++x)
            {
                if (x < source.Width && y < source.Height) continue;
                Color32 expected = source.ExtendEdges ?
                    original[Math.Min(y, source.Height - 1) * originalWidth + Math.Min(x, source.Width - 1)] :
                    new Color32(0, 0, 0, 0);
                Require(SameColor(pixels[y * width + x], expected),
                    source.Path + ": wrong padding pixel at (" + x + ", " + y + ")");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(decoded); }
    }

    private static void CompareRegion(Color32[] current, int currentWidth, Color32[] original, int originalWidth,
        int width, int height, string path)
    {
        for (int y = 0; y < height; ++y)
        for (int x = 0; x < width; ++x)
        {
            if (!SameColor(current[y * currentWidth + x], original[y * originalWidth + x]))
                throw new InvalidOperationException(path + ": original RGBA pixel changed at (" + x + ", " + y + ")");
        }
    }

    private static bool SameColor(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
