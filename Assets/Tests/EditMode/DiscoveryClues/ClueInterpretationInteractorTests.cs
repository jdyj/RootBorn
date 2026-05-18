using System;
using NUnit.Framework;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.DiscoveryClues;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rootborn.Tests.EditMode.DiscoveryClues
{
    public sealed class ClueInterpretationInteractorTests
    {
        [Test]
        public void CLUE_INTERPRET_EDIT_012_InteractorOpensChoicePanelAndCompletesSelectedInterpretationThroughUiCallback()
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<Rootborn.Game.Common.PassiveInputModule>();
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var player = new GameObject("Player", typeof(PlayerIdentity), typeof(PlayerInventory), typeof(StudentLifeProgressComponent));
            player.GetComponent<PlayerIdentity>().Configure("player-a");
            player.GetComponent<StudentLifeProgressComponent>().ConfigureForTests("slot-a", "player-a", 10, 10, 0, 0);
            SaveService.SetRootDirectoryForTests(System.IO.Path.Combine(Application.temporaryCachePath, "rootborn-clue-interpretation-interactor", Guid.NewGuid().ToString("N")));
            var clue = MakeClue();
            var source = MakeSource();
            var policy = ScriptableObject.CreateInstance<ClueInterpretationPolicyDefinition>();
            policy.ConfigureForTests("policy", ClueInterpretationPolicyKind.NonExclusive, "group", 0, 0, false);
            var interpretation = ScriptableObject.CreateInstance<ClueInterpretationDefinition>();
            interpretation.ConfigureForTests("interpret.ask-librarian", "Ask Librarian", "desc", clue, "Ask", "public", "hidden", new[] { source }, Array.Empty<ClueInterpretationConditionBase>(), Array.Empty<ClueInterpretationOutcomeBase>(), policy, 0);
            var interactorObject = new GameObject("InterpretationInteractor", typeof(BoxCollider2D));
            var interactor = interactorObject.AddComponent<ClueInterpretationInteractor>();
            interactor.Bind(new[] { interpretation }, ClueInterpretationSourceKind.NpcDialogue, "npc.librarian", new QuestLog(Array.Empty<QuestDefinition>()));

            Assert.IsTrue(interactor.TryInteract(player));
            var panel = canvasObject.GetComponentInChildren<ClueInterpretationChoicePanel>();
            Assert.IsNotNull(panel);
            Assert.AreEqual(1, panel.ButtonCountForTests);
            ExecuteEvents.Execute(panel.GetButtonForTests(0).gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);

            Assert.AreEqual(ClueInterpretationResultKind.Completed, interactor.LastResult.Kind);
            Assert.IsTrue(ClueInterpretationProgressPersistence.LoadOrCreate("slot-a", "player-a").GetRecord("interpret.ask-librarian").Completed);

            SaveService.SetRootDirectoryForTests(null);
            UnityEngine.Object.DestroyImmediate(eventSystem);
            UnityEngine.Object.DestroyImmediate(canvasObject);
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(interactorObject);
            UnityEngine.Object.DestroyImmediate(clue);
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(policy);
            UnityEngine.Object.DestroyImmediate(interpretation);
        }

        private static DiscoveryClueDefinition MakeClue()
        {
            var clue = ScriptableObject.CreateInstance<DiscoveryClueDefinition>();
            clue.ConfigureForTests("clue.discovery.play-loop", "Play clue", "desc", null, null, null, 1, "public", "hidden", Array.Empty<DiscoveryClueSourceDefinition>(), Array.Empty<DiscoveryClueConditionBase>(), Array.Empty<DiscoveryClueCompletionBase>(), Array.Empty<DiscoveryClueOutcomeBase>(), Array.Empty<DiscoveryClueSummarySurface>(), 0);
            return clue;
        }

        private static ClueInterpretationSourceDefinition MakeSource()
        {
            var source = ScriptableObject.CreateInstance<ClueInterpretationSourceDefinition>();
            source.ConfigureForTests("source", ClueInterpretationSourceKind.NpcDialogue, "npc.librarian", "Librarian", "Ask", 1);
            return source;
        }
    }
}
