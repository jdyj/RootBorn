using NUnit.Framework;
using Rootborn.Game.Interiors;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class InteriorDirectionalTileSetTests
    {
        private const string TileSetPath = "Assets/Data/Interiors/TileSets/TileSetDefinition_NewPaletteOffice.asset";
        private const string OfficeFolder = "Assets/Modern_Town/1_Room_Builder_Office_48x48";

        [Test]
        public void HouseOfficeTileSet_ProvidesDirectionalFurnitureTilesFromRoomBuilderOffice()
        {
            var tileSet = AssetDatabase.LoadAssetAtPath<InteriorTileSetDefinition>(TileSetPath);

            Assert.IsNotNull(tileSet);
            AssertDirectionalTile(tileSet.ResolveDesk(InteriorFacingDirection.North), "desk north");
            AssertDirectionalTile(tileSet.ResolveDesk(InteriorFacingDirection.East), "desk east");
            AssertDirectionalTile(tileSet.ResolveDesk(InteriorFacingDirection.South), "desk south");
            AssertDirectionalTile(tileSet.ResolveDesk(InteriorFacingDirection.West), "desk west");
            AssertDirectionalTile(tileSet.ResolveChair(InteriorFacingDirection.North), "chair north");
            AssertDirectionalTile(tileSet.ResolveChair(InteriorFacingDirection.East), "chair east");
            AssertDirectionalTile(tileSet.ResolveChair(InteriorFacingDirection.South), "chair south");
            AssertDirectionalTile(tileSet.ResolveChair(InteriorFacingDirection.West), "chair west");
            AssertDirectionalTile(tileSet.ResolveSofa(InteriorFacingDirection.North), "sofa north");
            AssertDirectionalTile(tileSet.ResolveSofa(InteriorFacingDirection.East), "sofa east");
            AssertDirectionalTile(tileSet.ResolveSofa(InteriorFacingDirection.South), "sofa south");
            AssertDirectionalTile(tileSet.ResolveSofa(InteriorFacingDirection.West), "sofa west");
        }

        [Test]
        public void Apply_UsesDirectionalObjectTilesWhenAvailable()
        {
            var floorTile = ScriptableObject.CreateInstance<Tile>();
            var wallTile = ScriptableObject.CreateInstance<Tile>();
            var deskNorth = ScriptableObject.CreateInstance<Tile>();
            var deskSouth = ScriptableObject.CreateInstance<Tile>();
            var chairEast = ScriptableObject.CreateInstance<Tile>();
            var sofaWest = ScriptableObject.CreateInstance<Tile>();
            var tileSet = ScriptableObject.CreateInstance<InteriorTileSetDefinition>();
            var root = new GameObject("DirectionalTileSetTestRoot");

            try
            {
                ConfigureTileSet(tileSet, floorTile, wallTile, deskNorth, deskSouth, chairEast, sofaWest);
                var map = new InteriorGeneratedMap(8, 8);
                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        bool edge = x == 0 || y == 0 || x == map.Width - 1 || y == map.Height - 1;
                        map.SetKind(new Vector2Int(x, y), edge ? InteriorCellKind.Wall : InteriorCellKind.Floor);
                    }
                }

                map.SpawnCell = new Vector2Int(1, 1);
                Assert.IsTrue(map.TryPlaceObject(new Vector2Int(2, 2), InteriorObjectKind.Desk, true, InteriorFacingDirection.North));
                Assert.IsTrue(map.TryPlaceObject(new Vector2Int(3, 2), InteriorObjectKind.Desk, true, InteriorFacingDirection.South));
                Assert.IsTrue(map.TryPlaceObject(new Vector2Int(4, 2), InteriorObjectKind.Chair, false, InteriorFacingDirection.East));
                Assert.IsTrue(map.TryPlaceObject(new Vector2Int(5, 2), InteriorObjectKind.Sofa, true, InteriorFacingDirection.West));

                var applier = CreateApplier(root, tileSet, out var decoration);
                applier.Apply(map);
                var offset = new Vector3Int(-map.Width / 2, -map.Height / 2, 0);

                Assert.AreSame(deskNorth, decoration.GetTile(new Vector3Int(2, 2, 0) + offset));
                Assert.AreSame(deskSouth, decoration.GetTile(new Vector3Int(3, 2, 0) + offset));
                Assert.AreSame(chairEast, decoration.GetTile(new Vector3Int(4, 2, 0) + offset));
                Assert.AreSame(sofaWest, decoration.GetTile(new Vector3Int(5, 2, 0) + offset));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tileSet);
                Object.DestroyImmediate(floorTile);
                Object.DestroyImmediate(wallTile);
                Object.DestroyImmediate(deskNorth);
                Object.DestroyImmediate(deskSouth);
                Object.DestroyImmediate(chairEast);
                Object.DestroyImmediate(sofaWest);
            }
        }

        private static InteriorTilemapApplier CreateApplier(GameObject root, InteriorTileSetDefinition tileSet, out Tilemap decoration)
        {
            var grid = root.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            var floor = CreateTilemap(root.transform, "HouseGroundTilemap");
            var walls = CreateTilemap(root.transform, "HouseWallTilemap");
            var doors = CreateTilemap(root.transform, "HouseDoorTilemap");
            decoration = CreateTilemap(root.transform, "HouseDecorationTilemap");
            var collision = CreateTilemap(root.transform, "HouseCollisionTilemap");
            var applier = root.AddComponent<InteriorTilemapApplier>();
            var serialized = new SerializedObject(applier);
            serialized.FindProperty("_tileSet").objectReferenceValue = tileSet;
            serialized.FindProperty("_floor").objectReferenceValue = floor;
            serialized.FindProperty("_walls").objectReferenceValue = walls;
            serialized.FindProperty("_doors").objectReferenceValue = doors;
            serialized.FindProperty("_decorations").objectReferenceValue = decoration;
            serialized.FindProperty("_collision").objectReferenceValue = collision;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return applier;
        }

        private static Tilemap CreateTilemap(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>();
            return tilemap;
        }

        private static void ConfigureTileSet(InteriorTileSetDefinition tileSet, TileBase floor, TileBase wall, TileBase deskNorth, TileBase deskSouth, TileBase chairEast, TileBase sofaWest)
        {
            var serialized = new SerializedObject(tileSet);
            serialized.FindProperty("_floor").objectReferenceValue = floor;
            serialized.FindProperty("_wall").objectReferenceValue = wall;
            serialized.FindProperty("_deskNorth").objectReferenceValue = deskNorth;
            serialized.FindProperty("_deskSouth").objectReferenceValue = deskSouth;
            serialized.FindProperty("_chairEast").objectReferenceValue = chairEast;
            serialized.FindProperty("_sofaWest").objectReferenceValue = sofaWest;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertDirectionalTile(TileBase tile, string label)
        {
            Assert.IsNotNull(tile, label);
            StringAssert.StartsWith(OfficeFolder, AssetDatabase.GetAssetPath(tile), label);
        }
    }
}
