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
            EnsureFarmGrid(groundTilemap);
            EnsureResourceNodes(registry);
            EnsurePlayer(registry, data);
            EnsureFarmCanvas(registry);

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

        private static void EnsureFarmGrid(Tilemap groundTilemap)
        {
            var fgRoot = FindObjectByName("[FarmGrid]");
            Rootborn.Game.Farming.FarmGrid grid = null;
            if (fgRoot != null)
            {
                grid = fgRoot.GetComponent<Rootborn.Game.Farming.FarmGrid>();
            }
            if (fgRoot == null)
            {
                fgRoot = new GameObject("[FarmGrid]");
                grid = fgRoot.AddComponent<Rootborn.Game.Farming.FarmGrid>();
            }

            // [Crops] parent — CropPlot 인스턴스가 spawn 될 위치.
            var cropsParent = FindObjectByName("[Crops]");
            if (cropsParent == null)
            {
                cropsParent = new GameObject("[Crops]");
            }

            // Ground Tilemap 의 현재 첫 셀 tile 을 "tilledTile" 로 임시 사용 — fallback (TODO(asset): 전용 tilled-tile sprite 필요).
            UnityEngine.Tilemaps.TileBase tilledTile = null;
            if (groundTilemap != null)
            {
                var bounds = groundTilemap.cellBounds;
                if (bounds.size.x > 0 && bounds.size.y > 0)
                {
                    tilledTile = groundTilemap.GetTile(new Vector3Int(bounds.xMin, bounds.yMin, 0));
                }
            }

            grid.Configure(groundTilemap, tilledTile, null, cropsParent.transform);
        }

        private static void EnsureResourceNodes(GameDataRegistry registry)
        {
            var rootName = "[Resources]";
            var existing = FindObjectByName(rootName);

            // 기존 [Resources] 자식이 있으면 spawn 은 스킵하지만 collider/_definition 은 보강.
            if (existing != null && existing.transform.childCount > 0)
            {
                ReinforceExistingResources(existing.transform, registry);
                return;
            }

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

        // 이미 씬에 있는 기존 자원 GameObject 에 collider 가 누락됐으면 부여.
        // Setup Everything 을 다시 안 돌려도 다음 Play 에서 자동 보강.
        private static void ReinforceExistingResources(Transform root, GameDataRegistry registry)
        {
            if (!Common.ResourceCollisionToggle.Enabled) return;
            int reinforced = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i).gameObject;
                var node = child.GetComponent<ResourceNode>();
                if (node == null) continue;

                var def = node.Definition;
                // 직렬화 누락: 이름 prefix(Tree_/Rock_) 로 registry 에서 정의 복원.
                if (def == null && registry != null)
                {
                    string n = child.name;
                    for (int j = 0; j < registry.Resources.Length; j++)
                    {
                        var r = registry.Resources[j];
                        if (r != null && !string.IsNullOrEmpty(r.Id) && n.StartsWith(r.Id + "_"))
                        {
                            def = r;
                            var sr = child.GetComponent<SpriteRenderer>();
                            node.BindForRuntime(def, sr);
                            break;
                        }
                    }
                }
                if (def == null) continue;

                if (!def.IsWalkable && child.GetComponent<Collider2D>() == null)
                {
                    var col = child.AddComponent<BoxCollider2D>();
                    col.size = def.ColliderSize;
                    col.isTrigger = false;
                    reinforced++;
                }
            }
            if (reinforced > 0)
            {
                Debug.Log($"[ROOTBORN/AutoFiller] Reinforced {reinforced} existing resource nodes with BoxCollider2D.");
            }
        }

        private static void SpawnNode(Transform parent, ResourceNodeDefinition def, Vector3 position, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = def.Sprite;
            sr.sortingOrder = 1;

            // 데이터 기반 충돌: SO 의 _isWalkable=false 자원에만 collider.
            // 전역 토글 ResourceCollisionToggle.Enabled=false 이면 모두 통과.
            if (!def.IsWalkable && Common.ResourceCollisionToggle.Enabled)
            {
                var col = go.AddComponent<BoxCollider2D>();
                col.size = def.ColliderSize;
                col.isTrigger = false;
            }

            var node = go.AddComponent<ResourceNode>();
            node.BindForRuntime(def, sr);
        }

        private void EnsurePlayer(GameDataRegistry registry, DataManager data)
        {
            var existingPlayer = FindObjectByName("Player");
            if (existingPlayer != null)
            {
                ReinforcePlayer(existingPlayer);
                return;
            }

            // Sprite 우선순위: DataManager → Registry → 런타임 동적 검색 → fallback red square
            Sprite sprite = data != null ? data.PlayerSprite : null;
            if (sprite == null) sprite = registry.PlayerSprite;
            string spriteSource = sprite != null ? sprite.name : null;

            if (sprite == null)
            {
                // Registry 와이어링 실패 시 — 메모리에 로드된 모든 Sprite 중에서 'Idle_Down_'으로 시작하는
                // sub-sprite를 검색 (Pixelwood Slice 결과가 메모리에 있다면 잡힘)
                var allSprites = UnityEngine.Resources.FindObjectsOfTypeAll<Sprite>();
                foreach (var s in allSprites)
                {
                    if (s == null) continue;
                    if (s.name.StartsWith("Idle_Down_"))
                    {
                        sprite = s;
                        spriteSource = $"runtime-search:{s.name}";
                        break;
                    }
                }
            }

            if (sprite == null)
            {
                sprite = MakeFallbackSprite(new Color(1f, 0.2f, 0.2f, 1f));
                spriteSource = "fallback-red-16x16";
                Debug.LogWarning("[ROOTBORN/AutoFiller] Player sprite not found in Registry or runtime search. " +
                                 "Run 'Rootborn → Setup Everything' to wire PlayerSprite.");
            }

            var playerInstance = new GameObject("Player");
            var sr = playerInstance.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            // sortingOrder 매우 크게 — 자원 노드(1)보다 무조건 위
            sr.sortingOrder = 1000;
            sr.sortingLayerID = 0;
            // 캐릭터 sheet 는 PPU 49 라 1 unit 정사각형이지만, sheet 안의 캐릭터 art 가
            // 셀의 ~30%만 차지함. 자원 sprite(16x16 PPU 16, art 거의 가득)와 시각 크기를
            // 맞추기 위해 2.0× 스케일. 미세 조정은 Inspector 에서.
            playerInstance.transform.localScale = new Vector3(2.0f, 2.0f, 1f);
            // 자원 스폰 범위(2~28, 2~18)와 안 겹치는 가장자리에 스폰 (왼쪽 아래 코너)
            playerInstance.transform.position = new Vector3(1f, 1f, 0f);

            Debug.Log($"[ROOTBORN/AutoFiller] Player sprite='{spriteSource}', size={sprite.rect.size}, ppu={sprite.pixelsPerUnit}, position=(1,1), scale=2x.");

            // Rigidbody2D (Dynamic) + BoxCollider2D — 자원 collider 와 부딪쳐 자동 차단.
            // Dynamic + MovePosition: 정적 collider 와의 충돌 해소를 물리 엔진이 처리.
            // (Kinematic 은 MovePosition 시 정적 collider 를 밀고 들어갈 수 있어 차단이 약함.)
            var rb = playerInstance.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.linearDamping = 8f; // 멈춤 즉각성 (관성 최소화)

            var pcol = playerInstance.AddComponent<BoxCollider2D>();
            pcol.size = new Vector2(0.6f, 0.5f); // 발 부근 작은 박스
            pcol.offset = new Vector2(0f, -0.25f);
            pcol.isTrigger = false;

            playerInstance.AddComponent<Rootborn.Game.Player.PlayerController>();

            var interactor = playerInstance.AddComponent<Rootborn.Game.Player.GatherInteractor>();

            // PlayerInventory 부착 + 시작 도구 장착 (BareHand).
            var playerInv = playerInstance.AddComponent<Rootborn.Game.Player.PlayerInventory>();
            playerInv.Bind(registry);
            interactor.BindInventory(playerInv);
            // 시작 시 인벤토리 데모 데이터 (UI 검증용 — 후속 본격 게임플레이에서는 제거).
            playerInv.TryAddById("BareHand", 1);
            playerInv.TryAddById("StoneAxe", 1);
            playerInv.TryAddById("Wood", 5);
            playerInv.TryAddById("Stone", 3);
            var bareHandItem = playerInv.FindById("BareHand");
            if (bareHandItem != null) playerInv.EquipTool(bareHandItem);

            if (data != null && data.Registry != null)
            {
                var progress = new Rootborn.Game.Knowledge.KnowledgeProgress(data.Registry.Knowledge);
                progress.OnUnlocked += node =>
                {
                    Debug.Log($"[ROOTBORN/Knowledge] Unlocked: {node.Id}");
                    // 신규 해금된 도구를 인벤토리에 자동 추가 (데이터드리븐 — ID 분기 없음).
                    if (node.UnlocksTools != null)
                    {
                        for (int i = 0; i < node.UnlocksTools.Length; i++)
                        {
                            var t = node.UnlocksTools[i];
                            if (t != null) playerInv.TryAddById(t.Id, 1);
                        }
                    }
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

        // Farm UI Canvas + FarmHudController 절차 생성. 이미 있으면 스킵.
        // Canvas 만 만들고 UI 컴포넌트(자식 트리 절차 생성)는 FarmHudController.Awake 위임.
        private static void EnsureFarmCanvas(GameDataRegistry registry)
        {
            const string CanvasName = "[FarmCanvas]";
            if (FindObjectByName(CanvasName) != null) return;

            var go = new GameObject(CanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // EventSystem 보장 (Farm 씬에 이미 있을 수 있음).
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            // FarmHudController 를 reflection 으로 추가 (Game 어셈블리는 UI 어셈블리를 참조하지 않음).
            var hudType = System.Type.GetType("Rootborn.UI.HUD.FarmHudController, Rootborn.UI");
            if (hudType != null)
            {
                go.AddComponent(hudType);
                Debug.Log("[ROOTBORN/AutoFiller] FarmCanvas created with FarmHudController.");
            }
            else
            {
                Debug.LogWarning("[ROOTBORN/AutoFiller] FarmHudController type not found — UI 어셈블리 컴파일 확인 필요.");
            }
        }

        // 기존 Player 인스턴스(prefab 등)에 Rigidbody2D + BoxCollider2D + scale 누락 시 보강.
        // Setup Everything 을 다시 안 돌려도 다음 Play 에서 collider 동작.
        private static void ReinforcePlayer(GameObject player)
        {
            int added = 0;

            // Scale 1 이면 2.0 으로 (자원 16ppu sprite 와 시각 크기 맞춤).
            if (Mathf.Approximately(player.transform.localScale.x, 1f))
            {
                player.transform.localScale = new Vector3(2.0f, 2.0f, 1f);
                added++;
            }

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = player.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                rb.linearDamping = 8f;
                added++;
            }

            if (player.GetComponent<Collider2D>() == null)
            {
                var pcol = player.AddComponent<BoxCollider2D>();
                pcol.size = new Vector2(0.6f, 0.5f);
                pcol.offset = new Vector2(0f, -0.25f);
                pcol.isTrigger = false;
                added++;
            }

            // PlayerController._rb 직렬화 필드도 보강 (없으면 Awake 가 GetComponent 로 재시도하지만 명시적으로).
            var ctrl = player.GetComponent<Rootborn.Game.Player.PlayerController>();
            if (ctrl != null)
            {
                var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var rbField = typeof(Rootborn.Game.Player.PlayerController).GetField("_rb", bind);
                if (rbField != null && rbField.GetValue(ctrl) == null)
                {
                    rbField.SetValue(ctrl, rb);
                }
            }

            // PlayerInventory 누락 시 부착 + GatherInteractor wiring + 시작 도구 장착.
            if (player.GetComponent<Rootborn.Game.Player.PlayerInventory>() == null)
            {
                var registry = Rootborn.Game.Managers.Managers.Data?.Registry ?? LoadRegistryFallback();
                var inv = player.AddComponent<Rootborn.Game.Player.PlayerInventory>();
                inv.Bind(registry);
                inv.TryAddById("BareHand", 1);
                inv.TryAddById("StoneAxe", 1);
                inv.TryAddById("Wood", 5);
                inv.TryAddById("Stone", 3);
                var bareHandItem = inv.FindById("BareHand");
                if (bareHandItem != null) inv.EquipTool(bareHandItem);
                var interactor = player.GetComponent<Rootborn.Game.Player.GatherInteractor>();
                if (interactor != null) interactor.BindInventory(inv);
                added++;
            }

            if (added > 0)
            {
                Debug.Log($"[ROOTBORN/AutoFiller] Reinforced existing Player with {added} missing component(s) (Rigidbody2D / BoxCollider2D / scale).");
            }
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
