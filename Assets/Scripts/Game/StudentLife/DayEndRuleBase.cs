using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public abstract class DayEndRuleBase : ScriptableObject
    {
        public abstract void Apply(StudentLifeProgress progress);
    }
}
