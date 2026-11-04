using System.Collections.Generic;
using UnityEngine;
namespace Rootborn.Game.Common
{
    /// <summary>Modal owners suppress world controls until every owner releases its lease.</summary>
    public static class UiGameplayInputLock
    {
        private static readonly HashSet<Object> Owners = new HashSet<Object>();
        public static bool IsBlocked
        {
            get { Owners.RemoveWhere(IsUnavailable); return Owners.Count > 0; }
        }
        public static void Acquire(Object owner) { if (owner != null) Owners.Add(owner); }
        public static void Release(Object owner) { if (!ReferenceEquals(owner,null)) Owners.Remove(owner); }
        private static bool IsUnavailable(Object owner) => owner == null || (owner is Behaviour b && !b.isActiveAndEnabled);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Owners.Clear();
    }
}
