using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Objectives;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class StatusActivityObjectiveSourceTests
    {
        [Test]
        public void StatusActivityObjectiveSource_ExistsAsHintsSource()
        {
            var type = Type.GetType("Rootborn.UI.Objectives.StatusActivityObjectiveSource, Rootborn.UI");
            Assert.IsNotNull(type, "Status activity guidance must be owned by a standalone Objective Journal source.");
            var source = (IObjectiveJournalSource)Activator.CreateInstance(type);

            Assert.AreEqual("status-activity", source.SourceId);
            Assert.AreEqual("Hints", source.Category.CategoryId);
        }

        [Test]
        public void StatusActivityObjectiveSource_EmitsStableHintForChangedStatus()
        {
            using (var fixture = new StatusActivityFixture(statusValue: 1))
            {
                var source = CreateSource();

                var items = source.BuildItems(fixture.Context);

                Assert.AreEqual(1, items.Length);
                Assert.AreEqual("status-activity:" + fixture.Status.Id, items[0].StableKey);
                Assert.AreEqual("Hints", items[0].CategoryId);
                Assert.AreEqual(fixture.Status.DisplayNameKey, items[0].Title);
                StringAssert.Contains(fixture.Status.Id, items[0].Subtitle);
                StringAssert.Contains("next", items[0].NextHintText.ToLowerInvariant());
            }
        }

        [Test]
        public void StatusActivityObjectiveSource_DoesNotEmitHintBeforeStatusChanges()
        {
            using (var fixture = new StatusActivityFixture(statusValue: 0))
            {
                var source = CreateSource();

                var items = source.BuildItems(fixture.Context);

                Assert.IsEmpty(items);
            }
        }

        [Test]
        public void StatusActivityObjectiveSource_StableKeyDoesNotCollideWithExistingHintFamilies()
        {
            using (var fixture = new StatusActivityFixture(statusValue: 2))
            {
                var aggregator = new ObjectiveJournalAggregator(new IObjectiveJournalSource[]
                {
                    new StaticSource(new ObjectiveJournalItem("resource-recipe:recipe.town.field-notes", "Hints", "Recipe", string.Empty, "Revealed", string.Empty, string.Empty, false, 0)),
                    new StaticSource(new ObjectiveJournalItem("daily-plan-route-hint:daily.workshop.shift", "Hints", "Route", string.Empty, "Available", string.Empty, string.Empty, false, 0)),
                    CreateSource()
                });

                var result = aggregator.Build(fixture.Context);

                Assert.IsTrue(result.TryGetValue("Hints", out var hints));
                Assert.AreEqual(1, CountStableKey(hints, "status-activity:" + fixture.Status.Id));
                Assert.AreEqual(1, CountStableKey(hints, "resource-recipe:recipe.town.field-notes"));
                Assert.AreEqual(1, CountStableKey(hints, "daily-plan-route-hint:daily.workshop.shift"));
            }
        }

        private static IObjectiveJournalSource CreateSource()
        {
            var type = Type.GetType("Rootborn.UI.Objectives.StatusActivityObjectiveSource, Rootborn.UI");
            Assert.IsNotNull(type, "StatusActivityObjectiveSource must exist.");
            return (IObjectiveJournalSource)Activator.CreateInstance(type);
        }

        private static int CountStableKey(ObjectiveJournalItem[] items, string stableKey)
        {
            int count = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (string.Equals(items[i].StableKey, stableKey, StringComparison.Ordinal)) count++;
            }

            return count;
        }

        private sealed class StatusActivityFixture : IDisposable
        {
            private readonly GameObject _player;
            private readonly GameDataRegistry _registry;

            public StatusActivityFixture(int statusValue)
            {
                Status = ScriptableObject.CreateInstance<StatusDefinition>();
                Status.ConfigureForTests("status.focused", "Focused");
                _registry = ScriptableObject.CreateInstance<GameDataRegistry>();
                SetStatuses(_registry, new[] { Status });

                _player = new GameObject("Player");
                _player.AddComponent<PlayerIdentity>().Configure("player-status");
                var student = _player.AddComponent<StudentLifeProgressComponent>();
                student.ConfigureForTests("slot-status", "player-status", 100, 100, 0, 480);
                if (statusValue != 0)
                {
                    student.Progress.AddStatus(Status, statusValue, "test.status.activity");
                }

                Context = new ObjectiveJournalContext(_registry, _player, null);
            }

            public StatusDefinition Status { get; }
            public ObjectiveJournalContext Context { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_player);
                UnityEngine.Object.DestroyImmediate(_registry);
                UnityEngine.Object.DestroyImmediate(Status);
            }
        }

        private sealed class StaticSource : IObjectiveJournalSource
        {
            private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Hints", "Hints");
            private readonly ObjectiveJournalItem _item;

            public StaticSource(ObjectiveJournalItem item)
            {
                _item = item;
            }

            public string SourceId => "static-" + _item.StableKey;
            public ObjectiveJournalCategory Category => CategoryValue;
            public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context) => new[] { _item };
        }

        private static void SetStatuses(GameDataRegistry registry, StatusDefinition[] statuses)
        {
            var field = typeof(GameDataRegistry).GetField("_studentConditionStatuses", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(registry, statuses);
        }
    }
}
