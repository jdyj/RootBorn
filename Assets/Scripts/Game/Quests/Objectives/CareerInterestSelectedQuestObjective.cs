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

        public override bool Matches(in QuestEvent questEvent)
        {
            if (questEvent.Kind != QuestEventKind.CareerInterestSelected) return false;
            if (_acceptedInterests == null || _acceptedInterests.Length == 0) return questEvent.CareerInterest != null;
            for (int i = 0; i < _acceptedInterests.Length; i++)
                if (_acceptedInterests[i] != null && _acceptedInterests[i] == questEvent.CareerInterest) return true;
            return false;
        }
    }
}
