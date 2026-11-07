using Rootborn.Core.Quests;
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
        protected override QuestObjectiveSpec BuildSpec() => QuestObjectiveSpec.Listed((int)QuestEventKind.PartTimeWork, QuestSubjectIds.Of(_acceptedWorks));
    }
}
