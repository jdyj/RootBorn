using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_AnyOf", menuName = "Rootborn/Quests/Objectives/Any Of")]
    public sealed class AnyOfQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private QuestObjectiveBase[] _alternatives = Array.Empty<QuestObjectiveBase>();
        [NonSerialized] private bool _evaluating;
        public IReadOnlyList<QuestObjectiveBase> Alternatives => _alternatives;
        public override bool Matches(in QuestEvent questEvent)
        {
            if (_evaluating || _alternatives == null) return false;
            _evaluating = true;
            try
            {
                for (int i = 0; i < _alternatives.Length; i++)
                    if (_alternatives[i] != null && _alternatives[i] != this && _alternatives[i].Matches(questEvent)) return true;
                return false;
            }
            finally { _evaluating = false; }
        }
    }
}
