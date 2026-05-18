using System.Collections.Generic;
using System.Threading.Tasks;
using Rootborn.Game.Common;
using Rootborn.Game.Crops;
using Rootborn.Game.Family;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Resources;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Managers
{
    /// <summary>
    /// 게임 부팅 시 한 번 GameDataRegistry를 로드한 뒤 도메인별 Dictionary로 lookup한다.
    /// </summary>
    public sealed class DataManager
    {
        public const string AddrRegistry = "data/registry";
        private const string EditorRegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        public GameDataRegistry Registry { get; private set; }
        public Sprite PlayerSprite { get; private set; }
        public Sprite GroundSprite { get; private set; }

        public Dictionary<string, CropDefinition> CropById { get; } = new Dictionary<string, CropDefinition>();
        public Dictionary<string, ToolDefinition> ToolById { get; } = new Dictionary<string, ToolDefinition>();
        public Dictionary<string, ResourceNodeDefinition> ResourceById { get; } = new Dictionary<string, ResourceNodeDefinition>();
        public Dictionary<string, KnowledgeNode> KnowledgeById { get; } = new Dictionary<string, KnowledgeNode>();
        public Dictionary<string, CharacterPartDefinition> CharacterPartById { get; } = new Dictionary<string, CharacterPartDefinition>();
        public Dictionary<string, List<CharacterPartDefinition>> CharacterPartsByCategory { get; } = new Dictionary<string, List<CharacterPartDefinition>>();
        public Dictionary<string, CharacterPartAnimationClipDefinition> CharacterPartAnimationClipById { get; } = new Dictionary<string, CharacterPartAnimationClipDefinition>();
        public Dictionary<string, FishingAnimationDefinition> FishingAnimationById { get; } = new Dictionary<string, FishingAnimationDefinition>();

        public bool IsInitialized { get; private set; }

        public async Task InitAsync(ResourceManager resource)
        {
            if (IsInitialized) return;

            Registry = LoadEditorRegistry();
            if (Registry == null)
            {
                Registry = await resource.LoadAsync<GameDataRegistry>(AddrRegistry);
            }
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
                Debug.LogError("[ROOTBORN/DataManager] GameDataRegistry not found. Run 'Rootborn -> Setup Everything (One Click)'.");
                return;
            }
            else
            {
                Debug.Log($"[ROOTBORN/DataManager] Registry loaded: crops={Registry.Crops?.Length}, tools={Registry.Tools?.Length}, resources={Registry.Resources?.Length}, characterParts={Registry.CharacterParts?.Length}");
            }

            BuildLookups();

            GroundSprite = Registry.GroundSprite;
            PlayerSprite = Registry.PlayerSprite;

            Debug.Log($"[ROOTBORN/DataManager] Sprites loaded - Ground={(GroundSprite != null ? GroundSprite.name : "null")}, Player={(PlayerSprite != null ? PlayerSprite.name : "null")}");

            IsInitialized = true;
        }

        private static GameDataRegistry LoadEditorRegistry()
        {
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataRegistry>(EditorRegistryPath);
            }
#endif
            return null;
        }

        private void BuildLookups()
        {
            CropById.Clear();
            ToolById.Clear();
            ResourceById.Clear();
            KnowledgeById.Clear();
            CharacterPartById.Clear();
            CharacterPartsByCategory.Clear();
            CharacterPartAnimationClipById.Clear();
            FishingAnimationById.Clear();

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
            if (Registry.CharacterParts != null)
            {
                for (int i = 0; i < Registry.CharacterParts.Length; i++)
                {
                    var part = Registry.CharacterParts[i];
                    if (part == null || string.IsNullOrEmpty(part.Id) || string.IsNullOrEmpty(part.CategoryId))
                    {
                        continue;
                    }

                    CharacterPartById[part.Id] = part;
                    if (!CharacterPartsByCategory.TryGetValue(part.CategoryId, out var categoryParts))
                    {
                        categoryParts = new List<CharacterPartDefinition>();
                        CharacterPartsByCategory[part.CategoryId] = categoryParts;
                    }
                    categoryParts.Add(part);
                }
            }
            if (Registry.CharacterPartAnimationClips != null)
            {
                for (int i = 0; i < Registry.CharacterPartAnimationClips.Length; i++)
                {
                    var clip = Registry.CharacterPartAnimationClips[i];
                    if (clip != null && !string.IsNullOrEmpty(clip.Id))
                    {
                        CharacterPartAnimationClipById[clip.Id] = clip;
                    }
                }
            }
            if (Registry.FishingAnimations != null)
            {
                for (int i = 0; i < Registry.FishingAnimations.Length; i++)
                {
                    var animation = Registry.FishingAnimations[i];
                    if (animation != null && !string.IsNullOrEmpty(animation.Id))
                    {
                        FishingAnimationById[animation.Id] = animation;
                    }
                }
            }
        }
    }
}
