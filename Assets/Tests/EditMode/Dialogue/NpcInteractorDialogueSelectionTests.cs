using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Dialogue;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Dialogue
{
    public sealed class NpcInteractorDialogueSelectionTests
    {
        [Test]
        public void NpcInteractor_StageDialogueOverridesScheduleFallbackDialogue()
        {
            var go = new GameObject("NpcInteractorDialogueSelectionFixture");
            var day2 = ScriptableObject.CreateInstance<TutorialStageDefinition>();
            var defaultDialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            var day2Dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            var npc = ScriptableObject.CreateInstance<NpcDefinition>();
            try
            {
                day2.ConfigureForTests("tutorial.day2", "tutorial.day2", "Day 2 guide text");
                SetField(defaultDialogue, "_lineKeys", new[] { "dialogue.guide.day1" });
                SetField(day2Dialogue, "_lineKeys", new[] { "dialogue.guide.day2" });
                SetField(day2Dialogue, "_requiredTutorialStage", day2);
                SetField(npc, "_defaultDialogue", defaultDialogue);
                SetField(npc, "_stageDialogues", new[] { day2Dialogue });

                var progress = new StudentLifeProgress("slot-dialogue", "player-dialogue", 10, 10);
                progress.SetTutorialStage(day2, "goal.talk-to-guide-day2");

                var interactor = go.AddComponent<NpcInteractor>();
                interactor.Bind(npc);
                interactor.BindSchedule(null, "time.morning", null, "dialogue.first-guide.morning", string.Empty);

                interactor.Interact(progress);

                Assert.AreSame(day2Dialogue, interactor.Session.Current, "Tutorial-stage dialogue must override schedule fallback dialogue so saved day-two tutorial data changes the visible NPC conversation.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(day2);
                Object.DestroyImmediate(defaultDialogue);
                Object.DestroyImmediate(day2Dialogue);
                Object.DestroyImmediate(npc);
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail(fieldName);
        }
    }
}
