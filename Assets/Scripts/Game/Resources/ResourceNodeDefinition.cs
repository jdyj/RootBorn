using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Resources
{
    [CreateAssetMenu(fileName = "Resource_New", menuName = "Rootborn/Resources/Resource Node Definition")]
    public sealed class ResourceNodeDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayKey;
        [SerializeField] private Sprite _sprite;
        [SerializeField] private float _baseHitsToBreak = 5f;
        [SerializeField] private ToolDefinition _preferredTool;
        [SerializeField] private float _bareHandPenaltyMul = 0.3f;
        [SerializeField] private ResourceDrop[] _drops = System.Array.Empty<ResourceDrop>();
        [SerializeField] private string _surfaceTag = "ground";

        public string Id => _id;
        public string DisplayKey => _displayKey;
        public Sprite Sprite => _sprite;
        public float BaseHitsToBreak => _baseHitsToBreak;
        public ToolDefinition PreferredTool => _preferredTool;
        public float BareHandPenaltyMul => _bareHandPenaltyMul;
        public ResourceDrop[] Drops => _drops;
        public string SurfaceTag => _surfaceTag;

        public float ComputeEffectivePower(ToolDefinition usedTool)
        {
            if (usedTool == null) return _bareHandPenaltyMul;
            float power = usedTool.PowerMultiplier;
            if (_preferredTool != null && usedTool != _preferredTool)
            {
                power *= 0.5f;
            }
            return power;
        }
    }
}
