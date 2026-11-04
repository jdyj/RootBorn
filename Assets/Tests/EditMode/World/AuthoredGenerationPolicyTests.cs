using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Interiors;
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
        [Test] public void TOWN_ConfiguredTown_DoesNotSynthesizePrototypeFarmGate()
        { _scene.name="Town"; Config(); Assert.That(ShouldGenerate(typeof(WorldEntryPointAutoFiller),"ShouldGenerateSceneEntryPoints"),Is.False); }
        [Test] public void TOWN_ConfiguredHouse_DoesNotOverwriteAuthoredEntry()
        { _scene.name="House"; Config(); Assert.That(ShouldGenerate(typeof(WorldEntryPointAutoFiller),"ShouldGenerateSceneEntryPoints"),Is.False); }
        [Test] public void TOWN_LegacyFarm_RetainsEntryGenerationEvenWithConfiguration()
        { _scene.name="Farm"; Config(); Assert.That(ShouldGenerate(typeof(WorldEntryPointAutoFiller),"ShouldGenerateSceneEntryPoints"),Is.True); }
        [Test] public void TOWN_LegacyTown_RetainsEntryGeneration()
        { _scene.name="Town"; Assert.That(ShouldGenerate(typeof(WorldEntryPointAutoFiller),"ShouldGenerateSceneEntryPoints"),Is.True); }
    }
}
