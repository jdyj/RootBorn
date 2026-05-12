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

        public virtual int GetDelta(in QuestEvent questEvent)
        {
            return Matches(in questEvent) ? Mathf.Max(1, questEvent.Count) : 0;
        }
    }
}
