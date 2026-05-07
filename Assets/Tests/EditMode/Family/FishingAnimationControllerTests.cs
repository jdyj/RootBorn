using NUnit.Framework;
using Rootborn.Game.Family;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class FishingAnimationControllerTests
    {
        [Test]
        public void FishingAnimationDefinition_IsValidOnlyWhenAllPhaseClipsAreAssigned()
        {
            var definition = ScriptableObject.CreateInstance<FishingAnimationDefinition>();
            var throwHook = CreateClip("character.action.fishing.throw_hook.side", 6, 40, 8f, false);
            var idle = CreateClip("character.action.fishing.idle.side", 7, 24, 6f, true);
            var pullHook = CreateClip("character.action.fishing.pull_hook.side", 8, 8, 10f, false);
            var caught = CreateClip("character.action.fishing.caught.side", 9, 40, 8f, false);
            try
            {
                SetDefinition(definition, "character.fishing.default", throwHook, idle, pullHook, caught);

                Assert.IsTrue(definition.IsValid(out var error), error);
                Assert.AreSame(throwHook, definition.ThrowHookClip);
                Assert.AreSame(idle, definition.WaitingIdleClip);
                Assert.AreSame(pullHook, definition.PullHookClip);
                Assert.AreSame(caught, definition.CaughtClip);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(throwHook);
                Object.DestroyImmediate(idle);
                Object.DestroyImmediate(pullHook);
                Object.DestroyImmediate(caught);
            }
        }

        [Test]
        public void FishingAnimationController_PlaysFishingPhaseClipsThroughCharacterPartAnimator()
        {
            var player = new GameObject("Player");
            var definition = ScriptableObject.CreateInstance<FishingAnimationDefinition>();
            var throwHook = CreateClip("character.action.fishing.throw_hook.side", 6, 40, 8f, false);
            var idle = CreateClip("character.action.fishing.idle.side", 7, 24, 6f, true);
            var pullHook = CreateClip("character.action.fishing.pull_hook.side", 8, 8, 10f, false);
            var caught = CreateClip("character.action.fishing.caught.side", 9, 40, 8f, false);
            try
            {
                SetDefinition(definition, "character.fishing.default", throwHook, idle, pullHook, caught);
                var animator = player.AddComponent<CharacterPartAnimator>();
                var controller = player.AddComponent<FishingAnimationController>();
                controller.Configure(animator, definition);

                controller.PlayThrowHook();
                Assert.AreSame(throwHook, animator.ActiveClip);

                controller.PlayWaitingIdle();
                Assert.AreSame(idle, animator.ActiveClip);

                controller.PlayPullHook(caughtFish: false);
                Assert.AreSame(pullHook, animator.ActiveClip);

                controller.PlayPullHook(caughtFish: true);
                Assert.AreSame(caught, animator.ActiveClip);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(throwHook);
                Object.DestroyImmediate(idle);
                Object.DestroyImmediate(pullHook);
                Object.DestroyImmediate(caught);
            }
        }

        private static CharacterPartAnimationClipDefinition CreateClip(string id, int row, int frameCount, float fps, bool loop)
        {
            var clip = ScriptableObject.CreateInstance<CharacterPartAnimationClipDefinition>();
            var serialized = new SerializedObject(clip);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_row").intValue = row;
            serialized.FindProperty("_frameCount").intValue = frameCount;
            serialized.FindProperty("_framesPerSecond").floatValue = fps;
            serialized.FindProperty("_loop").boolValue = loop;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return clip;
        }

        private static void SetDefinition(
            FishingAnimationDefinition definition,
            string id,
            CharacterPartAnimationClipDefinition throwHook,
            CharacterPartAnimationClipDefinition idle,
            CharacterPartAnimationClipDefinition pullHook,
            CharacterPartAnimationClipDefinition caught)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_throwHookClip").objectReferenceValue = throwHook;
            serialized.FindProperty("_waitingIdleClip").objectReferenceValue = idle;
            serialized.FindProperty("_pullHookClip").objectReferenceValue = pullHook;
            serialized.FindProperty("_caughtClip").objectReferenceValue = caught;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
