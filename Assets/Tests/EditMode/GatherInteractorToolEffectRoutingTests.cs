using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Tools;
using Rootborn.Game.World;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class GatherInteractorToolEffectRoutingTests
    {
        [Test]
        public void TriggerInteract_WithoutFarmGrid_StillDispatchesTargetOnlyToolEffects()
        {
            var player = new GameObject("Player");
            var tool = ScriptableObject.CreateInstance<ToolDefinition>();
            var effect = ScriptableObject.CreateInstance<RecordingToolEffect>();
            try
            {
                SetToolEffects(tool, effect);
                var interactor = player.AddComponent<GatherInteractor>();
                interactor.EquippedTool = tool;

                interactor.TriggerInteract();

                Assert.AreEqual(1, effect.Calls);
                Assert.AreSame(player, effect.LastTarget);
                Assert.AreEqual("Soil", effect.LastSurface);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(tool);
                Object.DestroyImmediate(effect);
            }
        }

        [Test]
        public void TriggerInteract_WhenFacingSurfaceTagZone_PassesZoneSurfaceToToolEffects()
        {
            var player = new GameObject("Player");
            var water = new GameObject("WaterSurface");
            var tool = ScriptableObject.CreateInstance<ToolDefinition>();
            var effect = ScriptableObject.CreateInstance<RecordingToolEffect>();
            try
            {
                SetToolEffects(tool, effect);
                var interactor = player.AddComponent<GatherInteractor>();
                interactor.EquippedTool = tool;

                water.transform.position = new Vector3(0f, -0.75f, 0f);
                water.AddComponent<BoxCollider2D>().size = Vector2.one;
                var zone = water.AddComponent<SurfaceTagZone>();
                SetSurface(zone, "Water");
                Physics2D.SyncTransforms();

                interactor.TriggerInteract();

                Assert.AreEqual(1, effect.Calls);
                Assert.AreEqual("Water", effect.LastSurface);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(water);
                Object.DestroyImmediate(tool);
                Object.DestroyImmediate(effect);
            }
        }

        private static void SetToolEffects(ToolDefinition tool, ToolEffectBase effect)
        {
            typeof(ToolDefinition)
                .GetField("_effects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(tool, new[] { effect });
        }

        private static void SetSurface(SurfaceTagZone zone, string surface)
        {
            typeof(SurfaceTagZone)
                .GetField("_surface", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(zone, surface);
        }

        private sealed class RecordingToolEffect : ToolEffectBase
        {
            public int Calls { get; private set; }
            public GameObject LastTarget { get; private set; }
            public string LastSurface { get; private set; }

            public override void Apply(in ToolUseContext ctx)
            {
                Calls++;
                LastTarget = ctx.Target;
                LastSurface = ctx.Surface;
            }
        }
    }
}
