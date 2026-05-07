using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode
{
    public sealed class FarmCharacterHudReferenceExactMatchPlayModeTests
    {
        private const string ReferencePath = "docs/art/reference/pixelwood-reference-frame0-top-left.png";
        private const string EvidenceDirectory = "Builds/Logs/modern-ui";

        [UnityTest]
        public IEnumerator FarmScene_TopLeftHudCropMatchesReferencePixelsStrictly()
        {
            yield return SceneManager.LoadSceneAsync("Farm");
            yield return Managers.BootstrapAsync().AsIEnumerator();
            var fillerGo = new GameObject("[FarmAutoFiller-ExactMatchTest]");
            fillerGo.AddComponent<FarmAutoFiller>().FillIfEmpty();
            yield return WaitForHud();
            yield return new WaitForEndOfFrame();

            Assert.IsTrue(File.Exists(ReferencePath), ReferencePath);
            var reference = LoadTexture(ReferencePath);
            try
            {
                string cropPath = CaptureTopLeftCrop("farm-top-left-hud-exact-crop.png", reference.width, reference.height);
                var candidate = LoadTexture(cropPath);
                try
                {
                    Assert.AreEqual(reference.width, candidate.width);
                    Assert.AreEqual(reference.height, candidate.height);
                    float exactMatchRatio = ExactMatchRatio(reference, candidate);
                    Assert.GreaterOrEqual(exactMatchRatio, 0.95f,
                        "Top-left HUD crop must be visually locked to the reference frame before the 100% match goal can be considered complete.");
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

        private static IEnumerator WaitForHud()
        {
            for (int frame = 0; frame < 1200; frame++)
            {
                var thumbnail = GameObject.Find("TopLeftCharacterHud")?.transform.Find("CharacterThumbnailFrame/CharacterThumbnail");
                if (thumbnail != null && CountHudPartImages(thumbnail) >= 3) yield break;
                yield return null;
            }
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

        private static float ExactMatchRatio(Texture2D reference, Texture2D candidate)
        {
            var referencePixels = reference.GetPixels32();
            var candidatePixels = candidate.GetPixels32();
            Assert.AreEqual(referencePixels.Length, candidatePixels.Length);
            int exact = 0;
            for (int i = 0; i < referencePixels.Length; i++)
            {
                if (referencePixels[i].Equals(candidatePixels[i])) exact++;
            }
            return exact / (float)referencePixels.Length;
        }
    }
}
