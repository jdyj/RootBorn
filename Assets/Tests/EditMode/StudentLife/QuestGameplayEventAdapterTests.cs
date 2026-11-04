using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class QuestGameplayEventAdapterTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private readonly List<QuestEvent> _events = new List<QuestEvent>();
        private readonly List<StudentLifeProgress> _players = new List<StudentLifeProgress>();
        private string _tempRoot;
        private EventInfo _recorded;
        private Action<QuestEvent, StudentLifeProgress> _handler;
        private GameObject _player;
        private StudentLifeProgressComponent _student;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "rootborn-quest-adapters", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
            SaveService.SetRootDirectoryForTests(_tempRoot);
            _player = Own(new GameObject("QuestAdapterPlayer"));
            _student = _player.AddComponent<StudentLifeProgressComponent>();
            _student.ConfigureForTests("adapter-slot", "adapter-player", 20, 20, 0, 480);
            _events.Clear();
            _players.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (_recorded != null && _handler != null) _recorded.RemoveEventHandler(null, _handler);
            _recorded = null;
            _handler = null;
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            SaveService.SetRootDirectoryForTests(null);
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true);
        }

        [Test]
        public void AllowedLocationInteraction_PublishesExactLocationAndPlayer_AndARevisitHasANewEventKey()
        {
            Subscribe();
            var location = MakeLocation();
            var interactor = Own(new GameObject("Location")).AddComponent<LocationVisitInteractor>();
            interactor.Bind(location);
            Assert.That(interactor.TryInteract(_player), Is.True);
            Assert.That(_events.Count, Is.EqualTo(1));
            Assert.That(_events[0].Kind.ToString(), Is.EqualTo("LocationVisit"));
            Assert.That(Payload(_events[0], "Location"), Is.SameAs(location));
            Assert.That(_players[0], Is.SameAs(_student.Progress));
            Assert.That(_events[0].EventKey, Is.Not.Empty);
            Assert.That(interactor.TryInteract(_player), Is.True);
            Assert.That(_events.Count, Is.EqualTo(2), "Revisiting after accepting a later quest must remain possible.");
            Assert.That(_events[1].EventKey, Is.Not.EqualTo(_events[0].EventKey));
        }

        [Test]
        public void RejectedLocationInteraction_DoesNotPublishQuestProgress()
        {
            Subscribe();
            var rule = Own(ScriptableObject.CreateInstance<AlwaysClosedQuestTestLocationRule>());
            var location = MakeLocation();
            location.ConfigureForTests("fixture.closed", "closed", "closed", null, "closed", Vector2.zero,
                null, null, null, new LocationVisitRuleBase[] { rule });
            var interactor = Own(new GameObject("ClosedLocation")).AddComponent<LocationVisitInteractor>();
            interactor.Bind(location);
            Assert.That(interactor.TryInteract(_player), Is.False);
            Assert.That(_events, Is.Empty);
        }

        [Test]
        public void WorkSuccess_PublishesAfterPayment_AndDuplicateRequestPublishesNothing()
        {
            Subscribe();
            var wage = MakeItem();
            var work = MakeWork(wage, 1);
            var inventory = _player.AddComponent<PlayerInventory>();
            var interactor = Own(new GameObject("Work")).AddComponent<PartTimeWorkInteractor>();
            interactor.Bind(work);
            int paidWhenObserved = -1;
            Action<QuestEvent, StudentLifeProgress> observer = (e, p) => paidWhenObserved = inventory.Inventory.CountOf(wage);
            _recorded.AddEventHandler(null, observer);
            try
            {
                Assert.That(interactor.Interact(_student, inventory, "work-request"), Is.True);
                Assert.That(paidWhenObserved, Is.EqualTo(1), "Quest credit may only observe committed work income.");
                Assert.That(_events.Count, Is.EqualTo(1));
                Assert.That(_events[0].Kind.ToString(), Is.EqualTo("PartTimeWork"));
                Assert.That(Payload(_events[0], "Work"), Is.SameAs(work));
                Assert.That(_players[0], Is.SameAs(_student.Progress));
                Assert.That(_events[0].EventKey, Is.Not.Empty);
                Assert.That(interactor.Interact(_student, inventory, "work-request"), Is.False);
                Assert.That(_events.Count, Is.EqualTo(1));
            }
            finally { _recorded.RemoveEventHandler(null, observer); }
        }

        [Test]
        public void WorkRejectedForResources_DoesNotPublishQuestProgressOrPay()
        {
            Subscribe();
            var wage = MakeItem();
            var work = MakeWork(wage, 99);
            var inventory = _player.AddComponent<PlayerInventory>();
            var interactor = Own(new GameObject("Work")).AddComponent<PartTimeWorkInteractor>();
            interactor.Bind(work);
            Assert.That(interactor.Interact(_student, inventory, "rejected-work"), Is.False);
            Assert.That(_events, Is.Empty);
            Assert.That(inventory.Inventory.CountOf(wage), Is.EqualTo(0));
        }

        [Test]
        public void NpcInteraction_PublishesTalkWithoutLegacyQuestSink_AndFreshKeysAllowLaterSteps()
        {
            Subscribe();
            var npc = Own(ScriptableObject.CreateInstance<NpcDefinition>());
            var interactor = Own(new GameObject("Npc")).AddComponent<NpcInteractor>();
            interactor.Bind(npc);
            Assert.That(interactor.TryInteract(_player), Is.True);
            Assert.That(_events.Count, Is.EqualTo(1));
            Assert.That(_events[0].Kind, Is.EqualTo(QuestEventKind.Talk));
            Assert.That(_events[0].Npc, Is.SameAs(npc));
            Assert.That(_players[0], Is.SameAs(_student.Progress));
            Assert.That(interactor.TryInteract(_player), Is.True);
            Assert.That(_events.Count, Is.EqualTo(2));
            Assert.That(_events[1].EventKey, Is.Not.EqualTo(_events[0].EventKey));
        }

        [Test]
        public void DailyPlanSuccess_PublishesSelectedRoute_AndRejectedSelectionDoesNotPublish()
        {
            Subscribe();
            var component = _player.AddComponent<DailyPlanProgressComponent>();
            component.Load("adapter-slot", "adapter-player", 1);
            var route = Own(ScriptableObject.CreateInstance<DailyPlanRouteDefinition>());
            route.ConfigureForTests("route.fixture", "fixture", null, LocationGrowthRoute.SelfStudy,
                null, null, null, "title", "detail", "next");
            Assert.That(component.TrySelect(route, _student.Progress), Is.True);
            Assert.That(_events.Count, Is.EqualTo(1));
            Assert.That(_events[0].Kind.ToString(), Is.EqualTo("DailyPlanSelected"));
            Assert.That(Payload(_events[0], "DailyPlanRoute"), Is.SameAs(route));
            Assert.That(_players[0], Is.SameAs(_student.Progress));
            Assert.That(component.TrySelect(null, _student.Progress), Is.False);
            Assert.That(_events.Count, Is.EqualTo(1));
        }

        private void Subscribe()
        {
            var bridge = typeof(QuestEvent).Assembly.GetType("Rootborn.Game.Quests.QuestGameplayEventBridge");
            Assert.That(bridge, Is.Not.Null, "Gameplay events need a shared bridge for Town and House.");
            _recorded = bridge.GetEvent("OnRecorded", BindingFlags.Public | BindingFlags.Static);
            Assert.That(_recorded, Is.Not.Null);
            _handler = (e, p) => { _events.Add(e); _players.Add(p); };
            _recorded.AddEventHandler(null, _handler);
        }
        private static object Payload(QuestEvent value, string field)
        {
            var member = typeof(QuestEvent).GetField(field, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(member, Is.Not.Null, "QuestEvent." + field + " must carry an SO reference.");
            return member.GetValue(value);
        }
        private LocationDefinition MakeLocation()
        {
            var location = Own(ScriptableObject.CreateInstance<LocationDefinition>());
            location.ConfigureForTests("location.fixture", "fixture", Vector2.zero, null);
            return location;
        }
        private ItemDefinition MakeItem()
        {
            var item = Own(ScriptableObject.CreateInstance<ItemDefinition>());
            Set(item, "_id", "item.fixture-wage");
            Set(item, "_maxStack", 99);
            return item;
        }
        private PartTimeWorkDefinition MakeWork(ItemDefinition wage, int energy)
        {
            var workplace = Own(ScriptableObject.CreateInstance<WorkplaceDefinition>());
            workplace.ConfigureForTests("workplace.fixture", "fixture", "location.fixture", "npc.fixture", null);
            var work = Own(ScriptableObject.CreateInstance<PartTimeWorkDefinition>());
            work.ConfigureForTests("work.fixture", "fixture", workplace, 1, energy, 0, 0,
                new[] { new InventoryGrant(wage, 1) }, null, null);
            return work;
        }
        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
        private T Own<T>(T value) where T : Object { _objects.Add(value); return value; }
    }

    public sealed class AlwaysClosedQuestTestLocationRule : LocationVisitRuleBase
    {
        public override bool IsSatisfied(StudentLifeProgress progress) => false;
    }
}
