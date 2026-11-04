using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Family;
using Rootborn.Game.Managers;
using Rootborn.Game.Resources;
using Rootborn.Game.Save;
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

            // Managers 媛 ?꾩쭅 遺??????寃쎌슦 (??吏곸젒 Play 濡??ㅼ뼱??寃쎌슦) 遺?몄뒪?몃옪 蹂댁옣
            if (Managers.Managers.Instance == null || Managers.Managers.Instance.IsBootstrapped == false)
            {
                await Managers.Managers.BootstrapAsync();
            }
            FillIfEmpty();
        }

        public void FillIfEmpty()
        {
            var data = Managers.Managers.Data;
            var registry = GameDataRegistryProvider.TryGet(out var activeRegistry) ? activeRegistry : LoadRegistryFallback();
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
            var allRegistries = UnityEngine.Resources.FindObjectsOfTypeAll<GameDataRegistry>();
            return (allRegistries != null && allRegistries.Length > 0) ? allRegistries[0] : null;
        }

        private static void EnsureCamera()
        {
            // 湲곗〈 Main Camera媛 ?덉쑝硫??ъ궗?⑺븯??Orthographic + ?꾩튂/諛곌꼍 媛뺤젣 ?뗭뾽.
            // SampleScene?먯꽌 蹂듭궗??Farm ?ъ? 3D ?먭렐 移대찓??+ Skybox ?대━?대? 媛吏怨??덉쓣 ???덉쓬.
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

            // Skybox ?꾧린 + Audio Listener ??媛쒕줈 ?뺣━
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

            // [Crops] parent ??CropPlot ?몄뒪?댁뒪媛 spawn ???꾩튂.
            var cropsParent = FindObjectByName("[Crops]");
            if (cropsParent == null)
            {
                cropsParent = new GameObject("[Crops]");
            }

            // Ground Tilemap ???꾩옱 泥?? tile ??"tilledTile" 濡??꾩떆 ?ъ슜 ??fallback (TODO(asset): ?꾩슜 tilled-tile sprite ?꾩슂).
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

            if (existing != null && existing.transform.childCount > 0)
            {
                if (ActiveSaveContext.Metadata == null)
                {
                    ReinforceExistingResources(existing.transform, registry);
                    return;
                }

                for (int i = existing.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(existing.transform.GetChild(i).gameObject);
                }
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

            int seed = ActiveSaveContext.Metadata != null ? ActiveSaveContext.Metadata.WorldSeed : 20260504;
            var rng = new System.Random(seed);
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

        // ?대? ?ъ뿉 ?덈뒗 湲곗〈 ?먯썝 GameObject ??collider 媛 ?꾨씫?먯쑝硫?遺??
        // Setup Everything ???ㅼ떆 ???뚮젮???ㅼ쓬 Play ?먯꽌 ?먮룞 蹂닿컯.
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
                // 吏곷젹???꾨씫: ?대쫫 prefix(Tree_/Rock_) 濡?registry ?먯꽌 ?뺤쓽 蹂듭썝.
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

            // ?곗씠??湲곕컲 異⑸룎: SO ??_isWalkable=false ?먯썝?먮쭔 collider.
            // ?꾩뿭 ?좉? ResourceCollisionToggle.Enabled=false ?대㈃ 紐⑤몢 ?듦낵.
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

            // Sprite ?곗꽑?쒖쐞: DataManager ??Registry ???고????숈쟻 寃????fallback red square
            Sprite sprite = data != null ? data.PlayerSprite : null;
            if (sprite == null) sprite = registry.PlayerSprite;
            string spriteSource = sprite != null ? sprite.name : null;

            if (sprite == null)
            {
                // Registry ??댁뼱留??ㅽ뙣 ????硫붾え由ъ뿉 濡쒕뱶??紐⑤뱺 Sprite 以묒뿉??'Idle_Down_'?쇰줈 ?쒖옉?섎뒗
                // sub-sprite瑜?寃??(Pixelwood Slice 寃곌낵媛 硫붾え由ъ뿉 ?덈떎硫??≫옒)
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
                                 "Run 'Rootborn ??Setup Everything' to wire PlayerSprite.");
            }

            var playerInstance = new GameObject("Player");
            var sr = playerInstance.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            // sortingOrder 留ㅼ슦 ?ш쾶 ???먯썝 ?몃뱶(1)蹂대떎 臾댁“嫄???            sr.sortingOrder = 1000;
            sr.sortingLayerID = 0;
            ConfigureCharacterParts(playerInstance, registry);
            playerInstance.transform.localScale = new Vector3(1f, 1f, 1f);
            // ?먯썝 ?ㅽ룿 踰붿쐞(2~28, 2~18)? ??寃뱀튂??媛?μ옄由ъ뿉 ?ㅽ룿 (?쇱そ ?꾨옒 肄붾꼫)
            playerInstance.transform.position = new Vector3(1f, 1f, 0f);

            Debug.Log($"[ROOTBORN/AutoFiller] Player sprite='{spriteSource}', size={sprite.rect.size}, ppu={sprite.pixelsPerUnit}, position=(1,1), scale=1x.");

            // Rigidbody2D (Dynamic) + BoxCollider2D ???먯썝 collider ? 遺?れ퀜 ?먮룞 李⑤떒.
            // Dynamic + MovePosition: ?뺤쟻 collider ???異⑸룎 ?댁냼瑜?臾쇰━ ?붿쭊??泥섎━.
            // (Kinematic ? MovePosition ???뺤쟻 collider 瑜?諛怨??ㅼ뼱媛????덉뼱 李⑤떒???쏀븿.)
            var rb = playerInstance.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.linearDamping = 8f;

            var pcol = playerInstance.AddComponent<BoxCollider2D>();
            pcol.size = new Vector2(0.6f, 0.5f);
            pcol.offset = new Vector2(0f, -0.25f);
            pcol.isTrigger = false;

            playerInstance.AddComponent<Rootborn.Game.Player.PlayerController>();

            var interactor = playerInstance.AddComponent<Rootborn.Game.Player.GatherInteractor>();

            // PlayerInventory 遺李?+ ?쒖옉 ?꾧뎄 ?μ갑 (BareHand).
            var playerInv = playerInstance.AddComponent<Rootborn.Game.Player.PlayerInventory>();
            playerInv.Bind(registry);
            interactor.BindInventory(playerInv);
            // ?쒖옉 ???몃깽?좊━ ?곕え ?곗씠??(UI 寃利앹슜 ???꾩냽 蹂멸꺽 寃뚯엫?뚮젅?댁뿉?쒕뒗 ?쒓굅).
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
                    // ?좉퇋 ?닿툑???꾧뎄瑜??몃깽?좊━???먮룞 異붽? (?곗씠?곕뱶由щ툙 ??ID 遺꾧린 ?놁쓬).
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

            // CameraFollow ??Main Camera媛 Player瑜?遺?쒕읇寃??곕씪媛?꾨줉
            if (Camera.main != null)
            {
                var follow = Camera.main.GetComponent<Rootborn.Game.Player.CameraFollow>();
                if (follow == null) follow = Camera.main.gameObject.AddComponent<Rootborn.Game.Player.CameraFollow>();
                follow.SetTarget(playerInstance.transform);
            }

            Debug.Log("[ROOTBORN/AutoFiller] Player spawned with GatherInteractor + KnowledgeProgress + CameraFollow.");
        }

        private static async void ConfigureCharacterParts(GameObject playerInstance, GameDataRegistry registry)
        {
            if (TownSpumVisualRuntimeInstaller.TryBindExistingVisual(playerInstance)) return;

            if (playerInstance == null || registry == null || registry.CharacterParts == null || registry.CharacterParts.Length == 0)
            {
                return;
            }

            var composer = playerInstance.GetComponent<CharacterPartComposer>();
            if (composer == null)
            {
                composer = playerInstance.AddComponent<CharacterPartComposer>();
            }

            composer.EnsureLayers(registry.CharacterParts);
            var animator = playerInstance.GetComponent<CharacterPartAnimator>();
            if (animator == null)
            {
                animator = playerInstance.AddComponent<CharacterPartAnimator>();
            }

            var rootRenderer = playerInstance.GetComponent<SpriteRenderer>();
            if (rootRenderer != null)
            {
                rootRenderer.sprite = null;
                rootRenderer.enabled = false;
            }
            var saved = ActiveSaveContext.Metadata != null ? ActiveSaveContext.Metadata.Appearance : null;
            var appearance = CharacterAppearance.ResolveWithDefaults(saved, registry.CharacterParts);
            var motion = registry.DefaultCharacterPartMotion;
            int idleRow = motion != null ? motion.IdleRow : 0;
            int idleColumn = motion != null ? motion.GetIdleColumn(Vector2.down) : 0;
            var resource = Managers.Managers.Resource;
            if (resource == null)
            {
                animator.Configure(composer, appearance, registry.CharacterParts, (part, subSpriteName) => ResolveLoadedCharacterPartSprite(subSpriteName));
            animator.ConfigureMotion(motion);
                animator.SetMotion(Vector2.zero, new Vector2(0f, -1f));
                animator.Tick(0f);
                return;
            }

            for (int i = 0; i < registry.CharacterParts.Length; i++)
            {
                var part = registry.CharacterParts[i];
                if (part != null)
                {
                    await resource.LoadSubSpriteAsync(part.SheetAddress, CharacterPartComposer.BuildFrameSubSpriteName(part, idleRow, idleColumn));
                    if (playerInstance == null || composer == null || animator == null)
                    {
                        return;
                    }
                }
            }

            if (playerInstance == null || composer == null || animator == null)
            {
                return;
            }

            animator.Configure(composer, appearance, registry.CharacterParts, (part, subSpriteName) => resource.GetCachedSubSprite(part.SheetAddress, subSpriteName));
            animator.ConfigureMotion(motion);
            animator.SetMotion(Vector2.zero, new Vector2(0f, -1f));
            animator.Tick(0f);
        }

        private static Sprite ResolveLoadedCharacterPartSprite(string subSpriteName)
        {
            if (string.IsNullOrEmpty(subSpriteName))
            {
                return null;
            }

            var allSprites = UnityEngine.Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < allSprites.Length; i++)
            {
                var sprite = allSprites[i];
                if (sprite != null && sprite.name == subSpriteName)
                {
                    return sprite;
                }
            }

            return null;
        }

        // Farm UI Canvas + FarmHudController ?덉감 ?앹꽦. ?대? ?덉쑝硫??ㅽ궢.
        // Canvas 留?留뚮뱾怨?UI 而댄룷?뚰듃(?먯떇 ?몃━ ?덉감 ?앹꽦)??FarmHudController.Awake ?꾩엫.
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

            // EventSystem 蹂댁옣 (Farm ?ъ뿉 ?대? ?덉쓣 ???덉쓬).
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem));
                Rootborn.Game.Common.UiInputModuleInstaller.AddPreferredInputModule(es);
            }

            // FarmHudController 瑜?reflection ?쇰줈 異붽? (Game ?댁뀍釉붾━??UI ?댁뀍釉붾━瑜?李몄“?섏? ?딆쓬).
            var hudType = System.Type.GetType("Rootborn.UI.HUD.FarmHudController, Rootborn.UI");
            if (hudType != null)
            {
                go.AddComponent(hudType);
                Debug.Log("[ROOTBORN/AutoFiller] FarmCanvas created with FarmHudController.");
            }
            else
            {
                Debug.LogWarning("[ROOTBORN/AutoFiller] FarmHudController type not found ??UI ?댁뀍釉붾━ 而댄뙆???뺤씤 ?꾩슂.");
            }
        }

        // 湲곗〈 Player ?몄뒪?댁뒪(prefab ????Rigidbody2D + BoxCollider2D ?꾨씫 ??蹂닿컯.
        // Setup Everything ???ㅼ떆 ???뚮젮???ㅼ쓬 Play ?먯꽌 collider ?숈옉.
        private static void ReinforcePlayer(GameObject player)
        {
            int added = 0;

            if (!Mathf.Approximately(player.transform.localScale.x, 1f) ||
                !Mathf.Approximately(player.transform.localScale.y, 1f) ||
                !Mathf.Approximately(player.transform.localScale.z, 1f))
            {
                player.transform.localScale = new Vector3(1f, 1f, 1f);
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

            // PlayerController._rb 吏곷젹???꾨뱶??蹂닿컯 (?놁쑝硫?Awake 媛 GetComponent 濡??ъ떆?꾪븯吏留?紐낆떆?곸쑝濡?.
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

            var registry = GameDataRegistryProvider.TryGet(out var playerRegistry) ? playerRegistry : LoadRegistryFallback();
            ConfigureCharacterParts(player, registry);
            var inv = player.GetComponent<Rootborn.Game.Player.PlayerInventory>();
            if (inv == null)
            {
                inv = player.AddComponent<Rootborn.Game.Player.PlayerInventory>();
                added++;
            }
            if (registry != null)
            {
                inv.Bind(registry);
                EnsureInventoryContains(inv, "BareHand", 1);
                EnsureInventoryContains(inv, "StoneAxe", 1);
                EnsureInventoryContains(inv, "Wood", 5);
                EnsureInventoryContains(inv, "Stone", 3);
                var bareHandItem = inv.FindById("BareHand");
                if (inv.EquippedToolItem == null && bareHandItem != null) inv.EquipTool(bareHandItem);
            }

            var interactor = player.GetComponent<Rootborn.Game.Player.GatherInteractor>();
            if (interactor == null)
            {
                interactor = player.AddComponent<Rootborn.Game.Player.GatherInteractor>();
                added++;
            }
            interactor.BindInventory(inv);
            if (interactor.EquippedTool == null && inv.EquippedToolItem != null)
            {
                var data = Rootborn.Game.Managers.Managers.Data;
                if (data != null && data.ToolById.TryGetValue(inv.EquippedToolItem.Id, out var equippedTool))
                {
                    interactor.EquippedTool = equippedTool;
                }
            }
            if (interactor.KnowledgeProgress == null && registry != null)
            {
                interactor.Bind(new Rootborn.Game.Knowledge.KnowledgeProgress(registry.Knowledge));
            }

            if (added > 0)
            {
                Debug.Log($"[ROOTBORN/AutoFiller] Reinforced existing Player with {added} missing component(s) (Rigidbody2D / BoxCollider2D / scale).");
            }
        }

        private static void EnsureInventoryContains(Rootborn.Game.Player.PlayerInventory inventory, string itemId, int count)
        {
            var item = inventory.FindById(itemId);
            if (item == null || inventory.Inventory.CountOf(item) > 0) return;
            inventory.Inventory.Add(item, count);
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
