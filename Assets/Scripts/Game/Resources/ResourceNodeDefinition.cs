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

        [Header("Walkability")]
        [Tooltip("true = 플레이어가 통과 가능 (풀/덤불). false = 충돌체로 막힘 (돌/나무).")]
        [SerializeField] private bool _isWalkable = true;
        [Tooltip("_isWalkable=false 일 때 BoxCollider2D 크기 (world unit).")]
        [SerializeField] private Vector2 _colliderSize = new Vector2(1f, 1f);

        public string Id => _id;
        public string DisplayKey => _displayKey;
        public Sprite Sprite => _sprite;
        public float BaseHitsToBreak => _baseHitsToBreak;
        public ToolDefinition PreferredTool => _preferredTool;
        public float BareHandPenaltyMul => _bareHandPenaltyMul;
        public ResourceDrop[] Drops => _drops;
        public string SurfaceTag => _surfaceTag;
        public bool IsWalkable => _isWalkable;
        public Vector2 ColliderSize => _colliderSize;

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
