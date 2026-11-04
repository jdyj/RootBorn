using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Time;
using Rootborn.Game.World;
using Rootborn.UI.HUD;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode
{
    public sealed class AuthoredWorldTimeHudTests
    {
        private Scene _scene;
        private Scene _origin;
        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            _origin = EditorSceneManager.NewPreviewScene();
        }
        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            if (_origin.IsValid()) EditorSceneManager.ClosePreviewScene(_origin);
        }
        private void ConfigureScene()
        {
            var marker = new GameObject("AuthoredConfig");
            SceneManager.MoveGameObjectToScene(marker, _scene);
            marker.AddComponent<AuthoredWorldScene>();
        }
        private void SceneLoaded()
        {
            typeof(TownTimeHudRuntimeInstaller).GetMethod("HandleSceneLoaded", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { _scene, LoadSceneMode.Single });
        }
        private T[] Components<T>() where T : Component => _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        [Test]
        public void ConfiguredNonTownSceneInstallsOneHudAndLifeTimeSource()
        {
            ConfigureScene();
            SceneLoaded();
            SceneLoaded();
            Assert.That(Components<TimeHud>(), Has.Length.EqualTo(1));
            Assert.That(Components<LifeTimePresentation>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void UnconfiguredNonTownSceneDoesNotInstallLifeHud()
        {
            SceneLoaded();
            Assert.That(Components<TimeHud>(), Is.Empty);
            Assert.That(Components<LifeTimePresentation>(), Is.Empty);
        }

        [Test]
        public void PostTravelBindingUsesExactMovedOwnerWithoutReplacingProgress()
        {
            ConfigureScene();
            SceneLoaded(); // Arrival precedes movement of the carried actor.
            var actor = new GameObject("CarriedActor");
            SceneManager.MoveGameObjectToScene(actor, _origin);
            var owner = actor.AddComponent<StudentLifeProgressComponent>();
            owner.ConfigureForTests("travel-clock", "carried-player", 12, 10, 0, 555);
            var progress = owner.Progress;
            SceneManager.MoveGameObjectToScene(actor, _scene);
            var bind = typeof(TownTimeHudRuntimeInstaller).GetMethod("BindOwner", BindingFlags.Static | BindingFlags.Public);
            Assert.That(bind, Is.Not.Null, "Carried owners do not receive OnEnable after MoveGameObjectToScene; handoff must bind explicitly.");
            bind.Invoke(null, new object[] { _scene, owner });
            var source = Components<LifeTimePresentation>().Single();
            Assert.That(source.Owner, Is.SameAs(owner));
            Assert.That(owner.Progress, Is.SameAs(progress));
            Assert.That(source.Minutes, Is.EqualTo(555));
        }
    }
}
