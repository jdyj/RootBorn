using System;
using System.Collections.Generic;
using Rootborn.Game.Tools;

namespace Rootborn.Game.Knowledge
{
    public sealed class KnowledgeProgress
    {
        private readonly HashSet<KnowledgeNode> _unlocked = new HashSet<KnowledgeNode>();
        private readonly Dictionary<string, int> _actionCounters = new Dictionary<string, int>();
        private readonly List<KnowledgeNode> _candidates;

        public event Action<KnowledgeNode> OnUnlocked;

        public KnowledgeProgress(IEnumerable<KnowledgeNode> candidates)
        {
            _candidates = new List<KnowledgeNode>(candidates);
        }

        public bool IsUnlocked(KnowledgeNode node) => node != null && _unlocked.Contains(node);

        public void Seed(KnowledgeNode node)
        {
            if (node != null) _unlocked.Add(node);
        }

        public void RecordAction(KnowledgeAction action, ToolDefinition tool, string targetResourceId, string surface)
        {
            string key = MakeKey(action, tool, targetResourceId, surface);
            _actionCounters.TryGetValue(key, out int prev);
            int repeat = prev + 1;
            _actionCounters[key] = repeat;

            var ctx = new KnowledgeContext(action, tool, targetResourceId, surface, repeat);
            EvaluateCandidates(in ctx);
        }

        private void EvaluateCandidates(in KnowledgeContext ctx)
        {
            for (int i = 0; i < _candidates.Count; i++)
            {
                var node = _candidates[i];
                if (node == null || _unlocked.Contains(node)) continue;
                if (node.IsSatisfied(in ctx))
                {
                    _unlocked.Add(node);
                    OnUnlocked?.Invoke(node);
                }
            }
        }

        private static string MakeKey(KnowledgeAction action, ToolDefinition tool, string targetResourceId, string surface)
        {
            string toolId = tool == null ? "_" : tool.Id;
            return $"{action}|{toolId}|{targetResourceId ?? "_"}|{surface ?? "_"}";
        }
    }
}
