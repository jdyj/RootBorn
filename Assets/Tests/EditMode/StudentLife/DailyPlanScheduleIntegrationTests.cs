using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyPlanScheduleIntegrationTests
    {
        private string _root;
        [SetUp] public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-diary-integrity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root); SaveService.SetRootDirectoryForTests(_root);
        }
        [TearDown] public void TearDown()
        {
            SaveService.SetRootDirectoryForTests(null);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void SharedSnapshot_RejectsForeignNestedDiary_InsteadOfDroppingReceiptButKeepingReward()
        {
            var student = new StudentLifeProgress("slot", "player", 100, 100);
            var snapshot = AdultCareerSnapshotPersistence.Capture(new AdultCareerProgress("slot", "player"), student, new Inventory(), new WorldStateProgress("slot", "player"));
            var field = typeof(StudentLifeProgressSaveData).GetField("DailyPlan", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            var diary = (DailyPlanProgressSaveData)field.GetValue(snapshot.Student);
            Assert.That(diary, Is.Not.Null); diary.PlayerId = "another-player";
            Assert.That(AdultCareerSnapshotPersistence.SaveSession(new SaveService("slot"), snapshot), Is.False,
                "A primary snapshot must reject mixed identity, not silently clear receipt history on restore.");
        }

        [Test]
        public void LegacyMigration_ImportsMatchingDiaryOnlyBeforeFirstPrimarySnapshot()
        {
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            try
            {
                route.ConfigureForTests("legacy.route", "Legacy", null, LocationGrowthRoute.SelfStudy, null, null, null, "Title", "Detail", "Next");
                var legacy = new DailyPlanProgress("slot", "player", 1); legacy.SelectRoute(route); DailyPlanProgressPersistence.Save(legacy);
                var student = new StudentLifeProgress("slot", "player", 100, 100);
                var method = typeof(DailyPlanProgressPersistence).GetMethod("MigrateInto", BindingFlags.Public | BindingFlags.Static);
                Assert.That(method, Is.Not.Null);
                Assert.That((bool)method.Invoke(null, new object[] { student }), Is.True);
                Assert.That(DailyPlanScheduleApi.Read<DailyPlanProgress>(student, "DailyPlan").SelectedRouteId, Is.EqualTo(route.Id));
                Assert.That((bool)method.Invoke(null, new object[] { student }), Is.False);
            }
            finally { Object.DestroyImmediate(route); }
        }

        [Test]
        public void LegacyStudentWithoutDiary_StartsOnItsCurrentDay_WithoutInventingOldPages()
        {
            var legacy = new StudentLifeProgressSaveData { SaveSlot = "slot", PlayerId = "player", CurrentDay = 6, Energy = 10, Focus = 10 };
            var serialized = JsonUtility.FromJson<StudentLifeProgressSaveData>(JsonUtility.ToJson(legacy));
            var student = StudentLifeProgress.FromSaveData(serialized, null, null, null);
            var diary = DailyPlanScheduleApi.Read<DailyPlanProgress>(student, "DailyPlan");
            Assert.That(diary.CurrentDay, Is.EqualTo(6));
            Assert.That(DailyPlanScheduleApi.Values(diary, "Diary"), Is.Empty,
                "Unity's absent inline DTO shell must not fabricate a day-one diary page.");
        }

        [Test]
        public void DayResult_IncludesEveryAssignedSlot_WithSeparateCompletionStatus()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            var route = registry.DailyPlanRouteSets[0].Routes[0];
            var player = new GameObject("DiaryDayResultFixture");
            try
            {
                var daily = player.AddComponent<DailyPlanProgressComponent>();
                string json = "{\"Version\":2,\"SaveSlot\":\"slot\",\"PlayerId\":\"player\",\"CurrentDay\":1,\"Slots\":["
                    + "{\"SlotIndex\":0,\"RouteId\":\"" + route.Id + "\",\"Completed\":true},"
                    + "{\"SlotIndex\":1,\"RouteId\":\"" + route.Id + "\",\"Completed\":false}]}";
                daily.RestoreFromSaveData(JsonUtility.FromJson<DailyPlanProgressSaveData>(json), "slot", "player", 1);
                var method = typeof(StudentDayEndInteractor).GetMethod("BuildDailyPlanSummaries", BindingFlags.Public | BindingFlags.Static);
                Assert.That(method, Is.Not.Null, "Day result must report both scheduled choices.");
                var summaries = (DailyPlanDaySummary[])method.Invoke(null, new object[] { player, registry });
                Assert.That(summaries.Length, Is.EqualTo(2));
                Assert.That(summaries[0].Completed, Is.True); Assert.That(summaries[1].Completed, Is.False);
                Assert.That(summaries[0].RouteDisplayNameKey, Is.EqualTo(route.DisplayNameKey));
                Assert.That(summaries[1].RouteDisplayNameKey, Is.EqualTo(route.DisplayNameKey));
            }
            finally { Object.DestroyImmediate(player); }
        }
    }
}
