using System.Collections;
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
    public sealed class TownAutoBootstrapInteractionRegressionTests : InputTestFixture
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            EnsureEventSystem();
            yield return null;
        }

        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TOWN_AUTOBOOT_001_LoadTown_NpcAndLocationVisitSpawnAutomatically_EOpensDialogue()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return LoadScene("Town");

            // No explicit installer calls. The fix under test is the [RuntimeInitializeOnLoadMethod]
            // entry point + sceneLoaded handler in LocationNpcRuntimeInstaller.
            yield return WaitForAutoBootstrap(15f);

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "Player was not created by TownPlayableBaselineRuntimeInstaller.");
            var router = player.GetComponent<PlayerInteractionRouter>();
            Assert.IsNotNull(router, "PlayerInteractionRouter was not attached.");

            var guide = GameObject.Find(LocationNpcRuntimeInstaller.NpcObjectName("npc.first-guide"));
            Assert.IsNotNull(guide, "First-guide NPC was not auto-spawned. LocationNpcRuntimeInstaller bootstrap regressed.");
            Assert.IsNotNull(guide.GetComponent<NpcInteractor>(), "NpcInteractor missing on auto-spawned guide.");

            var townSquare = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"));
            Assert.IsNotNull(townSquare, "Town square LocationVisit was not auto-spawned.");
            Assert.IsNotNull(townSquare.GetComponent<LocationVisitInteractor>(), "LocationVisitInteractor missing on auto-spawned town square.");

            yield return WalkPlayerWithKeyboardTo(player, keyboard, guide.transform.position, 0.3f);
            router.RefreshPromptNow();
            Assert.IsTrue(router.PromptVisible, "Interaction prompt was not visible after walking to the guide NPC.");
            StringAssert.Contains("Talk", router.PromptText, "Prompt did not advertise the NPC interaction.");

            yield return PressInteractKey(keyboard);
            yield return WaitForDialoguePanelOpen(5f);
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " must be in BuildSettings.");
            while (!op.isDone) yield return null;
        }

        private static IEnumerator WaitForAutoBootstrap(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var guide = GameObject.Find(LocationNpcRuntimeInstaller.NpcObjectName("npc.first-guide"));
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
            Key activeKey = Key.None;
            for (int i = 0; i < 600 && Vector3.Distance(player.transform.position, target) > tolerance; i++)
            {
                Vector3 delta = target - player.transform.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? (delta.x >= 0f ? Key.D : Key.A) : (delta.y >= 0f ? Key.W : Key.S);
                if (activeKey != key)
                {
                    if (activeKey != Key.None) Release(ControlFor(keyboard, activeKey));
                    Press(ControlFor(keyboard, key));
                    activeKey = key;
                }
                InputSystem.Update();
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            if (activeKey != Key.None) Release(ControlFor(keyboard, activeKey));
            InputSystem.Update();
            yield return new WaitForFixedUpdate();
            Assert.LessOrEqual(Vector3.Distance(player.transform.position, target), tolerance, "Keyboard movement could not reach the NPC.");
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
