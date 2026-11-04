using NUnit.Framework;
using Rootborn.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class TownLdtkMapDefinitionTests
    {
        [Test]
        public void TOWN_LDTK_EDIT_001_CurrentTownSourceReportsExpectedSizeAndLayerCounts()
        {
            var project = AssetDatabase.LoadAssetAtPath<Object>("Assets/LDtk/Town/town.ldtk");
            Assert.IsNotNull(project, "Current Town LDtk source must be imported from Assets/LDtk/Town.");

            var definition = ScriptableObject.CreateInstance<LdtkTownMapDefinition>();
            try
            {
                definition.ConfigureForTests(project, null, 32, new Vector2(45f, -31.5f));

                LdtkTownMapAnalysis analysis = definition.Analyze();

                Assert.AreEqual("Level_0", analysis.LevelIdentifier);
                Assert.AreEqual(3, analysis.LevelCount);
                Assert.AreEqual(new Vector2Int(6080, 3360), analysis.PixelSize);
                Assert.AreEqual(new Vector2Int(190, 105), analysis.CellSize);
                Assert.AreEqual(32, analysis.GridSize);
                Assert.AreEqual(17885, analysis.TotalTileCount);
                Assert.AreEqual(11, analysis.LayerCount);
                Assert.AreEqual(11746, analysis.GetLayerTileCount("Ground"));
                Assert.AreEqual(96, analysis.GetLayerTileCount("Ground2"));
                Assert.AreEqual(296, analysis.GetLayerTileCount("Building"));
                Assert.AreEqual(250, analysis.GetLayerTileCount("Shop"));
                Assert.AreEqual(609, analysis.GetLayerTileCount("Hospital"));
                Assert.AreEqual(141, analysis.GetLayerTileCount("Police"));
                Assert.AreEqual(1028, analysis.GetLayerTileCount("Beach"));
                Assert.AreEqual(719, analysis.GetLayerTileCount("School"));
                Assert.AreEqual(97, analysis.GetLayerTileCount("Fense"));
                Assert.AreEqual(2903, analysis.GetLayerTileCount("Props"));
                Assert.IsTrue(analysis.HasGameplayLayers, "Current Town includes LDtk Door entities for interior portal routing.");
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void TOWN_LDTK_EDIT_002_CurrentTownSourceNoLongerRequiresCompositePreviewSprite()
        {
            var project = AssetDatabase.LoadAssetAtPath<Object>("Assets/LDtk/Town/town.ldtk");
            var definition = ScriptableObject.CreateInstance<LdtkTownMapDefinition>();
            try
            {
                definition.ConfigureForTests(project, null, 32, new Vector2(45f, -31.5f));

                LdtkTownMapAnalysis analysis = definition.Analyze();

                Assert.AreEqual(new Vector2Int(6080, 3360), analysis.PixelSize);
                Assert.AreEqual(Vector2Int.zero, analysis.CompositePixelSize, "Town rendering is validated through generated Tilemap layers, not a stale composite preview sprite.");
                Assert.IsFalse(analysis.CompositeMatchesPixelSize, "A missing composite preview must not be treated as the player-facing Town render path.");
                Assert.AreEqual(new Vector2(45f, -31.5f), definition.WorldCenter);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void TOWN_LDTK_EDIT_003_TownSceneStoresOperationalLdtkTilemapLayers()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Town.unity", OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "Town scene must open for saved LDtk Tilemap verification.");

            GameObject root = GameObject.Find("Town2LdtkTilemaps");
            Assert.IsNotNull(root, "Town scene must store generated LDtk Tilemap layers, not only the composite preview sprite.");
            AssertLayer(root, "Town2LdtkGroundTilemap", 5050, 0);
            AssertLayer(root, "Town2LdtkGround2Tilemap", 96, 100);
            AssertLayer(root, "Town2LdtkBeachTilemap", 0, 900);
            AssertLayer(root, "Town2LdtkFenseTilemap", 96, 300);
            AssertLayer(root, "Town2LdtkPropsTilemap", 687, 200);
            AssertLayer(root, "Town2LdtkShopTilemap", 237, 400);
            AssertCollisionLayer(root, "Town2LdtkShopTilemap");
            AssertLayer(root, "Town2LdtkHospitalTilemap", 605, 500);
            AssertCollisionLayer(root, "Town2LdtkHospitalTilemap");
            AssertLayer(root, "Town2LdtkPoliceTilemap", 141, 600);
            AssertCollisionLayer(root, "Town2LdtkPoliceTilemap");
            AssertLayer(root, "Town2LdtkBuildingTilemap", 279, 700);
            AssertLayer(root, "Town2LdtkSchoolTilemap", 47, 800);
            AssertLayer(root, "Town2LdtkFenseTilemap", 96, 300);
            AssertLayer(root, "Town2LdtkPropsTilemap", 687, 200);
            AssertCollisionLayer(root, "Town2LdtkBuildingTilemap");
        }

        [Test]
        public void TOWN_LDTK_EDIT_004_TownSceneTilesUseExactLdtkSourceRows()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Town.unity", OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "Town scene must open for exact LDtk tile source verification.");

            GameObject root = GameObject.Find("Town2LdtkTilemaps");
            Assert.IsNotNull(root);
            AssertTile(root, "Town2LdtkBuildingTilemap", new Vector3Int(7, -13, 0), "_Generic_Buildings_32x32_x32_y4320_f0");
            AssertTile(root, "Town2LdtkBuildingTilemap", new Vector3Int(10, -13, 0), "_Generic_Buildings_32x32_x0_y352_f0");
            AssertTile(root, "Town2LdtkBuildingTilemap", new Vector3Int(26, -13, 0), "_Generic_Buildings_32x32_x736_y3072_f0");
            AssertTile(root, "Town2LdtkGroundTilemap", new Vector3Int(26, -13, 0), "_City_Terrains_32x32_x1248_y736_f0");
        }

        [Test]
        public void TOWN_LDTK_EDIT_005_TownScenePreservesSameCellOverdrawTileDraws()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Town.unity", OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "Town scene must open for LDtk overdraw verification.");

            GameObject root = GameObject.Find("Town2LdtkTilemaps");
            Assert.IsNotNull(root);
            AssertLayerDrawCount(root, "Town2LdtkGroundTilemap", 5050);
            AssertLayerDrawCount(root, "Town2LdtkGround2Tilemap", 96);
            AssertLayerDrawCount(root, "Town2LdtkPropsTilemap", 1003);
            AssertLayerDrawCount(root, "Town2LdtkFenseTilemap", 97);
            AssertLayerDrawCount(root, "Town2LdtkShopTilemap", 250);
            AssertLayerDrawCount(root, "Town2LdtkHospitalTilemap", 609);
            AssertLayerDrawCount(root, "Town2LdtkPoliceTilemap", 141);
            AssertLayerDrawCount(root, "Town2LdtkBuildingTilemap", 296);
            AssertLayerDrawCount(root, "Town2LdtkSchoolTilemap", 47);
        }

        [Test]
        public void TOWN_LDTK_EDIT_006_TownSceneRendererOrderMatchesLdtkLayerStack()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Town.unity", OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "Town scene must open for LDtk layer order verification.");

            GameObject root = GameObject.Find("Town2LdtkTilemaps");
            Assert.IsNotNull(root);
            AssertRendererOrder(root, "Town2LdtkGroundTilemap", 0);
            AssertRendererOrder(root, "Town2LdtkGround2Tilemap", 100);
            AssertRendererOrder(root, "Town2LdtkPropsTilemap", 200);
            AssertRendererOrder(root, "Town2LdtkPropsOverdraw01Tilemap", 201);
            AssertRendererOrder(root, "Town2LdtkPropsOverdraw02Tilemap", 202);
            AssertRendererOrder(root, "Town2LdtkFenseTilemap", 300);
            AssertRendererOrder(root, "Town2LdtkFenseOverdraw01Tilemap", 301);
            AssertRendererOrder(root, "Town2LdtkShopTilemap", 400);
            AssertRendererOrder(root, "Town2LdtkShopOverdraw01Tilemap", 401);
            AssertRendererOrder(root, "Town2LdtkHospitalTilemap", 500);
            AssertRendererOrder(root, "Town2LdtkHospitalOverdraw01Tilemap", 501);
            AssertRendererOrder(root, "Town2LdtkPoliceTilemap", 600);
            AssertRendererOrder(root, "Town2LdtkBuildingTilemap", 700);
            AssertRendererOrder(root, "Town2LdtkBuildingOverdraw01Tilemap", 701);
            AssertRendererOrder(root, "Town2LdtkSchoolTilemap", 800);
            AssertRendererOrder(root, "Town2LdtkBeachTilemap", 900);
        }

        [Test]
        public void TOWN_LDTK_EDIT_007_TownGeneratedTilesUseOriginalTilesetPixelCoordinates()
        {
            const string tilesetPath = "Assets/Modern_Exteriors_32x32/ME_Theme_Sorter_32x32/2_City_Terrains_32x32.png";
            Vector2Int pngSize = ReadPngSize(tilesetPath);
            Assert.AreEqual(new Vector2Int(1888, 3296), pngSize, "Ground tileset PNG size changed; LDtk src conversion must use the original PNG pixel height." );

            AssertGeneratedTileRect("Assets/LDtk/Generated/TownTiles/_City_Terrains_32x32_x320_y736_f0.asset", 320, 736, pngSize.y);
            AssertGeneratedTileRect("Assets/LDtk/Generated/TownTiles/_City_Terrains_32x32_x288_y736_f0.asset", 288, 736, pngSize.y);
            AssertGeneratedTileRect("Assets/LDtk/Generated/TownTiles/_City_Terrains_32x32_x384_y832_f0.asset", 384, 832, pngSize.y);
        }

        private static void AssertGeneratedTileRect(string tilePath, int ldtkSourceX, int ldtkSourceY, int sourceTextureHeight)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            Assert.IsNotNull(tile, "Generated LDtk tile missing: " + tilePath);
            Assert.IsNotNull(tile.sprite, "Generated LDtk tile must reference a sprite: " + tilePath);

            Rect rect = tile.sprite.rect;
            Assert.AreEqual(ldtkSourceX, (int)rect.x, tilePath + " must preserve the LDtk source x pixel." );
            Assert.AreEqual(sourceTextureHeight - ldtkSourceY - 32, (int)rect.y, tilePath + " must convert LDtk top-left source y using the original PNG height." );
            Assert.AreEqual(32, (int)rect.width, tilePath + " width must stay one LDtk tile." );
            Assert.AreEqual(32, (int)rect.height, tilePath + " height must stay one LDtk tile." );
        }

        private static Vector2Int ReadPngSize(string path)
        {
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            Assert.GreaterOrEqual(bytes.Length, 24, "PNG file is too short: " + path);
            int width = ReadBigEndianInt(bytes, 16);
            int height = ReadBigEndianInt(bytes, 20);
            return new Vector2Int(width, height);
        }

        private static int ReadBigEndianInt(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        private static void AssertRendererOrder(GameObject root, string layerName, int expectedSortingOrder)
        {
            Transform child = root.transform.Find(layerName);
            Assert.IsNotNull(child, "Missing LDtk renderer layer: " + layerName);
            TilemapRenderer renderer = child.GetComponent<TilemapRenderer>();
            Assert.IsNotNull(renderer, layerName + " must have a TilemapRenderer.");
            Assert.AreEqual(expectedSortingOrder, renderer.sortingOrder, layerName + " must preserve LDtk bottom-to-top draw order.");
        }

        private static void AssertLayerDrawCount(GameObject root, string baseLayerName, int expectedDraws)
        {
            int actual = 0;
            foreach (Transform child in root.transform)
            {
                if (!child.name.Equals(baseLayerName) && !child.name.StartsWith(baseLayerName.Replace("Tilemap", "Overdraw")))
                {
                    continue;
                }

                Tilemap tilemap = child.GetComponent<Tilemap>();
                if (tilemap != null)
                {
                    actual += CountTiles(tilemap);
                }
            }

            Assert.AreEqual(expectedDraws, actual, baseLayerName + " must preserve every LDtk tile draw, including same-cell overdraw sublayers.");
        }

        private static void AssertLayer(GameObject root, string name, int expectedTiles, int sortingOrder)
        {
            Transform child = root.transform.Find(name);
            Assert.IsNotNull(child, "Missing LDtk Tilemap layer: " + name);
            Tilemap tilemap = child.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap, name + " must have a Tilemap component.");
            Assert.AreEqual(expectedTiles, CountTiles(tilemap), name + " must preserve the renderable LDtk cell count after same-cell overdraw is resolved.");
            TilemapRenderer renderer = child.GetComponent<TilemapRenderer>();
            Assert.IsNotNull(renderer, name + " must have a TilemapRenderer.");
            Assert.AreEqual(sortingOrder, renderer.sortingOrder, name + " must preserve LDtk draw order.");
        }


        private static void AssertTile(GameObject root, string layerName, Vector3Int cell, string expectedTileName)
        {
            Transform child = root.transform.Find(layerName);
            Assert.IsNotNull(child, "Missing LDtk Tilemap layer: " + layerName);
            Tilemap tilemap = child.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap, layerName + " must have a Tilemap component.");
            TileBase tile = tilemap.GetTile(cell);
            Assert.IsNotNull(tile, layerName + " must have a tile at " + cell);
            Assert.AreEqual(expectedTileName, tile.name, layerName + " must use the exact LDtk source tile row at " + cell);
        }

        private static void AssertCollisionLayer(GameObject root, string name)
        {
            Transform child = root.transform.Find(name);
            Assert.IsNotNull(child, "Missing LDtk collision layer target: " + name);
            TilemapCollider2D tilemapCollider = child.GetComponent<TilemapCollider2D>();
            Assert.IsNotNull(tilemapCollider, name + " must block player movement through building art.");
            Assert.IsFalse(tilemapCollider.isTrigger, name + " collision must be solid, not a trigger.");
            CompositeCollider2D composite = child.GetComponent<CompositeCollider2D>();
            Assert.IsNotNull(composite, name + " must use a CompositeCollider2D to keep operational collision efficient.");
            Rigidbody2D body = child.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(body, name + " must have a Rigidbody2D for composite Tilemap collision.");
            Assert.AreEqual(RigidbodyType2D.Static, body.bodyType, name + " collision body must be static.");
        }

        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}