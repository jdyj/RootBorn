using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine.TestTools;

[assembly: Rootborn.Tests.PlayMode.PlayModeInputSystemGuardAction]

namespace Rootborn.Tests.PlayMode
{
    // New Game now opens the character creator. Scenario tests that are not about it press its real confirm button after a
    // short wait, so tests that confirm by themselves still go first. Tests about the creator opt out by name.
    // Preparation only, not player-input evidence: the creator's own E2E tests click Confirm with a mouse device.
    internal sealed class PlayModeCreatorAutoConfirm : UnityEngine.MonoBehaviour
    {
        private const float WaitSeconds = 3f, CheckSeconds = 0.25f;
        private static PlayModeCreatorAutoConfirm s_instance;
        private float _nextCheck, _openSince = -1f;

        public static void ConfigureFor(string testName)
        {
            if (s_instance == null)
            {
                var go = new UnityEngine.GameObject("[PlayModeCreatorAutoConfirm]");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<PlayModeCreatorAutoConfirm>();
            }
            s_instance._openSince = -1f;
        }

        private static bool CurrentTestIsAboutCreator()
        {
            string name = TestContext.CurrentContext?.Test?.FullName ?? string.Empty;
            return name.Contains("Spum") || name.Contains("CharacterPart") || name.Contains("Creator");
        }

        private void Update()
        {
            float now = UnityEngine.Time.unscaledTime;
            if (now < _nextCheck) return;
            _nextCheck = now + CheckSeconds;
            if (CurrentTestIsAboutCreator()) return;
            var panel = UnityEngine.GameObject.Find("SpumCharacterCreatorPanel");
            if (panel == null) { _openSince = -1f; return; }
            if (_openSince < 0f) _openSince = now;
            if (now - _openSince < WaitSeconds) return;
            foreach (var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                if (button.name != "ConfirmButton" || !button.IsInteractable()) continue;
                _openSince = -1f;
                button.onClick.Invoke();
                return;
            }
        }
    }

    // The House furniture panel starts closed and shows 20 items per page, the way players see it.
    internal static class HousePlacementTestDriver
    {
        public const string ToggleName = "PlacementModeToggleButton";

