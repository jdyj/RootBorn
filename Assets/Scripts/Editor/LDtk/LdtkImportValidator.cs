using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Editor.LDtk
{
    public readonly struct LdtkImportIssue
    {
        public LdtkImportIssue(string category, string assetPath, string message)
        {
            Category = category;
            AssetPath = assetPath;
            Message = message;
        }

        public string Category { get; }
        public string AssetPath { get; }
        public string Message { get; }
    }

    public static class LdtkImportValidator
    {
        private const int TileSize = 32;
        private static readonly Regex TileNamePattern = new Regex(@"_x(?<x>\d+)_y(?<y>\d+)_f(?<frame>\d+)\.asset$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static IReadOnlyList<LdtkImportIssue> ValidateGeneratedTileRects(string generatedFolder)
        {
            var issues = new List<LdtkImportIssue>();
            foreach (string tilePath in FindTileAssetPaths(generatedFolder, issues))
            {
                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile == null)
                {
                    issues.Add(new LdtkImportIssue("path", tilePath, "Generated LDtk Tile asset could not be loaded."));
                    continue;
                }

                if (tile.sprite == null)
                {
                    issues.Add(new LdtkImportIssue("path", tilePath, "Generated LDtk Tile has no sprite reference."));
                    continue;
                }

                Match match = TileNamePattern.Match(Path.GetFileName(tilePath));
                if (!match.Success)
                {
                    issues.Add(new LdtkImportIssue("rect", tilePath, "Generated Tile filename does not encode LDtk source coordinates as _x{srcX}_y{srcY}_f{frame}."));
                    continue;
                }

                int sourceX = int.Parse(match.Groups["x"].Value);
                int sourceY = int.Parse(match.Groups["y"].Value);
                string texturePath = AssetDatabase.GetAssetPath(tile.sprite.texture);
                if (string.IsNullOrEmpty(texturePath) || !File.Exists(texturePath))
                {
                    issues.Add(new LdtkImportIssue("path", tilePath, "Generated Tile sprite texture path is missing or does not exist: " + texturePath));
                    continue;
                }

                Vector2Int pngSize;
                try
                {
                    pngSize = ReadPngSize(texturePath);
                }
                catch (Exception ex)
                {
                    issues.Add(new LdtkImportIssue("path", texturePath, "Could not read source PNG dimensions: " + ex.Message));
                    continue;
                }

                Rect rect = tile.sprite.rect;
                int expectedY = pngSize.y - sourceY - TileSize;
                if ((int)rect.x != sourceX || (int)rect.y != expectedY || (int)rect.width != TileSize || (int)rect.height != TileSize)
                {
                    issues.Add(new LdtkImportIssue("rect", tilePath, $"Sprite rect must match LDtk source coordinates. expected=({sourceX},{expectedY},{TileSize},{TileSize}) actual=({(int)rect.x},{(int)rect.y},{(int)rect.width},{(int)rect.height}) texture={texturePath}"));
                }
            }

            return issues;
        }

        public static IReadOnlyList<LdtkImportIssue> ValidateTextureImportSettings(IEnumerable<string> generatedFolders)
        {
            var issues = new List<LdtkImportIssue>();
            var texturePaths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string folder in generatedFolders)
            {
                foreach (string tilePath in FindTileAssetPaths(folder, issues))
                {
                    Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                    if (tile?.sprite?.texture == null)
                    {
                        issues.Add(new LdtkImportIssue("path", tilePath, "Generated LDtk Tile cannot resolve its sprite texture."));
                        continue;
                    }

                    texturePaths.Add(AssetDatabase.GetAssetPath(tile.sprite.texture));
                }
            }

            foreach (string texturePath in texturePaths)
            {
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer == null)
                {
                    issues.Add(new LdtkImportIssue("path", texturePath, "LDtk tileset texture is not imported by TextureImporter."));
                    continue;
                }

                Vector2Int pngSize;
                try
                {
                    pngSize = ReadPngSize(texturePath);
                }
                catch (Exception ex)
                {
                    issues.Add(new LdtkImportIssue("path", texturePath, "Could not read texture PNG dimensions: " + ex.Message));
                    continue;
                }

                if (Math.Abs(importer.spritePixelsPerUnit - 32f) > 0.001f)
                {
                    issues.Add(new LdtkImportIssue("PPU", texturePath, "LDtk tileset texture must use Pixels Per Unit 32, actual=" + importer.spritePixelsPerUnit));
                }

                if (importer.filterMode != FilterMode.Point)
                {
                    issues.Add(new LdtkImportIssue("PPU", texturePath, "LDtk pixel art texture must use Point filtering, actual=" + importer.filterMode));
                }

                if (importer.mipmapEnabled)
                {
                    issues.Add(new LdtkImportIssue("PPU", texturePath, "LDtk pixel art texture must have mipmaps disabled."));
                }

                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    issues.Add(new LdtkImportIssue("PPU", texturePath, "LDtk pixel art texture must be imported uncompressed, actual=" + importer.textureCompression));
                }

                if (importer.maxTextureSize < Math.Max(pngSize.x, pngSize.y))
                {
                    issues.Add(new LdtkImportIssue("PPU", texturePath, "LDtk tileset texture max size would downscale source PNG. maxTextureSize=" + importer.maxTextureSize + " png=" + pngSize));
                }
            }

            return issues;
        }

        public static IReadOnlyList<LdtkImportIssue> ValidateGeneratedTileAssetHygiene(IEnumerable<string> generatedFolders)
        {
            var issues = new List<LdtkImportIssue>();
            foreach (string folder in generatedFolders)
            {
                foreach (string tilePath in FindTileAssetPaths(folder, issues))
                {
                    string fileName = Path.GetFileName(tilePath);
                    if (fileName.IndexOf(" 1", StringComparison.OrdinalIgnoreCase) >= 0 || fileName.IndexOf("copy", StringComparison.OrdinalIgnoreCase) >= 0 || fileName.IndexOf("sample", StringComparison.OrdinalIgnoreCase) >= 0 || fileName.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        issues.Add(new LdtkImportIssue("stale asset", tilePath, "Generated LDtk Tile asset looks like a stale duplicate/sample/placeholder file."));
                    }

                    if (!TileNamePattern.IsMatch(fileName))
                    {
                        issues.Add(new LdtkImportIssue("stale asset", tilePath, "Generated LDtk Tile asset does not follow the canonical source-coordinate filename pattern."));
                    }

                    Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                    if (tile == null || tile.sprite == null)
                    {
                        issues.Add(new LdtkImportIssue("stale asset", tilePath, "Generated LDtk Tile asset is stale because it has no loadable Tile or sprite."));
                    }
                }
            }

            return issues;
        }

        public static IReadOnlyList<LdtkImportIssue> ValidateSavedSceneLayerSorting()
        {
            var issues = new List<LdtkImportIssue>();
            ValidateScene(issues, "Assets/Scenes/Town.unity", "Town2LdtkTilemaps", new[]
            {
                new ExpectedLayer("Town2LdtkGroundTilemap", 5050, 0),
                new ExpectedLayer("Town2LdtkGround2Tilemap", 96, 100),
                new ExpectedLayer("Town2LdtkPropsTilemap", 687, 200),
                new ExpectedLayer("Town2LdtkPropsOverdraw01Tilemap", 250, 201),
                new ExpectedLayer("Town2LdtkPropsOverdraw02Tilemap", 66, 202),
                new ExpectedLayer("Town2LdtkFenseTilemap", 96, 300),
                new ExpectedLayer("Town2LdtkFenseOverdraw01Tilemap", 1, 301),
                new ExpectedLayer("Town2LdtkShopTilemap", 237, 400),
                new ExpectedLayer("Town2LdtkShopOverdraw01Tilemap", 13, 401),
                new ExpectedLayer("Town2LdtkHospitalTilemap", 605, 500),
                new ExpectedLayer("Town2LdtkHospitalOverdraw01Tilemap", 4, 501),
                new ExpectedLayer("Town2LdtkPoliceTilemap", 141, 600),
                new ExpectedLayer("Town2LdtkBuildingTilemap", 279, 700),
                new ExpectedLayer("Town2LdtkBuildingOverdraw01Tilemap", 17, 701),
                new ExpectedLayer("Town2LdtkSchoolTilemap", 47, 800),
                new ExpectedLayer("Town2LdtkBeachTilemap", 0, 900),
                new ExpectedLayer("Town2LdtkModularBuildingTilemap", 70, 1000),
                new ExpectedLayer("Town2LdtkModularBuildingOverdraw01Tilemap", 12, 1001)
            });
            ValidateScene(issues, "Assets/Scenes/HouseInterior.unity", "HouseInteriorLdtkTilemaps", new[]
            {
                new ExpectedLayer("HouseInteriorGroundTilemap", 154, 0),
                new ExpectedLayer("HouseInteriorWallTilemap", 0, 1),
                new ExpectedLayer("HouseInteriorWall3dTilemap", 64, 2),
                new ExpectedLayer("HouseInteriorWall3dOverdraw01Tilemap", 1, 3),
                new ExpectedLayer("HouseInteriorForeGroundTilemap", 0, 3),
                new ExpectedLayer("HouseInteriorShopTilemap", 0, 4),
                new ExpectedLayer("HouseInteriorBorderTilemap", 8, 5),
                new ExpectedLayer("HouseInteriorHouse1Tilemap", 0, 6),
                new ExpectedLayer("HouseInteriorHouse2Tilemap", 17, 7),
                new ExpectedLayer("HouseInteriorPoliceTilemap", 0, 8),
                new ExpectedLayer("HouseInteriorShootingTilemap", 0, 9),
                new ExpectedLayer("HouseInteriorSchoolTilemap", 0, 10),
                new ExpectedLayer("HouseInteriorKitchenTilemap", 0, 11)
            });
            ValidateScene(issues, "Assets/Scenes/ShopInterior.unity", "ShopInteriorLdtkTilemaps", new[]
            {
                new ExpectedLayer("ShopInteriorGroundTilemap", 149, 0),
                new ExpectedLayer("ShopInteriorWallTilemap", 48, 1),
                new ExpectedLayer("ShopInteriorWall3dTilemap", 0, 2),
                new ExpectedLayer("ShopInteriorForeGroundTilemap", 0, 3),
                new ExpectedLayer("ShopInteriorShopTilemap", 68, 4),
                new ExpectedLayer("ShopInteriorShopOverdraw01Tilemap", 20, 5),
                new ExpectedLayer("ShopInteriorShopOverdraw02Tilemap", 7, 6),
                new ExpectedLayer("ShopInteriorBorderTilemap", 42, 5),
                new ExpectedLayer("ShopInteriorBorderOverdraw01Tilemap", 3, 6),
                new ExpectedLayer("ShopInteriorBorderOverdraw02Tilemap", 1, 7),
                new ExpectedLayer("ShopInteriorHouse1Tilemap", 0, 6),
                new ExpectedLayer("ShopInteriorHouse2Tilemap", 0, 7),
                new ExpectedLayer("ShopInteriorPoliceTilemap", 0, 8),
                new ExpectedLayer("ShopInteriorShootingTilemap", 0, 9),
                new ExpectedLayer("ShopInteriorSchoolTilemap", 0, 10),
                new ExpectedLayer("ShopInteriorKitchenTilemap", 0, 11)
            });
            ValidateScene(issues, "Assets/Scenes/HospitalInterior.unity", "HospitalInteriorLdtkTilemaps", new[]
            {
                new ExpectedLayer("HospitalInteriorGroundTilemap", 241, 0),
                new ExpectedLayer("HospitalInteriorWallTilemap", 0, 1),
                new ExpectedLayer("HospitalInteriorWall3dTilemap", 60, 2),
                new ExpectedLayer("HospitalInteriorForeGroundTilemap", 8, 3),
                new ExpectedLayer("HospitalInteriorForeGroundOverdraw01Tilemap", 2, 4),
                new ExpectedLayer("HospitalInteriorShopTilemap", 0, 4),
                new ExpectedLayer("HospitalInteriorBorderTilemap", 37, 5),
                new ExpectedLayer("HospitalInteriorHouse1Tilemap", 0, 6),
                new ExpectedLayer("HospitalInteriorHouse2Tilemap", 0, 7),
                new ExpectedLayer("HospitalInteriorPoliceTilemap", 0, 8),
                new ExpectedLayer("HospitalInteriorShootingTilemap", 0, 9),
                new ExpectedLayer("HospitalInteriorSchoolTilemap", 0, 10),
                new ExpectedLayer("HospitalInteriorKitchenTilemap", 0, 11)
            });
            ValidateScene(issues, "Assets/Scenes/PoliceInterior.unity", "PoliceInteriorLdtkTilemaps", new[]
            {
                new ExpectedLayer("PoliceInteriorGroundTilemap", 105, 0),
                new ExpectedLayer("PoliceInteriorWallTilemap", 39, 1),
                new ExpectedLayer("PoliceInteriorWall3dTilemap", 0, 2),
                new ExpectedLayer("PoliceInteriorForeGroundTilemap", 0, 3),
                new ExpectedLayer("PoliceInteriorShopTilemap", 0, 4),
                new ExpectedLayer("PoliceInteriorBorderTilemap", 39, 5),
                new ExpectedLayer("PoliceInteriorHouse1Tilemap", 0, 6),
                new ExpectedLayer("PoliceInteriorHouse2Tilemap", 0, 7),
                new ExpectedLayer("PoliceInteriorPoliceTilemap", 40, 8),
                new ExpectedLayer("PoliceInteriorPoliceOverdraw01Tilemap", 8, 9),
                new ExpectedLayer("PoliceInteriorPoliceOverdraw02Tilemap", 1, 10),
                new ExpectedLayer("PoliceInteriorShootingTilemap", 0, 9),
                new ExpectedLayer("PoliceInteriorSchoolTilemap", 0, 10),
                new ExpectedLayer("PoliceInteriorKitchenTilemap", 0, 11)
            });
            ValidateScene(issues, "Assets/Scenes/PoliceShootingInterior.unity", "PoliceShootingInteriorLdtkTilemaps", new[]
            {
                new ExpectedLayer("PoliceShootingInteriorGroundTilemap", 60, 0),
                new ExpectedLayer("PoliceShootingInteriorWallTilemap", 0, 1),
                new ExpectedLayer("PoliceShootingInteriorWall3dTilemap", 44, 2),
                new ExpectedLayer("PoliceShootingInteriorWall3dOverdraw01Tilemap", 3, 3),
                new ExpectedLayer("PoliceShootingInteriorForeGroundTilemap", 0, 3),
                new ExpectedLayer("PoliceShootingInteriorShopTilemap", 0, 4),
                new ExpectedLayer("PoliceShootingInteriorBorderTilemap", 0, 5),
                new ExpectedLayer("PoliceShootingInteriorHouse1Tilemap", 0, 6),
                new ExpectedLayer("PoliceShootingInteriorHouse2Tilemap", 0, 7),
                new ExpectedLayer("PoliceShootingInteriorPoliceTilemap", 0, 8),
                new ExpectedLayer("PoliceShootingInteriorShootingTilemap", 32, 9),
                new ExpectedLayer("PoliceShootingInteriorShootingOverdraw01Tilemap", 9, 10),
                new ExpectedLayer("PoliceShootingInteriorSchoolTilemap", 0, 10),
                new ExpectedLayer("PoliceShootingInteriorKitchenTilemap", 0, 11)
            });
            ValidateScene(issues, "Assets/Scenes/KitchenInterior.unity", "KitchenInteriorLdtkTilemaps", new[]
            {
                new ExpectedLayer("KitchenInteriorGroundTilemap", 167, 0),
                new ExpectedLayer("KitchenInteriorWallTilemap", 40, 1),
                new ExpectedLayer("KitchenInteriorWall3dTilemap", 0, 2),
                new ExpectedLayer("KitchenInteriorForeGroundTilemap", 0, 3),
                new ExpectedLayer("KitchenInteriorShopTilemap", 0, 4),
                new ExpectedLayer("KitchenInteriorBorderTilemap", 45, 5),
                new ExpectedLayer("KitchenInteriorBorderOverdraw01Tilemap", 3, 6),
                new ExpectedLayer("KitchenInteriorBorderOverdraw02Tilemap", 1, 7),
                new ExpectedLayer("KitchenInteriorHouse1Tilemap", 0, 6),
                new ExpectedLayer("KitchenInteriorHouse2Tilemap", 0, 7),
                new ExpectedLayer("KitchenInteriorPoliceTilemap", 0, 8),
                new ExpectedLayer("KitchenInteriorShootingTilemap", 0, 9),
                new ExpectedLayer("KitchenInteriorSchoolTilemap", 0, 10),
                new ExpectedLayer("KitchenInteriorKitchenTilemap", 63, 11),
                new ExpectedLayer("KitchenInteriorKitchenOverdraw01Tilemap", 20, 12),
                new ExpectedLayer("KitchenInteriorKitchenOverdraw02Tilemap", 5, 13)
            });
            ValidateScene(issues, "Assets/Scenes/Kitchen2Interior.unity", "Kitchen2InteriorLdtkTilemaps", new[]
            {
                new ExpectedLayer("Kitchen2InteriorGroundTilemap", 77, 0),
                new ExpectedLayer("Kitchen2InteriorWallTilemap", 24, 1),
                new ExpectedLayer("Kitchen2InteriorWall3dTilemap", 0, 2),
                new ExpectedLayer("Kitchen2InteriorForeGroundTilemap", 0, 3),
                new ExpectedLayer("Kitchen2InteriorShopTilemap", 0, 4),
                new ExpectedLayer("Kitchen2InteriorBorderTilemap", 23, 5),
                new ExpectedLayer("Kitchen2InteriorBorderOverdraw01Tilemap", 2, 6),
                new ExpectedLayer("Kitchen2InteriorHouse1Tilemap", 0, 6),
                new ExpectedLayer("Kitchen2InteriorHouse2Tilemap", 0, 7),
                new ExpectedLayer("Kitchen2InteriorPoliceTilemap", 0, 8),
                new ExpectedLayer("Kitchen2InteriorShootingTilemap", 0, 9),
                new ExpectedLayer("Kitchen2InteriorSchoolTilemap", 0, 10),
                new ExpectedLayer("Kitchen2InteriorKitchenTilemap", 40, 11),
                new ExpectedLayer("Kitchen2InteriorKitchenOverdraw01Tilemap", 7, 12),
                new ExpectedLayer("Kitchen2InteriorKitchenOverdraw02Tilemap", 3, 13)
            });
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return issues;
        }

        private static void ValidateScene(List<LdtkImportIssue> issues, string scenePath, string rootName, IReadOnlyList<ExpectedLayer> layers)
        {
            if (!File.Exists(scenePath))
            {
                issues.Add(new LdtkImportIssue("path", scenePath, "Saved scene path does not exist."));
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                issues.Add(new LdtkImportIssue("path", scenePath, "Saved scene could not be opened."));
                return;
            }

            GameObject root = GameObject.Find(rootName);
            if (root == null)
            {
                issues.Add(new LdtkImportIssue("sorting", scenePath, "Saved scene is missing LDtk Tilemap root " + rootName));
                return;
            }

            foreach (ExpectedLayer layer in layers)
            {
                Transform child = root.transform.Find(layer.Name);
                if (child == null)
                {
                    issues.Add(new LdtkImportIssue("sorting", scenePath, "Missing LDtk Tilemap layer " + layer.Name));
                    continue;
                }

                Tilemap tilemap = child.GetComponent<Tilemap>();
                TilemapRenderer renderer = child.GetComponent<TilemapRenderer>();
                if (tilemap == null)
                {
                    issues.Add(new LdtkImportIssue("sorting", scenePath, layer.Name + " has no Tilemap component."));
                }
                else
                {
                    int actualTiles = CountTiles(tilemap);
                    if (actualTiles != layer.TileCount)
                    {
                        issues.Add(new LdtkImportIssue("sorting", scenePath, layer.Name + " tile count mismatch. expected=" + layer.TileCount + " actual=" + actualTiles));
                    }
                }

                if (renderer == null)
                {
                    issues.Add(new LdtkImportIssue("sorting", scenePath, layer.Name + " has no TilemapRenderer component."));
                }
                else
                {
                    if (!renderer.enabled)
                    {
                        issues.Add(new LdtkImportIssue("sorting", scenePath, layer.Name + " TilemapRenderer is disabled."));
                    }

                    if (renderer.sortingOrder != layer.SortingOrder)
                    {
                        issues.Add(new LdtkImportIssue("sorting", scenePath, layer.Name + " sorting order mismatch. expected=" + layer.SortingOrder + " actual=" + renderer.sortingOrder));
                    }
                }
            }
        }

        private static IEnumerable<string> FindTileAssetPaths(string folder, List<LdtkImportIssue> issues)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                issues.Add(new LdtkImportIssue("path", folder, "Generated LDtk Tile folder does not exist."));
                return Array.Empty<string>();
            }

            return AssetDatabase.FindAssets("t:Tile", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".asset", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
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

        private static Vector2Int ReadPngSize(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 24 || bytes[12] != (byte)'I' || bytes[13] != (byte)'H' || bytes[14] != (byte)'D' || bytes[15] != (byte)'R')
            {
                throw new InvalidOperationException("File is not a PNG with an IHDR header.");
            }

            return new Vector2Int(ReadBigEndianInt(bytes, 16), ReadBigEndianInt(bytes, 20));
        }

        private static int ReadBigEndianInt(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        private readonly struct ExpectedLayer
        {
            public ExpectedLayer(string name, int tileCount, int sortingOrder)
            {
                Name = name;
                TileCount = tileCount;
                SortingOrder = sortingOrder;
            }

            public string Name { get; }
            public int TileCount { get; }
            public int SortingOrder { get; }
        }
    }

    // Interior exits used to be built only from the LDtk level files at play time. Those files never ship in a player
    // build, so the exits are saved into each interior scene here and the runtime pass just refreshes them in the editor.
    public static class InteriorDoorPortalBaker
    {
        [MenuItem("Rootborn/World/Bake Interior Exit Doors")]
        public static void BakeAll()
        {
            var failures = new List<string>();
            foreach (string sceneName in Rootborn.Game.World.InteriorDoorPortalRuntimeInstaller.InteriorSceneNames)
            {
                string path = "Assets/Scenes/" + sceneName + ".unity";
                if (!File.Exists(path)) { failures.Add(path + " missing"); continue; }
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                string doorsBefore = DescribeExitDoors(scene);
                int count = Rootborn.Game.World.InteriorDoorPortalRuntimeInstaller.EnsureForScene(scene);
                if (count == 0) { failures.Add(path + " has no exit doors"); continue; }
                // Doors are edited in place (position, size, destination), so compare their full description, not the count.
                bool changed = DescribeExitDoors(scene) != doorsBefore;
                if (Array.IndexOf(CollisionSceneNames, sceneName) >= 0)
                {
                    string problem = BakeCollision(scene, out int blocked, out bool collisionChanged);
                    if (problem != null) { failures.Add(path + " " + problem); continue; }
                    changed |= collisionChanged;
                    Debug.Log("[ROOTBORN] " + (collisionChanged ? "Baked " : "Kept ") + blocked + " collision cell(s) in " + path);
                }
                // Rebuilding identical objects rewrote whole scene files with new ids; save only real changes.
                if (!changed) { Debug.Log("[ROOTBORN] " + path + " unchanged"); continue; }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[ROOTBORN] Baked " + count + " exit door(s) into " + path);
            }
            if (failures.Count > 0) throw new InvalidOperationException("[ROOTBORN] Interior exit bake failed: " + string.Join("; ", failures));
        }

        // These interiors were saved with no collision at all, so players walked through walls and off the map.
        // Everything that is not floor, plus wall and border tiles, becomes an invisible grid-collider cell; exits stay open.
        private const string CollisionTilePath = "Assets/Data/Interiors/TileSets/UrbanStreet31/UrbanStreetCollision.asset";
        public static readonly string[] CollisionSceneNames = { "HospitalInterior", "ShopInterior", "PoliceInterior", "PoliceShootingInterior", "KitchenInterior", "Kitchen2Interior" };

        public static string CollisionTilemapName(string sceneName) => sceneName + "CollisionTilemap";
        private const float MinSpawnDistanceFromExit = 0.9f;
        // Furniture and fixtures (counters, tables, cell bars) block like walls; their raised overdraw parts and
        // the foreground layer are drawn above the player and stay walkable.
        private static readonly string[] PropLayerKeys = { "Police", "Kitchen", "Shop", "Shooting", "School", "House1", "House2" };

        private static bool SameCollision(Tilemap existing, HashSet<Vector3Int> wanted, TileBase tile)
        {
            if (existing == null || existing.GetComponent<Grid>() == null || existing.GetComponent<TilemapCollider2D>() == null) return false;
            int count = 0;
            existing.CompressBounds();
            foreach (var cell in existing.cellBounds.allPositionsWithin)
            {
                if (!existing.HasTile(cell)) continue;
                if (!wanted.Contains(cell) || existing.GetTile(cell) != tile) return false;
                count++;
            }
            return count == wanted.Count;
        }

        private static string DescribeExitDoors(UnityEngine.SceneManagement.Scene scene)
        {
            var lines = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var portal in root.GetComponentsInChildren<Rootborn.Game.World.WorldPortal>(true))
            {
                var box = portal.GetComponent<BoxCollider2D>();
                lines.Add(portal.name + "|" + portal.transform.position + "|" + (box != null ? box.size.ToString() + box.offset + box.isTrigger : "-")
                    + "|" + portal.DestinationScene + "|" + portal.DestinationSpawnId + "|" + portal.gameObject.activeSelf);
            }
            lines.Sort(StringComparer.Ordinal);
            return string.Join("
", lines);
        }

        private static string BakeCollision(UnityEngine.SceneManagement.Scene scene, out int blocked, out bool changed)
        {
            blocked = 0;
            changed = false;
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(CollisionTilePath);
            if (tile == null) return "collision tile missing at " + CollisionTilePath;
            string collisionName = CollisionTilemapName(scene.name);
            var maps = new List<Tilemap>();
            Tilemap existing = null;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (map.name == collisionName) { existing = map; continue; }
                maps.Add(map);
            }
            Tilemap groundMap = null;
            var ground = new HashSet<Vector3Int>();
            var solid = new HashSet<Vector3Int>();
            var walls = new HashSet<Vector3Int>();
            var min = new Vector3Int(int.MaxValue, int.MaxValue, 0);
            var max = new Vector3Int(int.MinValue, int.MinValue, 0);
            foreach (var map in maps)
            {
                if (map == null) continue;
                // Layer names repeat the scene name ("PoliceInterior..."), so judge by the layer part alone.
                string layer = map.name.StartsWith(scene.name, StringComparison.Ordinal) ? map.name.Substring(scene.name.Length) : map.name;
                bool isGround = layer.Contains("Ground");
                bool isProp = Array.Exists(PropLayerKeys, k => layer.StartsWith(k, StringComparison.Ordinal)) && !layer.Contains("Overdraw");
                bool isSolid = layer.Contains("Wall") || layer.Contains("Border") || isProp;
                if (isGround && groundMap == null) groundMap = map;
                map.CompressBounds();
                foreach (var cell in map.cellBounds.allPositionsWithin)
                {
                    if (!map.HasTile(cell)) continue;
                    min = Vector3Int.Min(min, cell); max = Vector3Int.Max(max, cell);
                    if (isGround) ground.Add(cell);
                    if (isSolid) solid.Add(cell);
                    if (isSolid && !isProp) walls.Add(cell);
                }
            }
            if (groundMap == null || ground.Count == 0) return "has no ground tilemap";
            var open = new HashSet<Vector3Int>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var portal in root.GetComponentsInChildren<Rootborn.Game.World.WorldPortal>(true))
            {
                var box = portal.GetComponent<BoxCollider2D>();
                if (box == null) continue;
                Vector2 center = (Vector2)portal.transform.position + box.offset;
                Vector2 half = box.size * 0.5f;
                for (int x = Mathf.FloorToInt(center.x - half.x + 0.01f); x <= Mathf.FloorToInt(center.x + half.x - 0.01f); x++)
                for (int y = Mathf.FloorToInt(center.y - half.y + 0.01f); y <= Mathf.FloorToInt(center.y + half.y - 0.01f); y++)
                    open.Add(new Vector3Int(x, y, 0));
            }
            // Each LDtk layer carries its own Grid, so the collision layer needs one too or its cells collapse to the origin.
            // A fixture drawn right at a door (the police entrance gate) must not shut the exit; floor next to an exit stays open.
            foreach (var cell in new List<Vector3Int>(open))
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var near = cell + new Vector3Int(dx, dy, 0);
                    if (ground.Contains(near) && !walls.Contains(near)) open.Add(near);
                }
            var blockedCells = new HashSet<Vector3Int>();
            for (int x = min.x - 1; x <= max.x + 1; x++)
            for (int y = min.y - 1; y <= max.y + 1; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (open.Contains(cell) || (ground.Contains(cell) && !solid.Contains(cell))) continue;
                blockedCells.Add(cell);
            }
            blocked = blockedCells.Count;
            if (!SameCollision(existing, blockedCells, tile))
            {
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
                var go = new GameObject(collisionName, typeof(Grid), typeof(Tilemap), typeof(TilemapCollider2D));
                var sourceGrid = groundMap.GetComponentInParent<Grid>();
                if (sourceGrid != null) { var grid = go.GetComponent<Grid>(); grid.cellSize = sourceGrid.cellSize; grid.cellGap = sourceGrid.cellGap; grid.cellLayout = sourceGrid.cellLayout; grid.cellSwizzle = sourceGrid.cellSwizzle; }
                go.transform.SetParent(groundMap.transform.parent, false);
                var collision = go.GetComponent<Tilemap>();
                foreach (var cell in blockedCells) collision.SetTile(cell, tile);
                changed = true;
            }
            var portalCells = new List<HashSet<Vector3Int>>();
            var portalBoxes = new List<Rect>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var portal in root.GetComponentsInChildren<Rootborn.Game.World.WorldPortal>(true))
            {
                var box = portal.GetComponent<BoxCollider2D>();
                if (box == null) continue;
                var cells = new HashSet<Vector3Int>();
                Vector2 center = (Vector2)portal.transform.position + box.offset;
                Vector2 half = box.size * 0.5f;
                for (int x = Mathf.FloorToInt(center.x - half.x + 0.01f); x <= Mathf.FloorToInt(center.x + half.x - 0.01f); x++)
                for (int y = Mathf.FloorToInt(center.y - half.y + 0.01f); y <= Mathf.FloorToInt(center.y + half.y - 0.01f); y++)
                    cells.Add(new Vector3Int(x, y, 0));
                portalCells.Add(cells);
                portalBoxes.Add(new Rect(center - half, box.size));
            }
            foreach (var root in scene.GetRootGameObjects())
            foreach (var spawn in root.GetComponentsInChildren<Rootborn.Game.World.WorldSpawnPoint>(true))
            {
                var p = spawn.transform.position;
                var cell = new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), 0);
                if (blockedCells.Contains(cell)) return "spawn " + spawn.SpawnId + " at " + cell + " is not on open floor";
                // A spawn hugging an exit trigger sends the arriving player straight back through it (the shooting range did).
                foreach (var exitBox in portalBoxes)
                {
                    float dx = Mathf.Max(exitBox.xMin - p.x, 0f, p.x - exitBox.xMax);
                    float dy = Mathf.Max(exitBox.yMin - p.y, 0f, p.y - exitBox.yMax);
                    if (Mathf.Sqrt(dx * dx + dy * dy) < MinSpawnDistanceFromExit) return "spawn " + spawn.SpawnId + " is within " + MinSpawnDistanceFromExit + " of an exit trigger";
                }
                // Every exit must stay reachable on foot from where the player arrives.
                var reached = new HashSet<Vector3Int> { cell };
                var queue = new Queue<Vector3Int>(); queue.Enqueue(cell);
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    foreach (var d in new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down })
                    {
                        var n = c + d;
                        if (n.x < min.x - 1 || n.x > max.x + 1 || n.y < min.y - 1 || n.y > max.y + 1) continue;
                        if (blockedCells.Contains(n) || !reached.Add(n)) continue;
                        queue.Enqueue(n);
                    }
                }
                for (int i = 0; i < portalCells.Count; i++)
                    if (!portalCells[i].Overlaps(reached)) return "spawn " + spawn.SpawnId + " cannot walk to exit " + i;
            }
            return null;
        }
    }
}
