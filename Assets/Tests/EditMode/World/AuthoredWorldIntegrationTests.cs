using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.World;
using Rootborn.UI.StudentLife;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class AuthoredWorldIntegrationTests
    {
        private Scene _scene;
        private LocationDefinition _location;
        [SetUp] public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            _location = ScriptableObject.CreateInstance<LocationDefinition>();
            _location.ConfigureForTests("test-location", "test-location", new Vector2(-20,-20), Array.Empty<DiscoveryDefinition>());
        }
        [TearDown] public void TearDown()
        {
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            UnityEngine.Object.DestroyImmediate(_location);
        }
        private GameObject Make(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, _scene);
            return go;
        }
        private Component Config(LocationDefinition[] locations, Transform[] anchors)
        {
            var type = typeof(LocationDefinition).Assembly.GetType("Rootborn.Game.World.AuthoredWorldScene");
            Assert.That(type, Is.Not.Null, "Authored scene membership is missing; legacy installers still synthesize prototypes.");
            var config = Make("AuthoredWorld").AddComponent(type);
            var configure = type.GetMethod("ConfigureLocations");
            Assert.That(configure, Is.Not.Null);
            configure.Invoke(config, new object[] { locations, anchors });
            return config;
        }
        private bool Position(Component config, LocationDefinition location, out Vector2 result)
        {
            var args = new object[] { location, Vector2.zero };
            var method = config.GetType().GetMethod("TryGetLocationPosition");
            Assert.That(method, Is.Not.Null);
            var success = (bool)method.Invoke(config, args);
            result = (Vector2)args[1];
            return success;
        }
        [Test] public void TOWN_AuthoredMembership_UsesSceneEntranceInsteadOfPrototypePosition()
        {
            var door = Make("Door").transform; door.position = new Vector3(35,18,0);
            var config = Config(new[] { _location }, new[] { door });
            Assert.That(Position(config,_location,out var position), Is.True);
            Assert.That(position, Is.EqualTo(new Vector2(35,18)));
        }
        [Test] public void TOWN_AuthoredMembership_DoesNotSpawnUnlistedLocation()
        {
            var config = Config(Array.Empty<LocationDefinition>(), Array.Empty<Transform>());
            Assert.That(Position(config,_location,out _), Is.False);
        }
        [Test] public void TOWN_AuthoredMembership_DuplicateLocationFailsClosed()
        {
            var config = Config(new[] { _location, _location }, new[] { Make("A").transform, Make("B").transform });
            Assert.That(Position(config,_location,out _), Is.False);
        }
        [Test] public void TOWN_AuthoredMembership_RejectsForeignSceneAnchor()
        {
            var foreign = EditorSceneManager.NewPreviewScene();
            try
            {
                var anchor = new GameObject("Foreign").transform;
                SceneManager.MoveGameObjectToScene(anchor.gameObject,foreign);
                var config = Config(new[] { _location }, new[] { anchor });
                Assert.That(Position(config,_location,out _), Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(foreign); }
        }
        [Test] public void TOWN_LocationInstaller_ExcludesUnlistedLocations()
        {
            Config(Array.Empty<LocationDefinition>(),Array.Empty<Transform>());
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            try
            {
                typeof(GameDataRegistry).GetField("_locations",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(registry,new[] { _location });
                typeof(LocationNpcRuntimeInstaller).GetMethod("EnsureLocations",BindingFlags.Static|BindingFlags.NonPublic)
                    .Invoke(null,new object[] { _scene,registry,null });
                foreach(var root in _scene.GetRootGameObjects())
                    Assert.That(root.GetComponentsInChildren<LocationVisitInteractor>(true),Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(registry); }
        }
        [Test] public void TOWN_LocationInstaller_BindsAuthoredAnchorWithoutPrototypeRenderer()
        {
            var entrance = Make("ActualEntrance"); entrance.transform.position = new Vector3(40,20,0);
            var collider = entrance.AddComponent<BoxCollider2D>(); collider.size = new Vector2(2,1);
            Config(new[] { _location },new[] { entrance.transform });
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            try
            {
                typeof(GameDataRegistry).GetField("_locations",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(registry,new[] { _location });
                typeof(LocationNpcRuntimeInstaller).GetMethod("EnsureLocations",BindingFlags.Static|BindingFlags.NonPublic)
                    .Invoke(null,new object[] { _scene,registry,null });
                Assert.That(entrance.GetComponent<LocationVisitInteractor>(),Is.Not.Null);
                Assert.That(entrance.transform.position,Is.EqualTo(new Vector3(40,20,0)));
                Assert.That(collider.size,Is.EqualTo(new Vector2(2,1)));
                Assert.That(entrance.GetComponent<SpriteRenderer>(),Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(registry); }
        }
        [Test] public void LIFE_DailyPlan_ConfiguredSceneWithoutBoardDoesNotCreatePrototype()
        {
            Config(Array.Empty<LocationDefinition>(),Array.Empty<Transform>());
            var value = typeof(DailyPlanRuntimeInstaller).GetMethod("EnsureBoard",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[] { _scene });
            Assert.That(value,Is.Null);
        }
        [Test] public void TOWN_ConfiguredScene_RegistersLocationRunnerOutsideLegacyTown()
        {
            Config(Array.Empty<LocationDefinition>(),Array.Empty<Transform>());
            LocationNpcRuntimeInstaller.EnsureScene(_scene);
            Assert.That(Array.Exists(_scene.GetRootGameObjects(),r=>r.name=="[LocationNpcRuntimeInstaller]"),Is.True);
        }
        [Test] public void TOWN_AuthoredNpc_UsesDedicatedSlotAndPreservesCollider()
        {
            var door=Make("Door").transform; var slot=Make("NpcSlot").transform; slot.position=new Vector3(44,23,0);
            var config=Config(new[] { _location },new[] { door });
            config.GetType().GetMethod("ConfigureNpcSlots").Invoke(config,new object[] { new[] { _location },new[] { slot } });
            var npc=ScriptableObject.CreateInstance<NpcDefinition>();
            try
            {
                var actor=Make("NPC"); var collider=actor.AddComponent<BoxCollider2D>(); collider.size=new Vector2(1,2);
                var result=new NpcScheduleResult(npc,_location,null,string.Empty,string.Empty,string.Empty,null);
                typeof(LocationNpcRuntimeInstaller).GetMethod("ApplyNpcObject",BindingFlags.Static|BindingFlags.NonPublic)
                    .Invoke(null,new object[] { actor,npc,_location,"morning",result,0 });
                Assert.That(actor.transform.position,Is.EqualTo(new Vector3(44,23,0)));
                Assert.That(collider.size,Is.EqualTo(new Vector2(1,2)));
                Assert.That(actor.GetComponent<SpriteRenderer>(),Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(npc); }
        }
        [Test] public void LIFE_ConfiguredScene_StudentLifeInstallerDoesNotCreatePrototypeCluster()
        {
            Config(Array.Empty<LocationDefinition>(),Array.Empty<Transform>());
            typeof(TownStudentLifeRuntimeInstaller).GetMethod("EnsureActivityObjects",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[] { _scene });
            Assert.That(_scene.GetRootGameObjects().Length,Is.EqualTo(1));
        }
        [Test] public void LIFE_AuthoredDailyPlan_DoesNotCaptureDestinationPlaceholderActor()
        {
            Config(Array.Empty<LocationDefinition>(),Array.Empty<Transform>());
            var board=Make("DailyPlanBoard").AddComponent<DailyPlanBoardInteractor>();
            var placeholder=Make("DestinationPlaceholder");
            var canvas=Make("Canvas").AddComponent<Canvas>();
            var registry=ScriptableObject.CreateInstance<GameDataRegistry>();
            try
            {
                DailyPlanRuntimeInstaller.EnsureSceneForTests(_scene,canvas,registry,placeholder);
                Assert.That(typeof(DailyPlanBoardInteractor).GetField("_dailyProgress",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(board),Is.Null,
                    "Board must resolve the actual interacting actor after travel, not retain an earlier destination placeholder.");
                Assert.That(typeof(DailyPlanBoardInteractor).GetField("_studentProgress",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(board),Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(registry); }
        }
        [Test] public void LIFE_AuthoredDayEnd_RebindingPreservesSavedPositionAndCollision()
        {
            Config(Array.Empty<LocationDefinition>(),Array.Empty<Transform>());
            var board = Make("StudentDayEndBoard");
            board.transform.position = new Vector3(5,7,0);
            board.AddComponent<StudentDayEndInteractor>();
            var collider = board.AddComponent<BoxCollider2D>(); collider.isTrigger = true; collider.size = new Vector2(2,1);
            var method = typeof(StudentDayRuntimeInstaller).GetMethod("EnsureDayEndBoard",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That(method,Is.Not.Null);
            method.Invoke(null,new object[] { _scene,null });
            Assert.That(board.transform.position,Is.EqualTo(new Vector3(5,7,0)));
            Assert.That(collider.size,Is.EqualTo(new Vector2(2,1)));
            Assert.That(board.GetComponent<SpriteRenderer>(),Is.Null,"Binding must not add a colored prototype sprite.");
        }
        [Test] public void LIFE_AuthoredDayEnd_NoBoardMeansNoPrototype()
        {
            Config(Array.Empty<LocationDefinition>(),Array.Empty<Transform>());
            typeof(StudentDayRuntimeInstaller).GetMethod("EnsureDayEndBoard",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[] { _scene,null });
            foreach(var root in _scene.GetRootGameObjects())
                Assert.That(root.GetComponentsInChildren<StudentDayEndInteractor>(true),Is.Empty);
        }
    }
}

