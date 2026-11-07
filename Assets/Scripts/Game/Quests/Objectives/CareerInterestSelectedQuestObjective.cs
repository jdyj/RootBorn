using Rootborn.Core.Quests;
using System;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_CareerInterestSelected", menuName = "Rootborn/Quests/Objectives/Career Interest Selected")]
    public sealed class CareerInterestSelectedQuestObjective : QuestObjectiveBase
    {
        [Tooltip("Empty accepts any dream, so the player's choice is never limited by this objective.")]
        [SerializeField] private CareerInterestDefinition[] _acceptedInterests = Array.Empty<CareerInterestDefinition>();

        protected override QuestObjectiveSpec BuildSpec() => _acceptedInterests == null || _acceptedInterests.Length == 0
            ? QuestObjectiveSpec.AnySubject((int)QuestEventKind.CareerInterestSelected)
            : QuestObjectiveSpec.Listed((int)QuestEventKind.CareerInterestSelected, QuestSubjectIds.Of(_acceptedInterests));
    }
}
