using NUnit.Framework;
using Rootborn.Game.DiscoveryClues;
using Rootborn.UI.DiscoveryClues;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class ClueInterpretationUiSurfaceTests
    {
        [Test]
        public void CLUE_INTERPRET_UI_001_ChoicePanelRendersSummaryModelsAndInvokesSelectedModel()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var canvas = canvasObject.GetComponent<Canvas>();
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<Rootborn.Game.Common.PassiveInputModule>();
            var available = MakeModel("interpret.ask-librarian", "Ask Librarian", true, false, "");
            var locked = MakeModel("interpret.restore-workbench", "Restore at Workbench", false, false, "Need a tool");
            ClueInterpretationSummaryModel selected = default;

            var panel = ClueInterpretationChoicePanel.EnsureInScene(canvas);
            panel.Show(new[] { available, locked }, value => selected = value);
            ExecuteEvents.Execute(panel.GetButtonForTests(0).gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);

            Assert.AreEqual(2, panel.ButtonCountForTests);
            Assert.AreEqual("interpret.ask-librarian", selected.InterpretationId);
            StringAssert.Contains("Ask Librarian", panel.TextForTests);
            StringAssert.Contains("Restore at Workbench", panel.TextForTests);
            StringAssert.Contains("Need a tool", panel.TextForTests);
            Assert.IsNotNull(canvasObject.transform.Find("ClueInterpretationChoicePanel/ClueInterpretationChoiceRoot").GetComponent<Rootborn.UI.Modern.ModernUiTileImage>());

            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(eventSystem);
        }

        private static ClueInterpretationSummaryModel MakeModel(string interpretationId, string displayName, bool available, bool completed, string lockedReason)
        {
            return new ClueInterpretationSummaryModel(
                "clue.discovery.play-loop",
                interpretationId,
                interpretationId + ".source",
                ClueInterpretationSourceKind.NpcDialogue,
                "npc.librarian",
                displayName,
                "Ask",
                "Ask about the clue",
                "The clue opens a new path.",
                "???",
                available,
                false,
                completed,
                completed,
                lockedReason,
                ClueInterpretationPolicyKind.NonExclusive,
                "policy.group.play-loop",
                10);
        }
    }
}
