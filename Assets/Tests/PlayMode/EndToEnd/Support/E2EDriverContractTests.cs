using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.UI.Objectives;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd.Support
{
    public sealed class E2EDriverContractTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (EventSystem.current == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                go.AddComponent<PassiveInputModule>();
            }
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerFlowE2EDriver.DestroyAllNamed("EventSystem");
            PlayerFlowE2EDriver.DestroyAllNamed("ContractRoot");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerFlowE2EDriver_ClickButtonInvokesVisibleInteractableButton()
        {
            // A real screen canvas and EventSystem: ClickButton presses with a mouse device through the UI module.
            var root = new GameObject("ContractRoot", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            if (UnityEngine.EventSystems.EventSystem.current == null)
                new GameObject("ContractEventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)).transform.SetParent(root.transform, false);
            var buttonGo = new GameObject("TargetButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(root.transform, false);
            bool clicked = false;
            buttonGo.GetComponent<Button>().onClick.AddListener(() => clicked = true);

            yield return null;
            yield return PlayerFlowE2EDriver.ClickButton(buttonGo.GetComponent<Button>());

            Assert.IsTrue(clicked);
            Object.Destroy(root);
        }

        [Test]
        public void PlayerFlowE2EDriver_FindButtonFailsClearlyWhenMissing()
        {
            var root = new GameObject("ContractRoot", typeof(RectTransform));
            try
            {
                var ex = Assert.Throws<AssertionException>(() => PlayerFlowE2EDriver.FindButton(root.transform, "MissingButton"));
                StringAssert.Contains("UI button missing: MissingButton", ex.Message);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayerFlowE2EDriver_FindButtonFailureListsAvailableButtonNamesAndText()
        {
            var root = new GameObject("ContractRoot", typeof(RectTransform));
            try
            {
                CreateButton(root.transform, "AcceptButton", "Accept Quest");
                CreateButton(root.transform, "TrackButton", "Track Route");

                var ex = Assert.Throws<AssertionException>(() => PlayerFlowE2EDriver.FindButton(root.transform, "MissingButton"));

                StringAssert.Contains("UI button missing: MissingButton", ex.Message);
                StringAssert.Contains("Available buttons:", ex.Message);
                StringAssert.Contains("AcceptButton", ex.Message);
                StringAssert.Contains("Accept Quest", ex.Message);
                StringAssert.Contains("TrackButton", ex.Message);
                StringAssert.Contains("Track Route", ex.Message);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator ScreenshotEvidence_WaitsForNonZeroFile()
        {
            string path = Path.Combine(Application.temporaryCachePath, "rootborn-screenshot-contract.png");
            if (File.Exists(path)) File.Delete(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });

            yield return ScreenshotEvidence.WaitForNonZeroFile(path, 0.5f, "contract screenshot missing");

            Assert.Greater(new FileInfo(path).Length, 0);
            File.Delete(path);
        }

        [UnityTest]
        public IEnumerator ObjectiveJournalE2EDriver_SelectsCategoryAndRowByVisibleButtons()
        {
            // A real screen canvas and EventSystem: the driver clicks with a mouse device, not by calling handlers.
            var root = new GameObject("ContractRoot", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            if (UnityEngine.EventSystems.EventSystem.current == null)
                new GameObject("ContractEventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)).transform.SetParent(root.transform, false);
            var panelGo = new GameObject("ObjectiveJournalPanel", typeof(RectTransform));
            panelGo.transform.SetParent(root.transform, false);
            var panel = panelGo.AddComponent<ObjectiveJournalPanel>();
            panel.SetItems("Goals", new[]
            {
                new ObjectiveJournalItem("goal.one", "Goals", "Goal One", string.Empty, "Active", "0 / 1", "Next", false, 0),
                new ObjectiveJournalItem("goal.two", "Goals", "Goal Two", string.Empty, "Active", "1 / 2", "Next", false, 1),
            });
            panel.Show();
            yield return null;

            yield return ObjectiveJournalE2EDriver.SelectCategoryAndRow(panel, "Goals", 1);

            StringAssert.Contains("Goal Two", panel.VisibleText);
            Object.DestroyImmediate(root);
        }

        private static void CreateButton(Transform parent, string name, string label)
        {
            var buttonGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, false);
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(buttonGo.transform, false);
            labelGo.GetComponent<Text>().text = label;
        }
    }
}