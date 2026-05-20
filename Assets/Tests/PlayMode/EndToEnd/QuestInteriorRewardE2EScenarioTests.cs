using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Placement;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using Rootborn.UI.Interiors;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Objectives;
using Rootborn.UI.Quests;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class QuestInteriorRewardE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private const string ChainId = "questchain.test.career-interest.library";
        private const string ChainCardName = "QuestChainCard_questchain_test_career_interest_library";
        private const string UnlockToken = "unlock.furniture.quest.library-chair";
        private const string FurnitureId = "chair.quest-reward";
        private const string FurnitureButtonName = "PaletteFurniture_chair.quest-reward";
        private const string FurnitureTileName = "BlackChair_00";
        private const string ScreenshotPath = "Builds/Logs/quest-interior-reward-e2e.png";

        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            ClearStaticFurnitureLayoutCache();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-quest-interior-reward-e2e", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            ScreenshotEvidence.DeleteIfExists(ScreenshotPath);
            yield return null;
        }

        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            SaveService.SetRootDirectoryForTests(null);
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            ClearStaticFurnitureLayoutCache();
            CleanupResidue();
            InputSystemUiModulePlayModeTestGuard.UninstallForCurrentTest();
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindTownRuntime();
            string playerId = ResolvePlayerId(runtime.Player);
            Assert.IsFalse(FurnitureUnlockProgressPersistence.LoadOrCreate(TestSlotId, playerId).IsUnlocked(UnlockToken), "QUEST-INTERIOR-REWARD-E2E-001 failed: furniture unlock token should start locked.");
            AssertFurnitureCatalogState(false, "QUEST-INTERIOR-REWARD-E2E-001 failed: locked quest reward furniture was visible before reward claim.");

            yield return PlayerFlowE2EDriver.ClickButton(FindRootButton("QuestChainLogOpenButton"));
            AssertPanelContains(runtime.ChainPanel.transform, ChainId, "QUEST-INTERIOR-REWARD-E2E-001 failed: quest-chain panel did not show the interior reward chain.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.ChainPanel.transform, ChainCardName));
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.ChainPanel.transform, "QuestChainAcceptButton"));
            AssertPanelContains(runtime.ChainPanel.transform, "Active", "QUEST-INTERIOR-REWARD-E2E-001 failed: accept button did not activate quest chain.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.ChainPanel.transform, "QuestChainTrackButton"));
            AssertPanelContains(runtime.ChainPanel.transform, "Tracked", "QUEST-INTERIOR-REWARD-E2E-001 failed: track button did not track quest chain.");
            runtime.ChainPanel.Hide();

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Library.transform.position, 0.25f, 720, "QUEST-INTERIOR-REWARD-E2E-001 failed: keyboard movement could not reach Library.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "QUEST-INTERIOR-REWARD-E2E-001 failed: Library prompt was not visible.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return WaitForLocationPanel(3f, "QUEST-INTERIOR-REWARD-E2E-001 failed: location activity panel did not open.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.LocationPanel.transform, "LocationActivityButton_0"));
            yield return null;
            AssertPanelContains(runtime.ChainPanel.transform, "Completed", "QUEST-INTERIOR-REWARD-E2E-001 failed: actual location activity did not complete quest chain.");

            yield return PlayerFlowE2EDriver.ClickButton(FindRootButton("QuestChainLogOpenButton"));
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.ChainPanel.transform, "QuestChainClaimRewardButton"));
            AssertPanelContains(runtime.ChainPanel.transform, "Reward: Claimed", "QUEST-INTERIOR-REWARD-E2E-001 failed: claim button did not mark quest-chain reward claimed.");
            Assert.IsFalse(PlayerFlowE2EDriver.FindButton(runtime.ChainPanel.transform, "QuestChainClaimRewardButton").interactable, "QUEST-INTERIOR-REWARD-E2E-001 failed: completion reward stayed claimable after claim.");
            Assert.IsTrue(FurnitureUnlockProgressPersistence.LoadOrCreate(TestSlotId, playerId).IsUnlocked(UnlockToken), "QUEST-INTERIOR-REWARD-E2E-001 failed: reward claim did not persist the furniture unlock token.");
            AssertFurnitureCatalogState(true, "QUEST-INTERIOR-REWARD-E2E-001 failed: unlocked quest reward furniture was not visible in runtime catalog after reward claim.");
            runtime.ObjectiveProvider.RefreshNow();
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            Assert.IsTrue(runtime.Journal.gameObject.activeInHierarchy, "QUEST-INTERIOR-REWARD-E2E-001 failed: Tab did not show Objective Journal after reward claim.");
            ScreenshotEvidence.Capture(ScreenshotPath);
            yield return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, 3f, "QUEST-INTERIOR-REWARD-E2E-001 failed: Town quest reward Game View screenshot evidence was not written.");
            runtime.ChainPanel.Hide();

            AssertFileContains(FurnitureUnlockProgressPersistence.FileNameFor(playerId), UnlockToken, "QUEST-INTERIOR-REWARD-E2E-001 failed: furniture unlock file did not contain the unlock token.");

            yield return PlayerFlowE2EDriver.LoadScene("House");
            yield return WaitForStableScene("House", 0.3f, 10f, "QUEST-INTERIOR-REWARD-E2E-001 failed: project-supported House scene transition did not stabilize after quest reward claim.");
            yield return PlaceUnlockedFurnitureByPlayerFacingInput();

            var furnitureTilemap = RequiredTilemap("HouseFurnitureObjectTilemap");
            var occupancyTilemap = RequiredTilemap("HouseFurnitureOccupancyTilemap");
            Assert.IsTrue(ContainsTile(furnitureTilemap, FurnitureTileName, out var placedCell), "QUEST-INTERIOR-REWARD-E2E-001 failed: unlocked furniture was not visibly placed in House tilemap.");
            Assert.IsNotNull(occupancyTilemap.GetTile(placedCell), "QUEST-INTERIOR-REWARD-E2E-001 failed: placed unlocked furniture did not write occupancy.");
            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            StringAssert.Contains(FurnitureId, overlay.LastMessage, "QUEST-INTERIOR-REWARD-E2E-001 failed: placement runtime state did not record selected furniture id.");

            yield return PlayerFlowE2EDriver.ClickButton(FindRootButton("SaveFurnitureLayoutButton"));
            string layoutPath = FindLayoutFilePath();
            Assert.IsTrue(File.Exists(layoutPath), "QUEST-INTERIOR-REWARD-E2E-001 failed: House furniture layout save file was not written.");
            string layoutJson = File.ReadAllText(layoutPath);
            StringAssert.Contains(FurnitureId, layoutJson, "QUEST-INTERIOR-REWARD-E2E-001 failed: saved furniture layout did not include unlocked furniture id.");
            StringAssert.Contains(FurnitureTileName, layoutJson, "QUEST-INTERIOR-REWARD-E2E-001 failed: saved furniture layout did not include placed tile name.");
            ScreenshotEvidence.Capture(ScreenshotPath);
            yield return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, 3f, "QUEST-INTERIOR-REWARD-E2E-001 failed: House placement Game View screenshot evidence was not written.");

            ClearStaticFurnitureLayoutCache();
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 15f, "QUEST-INTERIOR-REWARD-E2E-001 failed: SaveSlot Load did not re-enter Town.");
            EnsureTownInstallers();
            yield return WaitForTownRuntime(15f);

            var reloadedTown = FindTownRuntime();
            string reloadedPlayerId = ResolvePlayerId(reloadedTown.Player);
            Assert.IsTrue(FurnitureUnlockProgressPersistence.LoadOrCreate(TestSlotId, reloadedPlayerId).IsUnlocked(UnlockToken), "QUEST-INTERIOR-REWARD-E2E-001 failed: furniture unlock token did not survive SaveSlot reload.");
            yield return PlayerFlowE2EDriver.ClickButton(FindRootButton("QuestChainLogOpenButton"));
            AssertPanelContains(reloadedTown.ChainPanel.transform, "Reward: Claimed", "QUEST-INTERIOR-REWARD-E2E-001 failed: reward claim state did not survive reload.");
            Assert.IsFalse(PlayerFlowE2EDriver.FindButton(reloadedTown.ChainPanel.transform, "QuestChainClaimRewardButton").interactable, "QUEST-INTERIOR-REWARD-E2E-001 failed: reward became claimable again after reload.");

            yield return PlayerFlowE2EDriver.LoadScene("House");
            yield return WaitForStableScene("House", 0.3f, 10f, "QUEST-INTERIOR-REWARD-E2E-001 failed: House scene did not stabilize after reload.");
            var restoredTilemap = RequiredTilemap("HouseFurnitureObjectTilemap");
            var restoredOccupancy = RequiredTilemap("HouseFurnitureOccupancyTilemap");
            Assert.IsTrue(ContainsTile(restoredTilemap, FurnitureTileName, out var restoredCell), "QUEST-INTERIOR-REWARD-E2E-001 failed: placed unlocked furniture did not restore visibly after reload.");
            Assert.AreEqual(placedCell, restoredCell, "QUEST-INTERIOR-REWARD-E2E-001 failed: restored unlocked furniture moved after reload.");
            Assert.IsNotNull(restoredOccupancy.GetTile(restoredCell), "QUEST-INTERIOR-REWARD-E2E-001 failed: restored unlocked furniture did not restore occupancy.");
            Assert.AreEqual(1, CountTiles(restoredTilemap, FurnitureTileName), "QUEST-INTERIOR-REWARD-E2E-001 failed: reload duplicated the placed unlocked furniture tile.");
        }

        private IEnumerator StartNewTownFromSaveSlotUi()
        {
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            var creator = GameObject.Find("SpumCharacterCreatorRoot") ?? GameObject.Find("SpumCharacterCreatorPanel");
            if (creator != null) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(creator.transform, "ConfirmButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 15f, "QUEST-INTERIOR-REWARD-E2E-001 failed: SaveSlot New Game did not enter Town.");
            EnsureTownInstallers();
            yield return WaitForTownRuntime(15f);
        }

        private static void EnsureTownInstallers()
        {
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            DailyPlanRuntimeInstaller.EnsureForActiveScene();
            QuestChainRuntimeInstaller.EnsureForActiveScene();
            CampaignRuntimeInstaller.EnsureForActiveScene();
        }

        private static IEnumerator WaitForTownRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            string lastState = string.Empty;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var library = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.library"));
                var chainPanel = Object.FindFirstObjectByType<QuestChainLogPanel>(FindObjectsInactive.Include);
                var locationPanel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
                var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
                var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
                lastState = "player=" + (player != null)
                    + " router=" + (player != null && player.GetComponent<PlayerInteractionRouter>() != null)
                    + " library=" + (library != null)
                    + " chainPanel=" + (chainPanel != null)
                    + " locationPanel=" + (locationPanel != null)
                    + " journal=" + (journal != null)
                    + " provider=" + (provider != null)
                    + " eventSystem=" + (EventSystem.current != null)
                    + " questButton=" + (GameObject.Find("QuestChainLogOpenButton") != null);

                if (player != null && player.GetComponent<PlayerInteractionRouter>() != null && library != null && chainPanel != null && locationPanel != null && journal != null && provider != null && EventSystem.current != null && GameObject.Find("QuestChainLogOpenButton") != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("QUEST-INTERIOR-REWARD-E2E-001 failed: Town runtime did not install within timeout. Last state: " + lastState);
        }

        private static QuestInteriorRuntime FindTownRuntime()
        {
            var player = GameObject.Find("Player");
            return new QuestInteriorRuntime(
                player,
                player.GetComponent<PlayerInteractionRouter>(),
                GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.library")).GetComponent<LocationActivityInteractor>(),
                Object.FindFirstObjectByType<QuestChainLogPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include));
        }

        private static IEnumerator WaitForLocationPanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
                if (panel != null && panel.IsOpen) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure);
        }

        private static IEnumerator WaitForStableScene(string sceneName, float stableSeconds, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            float stable = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (SceneManager.GetActiveScene().name == sceneName)
                {
                    stable += Time.unscaledDeltaTime;
                    if (stable >= stableSeconds) yield break;
                }
                else
                {
                    stable = 0f;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure);
        }

        private static IEnumerator PlaceUnlockedFurnitureByPlayerFacingInput()
        {
            Assert.IsNull(GameObject.Find("ModernOfficeChairSampleTilemap"), "QUEST-INTERIOR-REWARD-E2E-001 failed: stale sample furniture tilemap must not be visible in playable House.");
            var button = GameObject.Find(FurnitureButtonName);
            Assert.IsNotNull(button, "QUEST-INTERIOR-REWARD-E2E-001 failed: unlocked furniture palette button was not visible after reward claim.");
            ExecuteEvents.Execute<IPointerClickHandler>(button, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay, "QUEST-INTERIOR-REWARD-E2E-001 failed: selecting unlocked furniture did not create placement overlay.");
            Assert.Greater(overlay.ValidCellCount, 0, "QUEST-INTERIOR-REWARD-E2E-001 failed: selecting unlocked furniture did not show valid placement cells.");
            var overlayTilemap = RequiredTilemap("HousePlacementAvailabilityTilemap");
            var camera = Camera.main;
            Assert.IsNotNull(camera, "QUEST-INTERIOR-REWARD-E2E-001 failed: House placement requires a Main Camera for world click projection.");
            Assert.IsTrue(TryFindVisibleValidOverlayCell(overlayTilemap, camera, out _, out var screenPosition), "QUEST-INTERIOR-REWARD-E2E-001 failed: no visible valid placement cell outside UI was available for mouse click.");

            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var driver = new GameObject("QuestInteriorRewardMouseClickDriver").AddComponent<MouseClickFrameDriver>();
            driver.Configure(mouse, screenPosition);
            yield return null;
            yield return null;
            InputSystem.RemoveDevice(mouse);
            Assert.AreEqual(0, overlay.ValidCellCount, "QUEST-INTERIOR-REWARD-E2E-001 failed: successful world click placement should clear availability overlay. Last message: " + overlay.LastMessage);
        }

        private static bool TryFindVisibleValidOverlayCell(Tilemap tilemap, Camera camera, out Vector3Int foundCell, out Vector2 screenPosition)
        {
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile<Tile>(position);
                if (tile == null || tile.color.g <= 0.8f || tile.color.r >= 0.3f) continue;
                var screen = camera.WorldToScreenPoint(tilemap.GetCellCenterWorld(position));
                if (screen.z > 0f && screen.x >= Screen.width * 0.2f && screen.y >= Screen.height * 0.35f && screen.x <= Screen.width * 0.8f && screen.y <= Screen.height * 0.85f && !IsPointerOverUi(screen))
                {
                    foundCell = position;
                    screenPosition = new Vector2(screen.x, screen.y);
                    return true;
                }
            }

            foundCell = default;
            screenPosition = default;
            return false;
        }

        private static bool IsPointerOverUi(Vector3 screen)
        {
            if (EventSystem.current == null) return false;
            var eventData = new PointerEventData(EventSystem.current) { position = new Vector2(screen.x, screen.y) };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0;
        }

        private static Tilemap RequiredTilemap(string name)
        {
            var tilemap = GameObject.Find(name)?.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap, "QUEST-INTERIOR-REWARD-E2E-001 failed: required Tilemap missing: " + name);
            return tilemap;
        }

        private static bool ContainsTile(Tilemap tilemap, string tileName, out Vector3Int foundCell)
        {
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(position);
                if (tile != null && tile.name == tileName)
                {
                    foundCell = position;
                    return true;
                }
            }

            foundCell = default;
            return false;
        }

        private static int CountTiles(Tilemap tilemap, string tileName)
        {
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(position);
                if (tile != null && tile.name == tileName) count++;
            }
            return count;
        }

        private static string ResolvePlayerId(GameObject player)
        {
            var identity = player != null ? player.GetComponent<PlayerIdentity>() : null;
            return identity != null && !string.IsNullOrEmpty(identity.PlayerId) ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
        }

        private static Button FindRootButton(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, "UI button root missing: " + name);
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, name + " does not have Button component.");
            return button;
        }

        private static void AssertPanelContains(Transform root, string expected, string failure)
        {
            var texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) if (texts[i].text.Contains(expected)) return;
            Assert.Fail(failure + " Missing text: " + expected + "\n" + PlayerFlowE2EDriver.DescribeButtons(root));
        }

        private static void AssertFurnitureCatalogState(bool expectedVisible, string failure)
        {
            var registry = Rootborn.Game.Managers.Managers.Data.Registry;
            var ids = InteriorFurnitureCatalog.LoadFurniture(registry).Select(item => item.Id).ToArray();
            bool visible = ids.Contains(FurnitureId);
            Assert.AreEqual(expectedVisible, visible, failure + " Catalog ids=" + string.Join(",", ids));
        }

        private void AssertFileContains(string fileName, string expected, string failure)
        {
            string path = Path.Combine(_saveRoot, TestSlotId, fileName);
            Assert.IsTrue(File.Exists(path), failure + " Missing file: " + path);
            StringAssert.Contains(expected, File.ReadAllText(path), failure);
        }

        private string FindLayoutFilePath()
        {
            string dir = Path.Combine(_saveRoot, TestSlotId);
            var files = Directory.GetFiles(dir, "house-furniture-layout*.json");
            Assert.Greater(files.Length, 0, "QUEST-INTERIOR-REWARD-E2E-001 failed: no House furniture layout JSON file was written in " + dir);
            return files.OrderByDescending(File.GetLastWriteTimeUtc).First();
        }

        private static void ClearStaticFurnitureLayoutCache()
        {
            var field = typeof(InteriorPlacementPreviewOverlay).GetField("SavedFurnitureLayout", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var list = field != null ? field.GetValue(null) as IList<FurniturePlacementSaveData> : null;
            list?.Clear();
        }

        private static void CleanupResidue()
        {
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectRoot");
            PlayerFlowE2EDriver.DestroyAllNamed("EventSystem");
            PlayerFlowE2EDriver.DestroyAllNamed("Player");
        }

        [DefaultExecutionOrder(-10000)]
        private sealed class MouseClickFrameDriver : MonoBehaviour
        {
            private Mouse _mouse;
            private Vector2 _screenPosition;
            private int _frame;

            public void Configure(Mouse mouse, Vector2 screenPosition)
            {
                _mouse = mouse;
                _screenPosition = screenPosition;
            }

            private void Update()
            {
                if (_mouse == null)
                {
                    Destroy(gameObject);
                    return;
                }

                _mouse.MakeCurrent();
                if (_frame == 0)
                {
                    InputSystem.QueueStateEvent(_mouse, new MouseState { position = _screenPosition, buttons = 1 });
                    InputSystem.Update();

                    var router = Object.FindFirstObjectByType<FurniturePlacementPointerRouter>();
                    if (router != null)
                    {
                        typeof(FurniturePlacementPointerRouter).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(router, null);
                    }

                    var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
                    if (overlay != null)
                    {
                        var overlayType = typeof(InteriorPlacementPreviewOverlay);
                        overlayType.GetField("_wasMousePressed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(overlay, false);
                        overlayType.GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(overlay, null);
                    }

                    _frame++;
                    return;
                }

                InputSystem.QueueStateEvent(_mouse, new MouseState { position = _screenPosition });
                InputSystem.Update();
                Destroy(gameObject);
            }
        }

        private readonly struct QuestInteriorRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly LocationActivityInteractor Library;
            public readonly QuestChainLogPanel ChainPanel;
            public readonly LocationIdentityPanel LocationPanel;
            public readonly ObjectiveJournalPanel Journal;
            public readonly ObjectiveJournalRuntimeProvider ObjectiveProvider;

            public QuestInteriorRuntime(GameObject player, PlayerInteractionRouter router, LocationActivityInteractor library, QuestChainLogPanel chainPanel, LocationIdentityPanel locationPanel, ObjectiveJournalPanel journal, ObjectiveJournalRuntimeProvider objectiveProvider)
            {
                Player = player;
                Router = router;
                Library = library;
                ChainPanel = chainPanel;
                LocationPanel = locationPanel;
                Journal = journal;
                ObjectiveProvider = objectiveProvider;
            }
        }
    }
}
