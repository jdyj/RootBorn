using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.World;
using Rootborn.UI.Interiors;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class AuthoredBootstrapSafetyTests
    {
        private Scene _scene;
        [SetUp] public void SetUp() { _scene=EditorSceneManager.NewPreviewScene(); _scene.name="House"; }
        [TearDown] public void TearDown() { if(_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); }
        private GameObject Go(string name) { var go=new GameObject(name); SceneManager.MoveGameObjectToScene(go,_scene); return go; }
        private void Configure() { Go("AuthoredWorld").AddComponent<AuthoredWorldScene>(); }
        private InteriorFurniturePlacementPanel CreatePanel()
        { Configure(); return InteriorFurniturePlacementPanel.Create(Go("Canvas").AddComponent<Canvas>(),null); }
        [Test] public void AuthoredHouse_PlacementPanelStartsHidden()
        { Assert.That(CreatePanel().gameObject.activeSelf,Is.False); }
        [Test] public void AuthoredHouse_PanelCreationDoesNotGenerateReplacementMap()
        { Assert.That(CreatePanel().Model.CurrentMap,Is.Null); }
        [Test] public void PlacementPanel_ReopeningClearsClosingFlagWithoutGeneratingMap()
        {
            var panel=Go("Panel").AddComponent<InteriorFurniturePlacementPanel>();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(InteriorFurniturePlacementPanel).GetMethod("OnDisable",flags).Invoke(panel,null);
            var enable=typeof(InteriorFurniturePlacementPanel).GetMethod("OnEnable",flags);
            Assert.That(enable,Is.Not.Null,"Reopening must permit explicit editing again.");
            enable.Invoke(panel,null);
            Assert.That((bool)typeof(InteriorFurniturePlacementPanel).GetField("_isClosing",flags).GetValue(panel),Is.False);
            Assert.That(panel.Model.CurrentMap,Is.Null,"Opening is not permission to regenerate saved tiles.");
        }
        private void RunNpcInstaller() { typeof(WorldNpcAutoFiller).GetMethod("StartRunner",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{_scene}); }
        private bool HasNpcRunner()
        { foreach(var root in _scene.GetRootGameObjects()) if(root.name=="[WorldNpcAutoFiller]") return true; return false; }
        [Test] public void AuthoredTown_DoesNotInstallPrototypeGuideNpc()
        { _scene.name="Town"; Configure(); RunNpcInstaller(); Assert.That(HasNpcRunner(),Is.False); }
        [Test] public void LegacyTown_KeepsGuideNpcInstaller()
        { _scene.name="Town"; RunNpcInstaller(); Assert.That(HasNpcRunner(),Is.True); }
        [Test] public void LegacyFarm_KeepsGuideNpcInstaller()
        { _scene.name="Farm"; RunNpcInstaller(); Assert.That(HasNpcRunner(),Is.True); }
    }
}
