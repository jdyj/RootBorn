using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Spum;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class SpumPlayerVisualMoveE2ETests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator SPUM_PLAYER_MOVE_E2E_001_KeyboardRightMovementDrivesSpumViewMoveAndFlip()
        {
            var player = new GameObject("Player");
            var visualChild = new GameObject("SpumBody");
            visualChild.transform.SetParent(player.transform, false);
            var renderer = visualChild.AddComponent<SpriteRenderer>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var view = player.AddComponent<SpumCharacterVisualView>();
                view.ConfigureForTests(null, player.transform, new[] { renderer });
                var adapter = player.AddComponent<PlayerCharacterVisualAdapter>();
                adapter.ConfigureForTests(view);
                player.AddComponent<PlayerController>();

                yield return null;
                Press(keyboard.dKey);
                InputSystem.Update();
                for (int i = 0; i < 12 && view.CurrentMotionState != "MOVE"; i++)
                {
                    yield return null;
                }

                Assert.AreEqual("MOVE", view.CurrentMotionState, "Actual D-key movement must drive the SPUM visual view into MOVE.");
                Assert.IsTrue(renderer.flipX, "Actual right movement must use visual-only right-facing flip on the SPUM renderers.");
            }
            finally
            {
                Release(keyboard.dKey);
                InputSystem.Update();
                InputSystem.RemoveDevice(keyboard);
                Object.Destroy(player);
            }
        }
    }
}
