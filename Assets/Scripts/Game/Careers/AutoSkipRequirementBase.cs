using System;
using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.Careers
{
    [Flags] public enum AutoSkipRequirementCoverage { None = 0, Mastery = 1, SuccessfulWork = 2, FacilityEquipment = 4, All = 7 }
    public readonly struct AutoSkipContext
    {
        public AutoSkipContext(FacilityWorkDefinition work, AdultCareerProgress progress, Inventory inventory)
        { Work = work; Progress = progress; Inventory = inventory; }
        public FacilityWorkDefinition Work { get; }
        public AdultCareerProgress Progress { get; }
        public Inventory Inventory { get; }
    }
    public abstract class AutoSkipRequirementBase : ScriptableObject
    {
        public abstract AutoSkipRequirementCoverage Coverage { get; }
        public abstract bool IsSatisfied(in AutoSkipContext context);
    }
}
