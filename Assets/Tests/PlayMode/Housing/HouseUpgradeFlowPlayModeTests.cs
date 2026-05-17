using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Housing;
using Rootborn.Game.Interiors;
using Rootborn.Game.Save;
using Rootborn.UI.Housing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Housing
{
    public sealed class HouseUpgradeFlowPlayModeTests
    {
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, "house-upgrade-flow-" + TestContext.CurrentContext.Test.ID);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot-0", DisplayName = "slot-0", WorldSeed = 1205, TileSeed = 1205 });
        }

        [TearDown]
        public void TearDown()
        {
            ActiveSaveContext.Clear();
            SaveService.SetRootDirectoryForTests(null);
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        [UnityTest]
        public IEnumerator HOUSE_UPGRADE_PM_001_SavedStageSelectsExpandedHouseGeneration()
        {
            HouseStatePersistence.Save("slot-0", new HouseStateSaveData { CurrentStageIndex = 0 });
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var baselineFloorCount = CountTiles(GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>());

            HouseStatePersistence.Save("slot-0", new HouseStateSaveData { CurrentStageIndex = 1 });
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var expandedFloorCount = CountTiles(GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>());

            Assert.Greater(baselineFloorCount, 100, "Stage 0 House should still generate a playable one-room baseline.");
            Assert.Greater(expandedFloorCount, baselineFloorCount, "Saved stage 1 should generate a visibly larger House floor area.");
        }

        [UnityTest]
        public IEnumerator HOUSE_UPGRADE_PM_002_NonInteriorPlayerSeesHireRouteOnly()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var panel = Object.FindFirstObjectByType<HouseUpgradePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "Town should install the House upgrade panel through the runtime provider flow.");

            panel.ShowForTests(CreateStageForPanelTests(includeDirectCondition: false), new HouseStateSaveData(), new HouseCurrencyWallet(500), directEligible: false);

            Assert.IsTrue(panel.HireButtonVisibleForTests);
            Assert.IsTrue(panel.HireButtonInteractableForTests);
            Assert.IsFalse(panel.DirectButtonVisibleForTests);
            StringAssert.Contains("300", panel.VisibleTextForTests);
        }

        [UnityTest]
        public IEnumerator HOUSE_UPGRADE_PM_003_InteriorEligiblePlayerSeesHireAndDirectRoutes()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var panel = Object.FindFirstObjectByType<HouseUpgradePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel);

            panel.ShowForTests(CreateStageForPanelTests(includeDirectCondition: true), new HouseStateSaveData(), new HouseCurrencyWallet(500), directEligible: true);

            Assert.IsTrue(panel.HireButtonVisibleForTests);
            Assert.IsTrue(panel.DirectButtonVisibleForTests);
            Assert.IsTrue(panel.DirectButtonInteractableForTests);
            StringAssert.Contains("120", panel.VisibleTextForTests);
        }

        [UnityTest]
        public IEnumerator HOUSE_UPGRADE_PM_004_HireRoutePaysSavesStageAndReloadsExpandedHouse()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return null;
            yield return null;

            HouseStatePersistence.Save("slot-0", new HouseStateSaveData { Currency = new HouseCurrencySaveData { Balance = 500 } });

            var panel = Object.FindFirstObjectByType<HouseUpgradePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel);
            panel.OpenDefaultOfferForTests("slot-0", directEligible: false);
            yield return null;

            var hireButton = GameObject.Find("HireConstructionButton").GetComponent<Button>();
            Assert.IsTrue(hireButton.interactable);
            hireButton.onClick.Invoke();
            yield return null;

            var saved = HouseStatePersistence.Load("slot-0");
            Assert.AreEqual(1, saved.CurrentStageIndex);
            Assert.AreEqual(HouseUpgradeRouteKind.HireConstruction, saved.LatestRoute);
            Assert.AreEqual(200, saved.Currency.Balance);

            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.Greater(CountTiles(GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>()), 120);
        }

        [UnityTest]
        public IEnumerator HOUSE_UPGRADE_PM_005_DirectRouteStartsConstructionOverlayAndTracksProgress()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var blueprint = CreateBlueprintForTests("blueprint.direct");
            var overlay = HouseConstructionOverlay.EnsureForTests(blueprint);
            Assert.IsNotNull(overlay);
            Assert.AreEqual("0/3", overlay.ProgressTextForTests);
            Assert.IsTrue(overlay.TryPlaceForTests(new Vector2Int(1, 1), HouseConstructionCellKind.Floor));
            Assert.AreEqual("1/3", overlay.ProgressTextForTests);
            Assert.IsFalse(overlay.CompleteButtonInteractableForTests);
        }

        [UnityTest]
        public IEnumerator HOUSE_UPGRADE_PM_006_DirectConstructionCompletesSavesStageAndClearsProgress()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var blueprint = CreateBlueprintForTests("blueprint.direct.complete");
            var stage = CreateDirectStageForTests("house.stage.direct.complete", blueprint);
            HouseStatePersistence.Save("slot-0", new HouseStateSaveData { Currency = new HouseCurrencySaveData { Balance = 500 } });

            var overlay = HouseConstructionOverlay.EnsureForTests(blueprint);
            overlay.BindCompletionForTests("slot-0", stage);
            Assert.IsTrue(overlay.TryPlaceForTests(new Vector2Int(1, 1), HouseConstructionCellKind.Floor));
            Assert.IsTrue(overlay.TryPlaceForTests(new Vector2Int(1, 2), HouseConstructionCellKind.Wall));
            Assert.IsTrue(overlay.TryPlaceForTests(new Vector2Int(2, 1), HouseConstructionCellKind.Door));
            Assert.IsTrue(overlay.CompleteButtonInteractableForTests);
            overlay.CompleteForTests();
            yield return null;

            var saved = HouseStatePersistence.Load("slot-0");
            Assert.AreEqual(1, saved.CurrentStageIndex);
            Assert.AreEqual(380, saved.Currency.Balance);
            Assert.AreEqual(string.Empty, saved.ActiveConstructionStageId);
            Assert.AreEqual(0, saved.PlacedConstructionCells.Length);
            Assert.AreEqual(HouseUpgradeRouteKind.DirectConstruction, saved.LatestRoute);
        }

        [UnityTest]
        public IEnumerator HOUSE_UPGRADE_PM_007_DirectConstructionPlacesRequiredCellsAndCompletesThroughMouseInput()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var blueprint = CreateBlueprintForTests("blueprint.direct.mouse");
            var stage = CreateDirectStageForTests("house.stage.direct.mouse", blueprint);
            HouseStatePersistence.Save("slot-0", new HouseStateSaveData { Currency = new HouseCurrencySaveData { Balance = 500 } });

            var overlay = HouseConstructionOverlay.EnsureForTests(blueprint);
            overlay.BindCompletionForTests("slot-0", stage);
            var tilemap = GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap);
            Assert.AreEqual("0/3", overlay.ProgressTextForTests);

            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return DriveMouseClick(mouse, (Vector2)Camera.main.WorldToScreenPoint(tilemap.GetCellCenterWorld(new Vector3Int(1, 1, 0))));
                Assert.AreEqual("1/3", overlay.ProgressTextForTests);
                yield return DriveMouseClick(mouse, (Vector2)Camera.main.WorldToScreenPoint(tilemap.GetCellCenterWorld(new Vector3Int(1, 2, 0))));
                Assert.AreEqual("2/3", overlay.ProgressTextForTests);
                yield return DriveMouseClick(mouse, (Vector2)Camera.main.WorldToScreenPoint(tilemap.GetCellCenterWorld(new Vector3Int(2, 1, 0))));
                Assert.AreEqual("3/3", overlay.ProgressTextForTests);
                Assert.IsTrue(overlay.CompleteButtonInteractableForTests);

                var completeButton = GameObject.Find("CompleteConstructionButton").GetComponent<Button>();
                completeButton.onClick.Invoke();
                yield return null;

                var saved = HouseStatePersistence.Load("slot-0");
                Assert.AreEqual(1, saved.CurrentStageIndex);
                Assert.AreEqual(380, saved.Currency.Balance);
                Assert.AreEqual(string.Empty, saved.ActiveConstructionStageId);
                Assert.AreEqual(0, saved.PlacedConstructionCells.Length);
                Assert.AreEqual(HouseUpgradeRouteKind.DirectConstruction, saved.LatestRoute);
            }
            finally
            {
                if (mouse.added)
                {
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }

        [UnityTest]
        public IEnumerator HOUSE_UPGRADE_PM_008_DirectButtonStartsSavedConstructionAndLoadsHouseOverlay()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return null;
            yield return null;

            HouseStatePersistence.Save("slot-0", new HouseStateSaveData { Currency = new HouseCurrencySaveData { Balance = 500 } });
            var panel = Object.FindFirstObjectByType<HouseUpgradePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel);
            panel.OpenDefaultOfferForTests("slot-0", directEligible: true);
            yield return null;

            var directButton = GameObject.Find("DirectConstructionButton").GetComponent<Button>();
            Assert.IsTrue(directButton.interactable);
            directButton.onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;

            var saved = HouseStatePersistence.Load("slot-0");
            Assert.AreEqual(0, saved.CurrentStageIndex);
            Assert.AreEqual("house.stage.expanded_room.01", saved.ActiveConstructionStageId);
            Assert.AreEqual(0, saved.PlacedConstructionCells.Length);
            Assert.AreEqual(500, saved.Currency.Balance);
            Assert.AreEqual("House", SceneManager.GetActiveScene().name);

            var overlay = Object.FindFirstObjectByType<HouseConstructionOverlay>(FindObjectsInactive.Include);
            Assert.IsNotNull(overlay);
            Assert.AreEqual("0/3", overlay.ProgressTextForTests);
            Assert.AreEqual(3, overlay.ValidMarkerCountForTests);
            Assert.AreEqual(0, overlay.PlacedMarkerCountForTests);
        }

        [UnityTest]
        public IEnumerator HOUSE_UPGRADE_PM_009_ActiveConstructionRestoresPlacedCellsAndCancelPreservesProgress()
        {
            var stage = LoadDefaultStageAssetForTests();
            Assert.IsNotNull(stage);
            HouseStatePersistence.Save("slot-0", new HouseStateSaveData
            {
                ActiveConstructionStageId = stage.Id,
                PlacedConstructionCells = new[] { new HouseConstructionCellSaveData { X = 1, Y = 1, Kind = HouseConstructionCellKind.Floor } },
                Currency = new HouseCurrencySaveData { Balance = 500 }
            });

            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            var overlay = Object.FindFirstObjectByType<HouseConstructionOverlay>(FindObjectsInactive.Include);
            Assert.IsNotNull(overlay);
            Assert.AreEqual("1/3", overlay.ProgressTextForTests);
            Assert.AreEqual(3, overlay.ValidMarkerCountForTests);
            Assert.AreEqual(1, overlay.PlacedMarkerCountForTests);

            var cancelButton = GameObject.Find("CancelConstructionButton").GetComponent<Button>();
            cancelButton.onClick.Invoke();
            yield return null;

            var saved = HouseStatePersistence.Load("slot-0");
            Assert.AreEqual(stage.Id, saved.ActiveConstructionStageId);
            Assert.AreEqual(1, saved.PlacedConstructionCells.Length);
        }

        private static HouseUpgradeStageDefinition CreateStageForPanelTests(bool includeDirectCondition)
        {
            var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.panel",
                new RectInt(0, 0, 3, 3),
                new[] { HouseConstructionCellRequirement.Floor(1, 1) });
            var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.panel", 1, 300, 120, InteriorGenerationProfile.CreateExpandedOfficeForTests(), null, blueprint);
            if (includeDirectCondition)
            {
                stage.ConfigureConditionsForTests(null, new[] { ScriptableObject.CreateInstance<HouseAlwaysCondition>() });
            }

            return stage;
        }

        private static HouseConstructionBlueprintDefinition CreateBlueprintForTests(string id)
        {
            return HouseConstructionBlueprintDefinition.CreateForTests(
                id,
                new RectInt(0, 0, 4, 4),
                new[] { HouseConstructionCellRequirement.Floor(1, 1), HouseConstructionCellRequirement.Wall(1, 2), HouseConstructionCellRequirement.Door(2, 1) });
        }

        private static HouseUpgradeStageDefinition CreateDirectStageForTests(string id, HouseConstructionBlueprintDefinition blueprint)
        {
            var stage = HouseUpgradeStageDefinition.CreateForTests(id, 1, 300, 120, InteriorGenerationProfile.CreateExpandedOfficeForTests(), null, blueprint);
            stage.ConfigureConditionsForTests(null, new[] { ScriptableObject.CreateInstance<HouseAlwaysCondition>() });
            return stage;
        }

        private static HouseUpgradeStageDefinition LoadDefaultStageAssetForTests()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<HouseUpgradeStageDefinition>("Assets/Data/Housing/UpgradeStages/HouseStage_ExpandedRoom_01.asset");
