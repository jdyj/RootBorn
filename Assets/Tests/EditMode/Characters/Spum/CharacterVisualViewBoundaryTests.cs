using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Family;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Characters.Spum
{
    public sealed class CharacterVisualViewBoundaryTests
    {
        private const BindingFlags Bind = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void SPUM_VISUAL_BOUNDARY_001_PlayerAdapterDelegatesVisualCallsWithoutSpumReference()
        {
            var go = new GameObject("visual-adapter");
            try
            {
                var spy = go.AddComponent<SpyCharacterVisualView>();
                var adapter = go.AddComponent<PlayerCharacterVisualAdapter>();
                adapter.ConfigureForTests(spy);
                var mapping = ScriptableObject.CreateInstance<ToolVisualMappingDefinition>();

                adapter.SetMotion(new Vector2(1f, 0f), Vector2.right);
                adapter.SetFlipX(true);
                adapter.PlayAction(CharacterVisualAction.Attack);
                adapter.ApplyEquippedToolVisual(mapping);

                Assert.AreEqual(new Vector2(1f, 0f), spy.LastInput);
                Assert.AreEqual(Vector2.right, spy.LastFacing);
                Assert.IsTrue(spy.LastFlipX);
                Assert.AreEqual(CharacterVisualAction.Attack, spy.LastAction);
                Assert.AreSame(mapping, spy.LastMapping);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SPUM_VISUAL_BOUNDARY_002_PixelwoodViewPlaysMappedCharacterPartClipForAttackAction()
        {
            var go = new GameObject("pixelwood-view");
            try
            {
                var animator = go.AddComponent<CharacterPartAnimator>();
                var view = go.AddComponent<PixelwoodCharacterVisualView>();
                view.ConfigureForTests(animator, null);
                var clip = ScriptableObject.CreateInstance<CharacterPartAnimationClipDefinition>();
                ConfigureClip(clip, "clip.attack.side", row: 5, frameCount: 4, framesPerSecond: 12f, loop: false);
                var mapping = ScriptableObject.CreateInstance<ToolVisualMappingDefinition>();
                mapping.ConfigureForTests(clip);

                view.ApplyEquippedToolVisual(mapping);
                view.PlayAction(CharacterVisualAction.Attack);

                Assert.AreSame(clip, animator.ActiveClip);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SPUM_VISUAL_BOUNDARY_003_PlayerControllerDoesNotReferenceSpumConcreteRuntimeTypes()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/Player/PlayerController.cs");

            StringAssert.DoesNotContain("SpumCharacterVisualView", source);
            StringAssert.DoesNotContain("SPUM_Prefabs", source);
        }

        private static void ConfigureClip(CharacterPartAnimationClipDefinition clip, string id, int row, int frameCount, float framesPerSecond, bool loop)
        {
            typeof(CharacterPartAnimationClipDefinition).GetField("_id", Bind).SetValue(clip, id);
            typeof(CharacterPartAnimationClipDefinition).GetField("_row", Bind).SetValue(clip, row);
            typeof(CharacterPartAnimationClipDefinition).GetField("_frameCount", Bind).SetValue(clip, frameCount);
            typeof(CharacterPartAnimationClipDefinition).GetField("_framesPerSecond", Bind).SetValue(clip, framesPerSecond);
            typeof(CharacterPartAnimationClipDefinition).GetField("_loop", Bind).SetValue(clip, loop);
        }

        private sealed class SpyCharacterVisualView : MonoBehaviour, ICharacterVisualView
        {
            public Vector2 LastInput { get; private set; }
            public Vector2 LastFacing { get; private set; }
            public bool LastFlipX { get; private set; }
            public CharacterVisualAction LastAction { get; private set; }
            public ToolVisualMappingDefinition LastMapping { get; private set; }

            public void SetMotion(Vector2 input, Vector2 facing)
            {
                LastInput = input;
                LastFacing = facing;
            }

            public void SetFlipX(bool flipX)
            {
                LastFlipX = flipX;
            }

            public void PlayAction(CharacterVisualAction action)
            {
                LastAction = action;
            }

            public void ApplyEquippedToolVisual(ToolVisualMappingDefinition mapping)
            {
                LastMapping = mapping;
            }
        }
    }
}
