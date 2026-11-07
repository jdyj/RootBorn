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

        public abstract bool Matches(in QuestEvent questEvent);

        /// <summary>
        /// True when two definitions are the same entry: the same object, or copies with the same non-empty id.
        /// Built Addressables load quest targets from bundles while scene objects keep their own copies.
        /// </summary>
        protected static bool SameEntry(Object eventValue, Object target, string eventId, string targetId)
        {
            if (eventValue == null || target == null) return false;
            return eventValue == target || (!string.IsNullOrEmpty(targetId) && string.Equals(eventId, targetId, System.StringComparison.Ordinal));
        }

        public virtual int GetDelta(in QuestEvent questEvent)
        {
            return Matches(in questEvent) ? Mathf.Max(1, questEvent.Count) : 0;
        }
    }
}
