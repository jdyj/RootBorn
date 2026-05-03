using Rootborn.Game.Crops;
using Rootborn.Game.Generation;
using Rootborn.Game.Heir;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Resources;
using Rootborn.Game.Status;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Common
{
    [CreateAssetMenu(fileName = "GameDataRegistry", menuName = "Rootborn/Common/Game Data Registry")]
    public sealed class GameDataRegistry : ScriptableObject
    {
        [SerializeField] private CropDefinition[] _crops = System.Array.Empty<CropDefinition>();
        [SerializeField] private ToolDefinition[] _tools = System.Array.Empty<ToolDefinition>();
        [SerializeField] private ResourceNodeDefinition[] _resources = System.Array.Empty<ResourceNodeDefinition>();
        [SerializeField] private KnowledgeNode[] _knowledge = System.Array.Empty<KnowledgeNode>();
        [SerializeField] private HeirTrait[] _traits = System.Array.Empty<HeirTrait>();
        [SerializeField] private StatusEffectDefinition[] _statuses = System.Array.Empty<StatusEffectDefinition>();
        [SerializeField] private GenerationProfile[] _generations = System.Array.Empty<GenerationProfile>();
        [SerializeField] private Sprite _groundSprite;
        [SerializeField] private Sprite _playerSprite;

        public CropDefinition[] Crops => _crops;
        public ToolDefinition[] Tools => _tools;
        public ResourceNodeDefinition[] Resources => _resources;
        public KnowledgeNode[] Knowledge => _knowledge;
        public HeirTrait[] Traits => _traits;
        public StatusEffectDefinition[] Statuses => _statuses;
        public GenerationProfile[] Generations => _generations;
        public Sprite GroundSprite => _groundSprite;
        public Sprite PlayerSprite => _playerSprite;
    }
}
