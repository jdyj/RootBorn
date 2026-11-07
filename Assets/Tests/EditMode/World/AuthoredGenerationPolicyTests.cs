using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.Game.StudentLife;
using Rootborn.Game.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class AuthoredGenerationPolicyTests
    {
        private Scene _scene;
        [SetUp] public void SetUp() { _scene=EditorSceneManager.NewPreviewScene(); }
        [TearDown] public void TearDown() { if(_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); }
        private void Config() { var go=new GameObject("AuthoredWorld"); SceneManager.MoveGameObjectToScene(go,_scene); go.AddComponent<AuthoredWorldScene>(); }
        private bool ShouldGenerate(System.Type type,string method)
        {
            var member=type.GetMethod(method,BindingFlags.Static|BindingFlags.Public);
            Assert.That(member,Is.Not.Null,"An explicit scene-load generation contract must protect saved authored content.");
            return (bool)member.Invoke(null,new object[]{_scene});
        }
        [Test] public void TOWN_AuthoredHouse_DoesNotRegenerateSavedFurnitureOnLoad()
        { _scene.name="House"; Config(); Assert.That(ShouldGenerate(typeof(InteriorTilemapApplier),"ShouldGenerateOnSceneLoad"),Is.False); }
        [Test] public void TOWN_LegacyHouse_KeepsSeededGeneration()
        { _scene.name="House"; Assert.That(ShouldGenerate(typeof(InteriorTilemapApplier),"ShouldGenerateOnSceneLoad"),Is.True); }
        private LocationDefinition Location(string id)
        { var l=ScriptableObject.CreateInstance<LocationDefinition>(); var so=new UnityEditor.SerializedObject(l); so.FindProperty("_id").stringValue=id; so.ApplyModifiedPropertiesWithoutUndo(); return l; }
        private AuthoredWorldScene Slots(LocationDefinition location,Vector2 position)
        {
            var go=new GameObject("AuthoredWorld"); SceneManager.MoveGameObjectToScene(go,_scene); var world=go.AddComponent<AuthoredWorldScene>();
            var anchor=new GameObject("Anchor").transform; SceneManager.MoveGameObjectToScene(anchor.gameObject,_scene); anchor.position=position;
            world.ConfigureLocations(new[]{location},new[]{anchor}); world.ConfigureNpcSlots(new[]{location},new[]{anchor}); return world;
        }
        // Packed play and player builds load the registry from a bundle while built-in scenes keep their own copy of each location.
        [Test] public void TOWN_NpcSlot_MatchesRegistryCopyOfSceneLocationById()
        {
            var sceneCopy=Location("location.same"); var registryCopy=Location("location.same");
            try
            {
                var world=Slots(sceneCopy,new Vector2(3f,4f));
                Assert.That(world.ContainsLocation(registryCopy),Is.True);
                Assert.That(world.TryGetNpcPosition(registryCopy,0,out var position),Is.True);
                Assert.That(position,Is.EqualTo(new Vector2(3f,4f)));
                Assert.That(world.TryGetNpcPosition(registryCopy,1,out _),Is.False);
            }
            finally { Object.DestroyImmediate(sceneCopy); Object.DestroyImmediate(registryCopy); }
        }
        [Test] public void TOWN_NpcSlot_RejectsDifferentLocationId()
        {
            var sceneCopy=Location("location.same"); var other=Location("location.other");
            try
            {
                var world=Slots(sceneCopy,Vector2.zero);
                Assert.That(world.ContainsLocation(other),Is.False);
                Assert.That(world.ContainsLocation(null),Is.False);
                Assert.That(world.TryGetNpcPosition(other,0,out _),Is.False);
            }
            finally { Object.DestroyImmediate(sceneCopy); Object.DestroyImmediate(other); }
        }
        // FacilityActivities points and doors keep scene copies; quest objectives and routes compare against registry instances.
        [Test] public void TOWN_SceneLocationInteractors_RebindToRegistryInstances()
        {
            var registry=UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Common.GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            var cache=new Rootborn.Game.Common.GameDataLookupCache(registry);
            LocationIdentityDefinition registered=null; foreach(var l in registry.Locations) if(l!=null&&cache.TryGetLocationIdentity(l.Id,out registered)) break;
            Assert.That(registered,Is.Not.Null); var location=registered.Location; Assert.That(location,Is.Not.Null);
            var locationCopy=Object.Instantiate(location); var identityCopy=Object.Instantiate(registered);
            try
            {
                var active=new GameObject("Point"); SceneManager.MoveGameObjectToScene(active,_scene);
                var visit=active.AddComponent<LocationVisitInteractor>(); visit.Bind(locationCopy);
                var hidden=new GameObject("Hidden"); SceneManager.MoveGameObjectToScene(hidden,_scene); hidden.SetActive(false);
                var activity=hidden.AddComponent<LocationActivityInteractor>(); activity.Bind(identityCopy);
                var empty=active.AddComponent<LocationActivityInteractor>();
                Rootborn.UI.StudentLife.LocationNpcRuntimeInstaller.RebindSceneLocationInteractors(_scene,registry);
                Assert.That(visit.Location,Is.SameAs(cache.TryGetLocation(location.Id,out var expected)?expected:null));
                Assert.That(activity.Identity,Is.SameAs(registered));
                Assert.That(empty.Identity,Is.Null);
            }
            finally { Object.DestroyImmediate(locationCopy); Object.DestroyImmediate(identityCopy); }
        }
    }
}
