using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_PartTimeWork", menuName = "Rootborn/Quests/Objectives/Part-Time Work")]
    public sealed class PartTimeWorkQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private PartTimeWorkDefinition[] _acceptedWorks = Array.Empty<PartTimeWorkDefinition>();
        public IReadOnlyList<PartTimeWorkDefinition> AcceptedWorks => _acceptedWorks;
        public override bool Matches(in QuestEvent questEvent)
        {
            if (questEvent.Kind != QuestEventKind.PartTimeWork || questEvent.Work == null || _acceptedWorks == null) return false;
            for (int i = 0; i < _acceptedWorks.Length; i++) if (_acceptedWorks[i] == questEvent.Work) return true;
            return false;
        }
    }
}
