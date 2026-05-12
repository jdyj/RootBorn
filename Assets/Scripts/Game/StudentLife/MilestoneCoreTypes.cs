using System;
using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public readonly struct MilestoneEvaluationContext
    {
        public readonly StudentLifeProgress StudentLifeProgress;
        public readonly Inventory Inventory;
        public readonly string[] AdditionalResultLogIds;

        public MilestoneEvaluationContext(StudentLifeProgress studentLifeProgress, Inventory inventory, string[] additionalResultLogIds)
        {
            StudentLifeProgress = studentLifeProgress;
            Inventory = inventory;
            AdditionalResultLogIds = additionalResultLogIds ?? Array.Empty<string>();
        }
    }

    public readonly struct MilestoneObjectiveEvaluation
    {
        public readonly string ObjectiveId;
        public readonly int Current;
        public readonly int Required;
        public readonly bool IsComplete;

        public MilestoneObjectiveEvaluation(string objectiveId, int current, int required)
        {
            ObjectiveId = string.IsNullOrEmpty(objectiveId) ? string.Empty : objectiveId;
            Current = Mathf.Max(0, current);
            Required = Mathf.Max(1, required);
            IsComplete = Current >= Required;
        }
    }
}
