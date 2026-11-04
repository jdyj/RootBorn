using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Player
{
    // Isolated construction-order fixture,not a live actor/progression injection.
    public sealed class PlayerRigidbodyLateBindingTests
    {
        private GameObject _actor;
        private PlayerController _controller;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [SetUp] public void SetUp()
        {
            Assert.That(UiGameplayInputLock.IsBlocked || UiTextInputFocus.HasFocus, Is.False);
            _actor = new GameObject("LateBodyUnitActor");
        }
        [TearDown] public void TearDown()
        {
            UiGameplayInputLock.Release(_actor);
            if (_actor != null) Object.DestroyImmediate(_actor);
        }
        [Test] public void FixedMovementBindsABodyAddedAfterControllerAwake()
        {
            CreateController(); Assert.That(BoundBody(), Is.Null);
            var body = AddBody();
            Step(Vector2.up);
            Assert.That(BoundBody(), Is.SameAs(body), "Town fallback creates the controller before body reinforcement.");
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.up * 4f));
        }
        [Test] public void FrameMovementBindsALateBodyBeforeChoosingTheTransformFallback()
        {
            CreateController(); var body = AddBody();
            // Avoid reading ambient hardware keys/attacks in this construction-order fixture.
            UiGameplayInputLock.Acquire(_actor);
            typeof(PlayerController).GetMethod("Update", Private).Invoke(_controller, null);
            Assert.That(BoundBody(), Is.SameAs(body));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        }
        [Test] public void AnAlreadyPresentPrefabBodyStillBindsInAwake()
        {
            var body = AddBody(); CreateController();
            Assert.That(BoundBody(), Is.SameAs(body));
            Step(Vector2.right);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.right * 4f));
        }
        [Test] public void ABodylessActorKeepsItsExistingOptionalFallback()
        {
            CreateController(); Step(Vector2.up);
            Assert.That(BoundBody(), Is.Null);
            Assert.That(_actor.GetComponent<Rigidbody2D>(), Is.Null, "Resolve an existing body;do not add/change physics automatically.");
        }
        [Test] public void AReplacedDestroyedBodyIsResolvedForTheNextPhysicsStep()
        {
            var old = AddBody(); CreateController(); Object.DestroyImmediate(old);
            var replacement = AddBody(); Step(Vector2.left);
            Assert.That(BoundBody(), Is.SameAs(replacement));
            Assert.That(replacement.linearVelocity, Is.EqualTo(Vector2.left * 4f));
        }
        [Test] public void AModalLeaseStopsALateBodyWithoutChangingItsPhysicsConfiguration()
        {
            CreateController(); var body = AddBody();
            body.gravityScale = .25f; body.linearDamping = 2f; body.mass = 3f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearVelocity = Vector2.one;
            UiGameplayInputLock.Acquire(_actor); Step(Vector2.up);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(body.gravityScale, Is.EqualTo(.25f)); Assert.That(body.linearDamping, Is.EqualTo(2f));
            Assert.That(body.mass, Is.EqualTo(3f)); Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
            Assert.That(body.interpolation, Is.EqualTo(RigidbodyInterpolation2D.Interpolate));
        }
        private void CreateController()
        {
            _controller = _actor.AddComponent<PlayerController>();
            typeof(PlayerController).GetMethod("Awake", Private).Invoke(_controller, null);
        }
        private Rigidbody2D AddBody()
        {
            var body = _actor.AddComponent<Rigidbody2D>(); body.gravityScale = 0f; return body;
        }
        private Rigidbody2D BoundBody() => (Rigidbody2D)typeof(PlayerController).GetField("_rb", Private).GetValue(_controller);
        private void Step(Vector2 input)
        {
            typeof(PlayerController).GetField("_input", Private).SetValue(_controller, input);
            typeof(PlayerController).GetMethod("FixedUpdate", Private).Invoke(_controller, null);
        }
    }
}
