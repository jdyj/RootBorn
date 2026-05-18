using UnityEngine;

namespace Rootborn.Game.Quests
{
    public abstract class QuestConditionBase : ScriptableObject
    {
        public virtual string BlockedReasonId => GetType().Name;
        public virtual string BlockedSummary => name;

        public abstract bool IsSatisfied(in QuestRuntimeContext context);
    }
}
