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

            var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.direct",
                new RectInt(0, 0, 4, 4),
                new[] { HouseConstructionCellRequirement.Floor(1, 1), HouseConstructionCellRequirement.Wall(1, 2), HouseConstructionCellRequirement.Door(2, 1) });

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

            var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.direct.complete",
                new RectInt(0, 0, 4, 4),
                new[] { HouseConstructionCellRequirement.Floor(1, 1), HouseConstructionCellRequirement.Wall(1, 2), HouseConstructionCellRequirement.Door(2, 1) });
            var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.direct.complete", 1, 300, 120, InteriorGenerationProfile.CreateExpandedOfficeForTests(), null, blueprint);
            stage.ConfigureConditionsForTests(null, new[] { ScriptableObject.CreateInstance<HouseAlwaysCondition>() });
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
        public IEnumerator HOUSE_UPGRADE_PM_007_DirectConstructionPlacesRequiredCellThroughMouseInput()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.direct.mouse",
                new RectInt(0, 0, 4, 4),
                new[] { HouseConstructionCellRequirement.Floor(1, 1), HouseConstructionCellRequirement.Wall(1, 2), HouseConstructionCellRequirement.Door(2, 1) });
            var overlay = HouseConstructionOverlay.EnsureForTests(blueprint);
            var tilemap = GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap);
            Assert.AreEqual("0/3", overlay.ProgressTextForTests);

            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var screenPosition = (Vector2)Camera.main.WorldToScreenPoint(tilemap.GetCellCenterWorld(new Vector3Int(1, 1, 0)));
                yield return DriveMouseClick(mouse, screenPosition);
                Assert.AreEqual("1/3", overlay.ProgressTextForTests);
            }
            finally
            {
                if (mouse.added)
                {
                    InputSystem.RemoveDevice(mouse);
                }
            }
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