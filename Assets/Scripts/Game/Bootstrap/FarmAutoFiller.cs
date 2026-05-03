using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Resources;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Bootstrap
{
    public sealed class FarmAutoFiller : MonoBehaviour
    {
        private const int GroundCols = 30;
        private const int GroundRows = 20;

        [SerializeField] private string _registryAddress = "GameDataRegistry";
        [SerializeField] private string _registryResourcePath = "GameDataRegistry";

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != "Farm") return;
            FillIfEmpty();
        }

        private void FillIfEmpty()
        {
            var registry = LoadRegistry();
            if (registry == null)
            {
                Debug.LogWarning("[ROOTBORN/AutoFiller] GameDataRegistry not found anywhere. Skipping auto-fill.");
                return;
            }

            EnsureCamera();
            var groundTilemap = EnsureGroundTilemap();
            EnsureGroundFilled(groundTilemap, registry);
            EnsureResourceNodes(registry);
            EnsurePlayer(registry);

            Debug.Log("[ROOTBORN/AutoFiller] Farm scene auto-fill complete.");
        }

        private GameDataRegistry LoadRegistry()
        {
            var fromResources = UnityEngine.Resources.Load<GameDataRegistry>(_registryResourcePath);
            if (fromResources != null) return fromResources;

            var allRegistries = UnityEngine.Resources.FindObjectsOfTypeAll<GameDataRegistry>();
            if (allRegistries != null && allRegistries.Length > 0) return allRegistries[0];
            return null;
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

        private static Tilemap EnsureGroundTilemap()
        {
            var existing = FindObjectByName("[Ground]");
            if (existing != null && existing.TryGetComponent(out Tilemap existingTm)) return existingTm;

            var gridGo = FindObjectByName("[Grid]");
            if (gridGo == null)
            {
                gridGo = new GameObject("[Grid]");
                gridGo.AddComponent<Grid>();
            }

            var groundGo = new GameObject("[Ground]", typeof(Tilemap), typeof(TilemapRenderer));
            groundGo.transform.SetParent(gridGo.transform, false);
            var tm = groundGo.GetComponent<Tilemap>();
            var renderer = groundGo.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = -10;
            return tm;
        }

        private static void EnsureGroundFilled(Tilemap tm, GameDataRegistry registry)
        {
            if (tm == null) return;
            var bounds = tm.cellBounds;
            if (bounds.size.x >= GroundCols && bounds.size.y >= GroundRows) return;

            Sprite groundSprite = registry.GroundSprite;
            if (groundSprite == null)
            {
                for (int i = 0; i < registry.Resources.Length; i++)
                {
                    if (registry.Resources[i] != null && registry.Resources[i].Sprite != null)
                    {
                        groundSprite = registry.Resources[i].Sprite;
                        break;
                    }
                }
            }
            if (groundSprite == null)
            {
                Debug.LogWarning("[ROOTBORN/AutoFiller] No ground sprite available. Tilemap left empty.");
                return;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = groundSprite;
            tile.colliderType = Tile.ColliderType.None;

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
            var existing = FindObjectByName(rootName);
            if (existing != null && existing.transform.childCount > 0) return;

            var rootGo = existing != null ? existing : new GameObject(rootName);

            ResourceNodeDefinition treeDef = null;
            ResourceNodeDefinition rockDef = null;
            for (int i = 0; i < registry.Resources.Length; i++)
            {
                var r = registry.Resources[i];
                if (r == null) continue;
                if (r.Id == "Tree") treeDef = r;
                else if (r.Id == "Rock") rockDef = r;
            }

            if (treeDef == null && rockDef == null)
            {
                Debug.LogWarning("[ROOTBORN/AutoFiller] No Tree/Rock definitions in registry.");
                return;
            }

            var rng = new System.Random(20260504);
            int treeTarget = treeDef != null ? 12 : 0;
            int rockTarget = rockDef != null ? 8 : 0;

            for (int i = 0; i < treeTarget; i++)
            {
                SpawnNode(rootGo.transform, treeDef, RandomCellPos(rng), $"Tree_{i:00}");
            }
            for (int i = 0; i < rockTarget; i++)
            {
                SpawnNode(rootGo.transform, rockDef, RandomCellPos(rng), $"Rock_{i:00}");
            }
            Debug.Log($"[ROOTBORN/AutoFiller] Resource nodes spawned: trees={treeTarget}, rocks={rockTarget}");
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
            node.BindForRuntime(def, sr);
        }

        private static void EnsurePlayer(GameDataRegistry registry)
        {
            if (FindObjectByName("Player") != null) return;

            var go = new GameObject("Player");
            go.transform.position = new Vector3(GroundCols * 0.5f, GroundRows * 0.5f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = registry.PlayerSprite;
            sr.sortingOrder = 5;
            go.AddComponent<Rootborn.Game.Player.PlayerController>();
        }

        private static GameObject FindObjectByName(string name)
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name) return roots[i];
                var t = roots[i].transform.Find(name);
                if (t != null) return t.gameObject;
            }
            return null;
        }
    }
}
