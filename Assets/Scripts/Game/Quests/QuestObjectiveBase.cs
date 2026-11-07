using Rootborn.Core.Quests;
using UnityEngine;

namespace Rootborn.Game.Quests
{
    public abstract class QuestObjectiveBase : ScriptableObject
    {
        [SerializeField] private string _displayKey;
        [SerializeField] private int _requiredCount = 1;

        public string DisplayKey => _displayKey;
        public int RequiredCount => Mathf.Max(1, _requiredCount);

        public void ConfigureForRuntime(string displayKey, int requiredCount)
        {
            _displayKey = displayKey;
            _requiredCount = Mathf.Max(1, requiredCount);
        }

        /// <summary>What this objective accepts, by id. Matching itself lives in Rootborn.Core (QuestObjectiveMatcher).</summary>
        protected virtual QuestObjectiveSpec BuildSpec() => null;

        // Built per call: objectives are checked on player actions, not every frame, and fields may change at runtime.
        public virtual bool Matches(in QuestEvent questEvent) => QuestObjectiveMatcher.Matches(BuildSpec(), questEvent.ToFacts());

        public virtual int GetDelta(in QuestEvent questEvent)
        {
            return Matches(in questEvent) ? Mathf.Max(1, questEvent.Count) : 0;
        }
    }
}
