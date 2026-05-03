using System.Collections.Generic;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Common;
using Rootborn.Game.Resources;
using Rootborn.Game.Time;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Editor.Tools
{
    public static class FarmSceneBuilder
    {
        private const string FarmScenePath = "Assets/Scenes/Farm.unity";
        private const string TileSheetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Tiles/Tile.png";
        private const string GroundTileAssetPath = "Assets/Data/Tiles/GroundTile.asset";
        private const string GroundTileFolder = "Assets/Data/Tiles";

        private const int GroundCols = 30;
        private const int GroundRows = 20;

        [MenuItem("Rootborn/Scene/Build Farm Scene")]
        public static void Build()
        {
            if (!OneClickSetup.EnsureNotPlaying()) return;
            EnsureFolder(GroundTileFolder);

            var groundSprite = PickGrassSprite();
            if (groundSprite == null)
            {
                Debug.LogError("[ROOTBORN] Tile sheet not sliced. Run 'Rootborn/Pixelwood/Slice Sprite Sheets' first.");
                return;
            }

            var groundTile = CreateOrLoadGroundTile(groundSprite);
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");

            var scene = EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Single);
            EnsureCamera();
            EnsureEventSystem();
            EnsureGameClock();
            EnsureDiagnostics();
            var grid = EnsureGrid();
            var tilemap = EnsureGroundTilemap(grid);
            FillGround(tilemap, groundTile);
            EnsureResourceNodes(registry);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ROOTBORN] Farm scene built — ground tilemap + resource nodes placed.");
        }

        private static Sprite PickGrassSprite()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(TileSheetPath);
            Sprite best = null;
            foreach (var a in assets)
            {
                var s = a as Sprite;
                if (s == null) continue;
                if (best == null) best = s;
                if (s.name == "Tile_r2_c4" || s.name == "Tile_r3_c4")
                {
                    return s;
                }
            }
            return best;
        }

        private static TileBase CreateOrLoadGroundTile(Sprite sprite)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(GroundTileAssetPath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, GroundTileAssetPath);
            }
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            AssetDatabase.SaveAssets();
            return tile;
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.backgroundColor = new Color(0.1f, 0.13f, 0.1f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            go.tag = "MainCamera";
            go.transform.position = new Vector3(GroundCols * 0.5f, GroundRows * 0.5f, -10f);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        private static void EnsureGameClock()
        {
            if (GameObject.Find("[GameClock]") != null) return;
            var go = new GameObject("[GameClock]");
            go.AddComponent<GameClock>();
        }

        private static void EnsureDiagnostics()
        {
            if (GameObject.Find("[SceneDiagnostics]") != null) return;
            var go = new GameObject("[SceneDiagnostics]");
            go.AddComponent<SceneDiagnostics>();
        }

        private static Grid EnsureGrid()
        {
            var grid = Object.FindFirstObjectByType<Grid>();
            if (grid != null) return grid;
            var go = new GameObject("Grid");
            grid = go.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);
            return grid;
        }

        private static Tilemap EnsureGroundTilemap(Grid grid)
        {
            var existing = grid.transform.Find("Ground");
            if (existing != null && existing.TryGetComponent(out Tilemap tm)) return tm;

            var go = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid.transform, false);
            tm = go.GetComponent<Tilemap>();
            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = -10;
            return tm;
        }

        private static void FillGround(Tilemap tm, TileBase tile)
        {
            tm.ClearAllTiles();
            for (int x = 0; x < GroundCols; x++)
            {
                for (int y = 0; y < GroundRows; y++)
                {
                    tm.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }
        }

        private static void EnsureResourceNodes(GameDataRegistry registry)
        {
            var rootName = "[Resources]";
            var rootGo = GameObject.Find(rootName);
            if (rootGo == null)
            {
                rootGo = new GameObject(rootName);
            }
            else
            {
                while (rootGo.transform.childCount > 0)
                {
                    Object.DestroyImmediate(rootGo.transform.GetChild(0).gameObject);
                }
            }

            if (registry == null)
            {
                Debug.LogWarning("[ROOTBORN] GameDataRegistry not found — run 'Rootborn/Data/Generate Default Data' first.");
                return;
            }

            ResourceNodeDefinition treeDef = null;
            ResourceNodeDefinition rockDef = null;
            foreach (var r in registry.Resources)
            {
                if (r == null) continue;
                if (r.Id == "Tree") treeDef = r;
                else if (r.Id == "Rock") rockDef = r;
            }

            var rng = new System.Random(20260504);
            int treeTarget = 12;
            int rockTarget = 8;
            for (int i = 0; i < treeTarget && treeDef != null; i++)
            {
                SpawnNode(rootGo.transform, treeDef, RandomCellPos(rng), $"Tree_{i:00}");
            }
            for (int i = 0; i < rockTarget && rockDef != null; i++)
            {
                SpawnNode(rootGo.transform, rockDef, RandomCellPos(rng), $"Rock_{i:00}");
            }
        }

        private static Vector3 RandomCellPos(System.Random rng)
        {
            float x = rng.Next(2, GroundCols - 2) + 0.5f;
            float y = rng.Next(2, GroundRows - 2) + 0.5f;
            return new Vector3(x, y, 0f);
        }

        private static void SpawnNode(Transform parent, ResourceNodeDefinition def, Vector3 position, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = def.Sprite;
            sr.sortingOrder = 1;
            var node = go.AddComponent<ResourceNode>();
            var so = new SerializedObject(node);
            so.FindProperty("_definition").objectReferenceValue = def;
            so.FindProperty("_renderer").objectReferenceValue = sr;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
