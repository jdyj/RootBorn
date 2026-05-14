using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Spum;
using Rootborn.Game.Player;
using Rootborn.Game.Tools;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class SpumPlayerAttackGatherE2ETests : InputTestFixture
    {
        private const BindingFlags Bind = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator SPUM_PLAYER_ATTACK_E2E_001_MouseClickTriggersSpumAttackAndGatherInteractionOnce()
        {
            var cameraObject = new GameObject("Main Camera");
            var player = new GameObject("Player");
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = 5f;

                var view = player.AddComponent<SpumCharacterVisualView>();
                player.AddComponent<PlayerCharacterVisualAdapter>().ConfigureForTests(view);
                var interactor = player.AddComponent<GatherInteractor>();
                var controller = player.AddComponent<PlayerController>();
                controller.Bind(null, null);

                var effect = ScriptableObject.CreateInstance<CountingToolEffect>();
                var tool = ScriptableObject.CreateInstance<ToolDefinition>();
                typeof(ToolDefinition).GetField("_effects", Bind).SetValue(tool, new ToolEffectBase[] { effect });
                typeof(GatherInteractor).GetField("_equippedTool", Bind).SetValue(interactor, tool);

                var mapping = ScriptableObject.CreateInstance<ToolVisualMappingDefinition>();
                mapping.ConfigureSpumForTests(attackClipIndex: 4);
                view.ApplyEquippedToolVisual(mapping);

                yield return null;
                Vector2 screenPoint = camera.WorldToScreenPoint(player.transform.position + Vector3.right * 2f);
                Set(mouse.position, screenPoint);
                Press(mouse.leftButton);
                InputSystem.Update();
                yield return null;
                Set(mouse.position, screenPoint);
                Release(mouse.leftButton);
                InputSystem.Update();
                for (int i = 0; i < 12 && (view.LastPlayedActionClipIndex != 4 || effect.ApplyCount != 1); i++)
                {
                    yield return null;
                }

                Assert.AreEqual(4, view.LastPlayedActionClipIndex, "Actual mouse attack must reach the SPUM visual view through the player adapter.");
                Assert.AreEqual(1, effect.ApplyCount, "Actual mouse attack must trigger GatherInteractor exactly once for the attack interaction.");
            }
            finally
            {
                Release(mouse.leftButton);
                InputSystem.Update();
                InputSystem.RemoveDevice(mouse);
                Object.Destroy(player);
                Object.Destroy(cameraObject);
            }
        }

        private sealed class CountingToolEffect : ToolEffectBase
        {
            public int ApplyCount { get; private set; }

            public override void Apply(in ToolUseContext ctx)
            {
                ApplyCount++;
            }
        }
    }
}
