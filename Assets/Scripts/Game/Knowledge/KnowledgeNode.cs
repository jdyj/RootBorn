using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Knowledge
{
    [CreateAssetMenu(fileName = "Knowledge_New", menuName = "Rootborn/Knowledge/Knowledge Node")]
    public sealed class KnowledgeNode : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayKey;
        [SerializeField] private KnowledgeTriggerBase[] _triggers = System.Array.Empty<KnowledgeTriggerBase>();
        [SerializeField] private ToolDefinition[] _unlocksTools = System.Array.Empty<ToolDefinition>();

        public string Id => _id;
        public string DisplayKey => _displayKey;
        public KnowledgeTriggerBase[] Triggers => _triggers;
        public ToolDefinition[] UnlocksTools => _unlocksTools;

        public bool IsSatisfied(in KnowledgeContext ctx)
        {
            if (_triggers == null || _triggers.Length == 0) return false;
            for (int i = 0; i < _triggers.Length; i++)
            {
                var t = _triggers[i];
                if (t == null) return false;
                if (!t.Evaluate(in ctx)) return false;
            }
            return true;
        }
    }
}