#else
            return null;
#endif
        }

        private static IEnumerator DriveMouseClick(Mouse mouse, Vector2 position)
        {
            var driver = new GameObject("HouseConstructionMouseClickInputDriver").AddComponent<MouseClickInputDriver>();
            driver.Configure(mouse, position);
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            if (driver != null)
            {
                Object.Destroy(driver.gameObject);
            }
        }

        private static int CountTiles(Tilemap tilemap)
        {
            Assert.IsNotNull(tilemap, "HouseGroundTilemap should exist for House generation checks.");
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(position) != null)
                {
                    count++;
                }
            }

            return count;
        }

        [DefaultExecutionOrder(-10000)]
        private sealed class MouseClickInputDriver : MonoBehaviour
        {
            private Mouse _mouse;
            private Vector2 _position;
            private int _frame;

            public void Configure(Mouse mouse, Vector2 position)
            {
                _mouse = mouse;
                _position = position;
            }

            private void Update()
            {
                if (_mouse == null || !_mouse.added)
                {
                    Destroy(gameObject);
                    return;
                }

                _mouse.MakeCurrent();
                if (_frame == 0)
                {
                    InputSystem.QueueStateEvent(_mouse, new MouseState
                    {
                        position = _position,
                        buttons = (ushort)(1u << (int)MouseButton.Left)
                    });
                    InputSystem.Update();
                    _frame++;
                    return;
                }

                InputSystem.QueueStateEvent(_mouse, new MouseState { position = _position });
                InputSystem.Update();
                Destroy(gameObject);
            }
        }
    }
}