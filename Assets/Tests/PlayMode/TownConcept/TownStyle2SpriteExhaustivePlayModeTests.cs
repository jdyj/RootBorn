using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.TownConcept
{
    public sealed class TownStyle2SpriteExhaustivePlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAYMODE_SPRITE_001_AllDeclaredModernUiSprites_RenderEverySpriteToPixels()
        {
            var resource = new ResourceManager();
            var initTask = resource.InitializeAsync();
            yield return WaitForTask(initTask);

            var missing = new List<string>();
            var invalidAssetPixels = new List<string>();
            var notRendered = new List<string>();
            int checkedCount = 0;
            int checkedAssetPixels = 0;
            int renderedCount = 0;

            GameObject cameraGo = null;
            GameObject canvasGo = null;
            RenderTexture renderTexture = null;
            Texture2D readback = null;

            try
            {
                cameraGo = new GameObject("SpriteRenderVerifierCamera", typeof(Camera));
                var camera = cameraGo.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);

                renderTexture = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32);
                renderTexture.Create();
                readback = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                camera.targetTexture = renderTexture;

                canvasGo = new GameObject("SpriteRenderVerifierCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.sortingOrder = 1000;

                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

                foreach (var sheet in ModernUISpriteAddresses.AllSheets)
                {
                    foreach (string subName in sheet.subNames)
                    {
                        string id = sheet.sheetAddress + "[" + subName + "]";
                        var task = resource.LoadSubSpriteAsync(sheet.sheetAddress, subName);
                        yield return WaitForTask(task);
                        Sprite sprite = task.Result;
                        checkedCount++;

                        if (sprite == null)
                        {
                            missing.Add(id);
                            continue;
                        }

                        if (sprite.name != subName)
                        {
                            missing.Add(id + " resolved as " + sprite.name);
                            continue;
                        }

                        int visibleAssetPixels = CountVisibleAssetPixels(sprite, out string pixelError);
                        if (visibleAssetPixels <= 0)
                        {
                            invalidAssetPixels.Add(id + " " + pixelError);
                            continue;
                        }
                        checkedAssetPixels += visibleAssetPixels;

                        if (!RenderSpriteAndReadVisiblePixels(canvasGo.transform, camera, renderTexture, readback, sprite))
                        {
                            notRendered.Add(id);
                            continue;
                        }

                        renderedCount++;
                    }
                }
            }
            finally
            {
                resource.ReleaseAll();
                if (readback != null) Object.Destroy(readback);
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    Object.Destroy(renderTexture);
                }
                if (canvasGo != null) Object.Destroy(canvasGo);
                if (cameraGo != null) Object.Destroy(cameraGo);
            }

            Assert.Greater(checkedCount, 0, "The exhaustive PlayMode sprite gate must check at least one sprite.");
            Assert.Greater(checkedAssetPixels, 0, "The exhaustive PlayMode sprite gate must read visible source sprite pixels.");
            Assert.IsEmpty(missing, "Missing or mismatched runtime sprite addresses: " + string.Join(", ", missing));
            Assert.IsEmpty(invalidAssetPixels, "Sprites without visible source pixels: " + string.Join(", ", invalidAssetPixels));
            Assert.IsEmpty(notRendered, "Sprites that loaded but did not render visible pixels through UI Image: " + string.Join(", ", notRendered));
            Assert.AreEqual(checkedCount, renderedCount, "Every declared sprite must render visibly in PlayMode.");
        }

        [Test]
        public void Constitution_RequiresExhaustivePlayModeSpritePixelAndAddressValidation()
        {
            string constitution = System.IO.File.ReadAllText(".claude/constitution.md");

            StringAssert.Contains("Sprite", constitution);
            StringAssert.Contains("PlayMode", constitution);
            StringAssert.Contains("textureRect", constitution);
            StringAssert.Contains("Image.sprite != null", constitution);
        }

        private static IEnumerator WaitForTask(System.Threading.Tasks.Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception;
            }
        }

        private static int CountVisibleAssetPixels(Sprite sprite, out string error)
        {
            error = null;
            if (sprite.texture == null)
            {
                error = "has no texture";
                return 0;
            }

            Rect rect = sprite.textureRect;
            int x = Mathf.FloorToInt(rect.xMin);
            int y = Mathf.FloorToInt(rect.yMin);
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            if (width <= 0 || height <= 0)
            {
                error = "has empty textureRect";
                return 0;
            }

            try
            {
                Color[] pixels = sprite.texture.GetPixels(x, y, width, height);
                int expected = width * height;
                if (pixels == null || pixels.Length != expected)
                {
                    error = "pixel count mismatch";
                    return 0;
                }

                int visible = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a > 0.01f)
                    {
                        visible++;
                    }
                }

                if (visible <= 0)
                {
                    error = "has zero non-transparent source pixels";
                }

                return visible;
            }
            catch (UnityException ex)
            {
                error = "pixels are not readable: " + ex.Message;
                return 0;
            }
        }

        private static bool RenderSpriteAndReadVisiblePixels(Transform canvasRoot, Camera camera, RenderTexture renderTexture, Texture2D readback, Sprite sprite)
        {
            for (int i = canvasRoot.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(canvasRoot.GetChild(i).gameObject);
            }

            var imageGo = new GameObject("RenderedSprite", typeof(RectTransform), typeof(Image));
            imageGo.transform.SetParent(canvasRoot, false);
            var rect = (RectTransform)imageGo.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(64f, 64f);

            var image = imageGo.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            readback.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            readback.Apply(false);
            RenderTexture.active = previous;

            Color[] pixels = readback.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                if (pixel.a > 0.01f && (pixel.r > 0.01f || pixel.g > 0.01f || pixel.b > 0.01f))
                {
                    Object.DestroyImmediate(imageGo);
                    return true;
                }
            }

            Object.DestroyImmediate(imageGo);
            return false;
        }
    }
}
