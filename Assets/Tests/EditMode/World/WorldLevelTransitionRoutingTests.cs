using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class WorldLevelTransitionRoutingTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private GameDataRegistry _oldRegistry;
        private TownLevelTransitionTrigger _trigger;
        private GameObject _actor;
        private bool _initialized;
        [SetUp] public void Setup()
        {
            Assert.That(typeof(TownLevelTransitionTrigger).GetMethod("ConfigureTravelHandler"), Is.Not.Null, "Legacy edge transitions must use the same registered, saved actor handoff as doors.");
            GameDataRegistryProvider.TryGet(out _oldRegistry); _initialized = true;
            var destination = New<WorldDestinationDefinition>(); Set(destination, "_sceneName", "CoastFixture"); Set(destination, "_spawnIds", new[] { "coast-entry" });
            var catalog = New<WorldTravelCatalogDefinition>(); Set(catalog, "_destinations", new[] { destination }); Set(catalog, "_newGameDestination", destination); Set(catalog, "_newGameSpawnId", "coast-entry");
            var registry = New<GameDataRegistry>(); Set(registry, "_worldTravelCatalog", catalog); GameDataRegistryProvider.SetOverrideForTests(registry);
            _trigger = Go("LevelEdge").AddComponent<TownLevelTransitionTrigger>(); _trigger.Configure("CoastFixture", "coast-entry", 0f, 0f);
            _actor = Go("ExactTriggerActor"); _actor.AddComponent<PlayerIdentity>(); _actor.AddComponent<PlayerController>();
        }
        [TearDown] public void Cleanup()
        {
            if (_trigger != null)
                foreach (var renderer in _trigger.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    var sprite = renderer.sprite;
                    if (sprite != null) { if (sprite.texture != null) Object.DestroyImmediate(sprite.texture); Object.DestroyImmediate(sprite); }
                }
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]); _objects.Clear();
            if (_initialized) GameDataRegistryProvider.SetOverrideForTests(_oldRegistry); _initialized = false;
        }
        [Test] public void WORLD_EDGE_001_ExactTriggerActorAndRegisteredPairReachSharedDriver()
        {
            GameObject received = null; Configure((actor, scene, spawn, label) => { received = actor; Assert.That(scene, Is.EqualTo("CoastFixture")); Assert.That(spawn, Is.EqualTo("coast-entry")); return true; });
            Assert.That(Travel(_actor), Is.True); Assert.That(received, Is.SameAs(_actor)); Assert.That(_trigger.HasTransitioned, Is.True);
        }
        [Test] public void WORLD_EDGE_002_RejectedHandoffCanRetryWithoutClaimingTransition()
        {
            bool accept = false; int calls = 0; Configure((actor, scene, spawn, label) => { calls++; return accept; });
            Assert.That(Travel(_actor), Is.False); Assert.That(_trigger.HasTransitioned, Is.False); Assert.That(_trigger.TransitionOverlayVisible, Is.False); accept = true; Assert.That(Travel(_actor), Is.True); Assert.That(calls, Is.EqualTo(2));
        }
        [Test] public void WORLD_EDGE_003_NullActorAndUnknownDestinationNeverReachDriver()
        {
            int calls = 0; Configure((actor, scene, spawn, label) => { calls++; return true; }); Assert.That(Travel(null), Is.False);
            _trigger.Configure("Unknown", "entry", 0f, 0f); Assert.That(Travel(_actor), Is.False); Assert.That(calls, Is.Zero);
        }
        [Test] public void WORLD_EDGE_004_AcceptedTransitionCannotBeRequestedTwice()
        {
            int calls = 0; Configure((actor, scene, spawn, label) => { calls++; return true; }); Assert.That(Travel(_actor), Is.True); Assert.That(Travel(_actor), Is.False); Assert.That(calls, Is.EqualTo(1));
        }
        [Test] public void WORLD_EDGE_005_ExistingFadeCoroutineUsesSharedDriverAndRestoresOverlayOnRejection()
        {
            int calls = 0; Configure((actor, scene, spawn, label) => { Assert.That(actor, Is.SameAs(_actor)); calls++; return false; });
            var method = typeof(TownLevelTransitionTrigger).GetMethod("Transition", BindingFlags.Instance | BindingFlags.NonPublic); Assert.That(method, Is.Not.Null);
            var sequence = (IEnumerator)method.Invoke(_trigger, new object[] { _actor }); Assert.That(sequence.MoveNext(), Is.True); Assert.That(_trigger.TransitionOverlayVisible, Is.True);
            Assert.That(sequence.MoveNext(), Is.False); Assert.That(calls, Is.EqualTo(1)); Assert.That(_trigger.TransitionOverlayVisible, Is.False); Assert.That(_trigger.HasTransitioned, Is.False);
        }
        private void Configure(Func<GameObject, string, string, string, bool> handler) => typeof(TownLevelTransitionTrigger).GetMethod("ConfigureTravelHandler").Invoke(_trigger, new object[] { handler });
        private bool Travel(GameObject actor) { var method = typeof(TownLevelTransitionTrigger).GetMethod("TryTravel"); Assert.That(method, Is.Not.Null); return (bool)method.Invoke(_trigger, new object[] { actor }); }
        private GameObject Go(string name) { var value = new GameObject(name); _objects.Add(value); return value; }
        private T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
