using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Characters.Spum;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class SpumVisualScaleComparisonTests
    {
        private const int PixelwoodPixelsPerUnit = 32;
        private const int SpumSpriteHeightPixels = 32;
        private const int PixelwoodReferenceHeightPixels = 32;
        private const int RepresentativeNpcCount = 8;
        private const float HeightToleranceRatio = 0.05f;
        private const int RendererBudget = 10;
        private const int AnimatorBudget = 9;
        private const double PreloadBudgetMilliseconds = 250d;
        private const long MemoryBudgetBytes = 16L * 1024L * 1024L;
        private const string AuditPath = "docs/superpowers/audits/2026-05-14-spum-visual-scale-performance.md";
        private const string EvidencePath = "Builds/Logs/spum-visual-scale-performance.txt";

        private readonly System.Collections.Generic.List<Object> _objects = new System.Collections.Generic.List<Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] != null)
                    Object.Destroy(_objects[i]);
            }

            _objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SPUM_VISUAL_PERF_001_SpumScaleMatchesPixelwoodAndPerformanceBudgetIsDocumented()
        {
            var pixelwoodReference = CreateSpriteObject("PixelwoodReference", PixelwoodReferenceHeightPixels, -1.5f);
            var spumPlayer = CreateSpumVisual("SPUM_Player", 0f);
            yield return null;

            float pixelwoodHeight = pixelwoodReference.Renderer.bounds.size.y;
            float spumHeight = spumPlayer.Renderer.bounds.size.y;
            float actualRatio = spumHeight / pixelwoodHeight;
            float relativeDelta = Mathf.Abs(actualRatio - SpumCharacterVisualView.DefaultVisualScale) / SpumCharacterVisualView.DefaultVisualScale;

            Assert.LessOrEqual(relativeDelta, HeightToleranceRatio, "SPUM scaled height must stay within 5% of the approved 32/49 Pixelwood reference ratio.");

            long memoryBefore = Profiler.GetTotalAllocatedMemoryLong();
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < RepresentativeNpcCount; i++)
                CreateSpumVisual("SPUM_NPC_" + i.ToString(CultureInfo.InvariantCulture), 1.5f + i);
            stopwatch.Stop();
            yield return null;
            long memoryAfter = Profiler.GetTotalAllocatedMemoryLong();

            int rendererCount = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            int animatorCount = Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            long memoryDelta = System.Math.Max(0L, memoryAfter - memoryBefore);

            Assert.LessOrEqual(rendererCount, RendererBudget, "Representative SPUM visuals should keep renderer count inside the PlayMode budget.");
            Assert.LessOrEqual(animatorCount, AnimatorBudget, "Representative SPUM visuals should keep Animator count inside the PlayMode budget.");
            Assert.LessOrEqual(stopwatch.Elapsed.TotalMilliseconds, PreloadBudgetMilliseconds, "CharacterVisuals preload probe exceeded the PlayMode budget.");
            Assert.LessOrEqual(memoryDelta, MemoryBudgetBytes, "Representative SPUM visual allocation exceeded the PlayMode budget.");

            WriteEvidence(pixelwoodHeight, spumHeight, actualRatio, relativeDelta, rendererCount, animatorCount, stopwatch.Elapsed.TotalMilliseconds, memoryDelta);

            Assert.IsTrue(File.Exists(AuditPath), "Task 13 must document SPUM visual scale and performance evidence.");
            string audit = File.ReadAllText(AuditPath);
            StringAssert.Contains("SPUM_VISUAL_PERF_001", audit);
            StringAssert.Contains("5%", audit);
            StringAssert.Contains("32/49", audit);
            StringAssert.Contains("renderer", audit.ToLowerInvariant());
            StringAssert.Contains("CharacterVisuals", audit);
        }

        private VisualProbe CreateSpriteObject(string name, int heightPixels, float x)
        {
            var root = new GameObject(name);
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSprite(name + "Sprite", 32, heightPixels);
            root.transform.position = new Vector3(x, 0f, 0f);
            _objects.Add(root);
            _objects.Add(renderer.sprite.texture);
            _objects.Add(renderer.sprite);
            return new VisualProbe(root, renderer);
        }

        private VisualProbe CreateSpumVisual(string name, float x)
        {
            var root = new GameObject(name);
            var visualRoot = new GameObject(name + "_VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);
            var renderer = visualRoot.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSprite(name + "Sprite", 32, SpumSpriteHeightPixels);
            var animator = visualRoot.AddComponent<Animator>();
            var view = root.AddComponent<SpumCharacterVisualView>();
            view.ConfigureForTests(animator, visualRoot.transform, new[] { renderer });
            root.transform.localScale = Vector3.one;
            root.transform.position = new Vector3(x, 0f, 0f);
            _objects.Add(root);
            _objects.Add(renderer.sprite.texture);
            _objects.Add(renderer.sprite);
            return new VisualProbe(root, renderer);
        }

        private static Sprite CreateSprite(string name, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            texture.name = name + "Texture";
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), PixelwoodPixelsPerUnit);
            sprite.name = name;
            return sprite;
        }

        private static void WriteEvidence(float pixelwoodHeight, float spumHeight, float actualRatio, float relativeDelta, int rendererCount, int animatorCount, double preloadMilliseconds, long memoryDelta)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath));
            File.WriteAllText(
                EvidencePath,
                "SPUM_VISUAL_PERF_001" + System.Environment.NewLine +
                "pixelwoodHeight=" + pixelwoodHeight.ToString("F4", CultureInfo.InvariantCulture) + System.Environment.NewLine +
                "spumHeight=" + spumHeight.ToString("F4", CultureInfo.InvariantCulture) + System.Environment.NewLine +
                "approvedHeightRatio=32/49" + System.Environment.NewLine +
                "actualHeightRatio=" + actualRatio.ToString("F4", CultureInfo.InvariantCulture) + System.Environment.NewLine +
                "relativeDelta=" + relativeDelta.ToString("P2", CultureInfo.InvariantCulture) + System.Environment.NewLine +
                "rendererCount=" + rendererCount.ToString(CultureInfo.InvariantCulture) + System.Environment.NewLine +
                "animatorCount=" + animatorCount.ToString(CultureInfo.InvariantCulture) + System.Environment.NewLine +
                "CharacterVisualsPreloadMilliseconds=" + preloadMilliseconds.ToString("F2", CultureInfo.InvariantCulture) + System.Environment.NewLine +
                "memoryDeltaBytes=" + memoryDelta.ToString(CultureInfo.InvariantCulture) + System.Environment.NewLine);
        }

        private readonly struct VisualProbe
        {
            public VisualProbe(GameObject root, SpriteRenderer renderer)
            {
                Root = root;
                Renderer = renderer;
            }

            public GameObject Root { get; }
            public SpriteRenderer Renderer { get; }
        }
    }
}
