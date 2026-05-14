using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Spum;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Characters.Spum
{
    public sealed class SpumVisualAdapterMotionTests
    {
        [Test]
        public void SPUM_VISUAL_001_ZeroInputMapsToIdle()
        {
            var go = new GameObject("spum-view");
            try
            {
                var view = go.AddComponent<SpumCharacterVisualView>();

                view.SetMotion(Vector2.zero, Vector2.down);

                Assert.AreEqual("IDLE", view.CurrentMotionState);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SPUM_VISUAL_002_NonZeroInputMapsToMove()
        {
            var go = new GameObject("spum-view");
            try
            {
                var view = go.AddComponent<SpumCharacterVisualView>();

                view.SetMotion(Vector2.right, Vector2.right);

                Assert.AreEqual("MOVE", view.CurrentMotionState);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SPUM_VISUAL_003_SideFacingAppliesVisualOnlyFlipToSpriteRenderers()
        {
            var go = new GameObject("spum-view");
            var child = new GameObject("body");
            child.transform.SetParent(go.transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            try
            {
                var view = go.AddComponent<SpumCharacterVisualView>();
                view.ConfigureForTests(null, go.transform, new[] { renderer });

                view.SetMotion(Vector2.zero, Vector2.right);

                Assert.IsTrue(renderer.flipX);
                Assert.AreEqual(Vector3.one * SpumCharacterVisualView.DefaultVisualScale, go.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SPUM_VISUAL_004_AttackActionUsesConfiguredAttackClipIndex()
        {
            var go = new GameObject("spum-view");
            try
            {
                var view = go.AddComponent<SpumCharacterVisualView>();
                var mapping = ScriptableObject.CreateInstance<ToolVisualMappingDefinition>();
                mapping.ConfigureSpumForTests(attackClipIndex: 3);

                view.ApplyEquippedToolVisual(mapping);
                view.PlayAction(CharacterVisualAction.Attack);

                Assert.AreEqual(3, view.LastPlayedActionClipIndex);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SPUM_VISUAL_005_DefaultVisualScaleIsThirtyTwoOverFortyNine()
        {
            Assert.AreEqual(32f / 49f, SpumCharacterVisualView.DefaultVisualScale);
            var go = new GameObject("spum-view");
            try
            {
                go.AddComponent<SpumCharacterVisualView>();

                Assert.AreEqual(Vector3.one * (32f / 49f), go.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
