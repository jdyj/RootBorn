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

        [System.NonSerialized] private QuestObjectiveSpec _spec;

        public void ConfigureForRuntime(string displayKey, int requiredCount)
        {
            _displayKey = displayKey;
            _requiredCount = Mathf.Max(1, requiredCount);
            InvalidateSpec();
        }

        /// <summary>What this objective accepts, by id. Matching itself lives in Rootborn.Core (QuestObjectiveMatcher).</summary>
        protected virtual QuestObjectiveSpec BuildSpec() => null;

        protected void InvalidateSpec() => _spec = null;

        public virtual bool Matches(in QuestEvent questEvent)
        {
            if (_spec == null) _spec = BuildSpec();
            return QuestObjectiveMatcher.Matches(_spec, questEvent.ToFacts());
        }

#if UNITY_EDITOR
        protected virtual void OnValidate() => InvalidateSpec();
#endif

        public virtual int GetDelta(in QuestEvent questEvent)
        {
            return Matches(in questEvent) ? Mathf.Max(1, questEvent.Count) : 0;
        }
    }
}
