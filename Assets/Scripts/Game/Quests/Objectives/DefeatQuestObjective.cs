using Rootborn.Core.Quests;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_Defeat", menuName = "Rootborn/Quests/Objectives/Defeat")]
    public sealed class DefeatQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private ScriptableObject _target;

        public ScriptableObject Target => _target;

        protected override QuestObjectiveSpec BuildSpec() => QuestObjectiveSpec.Listed((int)QuestEventKind.Defeat, _target != null ? QuestSubjectIds.Of(_target) : null);
    }
}
