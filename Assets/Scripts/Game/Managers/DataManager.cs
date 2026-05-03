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
        public const string AddrPlayerIdle = "sprites/player/idle_down";
        public const string AddrGroundSprite = "sprites/ground";

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

            // 2) Sprite preload — 핵심 sprite (ground / player idle down)
            //    Registry 자체에 직렬화된 GroundSprite/PlayerSprite 가 있으면 그것 우선.
            GroundSprite = Registry.GroundSprite;
            PlayerSprite = Registry.PlayerSprite;

            if (GroundSprite == null)
            {
                GroundSprite = await resource.LoadAsync<Sprite>(AddrGroundSprite);
            }
            if (PlayerSprite == null)
            {
                PlayerSprite = await resource.LoadAsync<Sprite>(AddrPlayerIdle);
            }

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
