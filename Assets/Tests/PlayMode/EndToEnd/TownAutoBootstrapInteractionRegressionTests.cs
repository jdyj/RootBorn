using System.Collections;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using NUnit.Framework;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Quests;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    // Regression guard for the 2026-05-12 bug where LocationNpcRuntimeInstaller had no
    // [RuntimeInitializeOnLoadMethod] entry point. Production builds therefore loaded the
    // Town scene without ever instantiating NPC or LocationVisit GameObjects, so pressing E
    // did nothing even though every interaction class compiled and tested correctly. This
    // test deliberately does NOT call EnsureForActiveScene; the scene must come up via the
    // automatic Bootstrap path alone, and the player must be able to walk to a real NPC and
    // open dialogue with the E key. If the auto-bootstrap regresses, this test fails.
    public sealed class TownAutoBootstrapInteractionRegressionTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            _saveRoot = System.IO.Path.Combine(Application.temporaryCachePath, "rootborn-autoboot-" + System.Guid.NewGuid().ToString("N"));
            Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(_saveRoot);
            Rootborn.Game.Save.ActiveSaveContext.Clear();
            EnsureEventSystem();
            yield return null;
        }

        private string _saveRoot;

        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            Rootborn.Game.Save.ActiveSaveContext.Clear();
            Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(null);
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        // Cells painted Block in the LDtk Collision layer stop the player in play; cells painted Open have no tile collider.
        [UnityTest]
        public IEnumerator TOWN_PAINTED_COLLISION_001_PaintedBlockCellsAreSolidAndOpenCellsAreClear()
        {
            yield return Rootborn.Tests.PlayMode.HousePlacementTestDriver.EnterWorld("painted", "Town", "town-home-entry");
            yield return PlayerFlowE2EDriver.WaitForArrival();
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            UnityEngine.Tilemaps.Tilemap blocks = null, opens = null;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var map in root.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true))
            {
                if (map.name == "LdtkPaintedCollision") blocks = map;
                if (map.name == "LdtkPaintedOpen") opens = map;
            }
            var solid = new ContactFilter2D { useTriggers = false };
            var hits = new System.Collections.Generic.List<Collider2D>();
            int checkedBlocks = 0;
            if (blocks != null)
            {
                blocks.CompressBounds();
                foreach (var cell in blocks.cellBounds.allPositionsWithin)
                {
                    if (!blocks.HasTile(cell)) continue;
                    Vector2 centre = blocks.GetCellCenterWorld(cell);
                    Physics2D.OverlapPoint(centre, solid, hits);
                    Assert.IsTrue(hits.Exists(h => h.gameObject == blocks.gameObject), "Painted Block cell " + cell + " at " + centre + " has no collider in play.");
                    checkedBlocks++;
                }
            }
            if (opens != null)
            {
                opens.CompressBounds();
                foreach (var cell in opens.cellBounds.allPositionsWithin)
                {
                    if (!opens.HasTile(cell)) continue;
                    Vector2 centre = opens.GetCellCenterWorld(cell);
                    Physics2D.OverlapPoint(centre, solid, hits);
                    Assert.IsFalse(hits.Exists(h => h.GetComponent<UnityEngine.Tilemaps.Tilemap>() != null), "Painted Open cell " + cell + " at " + centre + " still has a tile collider in play.");
                }
            }
            Debug.Log("[ROOTBORN] Painted collision checked: " + checkedBlocks + " block cell(s), open marker " + (opens != null));
        }

        [UnityTest]
        public IEnumerator TOWN_AUTOBOOT_001_LoadTown_NpcAndLocationVisitSpawnAutomatically_EOpensDialogue()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            // Enter Town as a new game does, so the guide's dialogue binds to the slot owner like in play.
            yield return Rootborn.Tests.PlayMode.HousePlacementTestDriver.EnterWorld("autoboot-npc", "Town", "town-home-entry");

            // No explicit installer calls. The fix under test is the [RuntimeInitializeOnLoadMethod]
            // entry point + sceneLoaded handler in LocationNpcRuntimeInstaller.
            yield return WaitForAutoBootstrap(15f);

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "Player was not created by TownPlayableBaselineRuntimeInstaller.");
            var router = player.GetComponent<PlayerInteractionRouter>();
            Assert.IsNotNull(router, "PlayerInteractionRouter was not attached.");

            // Any townsperson scheduled in Town now. (The first guide, Mom, lives at home since the 11-07 story change.)
            var guide = FindTownNpc();
            Assert.IsNotNull(guide, "No NPC was auto-spawned in Town. LocationNpcRuntimeInstaller bootstrap regressed.");
            Assert.IsNotNull(guide.GetComponent<NpcInteractor>(), "NpcInteractor missing on the auto-spawned NPC " + guide.name + ".");

            var townSquare = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"));
            Assert.IsNotNull(townSquare, "Town square LocationVisit was not auto-spawned.");
            Assert.IsNotNull(townSquare.GetComponent<LocationVisitInteractor>(), "LocationVisitInteractor missing on auto-spawned town square.");

            yield return WalkPlayerWithKeyboardTo(player, keyboard, guide.transform.position, 0.3f);
            router.RefreshPromptNow();
            Assert.IsTrue(router.PromptVisible, "Interaction prompt was not visible after walking to the guide NPC.");
            StringAssert.Contains(Rootborn.Game.Localization.GameText.Localized("ui.prompt.talk", "Talk"), router.PromptText, "Prompt did not advertise the NPC interaction.");

            yield return PressInteractKey(keyboard);
            yield return WaitForDialoguePanelOpen(5f);
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " must be in BuildSettings.");
            while (!op.isDone) yield return null;
        }

        private static GameObject FindTownNpc()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var npc in Object.FindObjectsByType<NpcInteractor>(FindObjectsSortMode.None))
                if (npc.isActiveAndEnabled && npc.gameObject.scene == scene && npc.name.StartsWith("TownNpc_", System.StringComparison.Ordinal))
                    return npc.gameObject;
            return null;
        }

        private static IEnumerator WaitForAutoBootstrap(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var guide = FindTownNpc();
                var square = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"));
                if (player != null
                    && player.GetComponent<PlayerInteractionRouter>() != null
                    && player.GetComponent<StudentLifeProgressComponent>() != null
                    && guide != null && guide.GetComponent<NpcInteractor>() != null
                    && square != null && square.GetComponent<LocationVisitInteractor>() != null)
                {
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("Auto-bootstrap did not produce Player + NPC + LocationVisit within " + timeoutSeconds + "s. LocationNpcRuntimeInstaller did not run automatically.");
        }

        private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 target, float tolerance)
        {
            // Routed around buildings by the shared driver; movement still comes from WASD key events.
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(player, keyboard, target, tolerance, 600, "Keyboard movement could not reach the NPC.");
        }

        private IEnumerator PressInteractKey(Keyboard keyboard)
        {
            Press(keyboard.eKey);
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            Release(keyboard.eKey);
            InputSystem.Update();
            yield return null;
        }

        private static IEnumerator WaitForDialoguePanelOpen(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panels = Object.FindObjectsByType<DialoguePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < panels.Length; i++)
                {
                    if (panels[i] != null && panels[i].gameObject.activeInHierarchy)
                    {
                        yield break;
                    }
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("DialoguePanel did not open after pressing E on the guide NPC.");
        }

        private static KeyControl ControlFor(Keyboard keyboard, Key key)
        {
            if (key == Key.A) return keyboard.aKey;
            if (key == Key.D) return keyboard.dKey;
            if (key == Key.W) return keyboard.wKey;
            if (key == Key.S) return keyboard.sKey;
            Assert.Fail("Unsupported key: " + key);
            return keyboard.eKey;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<Rootborn.Game.Common.PassiveInputModule>();
        }
    }
}
