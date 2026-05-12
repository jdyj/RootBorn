using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentRelationshipConditionTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [Test]
        public void RELCOND_EDIT_001_RelationshipAndStatusDefinitionsAreRegisteredInGameDataRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);

            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            Assert.IsNotNull(registry.Relationships, "RelationshipDefinition entries must be exposed by GameDataRegistry.");
            Assert.IsNotNull(registry.StudentConditionStatuses, "StatusDefinition entries must be exposed by GameDataRegistry.");
            Assert.IsNotEmpty(registry.Relationships, "At least one relationship SO must be registered for the playable day loop.");
            Assert.IsNotEmpty(registry.StudentConditionStatuses, "At least one condition status SO must be registered for the playable day loop.");
            StringAssert.StartsWith("Assets/Data/Relationships/", AssetDatabase.GetAssetPath(registry.Relationships[0]));
            StringAssert.StartsWith("Assets/Data/Status/", AssetDatabase.GetAssetPath(registry.StudentConditionStatuses[0]));
        }

        [Test]
        public void RELCOND_EDIT_002_003_GenericRelationshipAndStatusEffectsApplyWithoutEntityIdBranching()
        {
            var relationship = ScriptableObject.CreateInstance<RelationshipDefinition>();
            relationship.ConfigureForTests("relationship.first-guide.trust", "relationship.first-guide.trust", "npc.first-guide");
            var status = ScriptableObject.CreateInstance<StatusDefinition>();
            status.ConfigureForTests("status.fatigue", "status.fatigue", true);
            var relationshipEffect = ScriptableObject.CreateInstance<RelationshipDeltaEffect>();
            relationshipEffect.ConfigureForTests(relationship, 2);
            var statusEffect = ScriptableObject.CreateInstance<StatusDeltaEffect>();
            statusEffect.ConfigureForTests(status, 3);
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests("activity.help-guide", "activity.help-guide", LifeActivityCategory.Social, 10, 0, 0, 0, null, new LifeActivityEffectBase[] { relationshipEffect, statusEffect });
            var progress = new StudentLifeProgress("slot-rel", "player-rel", 10, 10);

            Assert.IsTrue(new LifeActivityRunner().TryPerform(activity, progress, "help-guide-1", out var result));

            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
            Assert.AreEqual(2, progress.GetRelationshipValue(relationship));
            Assert.AreEqual(3, progress.GetStatusValue(status));
            CollectionAssert.Contains(progress.GetTodayResultLogIds(), "activity.help-guide:+relationship.first-guide.trust=2");
            CollectionAssert.Contains(progress.GetTodayResultLogIds(), "activity.help-guide:+status.fatigue=3");

            string activitySource = File.ReadAllText("Assets/Scripts/Game/StudentLife/StudentLifeCore.cs");
            string relationshipSource = File.ReadAllText("Assets/Scripts/Game/StudentLife/RelationshipDeltaEffect.cs");
            string statusSource = File.ReadAllText("Assets/Scripts/Game/StudentLife/StatusDeltaEffect.cs");
            StringAssert.DoesNotContain("npc.first-guide", activitySource + relationshipSource + statusSource);
            StringAssert.DoesNotContain("status.fatigue", relationshipSource + statusSource);
            StringAssert.DoesNotContain("switch", relationshipSource + statusSource);
        }

        [Test]
        public void RELCOND_EDIT_004_ResultSummaryBuildsBeforeAfterDeltasForRelationshipAndCondition()
        {
            var relationship = ScriptableObject.CreateInstance<RelationshipDefinition>();
            relationship.ConfigureForTests("relationship.first-guide.trust", "Guide Trust", "npc.first-guide");
            var status = ScriptableObject.CreateInstance<StatusDefinition>();
            status.ConfigureForTests("status.fatigue", "Fatigue", true);
            var before = new StudentLifeProgress("slot-rel", "player-rel", 10, 10);
            before.AddRelationshipForTests(relationship, 1);
            before.AddStatusForTests(status, 4);
            var after = StudentLifeProgress.FromSaveData(before.ToSaveData(), null, null, null, new[] { relationship }, new[] { status });
            after.AddRelationship(relationship, 2, "activity.help-guide");
            after.AddStatus(status, 3, "activity.help-guide");

            var summary = StudentDayRelationshipConditionSummaryBuilder.Build(before, after, new[] { relationship }, new[] { status });

            Assert.AreEqual(1, summary.RelationshipEntries.Length);
            Assert.AreEqual("Guide Trust", summary.RelationshipEntries[0].DisplayName);
            Assert.AreEqual(1, summary.RelationshipEntries[0].BeforeValue);
            Assert.AreEqual(3, summary.RelationshipEntries[0].AfterValue);
            Assert.AreEqual(2, summary.RelationshipEntries[0].Delta);
            Assert.AreEqual(1, summary.StatusEntries.Length);
            Assert.AreEqual("Fatigue", summary.StatusEntries[0].DisplayName);
            Assert.AreEqual(4, summary.StatusEntries[0].BeforeValue);
            Assert.AreEqual(7, summary.StatusEntries[0].AfterValue);
            Assert.AreEqual(3, summary.StatusEntries[0].Delta);
            Assert.IsNotEmpty(summary.NextDayImpacts, "Relationship/condition summary must include at least one next-day impact text.");
        }

        [Test]
        public void RELCOND_EDIT_005_SaveLoadAndRepeatedDayResultDoNotDuplicateRelationshipOrStatusDeltas()
        {
            var relationship = ScriptableObject.CreateInstance<RelationshipDefinition>();
            relationship.ConfigureForTests("relationship.first-guide.trust", "relationship.first-guide.trust", "npc.first-guide");
            var status = ScriptableObject.CreateInstance<StatusDefinition>();
            status.ConfigureForTests("status.fatigue", "status.fatigue", true);
            var relationshipEffect = ScriptableObject.CreateInstance<RelationshipDeltaEffect>();
            relationshipEffect.ConfigureForTests(relationship, 2);
            var statusEffect = ScriptableObject.CreateInstance<StatusDeltaEffect>();
            statusEffect.ConfigureForTests(status, 3);
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests("activity.help-guide", "activity.help-guide", LifeActivityCategory.Social, 0, 0, 0, 0, null, new LifeActivityEffectBase[] { relationshipEffect, statusEffect });
            var progress = new StudentLifeProgress("slot-rel", "player-rel", 10, 10);
            var runner = new LifeActivityRunner();

            Assert.IsTrue(runner.TryPerform(activity, progress, "request-1", out _));
            Assert.IsFalse(runner.TryPerform(activity, progress, "request-1", out var duplicate));
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
            Assert.AreEqual(2, progress.GetRelationshipValue(relationship), "중복 관계 정산 발생");
            Assert.AreEqual(3, progress.GetStatusValue(status), "중복 컨디션 정산 발생");
            Assert.IsTrue(progress.TryEndDay(null, out _));
            Assert.IsFalse(progress.TryEndDay(null, out _));
            Assert.AreEqual(1, progress.GetPreviousDayRelationshipDeltas().Length);
            Assert.AreEqual(1, progress.GetPreviousDayStatusDeltas().Length);

            var loaded = StudentLifeProgress.FromSaveData(progress.ToSaveData(), null, null, null, new[] { relationship }, new[] { status });
            Assert.AreEqual(2, loaded.GetRelationshipValue(relationship), "저장 후 관계 복원 실패");
            Assert.AreEqual(3, loaded.GetStatusValue(status), "저장 후 컨디션 복원 실패");
            Assert.AreEqual(1, loaded.GetPreviousDayRelationshipDeltas().Length);
            Assert.AreEqual(1, loaded.GetPreviousDayStatusDeltas().Length);
        }

        [Test]
        public void RELCOND_EDIT_006_DialogueConditionEvaluatesRelationshipConditionAndTutorialStageFromData()
        {
            var stage = ScriptableObject.CreateInstance<TutorialStageDefinition>();
            stage.ConfigureForTests("tutorial.day2", "tutorial.day2", "Meet the guide again.");
            var relationship = ScriptableObject.CreateInstance<RelationshipDefinition>();
            relationship.ConfigureForTests("relationship.first-guide.trust", "relationship.first-guide.trust", "npc.first-guide");
            var status = ScriptableObject.CreateInstance<StatusDefinition>();
            status.ConfigureForTests("status.fatigue", "status.fatigue", true);
            var condition = ScriptableObject.CreateInstance<DialogueCondition>();
            condition.ConfigureForTests(stage, relationship, 2, status, 5);
            var dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            dialogue.ConfigureForTests(new[] { "dialogue.guide.trust-rest" }, null, new[] { condition });
            var progress = new StudentLifeProgress("slot-rel", "player-rel", 10, 10);
            progress.SetTutorialStageForTests("tutorial.day2");
            progress.AddRelationshipForTests(relationship, 2);
            progress.AddStatusForTests(status, 4);

            Assert.IsTrue(dialogue.IsAvailable(progress), "2일차 대사 변화 없음");
            progress.AddStatusForTests(status, 2);
            Assert.IsFalse(dialogue.IsAvailable(progress), "컨디션 조건을 넘으면 대사가 제한되어야 한다.");
        }

        [Test]
        public void RELCOND_EDIT_007_ResultPanelShowsRelationshipConditionAndNextDayImpactText()
        {
            var canvasObject = new GameObject("ResultCanvas", typeof(RectTransform), typeof(Canvas));
            var panelObject = new GameObject("ResultPanel");
            try
            {
                panelObject.transform.SetParent(canvasObject.transform, false);
                var panel = panelObject.AddComponent<StudentDayResultPanel>();
                var summary = new StudentDaySummary(1, new[] { "activity.help-guide" }, new[] { "activity.help-guide:+relationship.first-guide.trust=2" }, "home-entry");
                var relationSummary = new StudentDayRelationshipConditionSummary(
                    new[] { new RelationshipDeltaEntry("relationship.first-guide.trust", "Guide Trust", 0, 2) },
                    new[] { new StatusDeltaEntry("status.fatigue", "Fatigue", 0, 3, true) },
                    new[] { "Guide offers a warmer greeting tomorrow." });

                panel.Show(null, summary, new StudentDayQuestInventorySummary(null, null, null), relationSummary);

                string rendered = CollectText(panel.transform);
                StringAssert.Contains("Guide Trust", rendered, "하루 결과 관계 요약 누락");
                StringAssert.Contains("0 -> 2", rendered, "하루 결과 관계 이전/이후 값 누락");
                StringAssert.Contains("Fatigue", rendered, "컨디션 변화 미표시");
                StringAssert.Contains("0 -> 3", rendered, "하루 결과 컨디션 이전/이후 값 누락");
                StringAssert.Contains("warmer greeting", rendered, "다음 날 영향 표시 누락");
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
                Object.DestroyImmediate(canvasObject);
            }
        }

        private static string CollectText(Transform root)
        {
            var texts = root.GetComponentsInChildren<Text>(true);
            string result = string.Empty;
            for (int i = 0; i < texts.Length; i++)
            {
                result += "\n" + texts[i].text;
            }

            return result;
        }
    }
}
