using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    public sealed class DailyPlanRuntimeInstallerTests
    {
        [Test]
        public void EnsureSceneForTests_AddsOneBoardAndPanelOnlyOnce()
        {
            var scene = SceneManager.GetActiveScene();
            var beforePanels = CountComponents<DailyPlanPanel>(scene);
            var beforeBoards = CountComponents<DailyPlanBoardInteractor>(scene);
            var canvasGo = new GameObject("DailyPlanInstallerTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            var canvas = canvasGo.GetComponent<Canvas>();
            var player = new GameObject("DailyPlanInstallerTestPlayer", typeof(StudentLifeProgressComponent), typeof(DailyPlanProgressComponent));
            player.GetComponent<StudentLifeProgressComponent>().ConfigureForTests("slot-a", "player-a", 100, 100, 0, 8 * 60);
            SceneManager.MoveGameObjectToScene(player, scene);
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");

            try
            {
                DailyPlanRuntimeInstaller.EnsureSceneForTests(scene, canvas, registry, player);
                DailyPlanRuntimeInstaller.EnsureSceneForTests(scene, canvas, registry, player);

                Assert.That(CountComponents<DailyPlanPanel>(scene), Is.EqualTo(beforePanels + 1));
                Assert.That(CountComponents<DailyPlanBoardInteractor>(scene), Is.EqualTo(beforeBoards + 1));
            }
            finally
            {
                DestroyByName("DailyPlanInstallerTestCanvas");
                DestroyByName("DailyPlanInstallerTestPlayer");
                DestroyByName("DailyPlanBoard");
                DestroyByName("DailyPlanPanel");
            }
        }

        [Test]
        public void EnsureSceneForTests_AddsDailyPlanProgressComponentToRuntimePlayer()
        {
            var scene = SceneManager.GetActiveScene();
            var canvasGo = new GameObject("DailyPlanInstallerTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            var player = new GameObject("DailyPlanInstallerTestPlayer", typeof(StudentLifeProgressComponent));
            player.GetComponent<StudentLifeProgressComponent>().ConfigureForTests("slot-a", "player-a", 100, 100, 0, 8 * 60);
            SceneManager.MoveGameObjectToScene(player, scene);
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");

            try
            {
                DailyPlanRuntimeInstaller.EnsureSceneForTests(scene, canvasGo.GetComponent<Canvas>(), registry, player);

                Assert.IsNotNull(player.GetComponent<DailyPlanProgressComponent>(), "Town runtime installer must attach DailyPlanProgressComponent so player-facing board selection can persist.");
            }
            finally
            {
                DestroyByName("DailyPlanInstallerTestCanvas");
                DestroyByName("DailyPlanInstallerTestPlayer");
                DestroyByName("DailyPlanBoard");
                DestroyByName("DailyPlanPanel");
            }
        }

        [Test]
        public void EnsureSceneForTests_CreatesCanvasWhenSceneHasNone()
        {
            var scene = SceneManager.GetActiveScene();
            var beforeCanvases = CountComponents<Canvas>(scene);
            var player = new GameObject("DailyPlanInstallerTestPlayer", typeof(StudentLifeProgressComponent));
            player.GetComponent<StudentLifeProgressComponent>().ConfigureForTests("slot-a", "player-a", 100, 100, 0, 8 * 60);
            SceneManager.MoveGameObjectToScene(player, scene);
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");

            try
            {
                DailyPlanRuntimeInstaller.EnsureSceneForTests(scene, null, registry, player);

                Assert.That(CountComponents<Canvas>(scene), Is.EqualTo(beforeCanvases + 1));
                var panel = Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include);
                Assert.IsNotNull(panel);
                Assert.IsNotNull(panel.GetComponentInParent<Canvas>(), "Daily Plan panel must be under a Canvas for player-visible UI.");
            }
            finally
            {
                DestroyByName("DailyPlanInstallerTestPlayer");
                DestroyByName("DailyPlanBoard");
                DestroyByName("DailyPlanPanel");
                DestroyByName("DailyPlanCanvas");
            }
        }

        private static int CountComponents<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            var count = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                count += roots[i].GetComponentsInChildren<T>(true).Length;
            }

            return count;
        }

        private static void DestroyByName(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                var transform = transforms[i];
                if (transform != null && transform.name == objectName) Object.DestroyImmediate(transform.gameObject);
            }
        }
    }
}
