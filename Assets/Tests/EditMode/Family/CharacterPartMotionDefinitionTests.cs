using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Common;
using Rootborn.Game.Family;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Family
{
    // Reflection allows this test to compile and fail an assertion before the new SO/API exists.
    // No fixture or assertion depends on output/ staging files.
    public sealed class CharacterPartMotionDefinitionTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private GameObject _player;
        private CharacterPartComposer _composer;
        private CharacterPartAnimator _animator;
        private readonly Dictionary<string, Sprite> _frames = new Dictionary<string, Sprite>();

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("motion-layout-test");
            _owned.Add(_player);
            _composer = _player.AddComponent<CharacterPartComposer>();
            _animator = _player.AddComponent<CharacterPartAnimator>();
            var texture = Own(new Texture2D(16, 32));
            var appearance = new CharacterAppearance();
            var parts = new[] { CreatePart("test.body", "body", "TestBody"), CreatePart("test.clothes", "outfit", "TestClothes") };
            foreach (var part in parts)
            {
                appearance.SetSelectedPart(part.CategoryId, part.Id);
                for (int row = 0; row < 8; row++)
                for (int col = 0; col < 40; col++)
                {
                    string name = CharacterPartComposer.BuildFrameSubSpriteName(part, row, col);
                    var sprite = Own(Sprite.Create(texture, new Rect(0, 0, 16, 32), new Vector2(.5f, 0f), 16));
                    sprite.name = name;
                    _frames.Add(name, sprite);
                }
            }
            _animator.Configure(_composer, appearance, parts, (part, name) => _frames.TryGetValue(name, out var sprite) ? sprite : null);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            _frames.Clear();
        }

        [TestCase(1, 0, 0)]
        [TestCase(0, 1, 1)]
        [TestCase(-1, 0, 2)]
        [TestCase(0, -1, 3)]
        public void NativeProfile_IdleUsesDirectionColumnsOnSharedRow(int x, int y, int column)
        {
            ConfigureMotion(CreateModernProfile());
            _animator.SetMotion(Vector2.zero, new Vector2(x, y));
            _animator.Tick(.5f);
            AssertFrame(0, column);
        }

        [TestCase(1, 0, 0)]
        [TestCase(0, 1, 6)]
        [TestCase(-1, 0, 12)]
        [TestCase(0, -1, 18)]
        public void NativeProfile_WalkUsesSixFramesAndWrapsWithinItsDirectionBlock(int x, int y, int start)
        {
            ConfigureMotion(CreateModernProfile());
            _animator.FramesPerSecond = 100f; // Legacy settings must not override the explicit profile.
            _animator.WalkFrameCount = 2;
            var facing = new Vector2(x, y);
            _animator.SetMotion(facing, facing);
            _animator.Tick(0f);
            AssertFrame(1, start);
            _animator.Tick(.625f); // 8 fps => frame five; demonstrates six-frame rather than four-frame loop.
            AssertFrame(1, start + 5);
            _animator.Tick(.125f);
            AssertFrame(1, start);
        }

        [Test]
        public void DirectionChange_RightToLeftResetsTimerEvenThoughBothUseTheSameRow()
        {
            ConfigureMotion(CreateModernProfile());
            _animator.SetMotion(Vector2.right, Vector2.right);
            _animator.Tick(.375f);
            AssertFrame(1, 3);
            _animator.SetMotion(Vector2.left, Vector2.left);
            _animator.Tick(0f);
            AssertFrame(1, 12);
        }

        [Test]
        public void StopAndResume_UsesFacingIdleColumnAndRestartsWalking()
        {
            ConfigureMotion(CreateModernProfile());
            _animator.SetMotion(Vector2.up, Vector2.up);
            _animator.Tick(.375f);
            _animator.SetMotion(Vector2.zero, Vector2.zero); // Missing new facing retains last valid direction.
            _animator.Tick(.5f);
            AssertFrame(0, 1);
            _animator.SetMotion(Vector2.up, Vector2.up);
            _animator.Tick(0f);
            AssertFrame(1, 6);
        }

        [Test]
        public void DifferentProfile_UsesSerializedRowsColumnsRateAndFrameCount()
        {
            var profile = CreateModernProfile();
            SetInt(profile, "_idleRow", 3);
            SetInt(profile, "_walkRow", 4);
            SetInt(profile, "_idleDownColumn", 9);
            SetInt(profile, "_walkDownStartColumn", 24);
            SetInt(profile, "_framesPerDirection", 3);
            SetFloat(profile, "_framesPerSecond", 4f);
            ConfigureMotion(profile);
            _animator.SetMotion(Vector2.zero, Vector2.down);
            _animator.Tick(0f);
            AssertFrame(3, 9);
            _animator.SetMotion(Vector2.down, Vector2.down);
            _animator.Tick(.5f);
            AssertFrame(4, 26);
            _animator.Tick(.25f);
            AssertFrame(4, 24);
        }

        [Test]
        public void ConfigureMotion_ChangingProfileResetsClockAndExposesCurrentProfile()
        {
            ConfigureMotion(CreateModernProfile());
            _animator.SetMotion(Vector2.right, Vector2.right);
            _animator.Tick(.375f);
            var replacement = CreateModernProfile();
            SetInt(replacement, "_walkRightStartColumn", 25);
            ConfigureMotion(replacement);
            var property = typeof(CharacterPartAnimator).GetProperty("MotionDefinition");
            Assert.That(property, Is.Not.Null, "Expose current motion definition to the visual view.");
            Assert.That(property.GetValue(_animator), Is.SameAs(replacement));
            _animator.Tick(0f);
            AssertFrame(1, 25);
        }

        [Test]
        public void ConfigureMotion_SameProfileDoesNotRestartAnOngoingWalk()
        {
            var profile = CreateModernProfile();
            ConfigureMotion(profile);
            _animator.SetMotion(Vector2.right, Vector2.right);
            _animator.Tick(.375f);
            ConfigureMotion(profile);
            _animator.Tick(0f);
            AssertFrame(1, 3);
        }

        [Test]
        public void ConfigureMotion_NullRestoresLegacyRowAndFourFrameBehavior()
        {
            ConfigureMotion(CreateModernProfile());
            ConfigureMotion(null);
            _animator.FramesPerSecond = 8f;
            _animator.WalkFrameCount = 4;
            _animator.SetMotion(Vector2.zero, Vector2.down);
            _animator.Tick(0f);
            AssertFrame(0, 0);
            _animator.SetMotion(Vector2.up, Vector2.up);
            _animator.Tick(.25f);
            AssertFrame(2, 2);
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void PixelwoodView_UsesProfileMirrorSettingAndClearsExistingFlip(bool mirror, bool expected)
        {
            var profile = CreateModernProfile();
            SetBool(profile, "_mirrorHorizontal", mirror);
            ConfigureMotion(profile);
            var view = _player.AddComponent<PixelwoodCharacterVisualView>();
            view.ConfigureForTests(_animator, _composer);
            // Mirrors PlayerController's legacy direct composer call before the adapter call.
            _composer.SetFlipX(true);
            view.SetFlipX(true);
            foreach (var renderer in _player.GetComponentsInChildren<SpriteRenderer>())
                Assert.That(renderer.flipX, Is.EqualTo(expected), renderer.name);
            view.SetFlipX(false);
            foreach (var renderer in _player.GetComponentsInChildren<SpriteRenderer>())
                Assert.That(renderer.flipX, Is.False, renderer.name);
        }

        [Test]
        public void PixelwoodView_WithoutProfilePreservesLegacyFlip()
        {
            var view = _player.AddComponent<PixelwoodCharacterVisualView>();
            view.ConfigureForTests(_animator, _composer);
            view.SetFlipX(true);
            foreach (var renderer in _player.GetComponentsInChildren<SpriteRenderer>())
                Assert.That(renderer.flipX, Is.True, renderer.name);
        }

        [Test]
        public void NonLoopingActionClip_RetainsItsOwnFramesThenReturnsToProfileIdle()
        {
            ConfigureMotion(CreateModernProfile());
            _animator.SetMotion(Vector2.zero, Vector2.down);
            var clip = Own(ScriptableObject.CreateInstance<CharacterPartAnimationClipDefinition>());
            SetInt(clip, "_row", 5);
            SetInt(clip, "_frameCount", 3);
            SetFloat(clip, "_framesPerSecond", 8f);
            SetBool(clip, "_loop", false);
            _animator.PlayClip(clip);
            _animator.Tick(.25f);
            AssertFrame(5, 2);
            _animator.Tick(.125f);
            Assert.That(_animator.ActiveClip, Is.Null);
            AssertFrame(0, 3);
        }

        [TestCase("Assets/Data/Registry/GameDataRegistry.asset")]
        [TestCase("Assets/Resources/GameDataRegistry.asset")]
        public void BothRegistries_ProvideTheVerifiedModernMotionProfile(string path)
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(path);
            Assert.That(registry, Is.Not.Null, path);
            var property = typeof(GameDataRegistry).GetProperty("DefaultCharacterPartMotion");
            Assert.That(property, Is.Not.Null, "GameDataRegistry must expose DefaultCharacterPartMotion.");
            Assert.That(property.PropertyType, Is.EqualTo(ProfileType()));
            var profile = property.GetValue(registry) as ScriptableObject;
            Assert.That(profile, Is.Not.Null, path + " needs the explicit Modern motion asset.");
            AssertInt(profile, "_idleRow", 0);
            AssertInt(profile, "_walkRow", 1);
            AssertInt(profile, "_framesPerDirection", 6);
            Assert.That(new SerializedObject(profile).FindProperty("_framesPerSecond").floatValue, Is.EqualTo(8f));
            string[] directions = { "Right", "Up", "Left", "Down" };
            for (int i = 0; i < directions.Length; i++)
            {
                AssertInt(profile, "_idle" + directions[i] + "Column", i);
                AssertInt(profile, "_walk" + directions[i] + "StartColumn", i * 6);
            }
            Assert.That(new SerializedObject(profile).FindProperty("_mirrorHorizontal").boolValue, Is.False);
        }

        private ScriptableObject CreateModernProfile()
        {
            var profile = Own(ScriptableObject.CreateInstance(ProfileType()));
            SetInt(profile, "_idleRow", 0);
            SetInt(profile, "_walkRow", 1);
            SetInt(profile, "_framesPerDirection", 6);
            SetFloat(profile, "_framesPerSecond", 8f);
            string[] directions = { "Right", "Up", "Left", "Down" };
            for (int i = 0; i < directions.Length; i++)
            {
                SetInt(profile, "_idle" + directions[i] + "Column", i);
                SetInt(profile, "_walk" + directions[i] + "StartColumn", i * 6);
            }
            SetBool(profile, "_mirrorHorizontal", false);
            return profile;
        }

        private static Type ProfileType()
        {
            var type = typeof(CharacterPartAnimator).Assembly.GetType("Rootborn.Game.Family.CharacterPartMotionDefinition");
            Assert.That(type, Is.Not.Null, "Missing CharacterPartMotionDefinition: this is the expected initial RED assertion.");
            Assert.That(typeof(ScriptableObject).IsAssignableFrom(type), Is.True);
            return type;
        }

        private void ConfigureMotion(ScriptableObject profile)
        {
            var method = typeof(CharacterPartAnimator).GetMethod("ConfigureMotion", new[] { ProfileType() });
            Assert.That(method, Is.Not.Null, "CharacterPartAnimator needs ConfigureMotion(profile).");
            method.Invoke(_animator, new object[] { profile });
        }

        private void AssertFrame(int row, int col)
        {
            Assert.That(_player.transform.Find("Part_body").GetComponent<SpriteRenderer>().sprite, Is.SameAs(_frames[$"TestBody_r{row}_c{col}"]));
            Assert.That(_player.transform.Find("Part_outfit").GetComponent<SpriteRenderer>().sprite, Is.SameAs(_frames[$"TestClothes_r{row}_c{col}"]));
        }

        private CharacterPartDefinition CreatePart(string id, string category, string prefix)
        {
            var part = Own(ScriptableObject.CreateInstance<CharacterPartDefinition>());
            var serialized = new SerializedObject(part);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_categoryId").stringValue = category;
            serialized.FindProperty("_sheetAddress").stringValue = "test/sheet/" + category;
            serialized.FindProperty("_subSpriteName").stringValue = prefix + "_r0_c0";
            serialized.FindProperty("_isDefault").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return part;
        }

        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }

        private static void SetInt(Object target, string field, int value) => Set(target, field, property => property.intValue = value);
        private static void SetFloat(Object target, string field, float value) => Set(target, field, property => property.floatValue = value);
        private static void SetBool(Object target, string field, bool value) => Set(target, field, property => property.boolValue = value);
        private static void Set(Object target, string field, Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            Assert.That(property, Is.Not.Null, target.GetType().Name + " is missing serialized field " + field);
            assign(property);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void AssertInt(Object target, string field, int expected)
        {
            var property = new SerializedObject(target).FindProperty(field);
            Assert.That(property, Is.Not.Null, field);
            Assert.That(property.intValue, Is.EqualTo(expected), field);
        }
    }
}
