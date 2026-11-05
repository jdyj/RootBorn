using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    public sealed class DailyPlanPanelTests
    {
        [Test]
        public void Bind_NoRoutes_ShowsEmptyStateAndCommonPanelChrome()
        {
            var go = new GameObject("DailyPlanPanel", typeof(RectTransform));
            var panel = go.AddComponent<DailyPlanPanel>();

            panel.Bind(System.Array.Empty<DailyPlanRouteDefinition>(), null, null);

            Assert.That(panel.EmptyStateTextForTests, Is.EqualTo("objective.daily.choose.empty"));
            Assert.That(panel.RouteRowCountForTests, Is.EqualTo(0));
            Assert.That(panel.UsesBoundedRouteListForTests, Is.True);
            Assert.That(go.GetComponent<ModernUiTileImage>(), Is.Not.Null);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Bind_Routes_CreatesBoundedSelectableRows()
        {
            var go = new GameObject("DailyPlanPanel", typeof(RectTransform));
            var panel = go.AddComponent<DailyPlanPanel>();
            var route = CreateRoute("daily.library.self-study", "daily.route.library");

            panel.Bind(new[] { route }, null, null);

            Assert.That(panel.EmptyStateTextForTests, Is.Empty);
            Assert.That(panel.RouteRowCountForTests, Is.EqualTo(1));
            Assert.That(panel.UsesBoundedRouteListForTests, Is.True);
            Assert.That(panel.GetRouteButtonForTests(0), Is.Not.Null);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void RouteButton_Click_SelectsAndSavesRoute()
        {
            var go = new GameObject("DailyPlanPanel", typeof(RectTransform));
            var panel = go.AddComponent<DailyPlanPanel>();
            var progressGo = new GameObject("Progress");
            var daily = progressGo.AddComponent<DailyPlanProgressComponent>();
            daily.Load("slot-a", "player-a", 1);
            var studentGo = new GameObject("Student");
            var student = studentGo.AddComponent<StudentLifeProgressComponent>();
            student.ConfigureForTests("slot-a", "player-a", 100, 100, 0, 8 * 60);
            var route = CreateRoute("daily.library.self-study", "daily.route.library");

            panel.Bind(new[] { route }, daily, student);
            panel.GetRouteButtonForTests(0).onClick.Invoke();

            Assert.That(daily.Progress.SelectedRouteId, Is.EqualTo("daily.library.self-study"));
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(progressGo);
            Object.DestroyImmediate(studentGo);
        }

        [Test]
        public void RouteButton_Click_ClosesPanelAfterSelection()
        {
            var go = new GameObject("DailyPlanPanel", typeof(RectTransform));
            var panel = go.AddComponent<DailyPlanPanel>();
            string runId = System.Guid.NewGuid().ToString("N");
            var progressGo = new GameObject("Progress");
            var daily = progressGo.AddComponent<DailyPlanProgressComponent>();
            daily.Load("slot-close-" + runId, "player-close", 1);
            var studentGo = new GameObject("Student");
            var student = studentGo.AddComponent<StudentLifeProgressComponent>();
            student.ConfigureForTests("slot-close-" + runId, "player-close", 100, 100, 0, 8 * 60);
            var route = CreateRoute("daily.library.close-check." + runId, "daily.route.library");
            go.SetActive(true);

            panel.Bind(new[] { route }, daily, student);
            panel.GetRouteButtonForTests(0).onClick.Invoke();

            Assert.That(go.activeSelf, Is.False);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(progressGo);
            Object.DestroyImmediate(studentGo);
        }

        [Test]
        public void BoardInteractor_TryInteract_OpensPanel()
        {
            var panelGo = new GameObject("DailyPlanPanel");
            var panel = panelGo.AddComponent<DailyPlanPanel>();
            panelGo.SetActive(false);
            var boardGo = new GameObject("DailyPlanBoard");
            var board = boardGo.AddComponent<DailyPlanBoardInteractor>();
            board.Bind(panel, null, null, null);

            var result = board.TryInteract(new GameObject("Player"));

            Assert.That(result, Is.True);
            Assert.That(board.InteractionPrompt, Is.EqualTo("[E] " + Rootborn.Game.Localization.GameText.Localized("ui.prompt.plan-day", "Plan Day")));
            Assert.That(panelGo.activeSelf, Is.True);
            Object.DestroyImmediate(panelGo);
            Object.DestroyImmediate(boardGo);
        }

        private static DailyPlanRouteDefinition CreateRoute(string id, string displayNameKey)
        {
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            route.ConfigureForTests(
                id,
                displayNameKey,
                null,
                LocationGrowthRoute.SelfStudy,
                System.Array.Empty<LocationActivityDefinition>(),
                System.Array.Empty<DailyPlanRouteRequirementBase>(),
                System.Array.Empty<DailyPlanRouteOutcomeBase>(),
                "objective." + id + ".title",
                "objective." + id + ".detail",
                "objective." + id + ".next");
            return route;
        }
    }
}
