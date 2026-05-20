using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Save;
using Rootborn.UI.Objectives;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class ObjectiveJournalProviderTests
    {
        [Test]
        public void ObjectiveJournalAggregator_BuildsSortedItemsByCategoryFromSources()
        {
            var goals = new ObjectiveJournalCategory("Goals", "Goals");
            var quests = new ObjectiveJournalCategory("Quests", "Quests");
            var sources = new IObjectiveJournalSource[]
            {
                new FakeSource("quest-source", quests, new[]
                {
                    new ObjectiveJournalItem("quest.late", "Quests", "Late Quest", string.Empty, "Active", "2 / 3", "Continue", false, 20),
                    new ObjectiveJournalItem("quest.early", "Quests", "Early Quest", string.Empty, "Active", "0 / 1", "Start", false, 5),
                }),
                new FakeSource("goal-source", goals, new[]
                {
                    new ObjectiveJournalItem("goal.daily", "Goals", "Daily Goal", string.Empty, "Active", "0 / 1", "Pick an action", false, 0),
                }),
            };

            var aggregator = new ObjectiveJournalAggregator(sources);
            var result = aggregator.Build(new ObjectiveJournalContext(null, null, null));

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("goal.daily", result["Goals"][0].StableKey);
            Assert.AreEqual("quest.early", result["Quests"][0].StableKey);
            Assert.AreEqual("quest.late", result["Quests"][1].StableKey);
        }

        [Test]
        public void ObjectiveJournalAggregator_AppliesItemsAndRestoresTrackedStableKeyToPanelHud()
        {
            var source = new FakeSource("quest-source", new ObjectiveJournalCategory("Quests", "Quests"), new[]
            {
                new ObjectiveJournalItem("quest.target", "Quests", "Target Quest", "Detail", "Active", "1 / 2", "Do next step", false, 0),
            });
            var aggregator = new ObjectiveJournalAggregator(new IObjectiveJournalSource[] { source });
            var root = new GameObject("ObjectiveJournalProviderFixture", typeof(RectTransform));

            try
            {
                var panelGo = new GameObject("ObjectiveJournalPanel", typeof(RectTransform));
                panelGo.transform.SetParent(root.transform, false);
                var panel = panelGo.AddComponent<ObjectiveJournalPanel>();

                var hudGo = new GameObject("TrackedObjectiveHud", typeof(RectTransform));
                hudGo.transform.SetParent(root.transform, false);
                var hud = hudGo.AddComponent<TrackedObjectiveHud>();
                panel.BindTrackedHud(hud);

                aggregator.Refresh(panel, new ObjectiveJournalContext(null, null, null), "quest.target");

                Assert.IsTrue(hud.IsVisible);
                StringAssert.Contains("Target Quest", hud.VisibleText);
                StringAssert.Contains("1 / 2", hud.VisibleText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ObjectiveJournalAggregator_HidesTrackedHudWhenTrackedStableKeyDisappears()
        {
            var activeSource = new FakeSource("goal-source", new ObjectiveJournalCategory("Goals", "Goals"), new[]
            {
                new ObjectiveJournalItem("daily-plan:route", "Goals", "Daily Route", string.Empty, "Selected", "Selected for day 1", "Do the route", false, 0),
            });
            var emptySource = new FakeSource("goal-source", new ObjectiveJournalCategory("Goals", "Goals"), Array.Empty<ObjectiveJournalItem>());
            var root = new GameObject("ObjectiveJournalTrackedRemovalFixture", typeof(RectTransform));

            try
            {
                var panelGo = new GameObject("ObjectiveJournalPanel", typeof(RectTransform));
                panelGo.transform.SetParent(root.transform, false);
                var panel = panelGo.AddComponent<ObjectiveJournalPanel>();

                var hudGo = new GameObject("TrackedObjectiveHud", typeof(RectTransform));
                hudGo.transform.SetParent(root.transform, false);
                var hud = hudGo.AddComponent<TrackedObjectiveHud>();
                panel.BindTrackedHud(hud);

                new ObjectiveJournalAggregator(new IObjectiveJournalSource[] { activeSource }).Refresh(panel, new ObjectiveJournalContext(null, null, null), "daily-plan:route");
                Assert.IsTrue(hud.IsVisible);

                new ObjectiveJournalAggregator(new IObjectiveJournalSource[] { emptySource }).Refresh(panel, new ObjectiveJournalContext(null, null, null), "daily-plan:route");

                Assert.IsFalse(hud.IsVisible, "TrackedObjectiveHud should hide when its tracked stable key no longer exists in active Objective Journal sources.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ObjectiveJournalTrackingStore_RoundTripsTrackedStableKeyPerSaveSlot()
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "rootborn-objective-tracking-" + Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(rootDirectory);

            try
            {
                var metadata = new SaveSlotMetadata { SlotId = "slot-objective", DisplayName = "Objective Slot" };
                ObjectiveJournalTrackingStore.Save(metadata, "Quests", "quest.target");

                Assert.IsTrue(ObjectiveJournalTrackingStore.TryLoad(metadata, out var categoryId, out var stableKey));
                Assert.AreEqual("Quests", categoryId);
                Assert.AreEqual("quest.target", stableKey);
                Assert.IsFalse(ObjectiveJournalTrackingStore.TryLoad(new SaveSlotMetadata { SlotId = "slot-empty" }, out _, out _));
            }
            finally
            {
                SaveService.SetRootDirectoryForTests(null);
                if (Directory.Exists(rootDirectory)) Directory.Delete(rootDirectory, true);
            }
        }

        [Test]
        public void ObjectiveJournalPanel_TrackSelectedItemPersistsStableKeyForActiveSaveSlot()
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "rootborn-objective-track-action-" + Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(rootDirectory);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot-track-action", DisplayName = "Track Action" });
            var root = new GameObject("ObjectiveJournalTrackPersistenceFixture", typeof(RectTransform));

            try
            {
                var panelGo = new GameObject("ObjectiveJournalPanel", typeof(RectTransform));
                panelGo.transform.SetParent(root.transform, false);
                var panel = panelGo.AddComponent<ObjectiveJournalPanel>();

                var hudGo = new GameObject("TrackedObjectiveHud", typeof(RectTransform));
                hudGo.transform.SetParent(root.transform, false);
                panel.BindTrackedHud(hudGo.AddComponent<TrackedObjectiveHud>());

                panel.SetItems("Quests", new[]
                {
                    new ObjectiveJournalItem("quest.persisted", "Quests", "Persisted Quest", string.Empty, "Active", "0 / 1", "Continue", false, 0),
                });
                panel.Show();
                panel.SelectCategory("Quests");
                panel.SelectItem(0);
                panel.TrackSelectedItem();

                Assert.IsTrue(ObjectiveJournalTrackingStore.TryLoad(ActiveSaveContext.Metadata, out var categoryId, out var stableKey));
                Assert.AreEqual("Quests", categoryId);
                Assert.AreEqual("quest.persisted", stableKey);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                ActiveSaveContext.Clear();
                SaveService.SetRootDirectoryForTests(null);
                if (Directory.Exists(rootDirectory)) Directory.Delete(rootDirectory, true);
            }
        }

        [Test]
        public void ObjectiveJournalPanel_TrackButtonTracksClickedRowAndPersistsStableKey()
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "rootborn-objective-track-button-" + Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(rootDirectory);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot-track-button", DisplayName = "Track Button" });
            var root = new GameObject("ObjectiveJournalTrackButtonFixture", typeof(RectTransform));

            try
            {
                var panelGo = new GameObject("ObjectiveJournalPanel", typeof(RectTransform));
                panelGo.transform.SetParent(root.transform, false);
                var panel = panelGo.AddComponent<ObjectiveJournalPanel>();

                var hudGo = new GameObject("TrackedObjectiveHud", typeof(RectTransform));
                hudGo.transform.SetParent(root.transform, false);
                var hud = hudGo.AddComponent<TrackedObjectiveHud>();
                panel.BindTrackedHud(hud);

                panel.SetItems("Quests", new[]
                {
                    new ObjectiveJournalItem("quest.first", "Quests", "First Quest", string.Empty, "Active", "0 / 1", "Continue first", false, 0),
                    new ObjectiveJournalItem("quest.second", "Quests", "Second Quest", string.Empty, "Active", "1 / 2", "Continue second", false, 1),
                });
                panel.Show();
                panel.SelectCategory("Quests");

                FindObjectiveRow(root.transform, 1).GetComponent<Button>().onClick.Invoke();
                root.transform.Find("ObjectiveJournalPanel/ObjectiveJournalRoot/ObjectiveDetail/ObjectiveTrackButton").GetComponent<Button>().onClick.Invoke();

                Assert.IsTrue(hud.IsVisible);
                StringAssert.Contains("Second Quest", hud.VisibleText);
                StringAssert.Contains("1 / 2", hud.VisibleText);
                Assert.IsTrue(ObjectiveJournalTrackingStore.TryLoad(ActiveSaveContext.Metadata, out var categoryId, out var stableKey));
                Assert.AreEqual("Quests", categoryId);
                Assert.AreEqual("quest.second", stableKey);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                ActiveSaveContext.Clear();
                SaveService.SetRootDirectoryForTests(null);
                if (Directory.Exists(rootDirectory)) Directory.Delete(rootDirectory, true);
            }
        }

        private static Transform FindObjectiveRow(Transform root, int index)
        {
            var row = root.Find("ObjectiveJournalPanel/ObjectiveJournalRoot/ObjectiveList/ObjectiveListViewport/ObjectiveListContent/ObjectiveRow_" + index)
                ?? root.Find("ObjectiveJournalPanel/ObjectiveJournalRoot/ObjectiveList/ObjectiveRow_" + index);
            Assert.IsNotNull(row, "Missing ObjectiveRow_" + index + ".");
            return row;
        }

        private sealed class FakeSource : IObjectiveJournalSource
        {
            private readonly ObjectiveJournalItem[] _items;

            public FakeSource(string sourceId, ObjectiveJournalCategory category, ObjectiveJournalItem[] items)
            {
                SourceId = sourceId;
                Category = category;
                _items = items ?? Array.Empty<ObjectiveJournalItem>();
            }

            public string SourceId { get; }
            public ObjectiveJournalCategory Category { get; }

            public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
            {
                return _items;
            }
        }
    }
}
