using UnityEngine;

namespace Rootborn.Game.Crafting
{
    public abstract class CraftStepBase : ScriptableObject
    {
        [SerializeField] private string _displayKey;
        public string DisplayKey => _displayKey;

        public abstract bool IsSatisfied(in CraftAttemptState state);
    }

    public readonly struct CraftAttemptState
    {
        public readonly System.Collections.Generic.IReadOnlyDictionary<string, int> Inventory;
        public readonly string CurrentSurface;
        public readonly float ElapsedSec;

        public CraftAttemptState(
            System.Collections.Generic.IReadOnlyDictionary<string, int> inventory,
            string currentSurface,
            float elapsedSec)
        {
            Inventory = inventory;
            CurrentSurface = currentSurface;
            ElapsedSec = elapsedSec;
        }
    }
}
