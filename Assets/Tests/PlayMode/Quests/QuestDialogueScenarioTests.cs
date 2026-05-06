using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Dialogue;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Quests
{
    public sealed class QuestDialogueScenarioTests
    {
        [UnityTest]
        public IEnumerator QUEST_001_NpcInteraction_OpensAndClosesDialogue()
        {
            var npc = new GameObject("NPC");
            var interactor = npc.AddComponent<NpcInteractor>();
            var dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            var npcDef = ScriptableObject.CreateInstance<NpcDefinition>();
            SetField(npcDef, "_defaultDialogue", dialogue);
            interactor.Bind(npcDef);

            interactor.Interact();
            yield return null;

            Assert.IsTrue(interactor.Session.IsOpen);
            Assert.AreSame(dialogue, interactor.Session.Current);

            interactor.Close();
            yield return null;

            Assert.IsFalse(interactor.Session.IsOpen);
            Object.Destroy(npc);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }
    }
}
