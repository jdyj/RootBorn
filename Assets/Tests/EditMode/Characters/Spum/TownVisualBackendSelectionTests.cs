using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Spum;
using Rootborn.Game.Family;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Characters.Spum
{
    public sealed class TownVisualBackendSelectionTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _owned = new List<Object>();
        private GameObject _player;
        private PlayerCharacterVisualAdapter _adapter;
        private PixelwoodCharacterVisualView _nativeView;
        private SpriteRenderer _nativeBody;
        private Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            _player = Own(new GameObject("visual-selection-test"));
            _player.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
            var texture = Own(new Texture2D(16, 32));
            _sprite = Own(Sprite.Create(texture, new Rect(0, 0, 16, 32), new Vector2(.5f, 0f), 16));
            var definition = Own(ScriptableObject.CreateInstance<CharacterPartDefinition>());
            var fields = new SerializedObject(definition);
            fields.FindProperty("_id").stringValue = "test.native.body";
            fields.FindProperty("_categoryId").stringValue = "body";
            fields.FindProperty("_sheetAddress").stringValue = "test/native";
            fields.FindProperty("_subSpriteName").stringValue = "NativeBody_r0_c3";
            fields.FindProperty("_isDefault").boolValue = true;
            fields.ApplyModifiedPropertiesWithoutUndo();
            var composer = _player.AddComponent<CharacterPartComposer>();
            var animator = _player.AddComponent<CharacterPartAnimator>();
            var appearance = new CharacterAppearance();
            appearance.SetSelectedPart("body", definition.Id);
            animator.Configure(composer, appearance, new[] { definition }, (part, name) => _sprite);
            var motion = Own(ScriptableObject.CreateInstance<CharacterPartMotionDefinition>());
            var motionFields = new SerializedObject(motion);
            motionFields.FindProperty("_mirrorHorizontal").boolValue = false;
            motionFields.ApplyModifiedPropertiesWithoutUndo();
            animator.ConfigureMotion(motion);
            animator.SetMotion(Vector2.zero, Vector2.down);
            animator.Tick(0f);
            _nativeBody = _player.transform.Find("Part_body").GetComponent<SpriteRenderer>();
            _nativeView = _player.AddComponent<PixelwoodCharacterVisualView>();
            _nativeView.ConfigureForTests(animator, composer);
            _adapter = _player.AddComponent<PlayerCharacterVisualAdapter>();
            _adapter.ConfigureForTests(_nativeView);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }

        [Test]
        public void SnapshotIntentWithoutRenderedSpum_DoesNotCreateAViewOrReplaceNativeAdapter()
        {
            Vector3 scale = _player.transform.localScale;
            AttemptBind();
            Assert.That(SelectedView(), Is.SameAs(_nativeView), "A snapshot kind is not a rendered SPUM visual.");
            Assert.That(_player.GetComponent<SpumCharacterVisualView>(), Is.Null, "Do not synthesize a SPUM wrapper around native layers.");
            Assert.That(_player.transform.localScale, Is.EqualTo(scale));
        }

        [Test]
        public void FailedSpumBinding_NativeRightFacingKeepsItsOwnNonMirroredFrame()
        {
            AttemptBind();
            _adapter.SetMotion(Vector2.right, Vector2.right);
            _adapter.SetFlipX(true); // Same controller request that exposed this bug in actual Town input.
            Assert.That(_nativeBody.flipX, Is.False);
            Assert.That(SelectedView(), Is.SameAs(_nativeView));
        }

        [Test]
        public void EmptySpumComponentOnNativeRoot_DoesNotAdoptOrScaleNativeLayers()
        {
            Vector3 scale = _player.transform.localScale;
            var empty = _player.AddComponent<SpumCharacterVisualView>();
            Assert.That(_player.transform.localScale, Is.EqualTo(scale), "Awake/OnEnable must not mutate a native actor root.");
            empty.SetMotion(Vector2.right, Vector2.right);
            empty.SetFlipX(true);
            Assert.That(_nativeBody.flipX, Is.False, "A non-renderable SPUM view must not own native composer renderers.");
            AttemptBind();
            Assert.That(SelectedView(), Is.SameAs(_nativeView));
        }

        [Test]
        public void DedicatedSpumViewWithNullSprite_DoesNotDisplaceNativeFallback()
        {
            SpriteRenderer renderer;
            CreateDedicatedSpum(false, out renderer);
            Vector3 scale = _player.transform.localScale;
            AttemptBind();
            Assert.That(SelectedView(), Is.SameAs(_nativeView));
            Assert.That(_player.transform.localScale, Is.EqualTo(scale));
        }

        [Test]
        public void RenderableDedicatedSpumView_IsSelectedAndOnlyFlipsItsOwnRenderer()
        {
            SpriteRenderer renderer;
            var spum = CreateDedicatedSpum(true, out renderer);
            Vector3 scale = _player.transform.localScale;
            AttemptBind();
            Assert.That(SelectedView(), Is.SameAs(spum));
            _adapter.SetMotion(Vector2.right, Vector2.right);
            _adapter.SetFlipX(true);
            Assert.That(renderer.flipX, Is.True, "Actual SPUM keeps its existing side-facing convention.");
            Assert.That(_nativeBody.flipX, Is.False);
            Assert.That(_player.transform.localScale, Is.EqualTo(scale), "Only the dedicated visual child may receive SPUM scaling.");
            Assert.That(spum.transform.localScale, Is.EqualTo(Vector3.one * SpumCharacterVisualView.DefaultVisualScale));
        }

        [Test]
        public void DelayedSpumSprite_BecomesEligibleAfterItsOwnRendererIsReady()
        {
            SpriteRenderer renderer;
            var spum = CreateDedicatedSpum(false, out renderer);
            AttemptBind();
            Assert.That(SelectedView(), Is.SameAs(_nativeView));
            renderer.sprite = _sprite;
            AttemptBind();
            Assert.That(SelectedView(), Is.SameAs(spum));
        }

        [Test]
        public void SpumRendererOutsideItsVisualRoot_CannotClaimNativeBody()
        {
            SpriteRenderer renderer;
            var spum = CreateDedicatedSpum(false, out renderer);
            spum.ConfigureForTests(null, spum.transform, new[] { _nativeBody });
            AttemptBind();
            Assert.That(SelectedView(), Is.SameAs(_nativeView));
            spum.SetFlipX(true);
            Assert.That(_nativeBody.flipX, Is.False);
        }

        private SpumCharacterVisualView CreateDedicatedSpum(bool withSprite, out SpriteRenderer renderer)
        {
            var visual = new GameObject("DedicatedSpumVisual");
            visual.transform.SetParent(_player.transform, false);
            renderer = visual.AddComponent<SpriteRenderer>();
            if (withSprite) renderer.sprite = _sprite;
            var animator = visual.AddComponent<Animator>();
            var view = visual.AddComponent<SpumCharacterVisualView>();
            view.ConfigureForTests(animator, visual.transform, new[] { renderer });
            return view;
        }

        private void AttemptBind()
        {
            // Existing entry point: tests fail through actual installer behavior, not a missing helper.
            var method = typeof(TownSpumVisualRuntimeInstaller).GetMethod("BindSpumVisual", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { _player });
        }

        private object SelectedView() => typeof(PlayerCharacterVisualAdapter).GetField("_visualView", PrivateInstance).GetValue(_adapter);
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
    }
}
