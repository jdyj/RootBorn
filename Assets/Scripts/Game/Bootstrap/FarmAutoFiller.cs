using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
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

        private async void Awake()
        {
            if (SceneManager.GetActiveScene().name != "Farm") return;

            // Managers 가 아직 부팅 안 된 경우 (씬 직접 Play 로 들어온 경우) 부트스트랩 보장
            if (Managers.Managers.Instance == null || Managers.Managers.Instance.IsBootstrapped == false)
            {
                await Managers.Managers.BootstrapAsync();
            }
            FillIfEmpty();
        }

        public void FillIfEmpty()
        {
            var data = Managers.Managers.Data;
            var registry = data?.Registry ?? LoadRegistryFallback();
            if (registry == null)
            {
                Debug.LogWarning("[ROOTBORN/AutoFiller] GameDataRegistry not found via DataManager or Resources. Skipping auto-fill.");
                return;
            }

            EnsureCamera();
            var groundTilemap = EnsureGroundTilemap();
            EnsureGroundFilled(groundTilemap, registry, data);
            EnsureResourceNodes(registry);
            EnsurePlayer(registry, data);

            Debug.Log("[ROOTBORN/AutoFiller] Farm scene auto-fill complete.");
        }

        private static GameDataRegistry LoadRegistryFallback()
        {
            var fromResources = UnityEngine.Resources.Load<GameDataRegistry>("GameDataRegistry");
            if (fromResources != null) return fromResources;
            var allRegistries = UnityEngine.Resources.FindObjectsOfTypeAll<GameDataRegistry>();
            return (allRegistries != null && allRegistries.Length > 0) ? allRegistries[0] : null;
        }

        private static void EnsureCamera()
        {
            // 기존 Main Camera가 있으면 재사용하되 Orthographic + 위치/배경 강제 셋업.
            // SampleScene에서 복사한 Farm 씬은 3D 원근 카메라 + Skybox 클리어를 가지고 있을 수 있음.
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.backgroundColor = new Color(0.45f, 0.65f, 0.45f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = -50f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(GroundCols * 0.5f, GroundRows * 0.5f, -10f);
            cam.transform.rotation = Quaternion.identity;

            // Skybox 끄기 + Audio Listener 한 개로 정리
            var skybox = cam.GetComponent<Skybox>();
            if (skybox != null) skybox.enabled = false;
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

        private static void EnsureGroundFilled(Tilemap tm, GameDataRegistry registry, DataManager data)
        {
            if (tm == null) return;
            var bounds = tm.cellBounds;
            if (bounds.size.x >= GroundCols && bounds.size.y >= GroundRows) return;

            Sprite groundSprite = data != null ? data.GroundSprite : null;
            if (groundSprite == null) groundSprite = registry.GroundSprite;
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

        private void EnsurePlayer(GameDataRegistry registry, DataManager data)
        {
            if (FindObjectByName("Player") != null) return;

            // 즉시 sprite 단독 Player 생성 (Addressables 비동기 로드 대기 안 함)
            Sprite sprite = data != null ? data.PlayerSprite : null;
            if (sprite == null) sprite = registry.PlayerSprite;
            if (sprite == null)
            {
                // Sprite를 못 찾으면 코드로 빨간 8x8 fallback sprite 생성 (시각적으로 명확히 보이도록)
                sprite = MakeFallbackSprite(new Color(1f, 0.2f, 0.2f, 1f));
                Debug.LogWarning("[ROOTBORN/AutoFiller] Player sprite not found. Using red fallback square.");
            }

            var playerInstance = new GameObject("Player");
            var sr = playerInstance.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 5;
            // Sprite가 너무 작으면(16x16, scale 1) 0.5x0.5 world unit. 보기 쉽게 2배 스케일.
            playerInstance.transform.localScale = new Vector3(2f, 2f, 1f);
            playerInstance.transform.position = new Vector3(GroundCols * 0.5f, GroundRows * 0.5f, 0f);

            playerInstance.AddComponent<Rootborn.Game.Player.PlayerController>();

            var interactor = playerInstance.AddComponent<Rootborn.Game.Player.GatherInteractor>();

            if (data != null && data.Registry != null)
            {
                var progress = new Rootborn.Game.Knowledge.KnowledgeProgress(data.Registry.Knowledge);
                progress.OnUnlocked += node =>
                {
                    Debug.Log($"[ROOTBORN/Knowledge] Unlocked: {node.Id}");
                };
                interactor.Bind(progress);
                if (data.ToolById.TryGetValue("BareHand", out var bareHand))
                {
                    interactor.EquippedTool = bareHand;
                }
            }

            // CameraFollow — Main Camera가 Player를 부드럽게 따라가도록
            if (Camera.main != null)
            {
                var follow = Camera.main.GetComponent<Rootborn.Game.Player.CameraFollow>();
                if (follow == null) follow = Camera.main.gameObject.AddComponent<Rootborn.Game.Player.CameraFollow>();
                follow.SetTarget(playerInstance.transform);
            }

            Debug.Log("[ROOTBORN/AutoFiller] Player spawned with GatherInteractor + KnowledgeProgress + CameraFollow.");
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

        private static Sprite MakeFallbackSprite(Color color)
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
