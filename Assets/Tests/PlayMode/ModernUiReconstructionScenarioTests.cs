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
    public sealed class ModernUiReconstructionScenarioTests
    {
        [UnityTest]
        public IEnumerator SettingsPanel_ShowAndHide_UsesTiledModernUiPanel()
        {
            CleanupModernUiTestCanvases();
            yield return BootstrapManagers();
            var canvasGo = CreateCanvas();
            try
            {
                var panel = canvasGo.AddComponent<SettingsPanel>();
                panel.Show();
                yield return null;
                yield return new WaitForEndOfFrame();

                Assert.IsTrue(panel.IsVisible);
                Assert.GreaterOrEqual(panel.PanelTileCount, 30);
                Assert.AreEqual(4, panel.CornerTileCount);
                Assert.Zero(CountMissingTileSprites(canvasGo));
                Assert.Zero(CountStretchedTileImages(canvasGo));
                Assert.Zero(CountTextOverflows(canvasGo));

                string screenshotPath = CaptureScreenshot("settings-panel.png");
                Assert.IsTrue(File.Exists(screenshotPath), "Expected screenshot at " + screenshotPath);
                Assert.Greater(CountVisiblePixels(screenshotPath), 1000);

                panel.Hide();
                yield return null;

                Assert.IsFalse(panel.IsVisible);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
                CleanupModernUiTestCanvases();
            }
        }

        [UnityTest]
        public IEnumerator InventoryAndStatusPreviewWindows_UseTiledModernUiPanels()
        {
            CleanupModernUiTestCanvases();
            yield return BootstrapManagers();
            var canvasGo = CreateCanvas();
            try
            {
                var inventory = ModernUiWindowBuilder.BuildInventoryPreview(canvasGo.transform);
                var status = ModernUiWindowBuilder.BuildStatusPreview(canvasGo.transform);
                yield return null;
                yield return new WaitForEndOfFrame();

                Assert.GreaterOrEqual(inventory.GetComponentsInChildren<ModernUiTileImage>(true).Sum(t => t.TileCount), 80);
                Assert.GreaterOrEqual(status.GetComponentsInChildren<ModernUiTileImage>(true).Sum(t => t.TileCount), 60);
                Assert.Zero(CountMissingTileSprites(canvasGo));
                Assert.Zero(CountStretchedTileImages(canvasGo));
                Assert.Zero(CountTextOverflows(canvasGo));

                string screenshotPath = CaptureScreenshot("inventory-status-preview.png");
                Assert.IsTrue(File.Exists(screenshotPath), "Expected screenshot at " + screenshotPath);
                Assert.Greater(CountVisiblePixels(screenshotPath), 1000);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
                CleanupModernUiTestCanvases();
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
            var canvasGo = new GameObject("ModernUiTestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            var backdrop = new GameObject("ModernUiTestBackdrop", typeof(RectTransform), typeof(Image));
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

        private static void CleanupModernUiTestCanvases()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas != null && canvas.name == "ModernUiTestCanvas")
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

        private static int CountStretchedTileImages(GameObject root)
        {
            return root.GetComponentsInChildren<ModernUiTileImage>(true)
                .Count(tileImage => tileImage.HasStretchedCornerTiles);
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

        private static int CountVisiblePixels(string screenshotPath)
        {
            var bytes = File.ReadAllBytes(screenshotPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(bytes);
            int count = 0;
            foreach (var pixel in texture.GetPixels32())
            {
                if (pixel.a > 0 && (pixel.r > 4 || pixel.g > 4 || pixel.b > 4))
                {
                    count++;
                }
            }

            Object.Destroy(texture);
            return count;
        }
    }
}
