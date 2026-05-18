using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.Game.Placement;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Placement
{
    public sealed class NonHouseFurnitureSurfacePlayModeTests
    {
        private const string DeskButtonName = "PaletteFurniture_desk.large";
        private const string DeskTileName = "ShadowlessOffice_Desk_Table_r39_c01";
        private const string ComputerTileName = "ShadowlessOffice_Computer_Monitor_r44_c08";

        [UnityTest]
        public IEnumerator NonHouseTilePlacementSurface_UsesSameFurniturePaletteAndPlacementFlow()
        {
            var scene = SceneManager.CreateScene("NonHouseFurniturePlacementTest");
            SceneManager.SetActiveScene(scene);

            var canvasGo = new GameObject("NonHousePlacementCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var grid = new GameObject("TownTestFurnitureGrid", typeof(Grid));
            SceneManager.MoveGameObjectToScene(grid, scene);
            var ground = CreateTilemap(grid.transform, "TownTestGroundTilemap", 0, true);
            var objects = CreateTilemap(grid.transform, "TownTestFurnitureObjectTilemap", 20, true);
            var occupancy = CreateTilemap(grid.transform, "TownTestFurnitureOccupancyTilemap", 0, false);
            var preview = CreateTilemap(grid.transform, "TownTestFurniturePreviewTilemap", 30, true);
            var groundTile = ScriptableObject.CreateInstance<Tile>();
            for (int x = -14; x < 14; x++)
            {
                for (int y = -9; y < 9; y++)
                {
                    ground.SetTile(new Vector3Int(x, y, 0), groundTile);
                }
            }

            var surface = objects.gameObject.AddComponent<TilePlacementSurface>();
            surface.ConfigureForTests(
                "town-test",
                ground,
                objects,
                occupancy,
                preview,
                new BoundsInt(-14, -9, 0, 28, 18, 1),
                TilePlacementRuleFlags.RequireGround | TilePlacementRuleFlags.RejectOccupiedCells | TilePlacementRuleFlags.StayInsideBounds);

            InteriorFurniturePlacementPanel.Create(canvas, null);
            yield return null;
            yield return null;

            var deskButton = GameObject.Find(DeskButtonName)?.GetComponent<Button>();
            Assert.IsNotNull(deskButton, "The shared furniture palette should be available outside the House scene.");
            deskButton.onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var message), message);
            yield return null;

            Assert.AreEqual("town-test", surface.SurfaceId);
            Assert.IsTrue(ContainsTile(objects, DeskTileName, out var deskCell), "The non-House object Tilemap should receive the selected desk tile.");
            Assert.IsTrue(ContainsTile(objects, ComputerTileName, out var computerCell), "The non-House object Tilemap should receive all multi-tile parts.");
            Assert.AreEqual(Vector3Int.right, computerCell - deskCell);
            Assert.IsNotNull(occupancy.GetTile(deskCell));
            Assert.IsNotNull(occupancy.GetTile(computerCell));
            Assert.IsFalse(GameObject.Find("HouseFurnitureObjectTilemap") != null && GameObject.Find("HouseFurnitureObjectTilemap") != objects.gameObject,
                "Non-House placement must not create or use the hard-coded House furniture Tilemap.");
        }

        private static Tilemap CreateTilemap(Transform parent, string name, int sortingOrder, bool render)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = render;
            return go.GetComponent<Tilemap>();
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
    }
}
