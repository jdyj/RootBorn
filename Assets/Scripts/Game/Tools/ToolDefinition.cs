using UnityEngine;

namespace Rootborn.Game.Tools
{
    [CreateAssetMenu(fileName = "Tool_New", menuName = "Rootborn/Tools/Tool Definition")]
    public sealed class ToolDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayKey;
        [SerializeField] private Sprite _icon;
        [SerializeField] private float _powerMultiplier = 1f;
        [SerializeField] private ToolEffectBase[] _effects = System.Array.Empty<ToolEffectBase>();
        [SerializeField] private bool _isStartingTool;
        [SerializeField, TextArea(2, 4)] private string _description;

        public string Id => _id;
        public string DisplayKey => _displayKey;
        public Sprite Icon => _icon;
        public float PowerMultiplier => _powerMultiplier;
        public ToolEffectBase[] Effects => _effects;
        public bool IsStartingTool => _isStartingTool;
        public string Description => _description;

        public void ApplyEffects(in ToolUseContext ctx)
        {
            for (int i = 0; i < _effects.Length; i++)
            {
                if (_effects[i] != null)
                    _effects[i].Apply(in ctx);
            }
        }
    }
}
