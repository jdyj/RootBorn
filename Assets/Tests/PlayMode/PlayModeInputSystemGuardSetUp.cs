using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine.TestTools;

[assembly: Rootborn.Tests.PlayMode.PlayModeInputSystemGuardAction]

namespace Rootborn.Tests.PlayMode
{
    // New Game now opens the character creator. Scenario tests that are not about it press its real confirm button after a
    // short wait, so tests that confirm by themselves still go first. Tests about the creator opt out by name.
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

    // Reasons for tests set aside because what they check is no longer in the game by design.
    // Town and House are now authored scenes; the runtime installers skip their old generated objects there.
    internal static class LegacyPremise
    {
        public const string AuthoredTown = "레거시 전제: 저작 Town에서는 만들지 않는 프로토타입 오브젝트를 기다림 — ";
        public const string AuthoredHouse = "레거시 전제: 저작 House에서는 생성하지 않는 옛 집 생성·배치 흐름 — ";
        public const string Farm = "레거시 전제: Town에서 더 이상 이어지지 않는 Farm 씬과 옛 HUD — ";
        public const string ObjectiveJournal = "레거시 전제: 목표 표시는 Tab 목표 저널로 옮겨 옛 패널을 숨김 — ";
        public const string CharacterParts = "레거시 전제: 성장 캐릭터로 바뀐 옛 부위 조합 캐릭터 — ";
    }

    // The House furniture panel starts closed and shows 20 items per page, the way players see it.
    internal static class HousePlacementTestDriver
    {
        public const string ToggleName = "PlacementModeToggleButton";

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
                UnityEngine.EventSystems.ExecuteEvents.Execute(toggle,
                    new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),
                    UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
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
            float deadline = UnityEngine.Time.unscaledTime + 15f;
            while (!Rootborn.Game.Common.GameDataRegistryProvider.TryGet(out _) && UnityEngine.Time.unscaledTime < deadline) yield return null;
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
}
