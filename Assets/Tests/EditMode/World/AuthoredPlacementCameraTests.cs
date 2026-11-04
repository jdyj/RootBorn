using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.World;
using Rootborn.UI.Interiors;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Rootborn.Tests.EditMode.World
{
    public sealed class AuthoredPlacementCameraTests
    {
        private Scene _scene; private Camera _camera; private CameraFollow _follow;
        [SetUp] public void SetUp()
        {
            _scene=EditorSceneManager.NewPreviewScene(); _scene.name="House";
            var go=new GameObject("Camera",typeof(Camera),typeof(CameraFollow)); SceneManager.MoveGameObjectToScene(go,_scene);
            _camera=go.GetComponent<Camera>(); _camera.orthographic=true; _camera.orthographicSize=5f; _camera.transform.position=new Vector3(2,3,-10); _follow=go.GetComponent<CameraFollow>();
        }
        [TearDown] public void TearDown(){EditorSceneManager.ClosePreviewScene(_scene);}
        private void Config(){var go=new GameObject("Authored"); SceneManager.MoveGameObjectToScene(go,_scene); go.AddComponent<AuthoredWorldScene>();}
        private void Invoke(string method,params object[] args)
        {var m=typeof(HousePlacementCameraRuntimeInstaller).GetMethod(method,BindingFlags.Public|BindingFlags.Static); Assert.That(m,Is.Not.Null,"Scene-scoped placement camera entry point is required."); m.Invoke(null,args);}
        private HousePlacementCameraController Controller()
        {var c=_camera.gameObject.AddComponent<HousePlacementCameraController>(); c.Configure(_camera,new Bounds(Vector3.zero,new Vector3(30,20,1))); return c;}
        [Test] public void AuthoredHiddenPlacement_PreservesFollowAndZoom()
        {Config(); Invoke("InitializeForScene",_scene); Assert.That(_follow.enabled,Is.True); Assert.That(_camera.orthographicSize,Is.EqualTo(5f)); var c=_camera.GetComponent<HousePlacementCameraController>(); Assert.That(c==null || !c.enabled,Is.True);}
        [Test] public void LegacyHouse_RetainsInitialPlacementCamera()
        {Invoke("InitializeForScene",_scene); Assert.That(_follow.enabled,Is.False); Assert.That(_camera.GetComponent<HousePlacementCameraController>().enabled,Is.True);}
        [Test] public void ExplicitModeEntryAndExit_RestoresCamera()
        {Config(); Invoke("SetPlacementMode",_scene,true); Assert.That(_follow.enabled,Is.False); Invoke("SetPlacementMode",_scene,false); Assert.That(_follow.enabled,Is.True); Assert.That(_camera.orthographicSize,Is.EqualTo(5f)); Assert.That(_camera.GetComponent<HousePlacementCameraController>().enabled,Is.False);}
        [Test] public void PanelDisable_ReleasesPlacementCameraWithoutToggle()
        {
            Config(); Invoke("SetPlacementMode",_scene,true);
            var go=new GameObject("PlacementPanel",typeof(InteriorFurniturePlacementPanel)); SceneManager.MoveGameObjectToScene(go,_scene);
            typeof(InteriorFurniturePlacementPanel).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(go.GetComponent<InteriorFurniturePlacementPanel>(),null);
            Assert.That(_follow.enabled,Is.True); Assert.That(_camera.orthographicSize,Is.EqualTo(5f));
        }
        [Test] public void RepeatedBegin_DoesNotLoseOriginalFollowState()
        {var c=Controller(); c.BeginPlacementControl(); c.BeginPlacementControl(); c.EndPlacementControl(); Assert.That(_follow.enabled,Is.True);}
        [Test] public void End_RestoresOriginalZoomAndPosition()
        {var c=Controller(); var p=_camera.transform.position; c.BeginPlacementControl(); c.FullView(); c.EndPlacementControl(); Assert.That(_camera.orthographicSize,Is.EqualTo(5f)); Assert.That(_camera.transform.position,Is.EqualTo(p));}
        [Test] public void EndWithoutBegin_DoesNotDisableFollow()
        {var c=Controller(); c.EndPlacementControl(); Assert.That(_follow.enabled,Is.True);}
    }
}
