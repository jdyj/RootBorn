using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Managers;
using Rootborn.Game.Time;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode
{
    public sealed class FarmCharacterHudOverlayPlayModeTests
    {
        private const string TopLeftReferencePath = "docs/art/reference/pixelwood-reference-frame0-top-left.png";
        private const string EvidenceDirectory = "Builds/Logs/modern-ui";

        [UnityTest]
        public IEnumerator FarmScene_InstallsTopLeftLayeredCharacterThumbnailHud()
        {
            yield return LoadFarmAndBuildHud();
            var hud = GameObject.Find("TopLeftCharacterHud");
            Assert.IsNotNull(hud);
            Assert.IsNotNull(hud.transform.Find("CharacterThumbnailFrame"));
            var thumbnail = hud.transform.Find("CharacterThumbnailFrame/CharacterThumbnail");
            Assert.IsNotNull(thumbnail);
            Assert.GreaterOrEqual(CountHudPartImages(thumbnail), 3);
        }

        [UnityTest]
        public IEnumerator FarmScene_HidesBlockingPanelsAroundTopLeftCharacterHud()
        {
            yield return LoadFarmAndBuildHud();
            AssertMissingOrInactive("HUD");
            AssertMissingOrInactive("HotkeyHint");
            AssertMissingOrInactive("QuestLogPanel");
            AssertMissingOrInactive("BookPanel");
        }

        [UnityTest]
        public IEnumerator FarmScene_TopLeftHudMatchesReferenceScaleAndStructure()
        {
            yield return LoadFarmAndBuildHud();
            var hud = GameObject.Find("TopLeftCharacterHud");
            Assert.IsNotNull(hud);
            var rt = (RectTransform)hud.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float screenWidth = Mathf.Abs(corners[2].x - corners[0].x);
            float screenHeight = Mathf.Abs(corners[2].y - corners[0].y);
            Assert.LessOrEqual(screenWidth, 260f);
            Assert.LessOrEqual(screenHeight, 170f);
            Assert.IsNotNull(hud.transform.Find("CharacterThumbnailBackplate"));
            Assert.IsNotNull(hud.transform.Find("TimeLabel"));
            Assert.IsNotNull(hud.transform.Find("CurrencyLabel"));
            Assert.IsNotNull(hud.transform.Find("HudSlot_Inventory"));
            Assert.IsNotNull(hud.transform.Find("HudSlot_Health"));
            Assert.IsNotNull(hud.transform.Find("HudSlot_Tool"));
        }

        [UnityTest]
        public IEnumerator FarmScene_TopLeftHudUsesModern16x16TileImages()
        {
            yield return LoadFarmAndBuildHud();
            AssertModernTileImage("TopLeftCharacterHud");
            AssertModernTileImage("CharacterThumbnailFrame");
            AssertModernTileImage("HudSlot_Inventory");
            AssertModernTileImage("HudSlot_Health");
            AssertModernTileImage("HudSlot_Tool");
        }

        [UnityTest]
        public IEnumerator FarmScene_TopLeftHudReflectsGameClockLabels()
        {
            yield return LoadFarmAndBuildHud();
            var hud = GameObject.Find("TopLeftCharacterHud");
            Assert.IsNotNull(hud);
            var clock = GameClock.Instance;
            Assert.IsNotNull(clock);
            string expectedDay = "DAY " + clock.Day;
            string expectedTime = FormatClockTime(clock.DayProgress01);
            Assert.AreEqual(expectedDay, hud.transform.Find("DayLabel").GetComponent<Text>().text);
            Assert.AreEqual(expectedTime, hud.transform.Find("TimeLabel").GetComponent<Text>().text);
            Assert.AreEqual("0G", hud.transform.Find("CurrencyLabel").GetComponent<Text>().text);
        }

        [UnityTest]
        public IEnumerator FarmScene_TopLeftHudScreenshotAudit_WritesEvidence()
        {
            yield return LoadFarmAndBuildHud();
            yield return new WaitForEndOfFrame();
            var hud = GameObject.Find("TopLeftCharacterHud");
            Assert.IsNotNull(hud);
            Assert.Zero(CountTextOverflows(hud));
            AssertHudWithinTopLeftReferenceBounds((RectTransform)hud.transform);
            string screenshotPath = CaptureHudScreenshot("farm-top-left-hud.png");
            Assert.IsTrue(File.Exists(screenshotPath), "Expected screenshot at " + screenshotPath);
            Assert.Greater(CountVisiblePixels(screenshotPath), 1000);
        }

        [UnityTest]
        public IEnumerator FarmScene_TopLeftHudReferenceComparison_WritesReport()
        {
            yield return LoadFarmAndBuildHud();
            yield return new WaitForEndOfFrame();
            Assert.IsTrue(File.Exists(TopLeftReferencePath), TopLeftReferencePath);
            var reference = LoadTexture(TopLeftReferencePath);
            try
            {
                string cropPath = CaptureTopLeftCrop("farm-top-left-hud-crop.png", reference.width, reference.height);
                Assert.IsTrue(File.Exists(cropPath), "Expected crop at " + cropPath);
                var candidate = LoadTexture(cropPath);
                try
                {
                    Assert.AreEqual(reference.width, candidate.width);
                    Assert.AreEqual(reference.height, candidate.height);
                    Assert.Greater(CountVisiblePixels(candidate), 1000);
                    AssertReferenceScaleFillBounds(candidate);
                    AssertReferenceDarkFrameDensity(candidate);
                    string reportPath = WriteReferenceComparisonReport("farm-top-left-hud-reference-report.json", TopLeftReferencePath, cropPath, reference, candidate);
                    Assert.IsTrue(File.Exists(reportPath), "Expected report at " + reportPath);
                    string report = File.ReadAllText(reportPath);
                    StringAssert.Contains("\"referencePath\"", report);
                    StringAssert.Contains("\"candidatePath\"", report);
                    StringAssert.Contains("\"referenceVisiblePixels\"", report);
                    StringAssert.Contains("\"candidateVisiblePixels\"", report);
                    StringAssert.Contains("\"exactMatchingPixels\"", report);
                    StringAssert.Contains("\"differentPixels\"", report);
                    StringAssert.Contains("\"meanChannelError\"", report);
                    StringAssert.Contains("\"maxChannelError\"", report);
                    StringAssert.Contains("\"exactMatchRatio\"", report);
                }
                finally
                {
                    Object.Destroy(candidate);
                }
            }
            finally
            {
                Object.Destroy(reference);
            }
        }

        private static IEnumerator LoadFarmAndBuildHud()
        {
            yield return SceneManager.LoadSceneAsync("Farm");
            yield return Managers.BootstrapAsync().AsIEnumerator();
            var fillerGo = new GameObject("[FarmAutoFiller-Test]");
            fillerGo.AddComponent<FarmAutoFiller>().FillIfEmpty();
            yield return WaitForHud();
        }

        private static IEnumerator WaitForHud()
        {
            for (int frame = 0; frame < 1200; frame++)
            {
                var thumbnail = GameObject.Find("TopLeftCharacterHud")?.transform.Find("CharacterThumbnailFrame/CharacterThumbnail");
                if (thumbnail != null && CountHudPartImages(thumbnail) >= 3) yield break;
                yield return null;
            }
        }

        private static string FormatClockTime(float dayProgress01)
        {
            int totalMinutes = Mathf.FloorToInt(Mathf.Repeat(dayProgress01, 1f) * 24f * 60f);
            int hour = totalMinutes / 60;
            int minute = totalMinutes % 60;
            return hour.ToString("00") + ":" + minute.ToString("00");
        }

        private static void AssertMissingOrInactive(string objectName)
        {
            var go = FindByNameIncludingInactive(objectName);
            if (go == null) return;
            Assert.IsFalse(go.activeInHierarchy, objectName + " should not block the default Farm gameplay view.");
        }

        private static void AssertModernTileImage(string objectName)
        {
            var go = FindByNameIncludingInactive(objectName);
            Assert.IsNotNull(go, objectName);
            var tileImage = go.GetComponent<ModernUiTileImage>();
            Assert.IsNotNull(tileImage, objectName);
            Assert.AreEqual(new Vector2(16f, 16f), tileImage.TileSize, objectName);
            Assert.GreaterOrEqual(tileImage.TileCount, 9, objectName);
            Assert.AreEqual(4, tileImage.CornerTileCount, objectName);
            Assert.IsFalse(tileImage.HasStretchedCornerTiles, objectName);
            AssertAllGeneratedTileSpritesResolved(go.transform, objectName);
        }

        private static void AssertAllGeneratedTileSpritesResolved(Transform root, string objectName)
        {
            int imageCount = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (!child.name.StartsWith("Tile_")) continue;
                var image = child.GetComponent<Image>();
                Assert.IsNotNull(image, child.name);
                Assert.IsNotNull(image.sprite, objectName + "/" + child.name);
                imageCount++;
            }
            Assert.GreaterOrEqual(imageCount, 9, objectName);
        }

        private static void AssertHudWithinTopLeftReferenceBounds(RectTransform hud)
        {
            var corners = new Vector3[4];
            hud.GetWorldCorners(corners);
            Assert.GreaterOrEqual(corners[0].x, -1f, "HUD must stay inside left screen edge.");
            Assert.LessOrEqual(corners[2].x, 260f, "HUD must remain a compact top-left reference element.");
            Assert.GreaterOrEqual(corners[2].y, Screen.height - 175f, "HUD must stay in the top-left band.");
            Assert.LessOrEqual(corners[2].y, Screen.height + 1f, "HUD must stay inside top screen edge.");
        }

        private static void AssertReferenceScaleFillBounds(Texture2D texture)
        {
            var bounds = BrightBeigeBounds(texture);
            Assert.GreaterOrEqual(bounds.width, 120, "HUD bright panel area must match the reference crop scale.");
            Assert.GreaterOrEqual(bounds.height, 70, "HUD bright panel area must match the reference crop scale.");
        }

        private static void AssertReferenceDarkFrameDensity(Texture2D texture)
        {
            int totalDark = CountDarkPixels(texture, new RectInt(0, 0, texture.width, texture.height));
            int topDark = CountDarkPixels(texture, new RectInt(0, texture.height - 24, texture.width, 24));
            int leftDark = CountDarkPixels(texture, new RectInt(0, 0, 24, texture.height));
            Assert.GreaterOrEqual(totalDark, 1800, "HUD crop must retain the dark frame density visible in the reference crop.");
            Assert.GreaterOrEqual(topDark, 650, "HUD crop must retain the dark top strip visible in the reference crop.");
            Assert.GreaterOrEqual(leftDark, 300, "HUD crop must retain the dark left strip visible in the reference crop.");
        }

        private static int CountDarkPixels(Texture2D texture, RectInt region)
        {
            int count = 0;
            int maxX = Mathf.Min(texture.width, region.xMax);
            int maxY = Mathf.Min(texture.height, region.yMax);
            for (int y = Mathf.Max(0, region.yMin); y < maxY; y++)
            {
                for (int x = Mathf.Max(0, region.xMin); x < maxX; x++)
                {
                    var pixel = texture.GetPixel(x, y);
                    if (pixel.r < 0.36f && pixel.g < 0.36f && pixel.b < 0.40f) count++;
                }
            }
            return count;
        }

        private static RectInt BrightBeigeBounds(Texture2D texture)
        {
            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    var pixel = texture.GetPixel(x, y);
                    if (pixel.r <= 0.70f || pixel.g <= 0.58f || pixel.b <= 0.45f) continue;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            if (maxX < minX || maxY < minY) return new RectInt(0, 0, 0, 0);
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static int CountTextOverflows(GameObject root)
        {
            int overflow = 0;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                var rt = (RectTransform)text.transform;
                if (text.preferredWidth > rt.rect.width + 1f || text.preferredHeight > rt.rect.height + 1f) overflow++;
            }
            return overflow;
        }

        private static string CaptureHudScreenshot(string fileName)
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            string path = Path.Combine(EvidenceDirectory, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.Destroy(texture);
            return path;
        }

        private static string CaptureTopLeftCrop(string fileName, int width, int height)
        {
            int readWidth = Mathf.Min(width, Mathf.Max(1, Screen.width));
            int readHeight = Mathf.Min(height, Mathf.Max(1, Screen.height));
            var texture = new Texture2D(readWidth, readHeight, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, Screen.height - readHeight, readWidth, readHeight), 0, 0);
            texture.Apply();
            string path = Path.Combine(EvidenceDirectory, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.Destroy(texture);
            return path;
        }

        private static Texture2D LoadTexture(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(path)), path);
            return texture;
        }

        private static int CountVisiblePixels(string path)
        {
            var texture = LoadTexture(path);
            try
            {
                return CountVisiblePixels(texture);
            }
            finally
            {
                Object.Destroy(texture);
            }
        }

        private static int CountVisiblePixels(Texture2D texture)
        {
            int count = 0;
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (texture.GetPixel(x, y).a > 0f) count++;
                }
            }
            return count;
        }

        private static int CountHudPartImages(Transform thumbnail)
        {
            int count = 0;
            foreach (var image in thumbnail.GetComponentsInChildren<Image>(true))
            {
                if (image.name.StartsWith("HudPart_") && image.sprite != null) count++;
            }
            return count;
        }

        private static GameObject FindByNameIncludingInactive(string objectName)
        {
            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform.name == objectName && transform.hideFlags == HideFlags.None) return transform.gameObject;
            }
            return null;
        }

        private static string WriteReferenceComparisonReport(string fileName, string referencePath, string candidatePath, Texture2D reference, Texture2D candidate)
        {
            int referenceVisible = CountVisiblePixels(reference);
            int candidateVisible = CountVisiblePixels(candidate);
            var diff = BuildPixelDifferenceMetrics(reference, candidate);
            string path = Path.Combine(EvidenceDirectory, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string json = "{\n"
                + "  \"referencePath\": \"" + referencePath.Replace("\\", "/") + "\",\n"
                + "  \"candidatePath\": \"" + candidatePath.Replace("\\", "/") + "\",\n"
                + "  \"referenceWidth\": " + reference.width + ",\n"
                + "  \"referenceHeight\": " + reference.height + ",\n"
                + "  \"candidateWidth\": " + candidate.width + ",\n"
                + "  \"candidateHeight\": " + candidate.height + ",\n"
                + "  \"referenceVisiblePixels\": " + referenceVisible + ",\n"
                + "  \"candidateVisiblePixels\": " + candidateVisible + ",\n"
                + "  \"exactMatchingPixels\": " + diff.exactMatchingPixels + ",\n"
                + "  \"differentPixels\": " + diff.differentPixels + ",\n"
                + "  \"meanChannelError\": " + diff.meanChannelError.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + ",\n"
                + "  \"maxChannelError\": " + diff.maxChannelError + ",\n"
                + "  \"exactMatchRatio\": " + diff.exactMatchRatio.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + "\n"
                + "}\n";
            File.WriteAllText(path, json);
            return path;
        }

        private static PixelDifference BuildPixelDifferenceMetrics(Texture2D reference, Texture2D candidate)
        {
            Assert.AreEqual(reference.width, candidate.width);
            Assert.AreEqual(reference.height, candidate.height);
            var referencePixels = reference.GetPixels32();
            var candidatePixels = candidate.GetPixels32();
            Assert.AreEqual(referencePixels.Length, candidatePixels.Length);
            int exact = 0;
            int different = 0;
            int maxChannelError = 0;
            long totalChannelError = 0;
            int totalChannels = referencePixels.Length * 4;
            for (int i = 0; i < referencePixels.Length; i++)
            {
                var a = referencePixels[i];
                var b = candidatePixels[i];
                int dr = Mathf.Abs(a.r - b.r);
                int dg = Mathf.Abs(a.g - b.g);
                int db = Mathf.Abs(a.b - b.b);
                int da = Mathf.Abs(a.a - b.a);
                int pixelMax = Mathf.Max(Mathf.Max(dr, dg), Mathf.Max(db, da));
                if (pixelMax == 0) exact++;
                else different++;
                maxChannelError = Mathf.Max(maxChannelError, pixelMax);
                totalChannelError += dr + dg + db + da;
            }
            return new PixelDifference(exact, different, totalChannelError / (float)totalChannels, maxChannelError, exact / (float)referencePixels.Length);
        }

        private readonly struct PixelDifference
        {
            public PixelDifference(int exactMatchingPixels, int differentPixels, float meanChannelError, int maxChannelError, float exactMatchRatio)
            {
                this.exactMatchingPixels = exactMatchingPixels;
                this.differentPixels = differentPixels;
                this.meanChannelError = meanChannelError;
                this.maxChannelError = maxChannelError;
                this.exactMatchRatio = exactMatchRatio;
            }
            public readonly int exactMatchingPixels;
            public readonly int differentPixels;
            public readonly float meanChannelError;
            public readonly int maxChannelError;
            public readonly float exactMatchRatio;
        }
    }
}
