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
            Assert.LessOrEqual(screenWidth, 150f);
            Assert.LessOrEqual(screenHeight, 95f);
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
                if (thumbnail != null && CountHudPartImages(thumbnail) >= 3)
                {
                    yield break;
                }

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
            Assert.LessOrEqual(corners[2].x, 170f, "HUD must remain a compact top-left reference element.");
            Assert.GreaterOrEqual(corners[2].y, Screen.height - 115f, "HUD must stay in the top-left band.");
            Assert.LessOrEqual(corners[2].y, Screen.height + 1f, "HUD must stay inside top screen edge.");
        }

        private static int CountTextOverflows(GameObject root)
        {
            int overflow = 0;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                var rt = (RectTransform)text.transform;
                if (text.preferredWidth > rt.rect.width + 1f || text.preferredHeight > rt.rect.height + 1f)
                {
                    overflow++;
                }
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
            string directory = "Builds/Logs/modern-ui";
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.Destroy(texture);
            return path;
        }

        private static string CaptureTopLeftCrop(string fileName, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, Screen.height - height, width, height), 0, 0);
            texture.Apply();
            string directory = "Builds/Logs/modern-ui";
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.Destroy(texture);
            return path;
        }

        private static string WriteReferenceComparisonReport(string fileName, string referencePath, string candidatePath, Texture2D reference, Texture2D candidate)
        {
            string directory = "Builds/Logs/modern-ui";
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            string metrics = BuildPixelDifferenceMetrics(reference, candidate);
            string json = "{\n" +
                "  \"referencePath\": \"" + referencePath.Replace("\\", "/") + "\",\n" +
                "  \"candidatePath\": \"" + candidatePath.Replace("\\", "/") + "\",\n" +
                "  \"referenceWidth\": " + reference.width + ",\n" +
                "  \"referenceHeight\": " + reference.height + ",\n" +
                "  \"candidateWidth\": " + candidate.width + ",\n" +
                "  \"candidateHeight\": " + candidate.height + ",\n" +
                "  \"referenceVisiblePixels\": " + CountVisiblePixels(reference) + ",\n" +
                "  \"candidateVisiblePixels\": " + CountVisiblePixels(candidate) + ",\n" +
                metrics + "\n" +
                "}\n";
            File.WriteAllText(path, json);
            return path;
        }

        private static string BuildPixelDifferenceMetrics(Texture2D reference, Texture2D candidate)
        {
            var refPixels = reference.GetPixels32();
            var candidatePixels = candidate.GetPixels32();
            int count = Mathf.Min(refPixels.Length, candidatePixels.Length);
            int exactMatchingPixels = 0;
            long channelErrorSum = 0;
            int maxChannelError = 0;
            for (int i = 0; i < count; i++)
            {
                var a = refPixels[i];
                var b = candidatePixels[i];
                int dr = Mathf.Abs(a.r - b.r);
                int dg = Mathf.Abs(a.g - b.g);
                int db = Mathf.Abs(a.b - b.b);
                int da = Mathf.Abs(a.a - b.a);
                if (dr == 0 && dg == 0 && db == 0 && da == 0)
                {
                    exactMatchingPixels++;
                }
                channelErrorSum += dr + dg + db + da;
                maxChannelError = Mathf.Max(maxChannelError, dr, dg, db, da);
            }

            int differentPixels = count - exactMatchingPixels;
            float exactMatchRatio = count > 0 ? (float)exactMatchingPixels / count : 0f;
            float meanChannelError = count > 0 ? (float)channelErrorSum / (count * 4f) : 0f;
            return "  \"exactMatchingPixels\": " + exactMatchingPixels + ",\n" +
                "  \"differentPixels\": " + differentPixels + ",\n" +
                "  \"meanChannelError\": " + meanChannelError.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) + ",\n" +
                "  \"maxChannelError\": " + maxChannelError + ",\n" +
                "  \"exactMatchRatio\": " + exactMatchRatio.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static Texture2D LoadTexture(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }

        private static int CountVisiblePixels(string screenshotPath)
        {
            var texture = LoadTexture(screenshotPath);
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
            foreach (var pixel in texture.GetPixels32())
            {
                if (pixel.a > 0 && (pixel.r > 4 || pixel.g > 4 || pixel.b > 4))
                {
                    count++;
                }
            }

            return count;
        }

        private static GameObject FindByNameIncludingInactive(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == objectName) return transforms[i].gameObject;
            }

            return null;
        }

        private static int CountHudPartImages(Transform thumbnail)
        {
            if (thumbnail == null) return 0;
            int count = 0;
            for (int i = 0; i < thumbnail.childCount; i++)
            {
                var child = thumbnail.GetChild(i);
                if (!child.name.StartsWith("HudPart_")) continue;
                var image = child.GetComponent<Image>();
                if (image != null && image.sprite != null) count++;
            }
            return count;
        }
    }
}
