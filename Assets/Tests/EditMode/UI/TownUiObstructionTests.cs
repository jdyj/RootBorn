using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.Story;
using Rootborn.UI.Modern;
using Rootborn.UI.Objectives;
using Rootborn.UI.Quests;
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
        public void InstalledLegacyQuestStore_IsHiddenButStillFeedsTheObjectiveJournal()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            try
            {
                quest.ConfigureForRuntime("ui-obstruction-regression", "Visible journal quest", "A real journal row", null, null, null);
                typeof(GameDataRegistry).GetField("_quests", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(registry, new[] { quest });
                var runner = new GameObject("QuestFixtureRunner");
                var runnerType = typeof(QuestHudAutoFiller).GetNestedType("QuestHudAutoFillRunner", BindingFlags.NonPublic);
                Assert.IsNotNull(runnerType);
                var install = runnerType.GetMethod("EnsureQuestUi", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(install);
                install.Invoke(null, new object[] { registry, runner, null, null, new StoryFlagSet() });

                var panel = _testScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<QuestLogPanel>(true)).Single();
                Assert.IsFalse(panel.gameObject.activeInHierarchy, "Entering Town must not open an obsolete quest window over the world.");
                Assert.IsNotNull(panel.QuestLog, "Hiding the obsolete surface must retain quest/dialogue/reward state.");
                Assert.AreSame(quest, panel.Quests.Single());
                var source = new QuestLogObjectiveSource();
                Assert.IsTrue(source.BuildItems(new ObjectiveJournalContext(registry, null, null)).Any(item => item.StableKey == "quest.ui-obstruction-regression"),
                    "The inactive quest store must remain discoverable by the current Objective Journal owner.");

                var dialogue = _testScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DialoguePanel>(true)).Single();
                var dialogueRect = (RectTransform)dialogue.transform;
                Assert.AreEqual(new Vector2(0.5f, 0f), dialogueRect.anchorMin);
                Assert.AreEqual(new Vector2(0.5f, 0f), dialogueRect.anchorMax);
                Assert.AreEqual(new Vector2(0.5f, 0f), dialogueRect.pivot);
                Assert.AreEqual(new Vector2(0f, 36f), dialogueRect.anchoredPosition,
                    "Legacy fallback dialogue must grow upward inside the screen when choices require a taller panel.");

                var canvas = panel.GetComponentInParent<Canvas>();
                ModernUiPanelAutoInstaller.InstallOnCanvas(canvas, null);
                var provider = canvas.GetComponent<ObjectiveJournalRuntimeProvider>();
                var router = canvas.GetComponent<ModernUiPanelInputRouter>();
                Assert.IsNotNull(provider);
                Assert.IsNotNull(router);
                Assert.IsFalse(provider.Panel.IsVisible);
                router.ToggleObjectiveJournalPanel();
                Assert.IsTrue(provider.Panel.IsVisible, "The existing journal input route must remain available.");
                Assert.IsFalse(panel.gameObject.activeInHierarchy);
            }
            finally
            {
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
