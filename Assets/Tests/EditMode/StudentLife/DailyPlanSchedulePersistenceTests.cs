using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyPlanSchedulePersistenceTests
    {
        private string _root;
        private readonly List<Object> _objects = new List<Object>();
        private StudentLifeProgress _student;
        private LifeCalendarDefinition _calendar;
        private DailyPlanRouteDefinition _route;
        private LocationActivityDefinition _activity;
        private DailyPlanRouteSetDefinition _set;
        private TraitDefinition _trait;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-daily-schedule-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root); SaveService.SetRootDirectoryForTests(_root);
            _calendar = Make<LifeCalendarDefinition>(); _calendar.ConfigureForTests(2, 3);
            _student = new StudentLifeProgress("slot", "player", 100, 100); _student.ConfigureCalendar(_calendar);
            _activity = Make<LocationActivityDefinition>();
            _activity.ConfigureForTests("activity.fixture", "Activity", null, LocationGrowthRoute.SelfStudy, 30, 0, 0, 0, null, null);
            _trait = Make<TraitDefinition>(); _trait.ConfigureForTests("trait.diary", "Diary");
            var outcome = Make<DailyPlanScheduleTraitOutcome>(); outcome.Trait = _trait;
            _route = Make<DailyPlanRouteDefinition>();
            _route.ConfigureForTests("route.fixture", "Route", null, LocationGrowthRoute.SelfStudy, new[] { _activity }, null, new DailyPlanRouteOutcomeBase[] { outcome }, "Title", "Detail", "Next");
            _set = Make<DailyPlanRouteSetDefinition>(); _set.ConfigureForTests("set.fixture", "Set", new[] { _route }, 1);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _objects) if (obj != null) Object.DestroyImmediate(obj);
            _objects.Clear(); SaveService.SetRootDirectoryForTests(null);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void EnsureProgress_RebindsAwakeDefaultToActualStudentIdentity()
        {
            var component = Component();
            var daily = component.EnsureProgress(_student);
            Assert.That(daily.SaveSlot, Is.EqualTo("slot"));
            Assert.That(daily.PlayerId, Is.EqualTo("player"));
            Assert.That(daily, Is.SameAs(DailyPlanScheduleApi.Read<DailyPlanProgress>(_student, "DailyPlan")));
        }

        [Test]
        public void EnsureProgress_SwitchingPlayerDoesNotReuseThePreviousPlayersRoute()
        {
            var component = Component(); component.Load("slot", "player", 1);
            Assert.That(component.TrySelect(_route, _student), Is.True);
            var other = new StudentLifeProgress("slot", "other-player", 100, 100); other.ConfigureCalendar(_calendar);
            var changed = component.EnsureProgress(other);
            Assert.That(changed.PlayerId, Is.EqualTo("other-player"));
            Assert.That(changed.SelectedRouteId, Is.Empty);
        }

        [Test]
        public void Filenames_PreserveDistinctPunctuationPlayerIdentities()
        {
            string first = DailyPlanProgressPersistence.FileNameFor("a/b");
            string second = DailyPlanProgressPersistence.FileNameFor("a_b");
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(Path.GetFileName(first), Is.EqualTo(first));
        }

        [Test]
        public void WrongEmbeddedIdentity_CannotImportAnotherPlayersCompletion()
        {
            var saved = new DailyPlanProgressSaveData { SaveSlot = "foreign", PlayerId = "foreign-player", CurrentDay = 1,
                SelectedRouteId = _route.Id, CompletedRouteId = _route.Id, AppliedCompletionKeys = new[] { "foreign-reward" } };
            var loaded = DailyPlanProgress.FromSaveData(saved, "slot", "player", 1);
            Assert.That(loaded.SaveSlot, Is.EqualTo("slot"));
            Assert.That(loaded.PlayerId, Is.EqualTo("player"));
            Assert.That(loaded.SelectedRouteId, Is.Empty);
            Assert.That(loaded.AppliedCompletionKeys, Is.Empty);
        }

        [Test]
        public void StudentJsonRoundTrip_IncludesPlanAndCompletionReceiptWithItsTraitReward()
        {
            var daily = Owned(); Assign(daily);
            Assert.That(DailyPlanScheduleApi.Record(daily, new[] { _set }, _activity, _student, 1, "saved-completion"), Is.True);
            var dtoField = typeof(StudentLifeProgressSaveData).GetField("DailyPlan", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(dtoField, Is.Not.Null, "The shared student DTO must carry Daily Plan with the trait reward.");
            var dto = JsonUtility.FromJson<StudentLifeProgressSaveData>(JsonUtility.ToJson(_student.ToSaveData()));
            Assert.That(dtoField.GetValue(dto), Is.Not.Null);
            var restored = StudentLifeProgress.FromSaveData(dto, null, null, null); restored.ConfigureCalendar(_calendar);
            var restoredPlan = DailyPlanScheduleApi.Read<DailyPlanProgress>(restored, "DailyPlan");
            Assert.That(restored.GetTraitValue(_trait), Is.EqualTo(1));
            Assert.That(DailyPlanScheduleApi.Record(restoredPlan, new[] { _set }, _activity, restored, 1, "saved-completion"), Is.False);
            Assert.That(restored.GetTraitValue(_trait), Is.EqualTo(1));
        }

        [Test]
        public void StudentNextDay_ArchivesOwnedScheduleBeforeSnapshotCapture()
        {
            var daily = Owned(); Assign(daily);
            Assert.That(_student.TryEndDay(null, out _), Is.True);
            Assert.That(_student.TryStartNextDay(), Is.True);
            var current = DailyPlanScheduleApi.Read<DailyPlanProgress>(_student, "DailyPlan");
            Assert.That(current.CurrentDay, Is.EqualTo(2));
            Assert.That(DailyPlanScheduleApi.Values(current, "Diary").Length, Is.EqualTo(1));
            var restored = StudentLifeProgress.FromSaveData(_student.ToSaveData(), null, null, null);
            Assert.That(DailyPlanScheduleApi.Read<DailyPlanProgress>(restored, "DailyPlan").CurrentDay, Is.EqualTo(2));
        }

        [Test]
        public void AggregateCommit_PersistsReceiptAndRewardBeforePublishing_AndReloadRejectsReplay()
        {
            var daily = Owned(); Assign(daily);
            var storage = new SaveService("slot", _root);
            var current = Snapshot();
            var coordinator = new AdultCareerCommandCoordinator(Array.Empty<FacilityDefinition>(), Array.Empty<ItemDefinition>(), _calendar, storage);
            int publishes = 0;
            var result = coordinator.Execute(current, Command("committed"), session =>
            {
                publishes++;
                var persisted = AdultCareerSnapshotPersistence.Load(storage, "player"); Assert.That(persisted, Is.Not.Null);
                var loaded = AdultCareerSnapshotPersistence.Restore(persisted, "slot", "player", Array.Empty<ItemDefinition>(), _calendar);
                Assert.That(loaded.Student.GetTraitValue(_trait), Is.EqualTo(1));
                var plan = DailyPlanScheduleApi.Read<DailyPlanProgress>(loaded.Student, "DailyPlan");
                Assert.That(DailyPlanScheduleApi.Record(plan, new[] { _set }, _activity, loaded.Student, 1, "committed"), Is.False);
                Assert.That(session.Student.GetTraitValue(_trait), Is.EqualTo(1));
            });
            Assert.That(result.Applied, Is.True); Assert.That(publishes, Is.EqualTo(1));
            Assert.That(_student.GetTraitValue(_trait), Is.Zero, "Detached command must not mutate its source student.");
        }

        [Test]
        public void FailedAggregateWrite_PublishesNeitherCompletionNorReward()
        {
            var daily = Owned(); Assign(daily);
            var storage = new SaveService("slot", _root); var current = Snapshot();
            string before = JsonUtility.ToJson(current);
            Directory.Delete(storage.DirectoryPath); File.WriteAllText(storage.DirectoryPath, "blocked save target");
            var coordinator = new AdultCareerCommandCoordinator(Array.Empty<FacilityDefinition>(), Array.Empty<ItemDefinition>(), _calendar, storage);
            bool published = false;
            var result = coordinator.Execute(current, Command("failed-write"), _ => published = true);
            Assert.That(result.Kind, Is.EqualTo(AdultCareerResultKind.PersistenceFailed));
            Assert.That(published, Is.False);
            Assert.That(JsonUtility.ToJson(current), Is.EqualTo(before));
            Assert.That(_student.GetTraitValue(_trait), Is.Zero);
            Assert.That(DailyPlanScheduleApi.Read<bool>(DailyPlanScheduleApi.Values(daily, "Slots")[0], "Completed"), Is.False);
        }

        [Test]
        public void CommittedPrimary_PreventsStaleStandaloneDailyPlanMigration()
        {
            var owned = Owned();
            Assert.That(AdultCareerSnapshotPersistence.SaveSession(new SaveService("slot", _root), Snapshot()), Is.True);
            var stale = new DailyPlanProgress("slot", "player", 1); stale.SelectRoute(_route);
            DailyPlanProgressPersistence.Save(stale);
            var method = typeof(DailyPlanProgressPersistence).GetMethod("MigrateInto", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Migration must explicitly guard an existing player snapshot.");
            Assert.That((bool)method.Invoke(null, new object[] { _student }), Is.False);
            Assert.That(owned.SelectedRouteId, Is.Empty);
        }

        [Test]
        public void BoundSessionCommandFailure_DoesNotMutateLivePlanOrRaiseChanged()
        {
            var component = Component(); int changed = 0; component.Changed += () => changed++;
            Bind(component, () => _student, command => false);
            Assert.That(component.TrySelect(_route, _student), Is.False);
            Assert.That(Owned().SelectedRouteId, Is.Empty);
            Assert.That(changed, Is.Zero);
        }

        [Test]
        public void BoundSessionPublish_ReacquiresTheNewStudentInstance()
        {
            var component = Component(); var source = _student;
            Bind(component, () => _student, command =>
            {
                var detached = StudentLifeProgress.FromSaveData(_student.ToSaveData(), null, null, null); detached.ConfigureCalendar(_calendar);
                if (!command(detached)) return false;
                _student = detached; return true;
            });
            Assert.That(component.TrySelect(_route, source), Is.True);
            Assert.That(component.Progress, Is.SameAs(Owned()));
            Assert.That(component.Progress.SelectedRouteId, Is.EqualTo(_route.Id));
            Assert.That(DailyPlanScheduleApi.Read<DailyPlanProgress>(source, "DailyPlan").SelectedRouteId, Is.Empty);
            Assert.That(File.Exists(Path.Combine(new SaveService("slot").DirectoryPath, DailyPlanProgressPersistence.FileNameFor("player"))), Is.False,
                "A bound command cannot create a competing standalone save.");
        }

        private DailyPlanProgress Owned() => DailyPlanScheduleApi.Read<DailyPlanProgress>(_student, "DailyPlan");
        private void Assign(DailyPlanProgress daily)
        {
            DailyPlanScheduleApi.Call<bool>(daily, "SynchronizeDay", _student);
            Assert.That(DailyPlanScheduleApi.Call<bool>(daily, "TryAssignSlot", 0, _route, _student), Is.True);
        }
        private AdultCareerSnapshot Snapshot() => AdultCareerSnapshotPersistence.Capture(new AdultCareerProgress("slot", "player"), _student, new Inventory(), new WorldStateProgress("slot", "player"));
        private Func<AdultCareerSession, AdultCareerService, AdultCareerResult> Command(string key) => (session, service) =>
        {
            var plan = DailyPlanScheduleApi.Read<DailyPlanProgress>(session.Student, "DailyPlan");
            bool applied = DailyPlanScheduleApi.Record(plan, new[] { _set }, _activity, session.Student, 1, key);
            return new AdultCareerResult(applied ? AdultCareerResultKind.Applied : AdultCareerResultKind.InvalidRequest);
        };
        private DailyPlanProgressComponent Component()
        {
            var go = new GameObject("DailySchedulePlayer"); _objects.Add(go); return go.AddComponent<DailyPlanProgressComponent>();
        }
        private static void Bind(DailyPlanProgressComponent component, Func<StudentLifeProgress> resolver, Func<Func<StudentLifeProgress, bool>, bool> executor)
        {
            var method = typeof(DailyPlanProgressComponent).GetMethod("BindSession", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Daily Plan needs shared-session command binding.");
            method.Invoke(component, new object[] { resolver, executor });
        }
        private T Make<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
    }
}
