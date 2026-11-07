using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Placement;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Resources;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Tiles;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestObjectiveTests
    {
        [Test]
        public void QUEST_004_AllObjectivesRequiredBeforeCompletion()
        {
            var progress = new QuestProgress(objectiveCount: 2);
            progress.TryAccept();

            Assert.IsTrue(progress.TryAddObjectiveCount(0, 1, requiredCount: 1, eventKey: "event-a"));
            Assert.AreEqual(QuestState.Active, progress.State);

            Assert.IsTrue(progress.TryAddObjectiveCount(1, 1, requiredCount: 1, eventKey: "event-b"));
            Assert.AreEqual(QuestState.Completed, progress.State);
        }

        [Test]
        public void QUEST_007_DuplicateEventKey_DoesNotCountTwice()
        {
            var progress = new QuestProgress(objectiveCount: 1);
            progress.TryAccept();

            Assert.IsTrue(progress.TryAddObjectiveCount(0, 1, requiredCount: 2, eventKey: "gather-1"));
            Assert.IsFalse(progress.TryAddObjectiveCount(0, 1, requiredCount: 2, eventKey: "gather-1"));

            Assert.AreEqual(1, progress.GetObjectiveCount(0));
            Assert.AreEqual(QuestState.Active, progress.State);
        }

        [Test]
        public void QUEST_007_GatherObjective_MatchesResourceReference()
        {
            var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var other = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var objective = ScriptableObject.CreateInstance<GatherQuestObjective>();
            SetField(objective, "_targetResource", resource);

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.Gather, "g1", resource: resource)));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.Gather, "g2", resource: other)));
        }

        [Test]
        public void QUEST_007_GatherObjective_MatchesBundleCopyOfTheSameResourceById()
        {
            // Built Addressables load the quest's target from a bundle while the tree in the scene keeps its own copy.
            var sceneCopy = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var bundleCopy = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var otherResource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            SetField(sceneCopy, "_id", "resource.test-tree");
            SetField(bundleCopy, "_id", "resource.test-tree");
            SetField(otherResource, "_id", "resource.test-rock");
            var objective = ScriptableObject.CreateInstance<GatherQuestObjective>();
            SetField(objective, "_targetResource", bundleCopy);

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.Gather, "g1", resource: sceneCopy)));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.Gather, "g2", resource: otherResource)));
        }

        [Test]
        public void QUEST_009_CollectObjective_MatchesBundleCopyOfTheSameItemById()
        {
            var sceneCopy = ScriptableObject.CreateInstance<ItemDefinition>();
            var bundleCopy = ScriptableObject.CreateInstance<ItemDefinition>();
            var otherItem = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(sceneCopy, "_id", "item.test-wood");
            SetField(bundleCopy, "_id", "item.test-wood");
            SetField(otherItem, "_id", "item.test-stone");
            var objective = ScriptableObject.CreateInstance<CollectQuestObjective>();
            SetField(objective, "_targetItem", bundleCopy);

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.Collect, "c1", item: sceneCopy)));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.Collect, "c2", item: otherItem)));
        }

        [Test]
        public void QUEST_ID_001_TalkObjective_MatchesBundleCopyOfTheSameNpcById()
        {
            var (scene, bundle, other) = Copies<NpcDefinition>("_id", "npc.test-mom", "npc.test-teacher");
            var objective = ScriptableObject.CreateInstance<TalkQuestObjective>();
            SetField(objective, "_targetNpc", bundle);

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.Talk, "t1", npc: scene)));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.Talk, "t2", npc: other)));
        }

        [Test]
        public void QUEST_ID_002_LocationVisitObjective_MatchesBundleCopyOfTheSameLocationById()
        {
            var (scene, bundle, other) = Copies<LocationDefinition>("_id", "location.test-library", "location.test-park");
            var objective = ScriptableObject.CreateInstance<LocationVisitQuestObjective>();
            SetField(objective, "_targetLocation", bundle);

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.LocationVisit, "v1", location: scene)));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.LocationVisit, "v2", location: other)));
        }

        [Test]
        public void QUEST_ID_003_LocationActivityObjective_MatchesByIdAndAnyActivityWithoutTarget()
        {
            var (scene, bundle, other) = Copies<LocationActivityDefinition>("_id", "activity.test-read", "activity.test-run");
            var objective = ScriptableObject.CreateInstance<LocationActivityQuestObjective>();
            objective.ConfigureForRuntime("test", 1, bundle);

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.LocationActivity, "a1", activity: scene)));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.LocationActivity, "a2", activity: other)));

            objective.ConfigureForRuntime("test", 1, null);
            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.LocationActivity, "a3", activity: other)), "no target accepts any activity");
        }

        [Test]
        public void QUEST_ID_004_TilePlacedObjective_MatchesByIdAndAnyTileWithoutTarget()
        {
            var (scene, bundle, other) = Copies<PlaceableTileDefinition>("_id", "tile.test-wood", "tile.test-stone");
            var objective = ScriptableObject.CreateInstance<TilePlacedQuestObjective>();
            objective.ConfigureForRuntime("test", 1, bundle);

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.TilePlaced, "p1", tile: scene)));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.TilePlaced, "p2", tile: other)));

            objective.ConfigureForRuntime("test", 1, null);
            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.TilePlaced, "p3", tile: other)), "no target accepts any tile");
        }

        [Test]
        public void QUEST_ID_005_PartTimeWorkObjective_MatchesBundleCopyInTheAcceptedList()
        {
            var (scene, bundle, other) = Copies<PartTimeWorkDefinition>("_id", "work.test-cafe", "work.test-store");
            var objective = ScriptableObject.CreateInstance<PartTimeWorkQuestObjective>();
            SetField(objective, "_acceptedWorks", new[] { bundle });

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.PartTimeWork, "w1", work: scene)));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.PartTimeWork, "w2", work: other)));
        }

        [Test]
        public void QUEST_ID_006_FurniturePlacedObjective_MatchesByStableIdAndRequiredSurface()
        {
            var (scene, bundle, other) = Copies<FurnitureDefinition>("_stableId", "furniture.test-desk", "furniture.test-bed");
            var objective = ScriptableObject.CreateInstance<FurniturePlacedQuestObjective>();
            SetField(objective, "_acceptedFurniture", new[] { bundle });
            SetField(objective, "_requiredSurfaceId", "surface.test-room");

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.FurniturePlaced, "f1", furniture: scene, surfaceId: "surface.test-room")));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.FurniturePlaced, "f2", furniture: scene, surfaceId: "surface.test-yard")));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.FurniturePlaced, "f3", furniture: other, surfaceId: "surface.test-room")));
        }

        [Test]
        public void QUEST_ID_007_CareerInterestObjective_EmptyListAcceptsAnyDreamOtherwiseById()
        {
            var (scene, bundle, other) = Copies<CareerInterestDefinition>("_id", "career.test-chef", "career.test-nurse");
            var objective = ScriptableObject.CreateInstance<CareerInterestSelectedQuestObjective>();
            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.CareerInterestSelected, "c0", careerInterest: other)), "empty list accepts any dream");
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.CareerInterestSelected, "c1")), "but a dream must be chosen");

            var listed = ScriptableObject.CreateInstance<CareerInterestSelectedQuestObjective>();
            SetField(listed, "_acceptedInterests", new[] { bundle });
            Assert.IsTrue(listed.Matches(new QuestEvent(QuestEventKind.CareerInterestSelected, "c2", careerInterest: scene)));
            Assert.IsFalse(listed.Matches(new QuestEvent(QuestEventKind.CareerInterestSelected, "c3", careerInterest: other)));
        }

        [Test]
        public void QUEST_ID_009_QuestChainStartsWhenTalkingToAnyCopyOfItsStartNpc()
        {
            var (scene, bundle, other) = Copies<NpcDefinition>("_id", "npc.test-mom", "npc.test-teacher");
            var chain = ScriptableObject.CreateInstance<QuestChainDefinition>();
            SetField(chain, "_startNpc", bundle);

            Assert.IsTrue(chain.IsStartedByTalkingTo(scene));
            Assert.IsFalse(chain.IsStartedByTalkingTo(other));
            Assert.IsFalse(chain.IsStartedByTalkingTo(null));
            SetField(chain, "_startNpc", null);
            Assert.IsFalse(chain.IsStartedByTalkingTo(scene), "a chain without a start NPC is never started by talking");
        }

        [Test]
        public void QUEST_ID_008_DefinitionsWithoutIdOnlyMatchThemselves()
        {
            var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var lookAlike = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var objective = ScriptableObject.CreateInstance<GatherQuestObjective>();
            SetField(objective, "_targetResource", resource);

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.Gather, "n1", resource: resource)));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.Gather, "n2", resource: lookAlike)));
        }

        [Test]
        public void QUEST_008_DefeatObjective_MatchesTargetReference()
        {
            var target = ScriptableObject.CreateInstance<ScriptableObject>();
            var other = ScriptableObject.CreateInstance<ScriptableObject>();
            var objective = ScriptableObject.CreateInstance<DefeatQuestObjective>();
            SetField(objective, "_target", target);

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.Defeat, "d1", defeatTarget: target)));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.Defeat, "d2", defeatTarget: other)));
        }

        [Test]
        public void QUEST_009_CollectObjective_MatchesItemReference()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var other = ScriptableObject.CreateInstance<ItemDefinition>();
            var objective = ScriptableObject.CreateInstance<CollectQuestObjective>();
            SetField(objective, "_targetItem", item);

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.Collect, "c1", item: item)));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.Collect, "c2", item: other)));
        }

        private static void SetField(object target, string fieldName, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }

            Assert.Fail(fieldName);
        }

        // Two copies of one definition (scene copy and Addressables bundle copy) plus a different definition.
        private static (T sceneCopy, T bundleCopy, T other) Copies<T>(string idField, string id, string otherId) where T : ScriptableObject
        {
            var sceneCopy = ScriptableObject.CreateInstance<T>();
            var bundleCopy = ScriptableObject.CreateInstance<T>();
            var other = ScriptableObject.CreateInstance<T>();
            SetField(sceneCopy, idField, id);
            SetField(bundleCopy, idField, id);
            SetField(other, idField, otherId);
            return (sceneCopy, bundleCopy, other);
        }
    }
}
