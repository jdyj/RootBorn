using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Careers;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.UI.Careers;
using Rootborn.UI.Quests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Dialogue
{
    // Reuses the real callback-chain fixture, not a mock presentation service.
    // Import/run after slice2, before implementing choice or talk authority guards.
    public sealed class NpcDialogueAuthorityTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private NpcDialogueContextTests _fixture;
        private DialogueDefinition _choiceDialogue;
        private DialogueChoiceDefinition _choice;
        private Scene _otherScene;
        private GameObject _foreign;
        private AdultCareerRuntimeComponent Owner => Get<AdultCareerRuntimeComponent>("_owner");
        private NpcInteractor Npc => Get<NpcInteractor>("_npc");
        private QuestLog Log => Get<QuestLog>("_log");
        private QuestDefinition Quest => Get<QuestDefinition>("_quest");
        private DialoguePanel Panel => Get<DialoguePanel>("_panel");

        [SetUp]
        public void Setup()
        {
            _choice = null; _choiceDialogue = null; _foreign = null; _otherScene = default;
            _fixture = new NpcDialogueContextTests(); _fixture.Setup();
            InvokeFixture("WireLegacyQuestBinder"); InvokeFixture("WireLocationRunner");
        }

        [TearDown]
        public void Cleanup()
        {
            if (_foreign != null) Object.DestroyImmediate(_foreign);
            _fixture?.Cleanup();
            if (_choice != null) Object.DestroyImmediate(_choice);
            if (_choiceDialogue != null) Object.DestroyImmediate(_choiceDialogue);
            if (_otherScene.IsValid() && _otherScene.isLoaded) EditorSceneManager.ClosePreviewScene(_otherScene);
        }

        [Test]
        public void NPC_AUTHORITY_001_LostOwnerLeaseRejectsAVisibleAcceptChoice()
        {
            OpenChoice(DialogueQuestAction.AcceptQuest);
            AdultCareerPersistenceRouter.Unregister(Owner);
            Assert.That(Panel.Choose(0), Is.False, "Authority must be checked when a visible choice executes.");
            Assert.That(Log.GetState(Quest), Is.EqualTo(QuestState.NotStarted));
        }

        [Test]
        public void NPC_AUTHORITY_002_ChangedSaveSlotRejectsThePreviousConversation()
        {
            OpenChoice(DialogueQuestAction.AcceptQuest);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "other-dialogue-slot" });
            Assert.That(Panel.Choose(0), Is.False);
            Assert.That(Log.GetState(Quest), Is.EqualTo(QuestState.NotStarted));
        }

        [Test]
        public void NPC_AUTHORITY_003_MovedActorCannotExecuteTheOldSceneChoice()
        {
            OpenChoice(DialogueQuestAction.AcceptQuest);
            _otherScene = EditorSceneManager.NewPreviewScene(); _otherScene.name = "OtherDialogueScene";
            SceneManager.MoveGameObjectToScene(Owner.gameObject, _otherScene);
            Assert.That(Panel.Choose(0), Is.False);
            Assert.That(Log.GetState(Quest), Is.EqualTo(QuestState.NotStarted));
        }

        [Test]
        public void NPC_AUTHORITY_004_ForeignTalkCannotCompleteTheBoundOwnersAcceptedQuest()
        {
            Assert.That(Log.Accept(Quest), Is.True);
            _foreign = new GameObject("ForeignDialogueActor"); _foreign.AddComponent<PlayerIdentity>().Configure("foreign");
            SceneManager.MoveGameObjectToScene(_foreign, Npc.gameObject.scene);
            Npc.TryInteract(_foreign);
            Assert.That(Log.GetState(Quest), Is.EqualTo(QuestState.Active), "Reject before the legacy sink and quest bridge record talk.");
            Assert.That(Panel.IsOpen, Is.False);
        }

        [Test]
        public void NPC_AUTHORITY_005_LostOwnerLeaseRejectsRewardBeforeChangingInventoryOrReceipt()
        {
            Assert.That(Log.Accept(Quest), Is.True);
            OpenChoice(DialogueQuestAction.ClaimReward);
            Assert.That(Log.GetState(Quest), Is.EqualTo(QuestState.Completed));
            string before = JsonUtility.ToJson(Owner.PlayerInventory.ToSaveData());
            AdultCareerPersistenceRouter.Unregister(Owner);
            Assert.That(Panel.Choose(0), Is.False);
            Assert.That(Log.GetState(Quest), Is.EqualTo(QuestState.Completed));
            Assert.That(JsonUtility.ToJson(Owner.PlayerInventory.ToSaveData()), Is.EqualTo(before));
        }

        private void OpenChoice(DialogueQuestAction action)
        {
            Assert.That(Npc.TryInteract(Owner.gameObject), Is.True);
            Assert.That(Panel.IsOpen, Is.True);
            _choice = ScriptableObject.CreateInstance<DialogueChoiceDefinition>();
            Set(_choice, "_questAction", action); Set(_choice, "_quest", Quest);
            _choiceDialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            _choiceDialogue.ConfigureForTests(Array.Empty<string>(), new[] { _choice }, Array.Empty<DialogueCondition>());
            // Exercise real Choose after the real callbacks; avoid unrelated EditMode button Destroy.
            Set(Panel, "_dialogue", _choiceDialogue);
        }
        private T Get<T>(string field) => (T)typeof(NpcDialogueContextTests).GetField(field, PrivateInstance).GetValue(_fixture);
        private void InvokeFixture(string method) => typeof(NpcDialogueContextTests).GetMethod(method, PrivateInstance).Invoke(_fixture, null);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    }
}