        // Enters a world scene the way a new game does, so scene systems (NPC dialogue, furniture) bind to the slot's owner.
        // Loading a scene directly skips that binding and leaves those systems inert.
        public static System.Collections.IEnumerator EnterWorld(string slotId, string sceneName, string spawnId, float timeoutSeconds = 20f)
        {
            var bootstrap = Rootborn.Game.Managers.Managers.BootstrapAsync();
            while (!bootstrap.IsCompleted) yield return null;
            var metadata = new Rootborn.Game.Save.SaveSlotMetadata { SlotId = slotId, DisplayName = slotId, WorldSeed = 1205, TileSeed = 1205 };
            Assert.IsTrue(Rootborn.Game.World.WorldTravelRuntime.TryRequestEntry(metadata, sceneName, spawnId), sceneName + " entry request was refused.");
            float deadline = UnityEngine.Time.realtimeSinceStartup + timeoutSeconds;
            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == sceneName)
                    foreach (var owner in UnityEngine.Object.FindObjectsByType<Rootborn.UI.Careers.AdultCareerRuntimeComponent>(UnityEngine.FindObjectsSortMode.None))
                        if (owner.CanMutate && owner.gameObject.scene.name == sceneName) yield break;
                yield return null;
            }
            Assert.Fail(sceneName + " owner did not bind after entering as a player. " + OverlayDiagnostics());
        }

        // Enters House the way a new game does, so the furniture session binds to the slot's owner.
        // Loading the scene directly leaves the authored House without a placement session.
        public static System.Collections.IEnumerator EnterHouse(string slotId, float timeoutSeconds = 20f)
        {
            var bootstrap = Rootborn.Game.Managers.Managers.BootstrapAsync();
            while (!bootstrap.IsCompleted) yield return null;
            var metadata = new Rootborn.Game.Save.SaveSlotMetadata { SlotId = slotId, DisplayName = slotId, WorldSeed = 1205, TileSeed = 1205 };
            Assert.IsTrue(Rootborn.Game.World.WorldTravelRuntime.TryRequestEntry(metadata, "House", "house-entry"), "House entry request was refused.");
            float deadline = UnityEngine.Time.realtimeSinceStartup + timeoutSeconds;
            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                foreach (var session in UnityEngine.Object.FindObjectsByType<Rootborn.Game.Placement.AuthoredHouseFurnitureSession>(UnityEngine.FindObjectsSortMode.None))
                    if (session.Surface != null && session.gameObject.scene.name == "House") yield break;
                yield return null;
            }
            Assert.Fail("House furniture session did not bind after entering as a player. " + OverlayDiagnostics());
        }

        // Explains a missing placement overlay: none created, created inactive, or created in another scene.
        public static string OverlayDiagnostics()
        {
            var sb = new System.Text.StringBuilder("overlays:");
            foreach (var o in UnityEngine.Object.FindObjectsByType<Rootborn.UI.Interiors.InteriorPlacementPreviewOverlay>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
                sb.Append(' ').Append(o.name).Append('@').Append(o.gameObject.scene.name).Append(o.gameObject.activeInHierarchy ? "/active" : "/inactive");
            sb.Append(" panels:");
            foreach (var p in UnityEngine.Object.FindObjectsByType<Rootborn.UI.Interiors.InteriorFurniturePlacementPanel>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
                sb.Append(' ').Append(p.gameObject.scene.name).Append(p.gameObject.activeInHierarchy ? "/active" : "/inactive").Append(" parent=").Append(p.transform.parent != null ? p.transform.parent.name : "-");
            sb.Append(" sessions:");
            foreach (var s in UnityEngine.Object.FindObjectsByType<Rootborn.Game.Placement.AuthoredHouseFurnitureSession>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
                sb.Append(' ').Append(s.gameObject.scene.name).Append(s.Surface != null ? "/surface" : "/nosurface");
            sb.Append(" owners:").Append(UnityEngine.Object.FindObjectsByType<Rootborn.UI.Careers.AdultCareerRuntimeComponent>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None).Length);
            sb.Append(" slot:").Append(Rootborn.Game.Save.ActiveSaveContext.SlotId);
            return sb.ToString();
        }

        public static System.Collections.IEnumerator OpenPlacementMode(float timeoutSeconds = 5f)
        {
            float deadline = UnityEngine.Time.unscaledTime + timeoutSeconds;
            UnityEngine.GameObject toggle = null;
            Rootborn.UI.Interiors.InteriorFurniturePlacementPanel panel = null;
            while (UnityEngine.Time.unscaledTime < deadline)
            {
                toggle = UnityEngine.GameObject.Find(ToggleName);
                panel = UnityEngine.Object.FindFirstObjectByType<Rootborn.UI.Interiors.InteriorFurniturePlacementPanel>(UnityEngine.FindObjectsInactive.Include);
                if (toggle != null && panel != null) break;
                yield return null;
            }
            Assert.IsNotNull(toggle, "House must show the placement mode button.");
            Assert.IsNotNull(panel, "House must have the furniture placement panel.");
            if (!panel.gameObject.activeSelf)
            {
                // Mouse device through the scene UI module, never a direct handler call.
                yield return Rootborn.Tests.PlayMode.EndToEnd.Support.PlayerFlowE2EDriver.ClickWithMouse(toggle, toggle.name);
            }
            yield return null;
            Assert.IsTrue(panel.gameObject.activeInHierarchy, "The placement mode button must open the furniture panel.");
        }

        public static void SearchPalette(string query)
        {
            var input = UnityEngine.GameObject.Find("FurniturePaletteSearchInput")?.GetComponent<UnityEngine.UI.InputField>();
            if (input != null && input.text != query) input.text = query;
        }

        // Returns the palette button for a furniture id, typing it into the search field when it is not on the first page.
        public static UnityEngine.GameObject ShowFurniture(string furnitureId)
        {
            var button = UnityEngine.GameObject.Find("PaletteFurniture_" + furnitureId);
            if (button != null) return button;
            var input = UnityEngine.GameObject.Find("FurniturePaletteSearchInput")?.GetComponent<UnityEngine.UI.InputField>();
            if (input == null) return null;
            input.text = furnitureId;
            return UnityEngine.GameObject.Find("PaletteFurniture_" + furnitureId);
        }
    }

    // The approved urban-molding skin (AGENTS.md, 2026-10-27) draws one sliced image per panel instead of 3x3 Style2 tiles.
    internal static class UrbanSkinCheck
    {
        public static int SkinnedPanelCount(UnityEngine.GameObject root)
        {
            int count = 0;
            if (root == null) return 0;
            foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (image.gameObject.name == "[UrbanSkin]" && image.gameObject.activeSelf && image.sprite != null
                    && image.type == UnityEngine.UI.Image.Type.Sliced) count++;
            }
            return count;
        }

        public static bool TilesOrSkin(UnityEngine.GameObject root, int tileCount, int minimumTiles)
            => tileCount >= minimumTiles || SkinnedPanelCount(root) > 0;
    }

    // Applies to every test under this namespace, with or without InputTestFixture.
    [SetUpFixture]
    public sealed class PlayModeScenarioStart
    {
        [OneTimeSetUp]
        public void Apply()
        {
            // Scenario tests start from Town. The House start is covered by NewGame_StartsInTheHouse and real-input play.
            Rootborn.Game.World.WorldEntryService.SetNewGameEntryForTests("Town", "town-home-entry");
            PlayModeCreatorAutoConfirm.ConfigureFor(string.Empty);
        }

        // A player cannot press confirm in the first frames after the menu loads, while game data is still loading. Tests can.
        public static System.Collections.IEnumerator WaitForGameData()
        {
            // The game loads its data from the Boot scene. Tests that open MainMenu directly skip Boot, so start the same load here.
            // Without this the first test in a run waits for data that nothing is loading.
            var bootstrap = Rootborn.Game.Managers.Managers.BootstrapAsync();
            float deadline = UnityEngine.Time.unscaledTime + 15f;
            while (!bootstrap.IsCompleted && UnityEngine.Time.unscaledTime < deadline) yield return null;
            while (!Rootborn.Game.Common.GameDataRegistryProvider.TryGet(out _) && UnityEngine.Time.unscaledTime < deadline) yield return null;
            UnityEngine.Debug.Log("[ROOTBORN/Test] Game data ready=" + Rootborn.Game.Common.GameDataRegistryProvider.TryGet(out _) + " bootstrap=" + bootstrap.Status);
            yield return null;
        }

        [OneTimeTearDown]
        public void Clear() => Rootborn.Game.World.WorldEntryService.SetNewGameEntryForTests(null, null);
    }

    public abstract class PlayModeInputFixture : UnityEngine.InputSystem.InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            // The test editor runs in a virtual display whose Game View can lose focus for a moment (window moves, captures).
            // With the default settings the Input System then drops keyboard and pointer events, so a press or click simply
            // vanished in some runs (trace: mouse position stayed (0,0), E did nothing). Test input always goes to the game.
            UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode =
                UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            PlayModeInputSystemGuardAction.ForgetResolvedControls();
            PlayModeCreatorAutoConfirm.ConfigureFor(TestContext.CurrentContext.Test.FullName);
            // Scenario tests start from Town. The House start is covered by NewGame_StartsInTheHouse and real-input play.
            Rootborn.Game.World.WorldEntryService.SetNewGameEntryForTests("Town", "town-home-entry");
        }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class PlayModeInputSystemGuardAction : Attribute, ITestAction
    {
        public ActionTargets Targets => ActionTargets.Test;

        // InputTestFixture swaps the Input System per test. Shared action assets keep controls resolved against the swapped
        // system, and enabling them afterwards throws IndexOutOfRange inside the Input System, failing unrelated tests.
        internal static void ForgetResolvedControls()
        {
            foreach (var asset in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.InputSystem.InputActionAsset>())
            {
                try
                {
                    bool wasEnabled = asset.enabled;
                    asset.Disable();
                    asset.bindingMask = new UnityEngine.InputSystem.InputBinding { groups = "__test-reset__" };
                    asset.bindingMask = null;
                    if (wasEnabled) asset.Enable();
                }
                catch (Exception error)
                {
                    UnityEngine.Debug.LogWarning("Input action reset failed for " + asset.name + ": " + error.Message);
                }
            }
        }

        public void BeforeTest(ITest test)
        {
            // Scenario tests here start from Town. The House start is covered by NewGame_StartsInTheHouse and real-input play.
            Rootborn.Game.World.WorldEntryService.SetNewGameEntryForTests("Town", "town-home-entry");
            LogAssert.ignoreFailingMessages = true;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
        }

        public void AfterTest(ITest test)
        {
            LogAssert.ignoreFailingMessages = true;
            ForgetResolvedControls();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
        }
    }

    // The local player's quest session for the active save slot; it replaces the removed hidden quest list panel.
    internal static class QuestSessionLookup
    {
        public static Rootborn.Game.Player.PlayerQuestSession Active()
        {
            return Rootborn.Game.Player.PlayerQuestRuntime.TryGetActive(Rootborn.Game.Player.PlayerIdentity.DefaultPlayerId, out var session) ? session : null;
        }
    }
}
