using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rootborn.Game.Managers;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode
{
    public sealed class ModernUiPixelSimilarityAuditTests
    {
        [UnityTest]
        public IEnumerator SettingsPanel_WritesStrictReferencePixelSimilarityReport()
        {
            CleanupModernUiPixelAuditCanvases();
            yield return BootstrapManagers();
            var canvasGo = CreateCanvas();
            try
            {
                var panel = canvasGo.AddComponent<SettingsPanel>();
                panel.Show();

                yield return null;
                yield return new WaitForEndOfFrame();

                Assert.Zero(CountMissingTileSprites(canvasGo));

                string candidatePath = CaptureScreenshot("settings-panel-pixel-audit.png");
                string reportPath = "Builds/Logs/modern-ui/pixel-similarity-report.json";

                ModernUiPixelSimilarityResult result = ModernUiPixelSimilarityAudit.CompareToReference(
                    "docs/art/reference-modern-ui/settings-frame37.png",
                    candidatePath,
                    reportPath);

                Assert.Greater(result.ComparedPixels, 1000);
                Assert.IsTrue(
                    ModernUiPixelSimilarityAudit.MeetsStrictThreshold(result),
                    $"Strict pixel similarity failed: color={result.ColorSimilarity:0.000000}, edge={result.EdgeSimilarity:0.000000}, combined={result.CombinedSimilarity:0.000000}");
                Assert.IsTrue(File.Exists(reportPath), "Expected pixel similarity report at " + reportPath);

                string report = File.ReadAllText(reportPath);
                StringAssert.Contains("\"referencePath\"", report);
                StringAssert.Contains("\"candidatePath\"", report);
                StringAssert.Contains("\"colorSimilarity\"", report);
                StringAssert.Contains("\"edgeSimilarity\"", report);
                StringAssert.Contains("\"combinedSimilarity\"", report);
                StringAssert.Contains("\"strictThresholds\"", report);
                StringAssert.Contains("\"strictPass\": true", report);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
                CleanupModernUiPixelAuditCanvases();
            }
        }

        private static IEnumerator BootstrapManagers()
        {
            Task task = Managers.BootstrapAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                Assert.Fail(task.Exception != null ? task.Exception.ToString() : "Managers bootstrap failed.");
            }
        }

        private static GameObject CreateCanvas()
        {
            var canvasGo = new GameObject("ModernUiPixelAuditCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            MakeBackdrop(canvasGo.transform);
            return canvasGo;
        }

        private static void MakeBackdrop(Transform parent)
        {
            var backdrop = new GameObject("ModernUiPixelAuditBackdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(parent, false);
            var rect = (RectTransform)backdrop.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = backdrop.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
        }

        private static void CleanupModernUiPixelAuditCanvases()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas != null && canvas.name == "ModernUiPixelAuditCanvas")
                {
                    Object.DestroyImmediate(canvas.gameObject);
                }
            }
        }

        private static int CountMissingTileSprites(GameObject root)
        {
            return root.GetComponentsInChildren<Image>(true)
                .Count(image => image.transform.name.StartsWith("Tile_") && image.sprite == null);
        }

        private static string CaptureScreenshot(string fileName)
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
    }
}
