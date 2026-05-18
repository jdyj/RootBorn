using NUnit.Framework;
using Rootborn.Game.DiscoveryClues;
using Rootborn.UI.DiscoveryClues;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class DiscoveryClueUiSurfaceTests
    {
        [Test]
        public void DISCOVERY_CLUE_UI_001_LogPanelRendersSummaryModelsWithStyle2Cards()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var canvas = canvasObject.GetComponent<Canvas>();
            var model = MakeModel("source.board", DiscoveryClueSourceKind.BoardPost);

            var panel = DiscoveryClueLogPanel.EnsureInScene(canvas);
            panel.Show(new[] { model });

            Assert.AreEqual(1, panel.CardCountForTests);
            StringAssert.Contains("Library archive rumor", panel.TextForTests);
            StringAssert.Contains("BoardPost", panel.TextForTests);
            Assert.IsNotNull(canvasObject.transform.Find("DiscoveryClueLogPanel/DiscoveryClueLogRoot").GetComponent<Rootborn.UI.Modern.ModernUiTileImage>());

            Object.DestroyImmediate(canvasObject);
        }

        [Test]
        public void DISCOVERY_CLUE_UI_002_SourcePanelRendersButtonSummariesAndInvokesSelectedModel()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var canvas = canvasObject.GetComponent<Canvas>();
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<Rootborn.Game.Common.PassiveInputModule>();
            var model = MakeModel("source.board", DiscoveryClueSourceKind.BoardPost);
            DiscoveryClueSummaryModel selected = default;

            var panel = DiscoveryClueSourcePanel.EnsureInScene(canvas);
            panel.Show(new[] { model }, value => selected = value);
            var button = panel.GetButtonForTests(0);
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);

            Assert.AreEqual(1, panel.ButtonCountForTests);
            Assert.AreEqual("clue.library.archive-rumor", selected.ClueId);
            StringAssert.Contains("BoardPost", panel.TextForTests);

            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(eventSystem);
        }

        private static DiscoveryClueSummaryModel MakeModel(string sourceId, DiscoveryClueSourceKind sourceKind)
        {
            return new DiscoveryClueSummaryModel(
                "clue.library.archive-rumor",
                sourceId,
                sourceKind,
                "Library archive rumor",
                "A faded notice points toward the archive.",
                "A rumor hints at old records.",
                "??? archive record",
                3,
                10,
                true,
                false,
                false,
                false);
        }
    }
}
