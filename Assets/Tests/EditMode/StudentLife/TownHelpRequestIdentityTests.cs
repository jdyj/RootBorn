using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.StudentLife
{
    // Real component default path and real isolated persistence; no synthetic request-history injection.
    public sealed class TownHelpRequestIdentityTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private readonly Dictionary<FieldInfo, object> _priorStatics = new Dictionary<FieldInfo, object>();
        private SaveSlotMetadata _priorMetadata;
        private Scene _scene;
        private string _root, _slot, _player;
        private GameDataRegistry _registry;
        private TownHelpActionDefinition _action;
        private HelpActionTraitDeltaOutcome _traitOutcome;
        private HelpActionRelationshipDeltaOutcome _relationshipOutcome;
        private StudentLifeProgressComponent _actor;
        private TownHelpActionInteractor _point;
        private StudentLifeProgressSaveData _initial;
        private int _initialTrait, _initialRelationship;

        [SetUp]
        public void SetUp()
        {
            _priorMetadata = ActiveSaveContext.Metadata;
            Snapshot(typeof(SaveService), "s_rootDirectoryOverride");
            foreach (string name in new[] { "s_provider", "s_overrideRegistry", "s_cachedRegistry" }) Snapshot(typeof(GameDataRegistryProvider), name);
            _root = Path.Combine(Path.GetTempPath(), "rootborn-town-help-identity-" + Guid.NewGuid().ToString("N"));
            _slot = "help-" + Guid.NewGuid().ToString("N");
            _player = "help-player-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(_root);
            SaveService.SetRootDirectoryForTests(_root);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = _slot });
            _registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.That(_registry, Is.Not.Null);
            GameDataRegistryProvider.SetOverrideForTests(_registry);
            _action = _registry.TownHelpActions.Single(a => a != null && a.Id == "town.help.first-guide-errand");
            _traitOutcome = _action.Outcomes.OfType<HelpActionTraitDeltaOutcome>().Single();
            _relationshipOutcome = _action.Outcomes.OfType<HelpActionRelationshipDeltaOutcome>().Single();
            Assert.That(_action.TimePolicy, Is.Not.Null);
            Assert.That(_action.TimePolicy.TimePointCost, Is.EqualTo(1));
            Assert.That(_action.TimePolicy.OncePerDay, Is.False, "These tests must not bypass the authored repeat policy.");
            Assert.That(_action.TimeCostMinutes, Is.EqualTo(20));
            Assert.That(_action.EnergyCost, Is.EqualTo(1)); Assert.That(_action.FocusCost, Is.Zero); Assert.That(_action.StressDelta, Is.Zero);
            Assert.That(_traitOutcome.Delta, Is.EqualTo(2)); Assert.That(_relationshipOutcome.Delta, Is.EqualTo(2));
            _scene = EditorSceneManager.NewPreviewScene(); // Direct component APIs do not need global scene discovery.
            _actor = Actor();
            _point = Point();
            _initial = _actor.EnsureProgress().ToSaveData();
            _initialTrait = _actor.Progress.GetTraitValue(_traitOutcome.Trait);
            _initialRelationship = _actor.Progress.GetRelationshipValue(_relationshipOutcome.Relationship);
            Assert.That(_initial.AppliedRequestIds, Is.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
                _owned.Clear();
                if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.ClosePreviewScene(_scene);
            }
            finally
            {
                ActiveSaveContext.Set(_priorMetadata);
                foreach (var pair in _priorStatics) pair.Key.SetValue(null, pair.Value);
                _priorStatics.Clear();
                if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root))
                {
                    string absolute = Path.GetFullPath(_root);
                    string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    Assert.That(absolute.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
                        && Path.GetFileName(absolute).StartsWith("rootborn-town-help-identity-", StringComparison.Ordinal), Is.True,
                        "Delete only this fixture's verified absolute temporary root.");
                    Directory.Delete(absolute, true);
                }
            }
        }

        [Test]
        public void NewDefaultInteractionAfterRebindEarnsANewRequestAndAuthoredEffects()
        {
            Assert.That(_point.TryInteract(_actor.gameObject), Is.True);
            string first = _point.LastResult.RequestId;
            AssertEarned(1);
            _point.Bind(_action);
            Assert.That(_point.TryInteract(_actor.gameObject), Is.True,
                "Rebinding the same authored action must not turn a fresh user interaction into a replay.");
            string second = _point.LastResult.RequestId;
            AssertDistinctEarnedRequests(first, second);
            AssertEarned(2);
            AssertPersistedMatches(_actor);
        }

        [Test]
        public void RecreatedInteractorAfterActualSaveLoadRetainsHistoryAndEarnsTheNextDefaultAction()
        {
            Assert.That(_point.TryInteract(_actor.gameObject), Is.True);
            string first = _point.LastResult.RequestId;
            AssertEarned(1);
            ReloadEarnedActorAndRecreatePoint();
            Assert.That(_actor.Progress.HasAppliedRequest(first), Is.True, "Actual load must retain the earned request history.");
            AssertEarned(1);
            Assert.That(_point.TryInteract(_actor.gameObject), Is.True,
                "A new scene component must not reuse its previous local sequence key against persisted history.");
            AssertDistinctEarnedRequests(first, _point.LastResult.RequestId);
            AssertEarned(2);
            AssertPersistedMatches(_actor);
        }

        [Test]
        public void ExplicitRequestReplayAfterSaveLoadStillRejectsWithoutCostsGrowthOrFileWrites()
        {
            const string explicitId = "transport-stable-request";
            Assert.That(_point.Interact(_actor, explicitId), Is.True);
            Assert.That(_point.LastResult.RequestId, Is.EqualTo(explicitId));
            AssertEarned(1);
            ReloadEarnedActorAndRecreatePoint();
            string state = JsonUtility.ToJson(_actor.Progress.ToSaveData());
            string files = Files();
            Assert.That(_point.Interact(_actor, explicitId), Is.False);
            Assert.That(_point.LastResult.Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));
            Assert.That(_point.LastResult.RequestId, Is.EqualTo(explicitId));
            Assert.That(_point.LastResult.OutcomeLogIds, Is.Empty);
            Assert.That(JsonUtility.ToJson(_actor.Progress.ToSaveData()), Is.EqualTo(state));
            Assert.That(Files(), Is.EqualTo(files), "Replay rejection must not rewrite a successful save.");
            AssertEarned(1);
        }

        [Test]
        public void LegitimatelyEarnedLegacyZeroRequestCoexistsWithNewDefaultAndKeepsReplayProtection()
        {
            string legacy = _action.Id + ":0";
            Assert.That(_point.Interact(_actor, legacy), Is.True, "Earn legacy history through the real component, never MarkRequestApplied or injected save data.");
            AssertEarned(1);
            ReloadEarnedActorAndRecreatePoint();
            Assert.That(_actor.Progress.HasAppliedRequest(legacy), Is.True);
            Assert.That(_point.TryInteract(_actor.gameObject), Is.True,
                "Old legitimately saved :0 keys remain valid history while a new default request gets a distinct identity.");
            AssertDistinctEarnedRequests(legacy, _point.LastResult.RequestId);
            AssertEarned(2);
            string state = JsonUtility.ToJson(_actor.Progress.ToSaveData()); string files = Files();
            Assert.That(_point.Interact(_actor, legacy), Is.False);
            Assert.That(_point.LastResult.Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));
            Assert.That(_point.LastResult.OutcomeLogIds, Is.Empty);
            Assert.That(JsonUtility.ToJson(_actor.Progress.ToSaveData()), Is.EqualTo(state));
            Assert.That(Files(), Is.EqualTo(files));
            AssertPersistedMatches(_actor);
        }

        private void AssertEarned(int count)
        {
            var p = _actor.Progress;
            Assert.That(p.TimePointsSpentToday, Is.EqualTo(_initial.TimePointsSpentToday + count));
            Assert.That(p.TimeMinutes, Is.EqualTo(_initial.TimeMinutes + 20 * count));
            Assert.That(p.Energy, Is.EqualTo(_initial.Energy - count)); Assert.That(p.Focus, Is.EqualTo(_initial.Focus)); Assert.That(p.Stress, Is.EqualTo(_initial.Stress));
            Assert.That(p.GetTraitValue(_traitOutcome.Trait), Is.EqualTo(_initialTrait + 2 * count));
            Assert.That(p.GetRelationshipValue(_relationshipOutcome.Relationship), Is.EqualTo(_initialRelationship + 2 * count));
            Assert.That(p.ToSaveData().AppliedRequestIds.Length, Is.EqualTo(count));
        }
        private void AssertDistinctEarnedRequests(string first, string second)
        {
            Assert.That(first, Is.Not.Null.And.Not.Empty); Assert.That(second, Is.Not.Null.And.Not.Empty);
            Assert.That(second, Is.Not.EqualTo(first), "Request identity is opaque; a test must not prescribe GUID formatting.");
            Assert.That(_point.LastResult.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(_actor.Progress.HasAppliedRequest(first) && _actor.Progress.HasAppliedRequest(second), Is.True);
        }
        private void ReloadEarnedActorAndRecreatePoint()
        {
            string earned = JsonUtility.ToJson(_actor.Progress.ToSaveData());
            AssertPersistedMatches(_actor);
            Object.DestroyImmediate(_point.gameObject); Object.DestroyImmediate(_actor.gameObject);
            _actor = Actor();
            Assert.That(StudentLifeProgressPersistence.TryLoad(_actor, _registry.StudentLifeTraits, _registry.StudentLifeSkills, _registry.Careers), Is.True,
                "Use the real isolated persistence load, preserving costs, traits, relationships and request history.");
            Assert.That(JsonUtility.ToJson(_actor.Progress.ToSaveData()), Is.EqualTo(earned));
            _point = Point();
        }
        private void AssertPersistedMatches(StudentLifeProgressComponent actor)
        {
            string slotPath = Path.Combine(_root, _slot);
            var file = Directory.GetFiles(slotPath, "student-life-progress*.json").Single();
            Assert.That(File.ReadAllText(file), Is.EqualTo(JsonUtility.ToJson(actor.Progress.ToSaveData(), true)),
                "The actual automatic persistence write must contain the authoritative earned snapshot.");
        }
        private StudentLifeProgressComponent Actor()
        {
            var go = Go("OwnedHelpRequestActor");
            go.AddComponent<PlayerIdentity>().Configure(_player);
            var component = go.AddComponent<StudentLifeProgressComponent>();
            component.ConfigureForTests(_slot, _player, 12, 10, 0, 480);
            return component;
        }
        private TownHelpActionInteractor Point() { var point = Go("OwnedHelpRequestPoint").AddComponent<TownHelpActionInteractor>(); point.Bind(_action); return point; }
        private GameObject Go(string name) { var go = new GameObject(name); _owned.Add(go); SceneManager.MoveGameObjectToScene(go, _scene); return go; }
        private string Files() => string.Join("\n", Directory.GetFiles(_root, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => f.Substring(_root.Length + 1) + "\t" + Convert.ToBase64String(File.ReadAllBytes(f))));
        private void Snapshot(Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name); _priorStatics.Add(field, field.GetValue(null));
        }
    }
}
