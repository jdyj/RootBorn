using System.Collections.Generic;
using System.Threading.Tasks;
using Rootborn.Game.Common;
using Rootborn.Game.Crops;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Resources;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Managers
{
    /// <summary>
    /// 게임 부팅 시 한 번 GameDataRegistry 를 Addressables 로 로드한 뒤
    /// 도메인별 Dictionary 로 빠르게 lookup 한다 (SlimeMaster 의 DataManager 패턴).
    /// </summary>
    public sealed class DataManager
    {
        public const string AddrRegistry = "data/registry";

        // Sheet 주소 + sub-sprite 이름 (Pixelwood multi-sprite PNG에서 sliced sub 추출)
        public const string AddrSheetTile = "sheet/Tile";
        public const string AddrSheetIdleDown = "sheet/Down";
        public const string SubGroundTile = "Tile_r2_c4";
        public const string SubPlayerIdle = "Idle_Down_1";

        public GameDataRegistry Registry { get; private set; }
        public Sprite PlayerSprite { get; private set; }
        public Sprite GroundSprite { get; private set; }

        public Dictionary<string, CropDefinition> CropById { get; } = new Dictionary<string, CropDefinition>();
        public Dictionary<string, ToolDefinition> ToolById { get; } = new Dictionary<string, ToolDefinition>();
        public Dictionary<string, ResourceNodeDefinition> ResourceById { get; } = new Dictionary<string, ResourceNodeDefinition>();
        public Dictionary<string, KnowledgeNode> KnowledgeById { get; } = new Dictionary<string, KnowledgeNode>();

        public bool IsInitialized { get; private set; }

        public async Task InitAsync(ResourceManager resource)
        {
            if (IsInitialized) return;

            // 1) Registry — Addressables 우선, 실패 시 Resources.Load fallback
            Registry = await resource.LoadAsync<GameDataRegistry>(AddrRegistry);
            if (Registry == null)
            {
                Registry = UnityEngine.Resources.Load<GameDataRegistry>("GameDataRegistry");
                if (Registry != null)
                {
                    Debug.Log("[ROOTBORN/DataManager] Registry loaded via Resources.Load fallback.");
                }
            }
            if (Registry == null)
            {
                Debug.LogError("[ROOTBORN/DataManager] GameDataRegistry not found. Run 'Rootborn → Setup Everything (One Click)'.");
                return;
            }
            else
            {
                Debug.Log($"[ROOTBORN/DataManager] Registry loaded: crops={Registry.Crops?.Length}, tools={Registry.Tools?.Length}, resources={Registry.Resources?.Length}");
            }

            BuildLookups();

            // 2) Sprite preload — Addressables 우선 (sheet 주소 + sub-sprite 이름)
            //    SlimeMaster 패턴: sheet 자체를 Addressables에 등록하고 sub-sprite는 이름으로 조회.
            GroundSprite = await resource.LoadSubSpriteAsync(AddrSheetTile, SubGroundTile);
            PlayerSprite = await resource.LoadSubSpriteAsync(AddrSheetIdleDown, SubPlayerIdle);

            // Fallback — Addressables 실패 시 Registry 직접참조 (개발 편의)
            if (GroundSprite == null) GroundSprite = Registry.GroundSprite;
            if (PlayerSprite == null) PlayerSprite = Registry.PlayerSprite;

            Debug.Log($"[ROOTBORN/DataManager] Sprites loaded — Ground={(GroundSprite != null ? GroundSprite.name : "null")}, Player={(PlayerSprite != null ? PlayerSprite.name : "null")}");

            IsInitialized = true;
        }

        private void BuildLookups()
        {
            CropById.Clear();
            ToolById.Clear();
            ResourceById.Clear();
            KnowledgeById.Clear();

            if (Registry.Crops != null)
            {
                for (int i = 0; i < Registry.Crops.Length; i++)
                {
                    var c = Registry.Crops[i];
                    if (c != null && !string.IsNullOrEmpty(c.Id)) CropById[c.Id] = c;
                }
            }
            if (Registry.Tools != null)
            {
                for (int i = 0; i < Registry.Tools.Length; i++)
                {
                    var t = Registry.Tools[i];
                    if (t != null && !string.IsNullOrEmpty(t.Id)) ToolById[t.Id] = t;
                }
            }
            if (Registry.Resources != null)
            {
                for (int i = 0; i < Registry.Resources.Length; i++)
                {
                    var r = Registry.Resources[i];
                    if (r != null && !string.IsNullOrEmpty(r.Id)) ResourceById[r.Id] = r;
                }
            }
            if (Registry.Knowledge != null)
            {
                for (int i = 0; i < Registry.Knowledge.Length; i++)
                {
                    var k = Registry.Knowledge[i];
                    if (k != null && !string.IsNullOrEmpty(k.Id)) KnowledgeById[k.Id] = k;
                }
            }
        }
    }
}
