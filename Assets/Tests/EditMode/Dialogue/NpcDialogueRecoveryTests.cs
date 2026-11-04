using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Careers;
using Rootborn.Game.Save;
using Rootborn.UI.Careers;
using Rootborn.UI.Quests;
using Rootborn.UI.World;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Dialogue
{
    public sealed class NpcDialogueRecoveryTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private NpcDialogueContextTests _fixture;
        private AdultCareerRuntimeComponent Owner => Get<AdultCareerRuntimeComponent>("_owner");
        [SetUp] public void Setup()
        {
            _fixture = new NpcDialogueContextTests(); _fixture.Setup();
            Assert.That(Owner.SaveCurrent(), Is.True);
            new SaveService(Owner.Progress.SaveSlot).WriteJson("quest-log-actor.json", "{ invalid legacy quest json");
        }
        [TearDown] public void Cleanup() => _fixture?.Cleanup();

        [Test]
        public void NPC_RECOVERY_001_CorruptLegacyLogCannotBecomeFreshClaimableDialogueState()
        {
            Assert.That(NpcDialoguePresenter.BindSceneOwner(Owner.gameObject.scene, Owner), Is.False);
            var panel = Get<DialoguePanel>("_panel");
            Assert.That(panel.GetComponent<NpcDialoguePresenter>().IsBound, Is.False);
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(Owner.CanMutate, Is.True, "Legacy dialogue corruption must not replace the valid aggregate owner.");
        }

        [Test]
        public void NPC_RECOVERY_002_AuthoredOwnerBinderPropagatesDialogueFailureWithoutChangingPrimary()
        {
            var storage = new SaveService(Owner.Progress.SaveSlot);
            string before = JsonUtility.ToJson(AdultCareerSnapshotPersistence.Load(storage, Owner.Progress.PlayerId));
            bool bound = AuthoredWorldOwnerBinder.Bind(Owner.gameObject.scene, Owner).GetAwaiter().GetResult();
            Assert.That(bound, Is.False, "Travel recovery must observe failed dialogue binding instead of reporting silent success.");
            Assert.That(Owner.CanMutate, Is.True);
            Assert.That(JsonUtility.ToJson(AdultCareerSnapshotPersistence.Load(storage, Owner.Progress.PlayerId)), Is.EqualTo(before));
            Assert.That(storage.ReadJson("quest-log-actor.json"), Is.EqualTo("{ invalid legacy quest json"));
        }

        private T Get<T>(string field) => (T)typeof(NpcDialogueContextTests).GetField(field, PrivateInstance).GetValue(_fixture);
    }
}
