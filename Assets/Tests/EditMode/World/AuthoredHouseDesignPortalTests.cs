// Candidate regression fixture; production guard has not been staged.
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.Game.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Rootborn.Tests.EditMode.World
{
    public sealed class AuthoredHouseDesignPortalTests
    {
        private Scene _scene;
        [SetUp] public void SetUp(){_scene=EditorSceneManager.NewPreviewScene();_scene.name="House";}
        [TearDown] public void TearDown(){EditorSceneManager.ClosePreviewScene(_scene);}
        private int Install()
        {
            typeof(HouseDesignPortalRuntimeInstaller).GetMethod("EnsureForScene",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{_scene});
            return _scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldPortal>(true)).Count();
        }
        [Test] public void AuthoredHouse_DoesNotCreateFloatingDesignPrototypeDoors()
        {var go=new GameObject("AuthoredWorld");SceneManager.MoveGameObjectToScene(go,_scene);go.AddComponent<AuthoredWorldScene>();Assert.That(Install(),Is.Zero);}
        [Test] public void LegacyHouse_RetainsDesignPrototypeAccess()
        {Assert.That(Install(),Is.EqualTo(2));}
    }
}
