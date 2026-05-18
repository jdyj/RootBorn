using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public abstract class LocationStateConditionBase : ScriptableObject
    {
        public abstract bool IsSatisfied(in LocationStateContext context);
    }
}
