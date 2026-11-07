using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.UI.Modern;
using Rootborn.UI.Objectives;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class TownUiObstructionTests
    {
        private Scene _previousScene;
        private Scene _testScene;
        private SaveSlotMetadata _previousSave;

        [SetUp]
        public void SetUp()
        {
            _previousSave = ActiveSaveContext.Metadata;
            ActiveSaveContext.Clear();
            _previousScene = SceneManager.GetActiveScene();
            // Unity Test Runner owns and restores the editor scene around this fixture.
            _testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(_testScene);
        }

        [TearDown]
        public void TearDown()
        {
            if (_previousScene.IsValid() && _previousScene.isLoaded) SceneManager.SetActiveScene(_previousScene);
            if (_testScene.IsValid()) EditorSceneManager.CloseScene(_testScene, true);
            ActiveSaveContext.Set(_previousSave);
        }

        [Test]
        public void TownHudCompatibilityAnchor_DoesNotCreateAnEmptyStretchedImage()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            InvokeTownHud(canvas.transform);
            var hud = canvas.transform.Find("TownHud");
            Assert.IsNotNull(hud, "Keep the existing anchor for scene consumers.");
            Assert.IsNull(hud.GetComponent<Image>(), "A single Style2 atlas corner must not be stretched into a blank HUD panel.");
        }

        [Test]
        public void ExistingTownHud_StopsRenderingItsObsoleteBackground()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var hud = new GameObject("TownHud", typeof(RectTransform), typeof(Image));
            hud.transform.SetParent(canvas.transform, false);
            InvokeTownHud(canvas.transform);
            Assert.IsFalse(hud.GetComponent<Image>().enabled);
            Assert.IsFalse(hud.GetComponent<Image>().raycastTarget);
        }

        [Test]
        public void QuestSessionFeedsTheObjectiveJournalWithoutAnyQuestWindow()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            PlayerGlobalState.ClearForTests();
            try
            {
                quest.ConfigureForRuntime("ui-obstruction-regression", "Visible journal quest", "A real journal row", null, null, null);
                typeof(GameDataRegistry).GetField("_quests", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(registry, new[] { quest });

                var source = new QuestLogObjectiveSource();
                Assert.IsTrue(source.BuildItems(new ObjectiveJournalContext(registry, null, null)).Any(item => item.StableKey == "quest.ui-obstruction-regression"),
                    "The player's quest session must reach the Objective Journal with no quest window in the scene.");

                var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
                ModernUiPanelAutoInstaller.InstallOnCanvas(canvas, null);
                var provider = canvas.GetComponent<ObjectiveJournalRuntimeProvider>();
                var router = canvas.GetComponent<ModernUiPanelInputRouter>();
                Assert.IsNotNull(provider);
                Assert.IsNotNull(router);
                Assert.IsFalse(provider.Panel.IsVisible, "Entering a scene must not open a quest list over the world.");
                router.ToggleObjectiveJournalPanel();
                Assert.IsTrue(provider.Panel.IsVisible, "The existing journal input route must remain available.");
                Assert.IsEmpty(_testScene.GetRootGameObjects().Where(root => root.name.StartsWith("Quest")).ToArray(),
                    "No separate quest window is created any more.");
            }
            finally
            {
                PlayerGlobalState.ClearForTests();
                UnityEngine.Object.DestroyImmediate(registry);
                UnityEngine.Object.DestroyImmediate(quest);
            }
        }

        private static void InvokeTownHud(Transform canvas)
        {
            var method = typeof(TownPlayableBaselineRuntimeInstaller).GetMethod("EnsureTownHud", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { canvas });
        }
    }
}
