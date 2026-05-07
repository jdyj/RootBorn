using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode
{
    public sealed class FarmCharacterHudOverlayPlayModeTests
    {
        [UnityTest]
        public IEnumerator FarmScene_InstallsTopLeftLayeredCharacterThumbnailHud()
        {
            yield return SceneManager.LoadSceneAsync("Farm");
            yield return Managers.BootstrapAsync().AsIEnumerator();
            var fillerGo = new GameObject("[FarmAutoFiller-Test]");
            fillerGo.AddComponent<FarmAutoFiller>().FillIfEmpty();
            yield return WaitForHud();

            var hud = GameObject.Find("TopLeftCharacterHud");
            Assert.IsNotNull(hud);
            Assert.IsNotNull(hud.transform.Find("CharacterThumbnailFrame"));
            var thumbnail = hud.transform.Find("CharacterThumbnailFrame/CharacterThumbnail");
            Assert.IsNotNull(thumbnail);
            Assert.GreaterOrEqual(CountHudPartImages(thumbnail), 3);
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
