using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Network.Session;
using Rootborn.UI.Careers;
using Rootborn.UI.Quests;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class QuestChainOwnerLeaseTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private string _root; private AppConfig _oldConfig; private SaveSlotMetadata _oldMetadata; private INetworkSession _oldSession;
        private GameDataRegistry _registry; private LifeCalendarDefinition _calendar; private QuestChainRuntimeService _quests;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-quest-owner-" + Guid.NewGuid().ToString("N"));
            _oldConfig = GameBootstrap.Config; _oldMetadata = ActiveSaveContext.Metadata; _oldSession = NetworkBootstrap.ActiveSession;
            typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, null);
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, new AppConfig { Mode = SessionMode.Single, SaveSlot = "lease-slot" });
            SaveService.SetRootDirectoryForTests(_root); ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "lease-slot" });
            _calendar = Make<LifeCalendarDefinition>(); _calendar.ConfigureForTests(2, 3);
            _registry = Make<GameDataRegistry>(); Set(_registry, "_studentLifeCalendar", _calendar);
            var service = new GameObject("LeaseQuestService"); _objects.Add(service);
            _quests = service.AddComponent<QuestChainRuntimeService>(); Set(_quests, "_registry", _registry);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); ActiveSaveContext.Set(_oldMetadata); SaveService.SetRootDirectoryForTests(null);
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, _oldConfig);
            typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, _oldSession);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void CachedQuestOwner_IsReplacedWhenNewPortalActorOwnsSamePlayer()
        {
            var original = Actor("LeavingTownPlayer");
            Assert.That(_quests.GetLog(original.StudentComponent.Progress), Is.SameAs(original.QuestChains));
            var arriving = Actor("ArrivingHousePlayer");
            Assert.That(_quests.GetLog(arriving.StudentComponent.Progress), Is.SameAs(arriving.QuestChains),
                "A still-alive previous actor cannot remain the journal's authoritative log.");
        }

        [Test]
        public void QuestCommandAfterPortalOverlap_UsesNewOwnerAndDoesNotLoseTheCommand()
        {
            var original = Actor("OldPlayer"); _quests.GetLog(original.StudentComponent.Progress);
            var arriving = Actor("NewPlayer");
            var trait = Make<TraitDefinition>(); trait.ConfigureForTests("trait.quest-owner", "Quest owner");
            Assert.That(_quests.ExecuteUserCommand(arriving.StudentComponent.Progress, (log, rewards) =>
            {
                rewards.StudentLifeProgress.AddTrait(trait, 1); return true;
            }), Is.True, "Stale cached authority must not swallow a valid command for the new actor.");
            Assert.That(arriving.StudentComponent.Progress.GetTraitValue(trait), Is.EqualTo(1));
            Assert.That(original.StudentComponent.Progress.GetTraitValue(trait), Is.Zero);
        }

        private AdultCareerRuntimeComponent Actor(string name)
        {
            var player = new GameObject(name); _objects.Add(player);
            player.AddComponent<PlayerIdentity>().Configure(PlayerIdentity.DefaultPlayerId);
            var student = player.AddComponent<StudentLifeProgressComponent>(); student.ConfigureForTests("lease-slot", PlayerIdentity.DefaultPlayerId, 10, 10, 0, 480);
            student.Progress.ConfigureCalendar(_calendar); player.AddComponent<PlayerInventory>();
            var owner = player.AddComponent<AdultCareerRuntimeComponent>(); owner.Bind(_registry, new SaveService("lease-slot"));
            Assert.That(owner.IsBound, Is.True); return owner;
        }
        private T Make<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
