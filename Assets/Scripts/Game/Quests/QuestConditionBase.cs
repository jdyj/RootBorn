using UnityEngine;

namespace Rootborn.Game.Quests
{
    public abstract class QuestConditionBase : ScriptableObject
    {
        public abstract bool IsSatisfied(in QuestRuntimeContext context);
    }
}
