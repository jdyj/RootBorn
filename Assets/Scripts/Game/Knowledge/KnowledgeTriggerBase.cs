using UnityEngine;

namespace Rootborn.Game.Knowledge
{
    public abstract class KnowledgeTriggerBase : ScriptableObject
    {
        public abstract bool Evaluate(in KnowledgeContext ctx);

        [SerializeField] private int _requiredRepeats = 1;
        public int RequiredRepeats => Mathf.Max(1, _requiredRepeats);
    }
}
