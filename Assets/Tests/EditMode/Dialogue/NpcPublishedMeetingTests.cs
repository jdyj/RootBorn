using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Careers;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Dialogue
{
    public sealed class NpcPublishedMeetingTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private NpcDialogueContextTests _fixture;
        private GameObject _serviceObject;
        private QuestChainRuntimeService _service;
        private LocationDefinition _location;
        private AdultCareerRuntimeComponent Owner => Get<AdultCareerRuntimeComponent>("_owner");
        private NpcInteractor Npc => Get<NpcInteractor>("_npc");
        private NpcDefinition Definition => Get<NpcDefinition>("_definition");
        private GameDataRegistry Registry => Get<GameDataRegistry>("_registry");

        [SetUp]
        public void Setup()
        {
            _fixture = new NpcDialogueContextTests(); _fixture.Setup();
            _location = ScriptableObject.CreateInstance<LocationDefinition>();
            _location.ConfigureForTests("location.dialogue", "Location", Vector2.zero, null);
            Npc.BindSchedule(_location, "morning", Get<DialogueDefinition>("_defaultDialogue"), null, "notice.meeting");
            _serviceObject = new GameObject("RealQuestEventService");
            SceneManager.MoveGameObjectToScene(_serviceObject, Owner.gameObject.scene);
            _service = _serviceObject.AddComponent<QuestChainRuntimeService>();
            Set(_service, "_registry", Registry); Set(_service, "_activeSlot", Owner.Progress.SaveSlot);
            // Supply the already-authoritative owner to the production cache. No Managers or global player search.
            var owners = (Dictionary<string, AdultCareerRuntimeComponent>)typeof(QuestChainRuntimeService).GetField("_owners", PrivateInstance).GetValue(_service);
            string slot = Owner.Progress.SaveSlot;
            owners[slot.Length + ":" + slot + Owner.Progress.PlayerId] = Owner;
            typeof(QuestChainRuntimeService).GetMethod("BindEvents", PrivateInstance).Invoke(_service, null);
        }

        [TearDown]
        public void Cleanup()
        {
            if (_service != null) typeof(QuestChainRuntimeService).GetMethod("OnDisable", PrivateInstance).Invoke(_service, null);
            if (_serviceObject != null) Object.DestroyImmediate(_serviceObject);
            if (_location != null) Object.DestroyImmediate(_location);
            _fixture?.Cleanup();
        }

        [Test]
        public void NPC_MEETING_001_RealSynchronousQuestPublicationKeepsFirstMeetingOnCurrentAndSavedStudent()
        {
            var before = Owner.StudentComponent.Progress; int commits = 0;
            Owner.Committed += () => commits++;
            Assert.That(Npc.TryInteract(Owner.gameObject), Is.True);
            Assert.That(commits, Is.EqualTo(1), "The actual quest event service commits synchronously during NPC interaction.");
            Assert.That(Owner.StudentComponent.Progress, Is.Not.SameAs(before), "Exercise the actual published replacement, not a no-op bridge.");
            Assert.That(new LocationVisitProgress(Owner.StudentComponent.Progress).HasMet(Definition), Is.True);
            Assert.That(new LocationVisitProgress(LoadStudent()).HasMet(Definition), Is.True);
        }

        [Test]
        public void NPC_MEETING_002_RealSynchronousQuestPublicationPersistsScheduleMeetingAndNoticeOnce()
        {
            Assert.That(Npc.TryInteract(Owner.gameObject), Is.True);
            Assert.That(new NpcScheduleProgress(Owner.StudentComponent.Progress).HasMet(Definition, _location, "morning"), Is.True);
            var loaded = LoadStudent();
            Assert.That(new NpcScheduleProgress(loaded).HasMet(Definition, _location, "morning"), Is.True);
            CollectionAssert.Contains(loaded.GetTodayResultLogIds(), "notice.meeting");
            Assert.That(Npc.TryInteract(Owner.gameObject), Is.True);
            Assert.That(Npc.LastVisitResult.Kind, Is.EqualTo(LocationVisitResultKind.DuplicateMeeting));
            Assert.That(new NpcScheduleProgress(LoadStudent()).MeetingRecordIds.Length, Is.EqualTo(1));
        }

        private StudentLifeProgress LoadStudent()
        {
            string slot = Owner.Progress.SaveSlot, player = Owner.Progress.PlayerId;
            var snapshot = AdultCareerSnapshotPersistence.Load(new SaveService(slot), player);
            Assert.That(snapshot, Is.Not.Null);
            var session = AdultCareerSnapshotPersistence.Restore(snapshot, slot, player, Registry.Items, Registry.StudentLifeCalendar);
            Assert.That(session, Is.Not.Null); return session.Student;
        }
        private T Get<T>(string field) => (T)typeof(NpcDialogueContextTests).GetField(field, PrivateInstance).GetValue(_fixture);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    }
}
