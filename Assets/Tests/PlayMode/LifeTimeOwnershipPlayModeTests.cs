using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Time;
using Rootborn.UI.HUD;
using System.Collections;
using UnityEngine.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode
{
    public sealed class LifeTimeOwnershipPlayModeTests
    {
        private Scene _scene;
        private GameObject _sourceObject;
        private LifeTimePresentation _source;
        private GameObject _first;
        private GameObject _second;

        [SetUp]
        public void SetUp()
        {
            _scene = SceneManager.CreateScene("LifeTimeOwnership-" + Guid.NewGuid().ToString("N"));
            _sourceObject = new GameObject("ClockSource");
            SceneManager.MoveGameObjectToScene(_sourceObject, _scene);
            _source = _sourceObject.AddComponent<LifeTimePresentation>();
            _first = CreateActor("First");
            Assert.That(_source.Owner, Is.SameAs(_first.GetComponent<StudentLifeProgressComponent>()));
        }

        private GameObject CreateActor(string name)
        {
            var actor = new GameObject(name);
            SceneManager.MoveGameObjectToScene(actor, _scene);
            actor.AddComponent<PlayerController>();
            actor.AddComponent<StudentLifeProgressComponent>();
            return actor;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_sourceObject != null) Object.DestroyImmediate(_sourceObject);
            if (_first != null) Object.DestroyImmediate(_first);
            if (_second != null) Object.DestroyImmediate(_second);
            if (_scene.IsValid()) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [Test]
        public void SecondEligibleActorInvalidatesPreviouslySelectedOwner()
        {
            _second = CreateActor("Second");
            Assert.That(_source.HasTime, Is.False, "Arrival order cannot select an arbitrary actor.");
            Assert.That(_source.Owner, Is.Null);
            _second.SetActive(false);
            Assert.That(_source.Owner, Is.SameAs(_first.GetComponent<StudentLifeProgressComponent>()));
        }

        [Test]
        public void ControllerEligibilityChangesReevaluateOwnerWithoutPolling()
        {
            _first.GetComponent<PlayerController>().enabled = false;
            Assert.That(_source.HasTime, Is.False);
            _first.GetComponent<PlayerController>().enabled = true;
            Assert.That(_source.Owner, Is.SameAs(_first.GetComponent<StudentLifeProgressComponent>()));
            Assert.That(_source.HasTime, Is.True);
        }

        [Test]
        public void SourceDisableReenableResolvesCurrentActorAndSuspensionRecovers()
        {
            _source.enabled = false;
            _first.SetActive(false);
            _second = CreateActor("Second");
            _source.enabled = true;
            Assert.That(_source.Owner, Is.SameAs(_second.GetComponent<StudentLifeProgressComponent>()));
            _source.SetSuspended(true);
            Assert.That(_source.HasTime, Is.False);
            _source.SetSuspended(false);
            Assert.That(_source.HasTime, Is.True);
        }

    }
}
