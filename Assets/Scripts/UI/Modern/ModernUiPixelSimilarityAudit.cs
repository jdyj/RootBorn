using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Rootborn.UI.Modern
{
    public readonly struct ModernUiPixelSimilarityResult
    {
        public ModernUiPixelSimilarityResult(
            string referencePath,
            string candidatePath,
            int comparedWidth,
            int comparedHeight,
            int comparedPixels,
            float colorSimilarity,
            float edgeSimilarity,
            float combinedSimilarity)
        {
            ReferencePath = referencePath;
            CandidatePath = candidatePath;
            ComparedWidth = comparedWidth;
            ComparedHeight = comparedHeight;
            ComparedPixels = comparedPixels;
            ColorSimilarity = colorSimilarity;
            EdgeSimilarity = edgeSimilarity;
            CombinedSimilarity = combinedSimilarity;
        }

        public string ReferencePath { get; }
        public string CandidatePath { get; }
        public int ComparedWidth { get; }
        public int ComparedHeight { get; }
        public int ComparedPixels { get; }
        public float ColorSimilarity { get; }
        public float EdgeSimilarity { get; }
        public float CombinedSimilarity { get; }
    }

    public static class ModernUiPixelSimilarityAudit
    {
        public const float StrictColorSimilarityMinimum = 0.80f;
        public const float StrictEdgeSimilarityMinimum = 0.50f;
        public const float StrictCombinedSimilarityMinimum = 0.71f;

        private const int DefaultSampleWidth = 96;
        private const int DefaultSampleHeight = 72;
        private const byte ContentAlphaThreshold = 8;
        private const byte ContentBrightnessThreshold = 16;
        private const int EdgeLumaThreshold = 28;

        public static bool MeetsStrictThreshold(ModernUiPixelSimilarityResult result)
        {
            return result.ColorSimilarity >= StrictColorSimilarityMinimum
                && result.EdgeSimilarity >= StrictEdgeSimilarityMinimum
                && result.CombinedSimilarity >= StrictCombinedSimilarityMinimum;
        }

        public static ModernUiPixelSimilarityResult CompareToReference(
            string referencePath,
            string candidatePath,
            string reportPath,
            int sampleWidth = DefaultSampleWidth,
            int sampleHeight = DefaultSampleHeight)
        {
            if (string.IsNullOrWhiteSpace(referencePath))
            {
                throw new ArgumentException("Reference path is required.", nameof(referencePath));
            }

            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                throw new ArgumentException("Candidate path is required.", nameof(candidatePath));
            }

            if (sampleWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleWidth), "Sample width must be greater than zero.");
            }

            if (sampleHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleHeight), "Sample height must be greater than zero.");
            }

            Texture2D referenceTexture = LoadPng(referencePath);
            Texture2D candidateTexture = LoadPng(candidatePath);
            try
            {
                RectInt referenceBounds = FindContentBounds(referenceTexture);
                RectInt candidateBounds = FindContentBounds(candidateTexture);
                Color32[] referencePixels = Sample(referenceTexture, referenceBounds, sampleWidth, sampleHeight);
                Color32[] candidatePixels = Sample(candidateTexture, candidateBounds, sampleWidth, sampleHeight);

                float colorSimilarity = ComputeColorSimilarity(referencePixels, candidatePixels);
                float edgeSimilarity = ComputeEdgeSimilarity(referencePixels, candidatePixels, sampleWidth, sampleHeight);
                float combinedSimilarity = (colorSimilarity * 0.7f) + (edgeSimilarity * 0.3f);

                var result = new ModernUiPixelSimilarityResult(
                    referencePath,
                    candidatePath,
                    sampleWidth,
                    sampleHeight,
                    sampleWidth * sampleHeight,
                    colorSimilarity,
                    edgeSimilarity,
                    combinedSimilarity);

                if (!string.IsNullOrWhiteSpace(reportPath))
                {
                    WriteReport(reportPath, result, referenceBounds, candidateBounds);
                }

                return result;
            }
            finally
            {
                UnityEngine.Object.Destroy(referenceTexture);
                UnityEngine.Object.Destroy(candidateTexture);
            }
        }

        private static Texture2D LoadPng(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Pixel similarity input image was not found.", path);
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidOperationException("Unable to decode PNG image: " + path);
            }

            return texture;
        }

        private static RectInt FindContentBounds(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < texture.height; y++)
            {
                int row = y * texture.width;
                for (int x = 0; x < texture.width; x++)
                {
                    if (!IsContentPixel(pixels[row + x]))
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return new RectInt(0, 0, texture.width, texture.height);
            }

            return new RectInt(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
        }

        private static bool IsContentPixel(Color32 pixel)
        {
            if (pixel.a <= ContentAlphaThreshold)
            {
                return false;
            }

            return pixel.r > ContentBrightnessThreshold || pixel.g > ContentBrightnessThreshold || pixel.b > ContentBrightnessThreshold;
        }

        private static Color32[] Sample(Texture2D texture, RectInt bounds, int width, int height)
        {
            Color32[] source = texture.GetPixels32();
            var sampled = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                float v = height == 1 ? 0f : y / (float)(height - 1);
                int sourceY = Mathf.Clamp(bounds.yMin + Mathf.RoundToInt(v * (bounds.height - 1)), 0, texture.height - 1);
                int outputRow = y * width;

                for (int x = 0; x < width; x++)
                {
                    float u = width == 1 ? 0f : x / (float)(width - 1);
                    int sourceX = Mathf.Clamp(bounds.xMin + Mathf.RoundToInt(u * (bounds.width - 1)), 0, texture.width - 1);
                    sampled[outputRow + x] = source[(sourceY * texture.width) + sourceX];
                }
            }

            return sampled;
        }

        private static float ComputeColorSimilarity(Color32[] referencePixels, Color32[] candidatePixels)
        {
            long totalDelta = 0L;
            for (int i = 0; i < referencePixels.Length; i++)
            {
                Color32 reference = referencePixels[i];
                Color32 candidate = candidatePixels[i];
                totalDelta += Math.Abs(reference.r - candidate.r);
                totalDelta += Math.Abs(reference.g - candidate.g);
                totalDelta += Math.Abs(reference.b - candidate.b);
            }

            double maxDelta = referencePixels.Length * 255.0 * 3.0;
            return Mathf.Clamp01((float)(1.0 - (totalDelta / maxDelta)));
        }

        private static float ComputeEdgeSimilarity(Color32[] referencePixels, Color32[] candidatePixels, int width, int height)
        {
            int comparisons = 0;
            int matching = 0;

            for (int y = 0; y < height - 1; y++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    int index = (y * width) + x;
                    bool referenceEdge = IsEdge(referencePixels, index, width);
                    bool candidateEdge = IsEdge(candidatePixels, index, width);
                    if (referenceEdge == candidateEdge)
                    {
                        matching++;
                    }

                    comparisons++;
                }
            }

            return comparisons == 0 ? 1f : matching / (float)comparisons;
        }

        private static bool IsEdge(Color32[] pixels, int index, int width)
        {
            int current = Luma(pixels[index]);
            int right = Luma(pixels[index + 1]);
            int down = Luma(pixels[index + width]);
            return Math.Abs(current - right) > EdgeLumaThreshold || Math.Abs(current - down) > EdgeLumaThreshold;
        }

        private static int Luma(Color32 pixel)
        {
            return ((pixel.r * 299) + (pixel.g * 587) + (pixel.b * 114)) / 1000;
        }

        private static void WriteReport(
            string reportPath,
            ModernUiPixelSimilarityResult result,
            RectInt referenceBounds,
            RectInt candidateBounds)
        {
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var builder = new StringBuilder();
            builder.AppendLine("{");
            AppendJson(builder, "referencePath", result.ReferencePath, true);
            AppendJson(builder, "candidatePath", result.CandidatePath, true);
            AppendJson(builder, "comparedWidth", result.ComparedWidth, true);
            AppendJson(builder, "comparedHeight", result.ComparedHeight, true);
            AppendJson(builder, "comparedPixels", result.ComparedPixels, true);
            AppendJson(builder, "colorSimilarity", result.ColorSimilarity, true);
            AppendJson(builder, "edgeSimilarity", result.EdgeSimilarity, true);
            AppendJson(builder, "combinedSimilarity", result.CombinedSimilarity, true);
            AppendStrictThresholds(builder, true);
            AppendJson(builder, "strictPass", MeetsStrictThreshold(result), true);
            AppendRect(builder, "referenceBounds", referenceBounds, true);
            AppendRect(builder, "candidateBounds", candidateBounds, false);
            builder.AppendLine("}");
            File.WriteAllText(reportPath, builder.ToString());
        }

        private static void AppendJson(StringBuilder builder, string name, string value, bool trailingComma)
        {
            builder.Append("  \"").Append(name).Append("\": ");
            builder.Append('"').Append(EscapeJson(value)).Append('"');
            builder.AppendLine(trailingComma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, int value, bool trailingComma)
        {
            builder.Append("  \"").Append(name).Append("\": ");
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(trailingComma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, float value, bool trailingComma)
        {
            builder.Append("  \"").Append(name).Append("\": ");
            builder.Append(value.ToString("0.000000", CultureInfo.InvariantCulture));
            builder.AppendLine(trailingComma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, bool value, bool trailingComma)
        {
            builder.Append("  \"").Append(name).Append("\": ");
            builder.Append(value ? "true" : "false");
            builder.AppendLine(trailingComma ? "," : string.Empty);
        }

        private static void AppendStrictThresholds(StringBuilder builder, bool trailingComma)
        {
            builder.Append("  \"strictThresholds\": ");
            builder.Append('{')
                .Append("\"colorSimilarity\": ").Append(StrictColorSimilarityMinimum.ToString("0.000000", CultureInfo.InvariantCulture)).Append(", ")
                .Append("\"edgeSimilarity\": ").Append(StrictEdgeSimilarityMinimum.ToString("0.000000", CultureInfo.InvariantCulture)).Append(", ")
                .Append("\"combinedSimilarity\": ").Append(StrictCombinedSimilarityMinimum.ToString("0.000000", CultureInfo.InvariantCulture))
                .Append('}');
            builder.AppendLine(trailingComma ? "," : string.Empty);
        }

        private static void AppendRect(StringBuilder builder, string name, RectInt rect, bool trailingComma)
        {
            builder.Append("  \"").Append(name).Append("\": ");
            builder.Append('{')
                .Append("\"x\": ").Append(rect.x.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append("\"y\": ").Append(rect.y.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append("\"width\": ").Append(rect.width.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append("\"height\": ").Append(rect.height.ToString(CultureInfo.InvariantCulture))
                .Append('}');
            builder.AppendLine(trailingComma ? "," : string.Empty);
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
