using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public abstract class LocationStateEffectBase : ScriptableObject
    {
        public abstract void AppendTo(LocationStateSummaryBuilder builder);
    }
}
