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

        private static void AssertMissingOrInactive(string objectName)
        {
            var go = FindByNameIncludingInactive(objectName);
            if (go == null) return;
            Assert.IsFalse(go.activeInHierarchy, objectName + " should not block the default Farm gameplay view.");
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
