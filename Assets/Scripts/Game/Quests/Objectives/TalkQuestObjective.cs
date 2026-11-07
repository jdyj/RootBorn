using Rootborn.Core.Quests;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_Talk", menuName = "Rootborn/Quests/Objectives/Talk")]
    public sealed class TalkQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private ScriptableObject _targetNpc;

        public ScriptableObject TargetNpc => _targetNpc;

        protected override QuestObjectiveSpec BuildSpec() => QuestObjectiveSpec.Listed((int)QuestEventKind.Talk, _targetNpc != null ? QuestSubjectIds.Of(_targetNpc) : null);
    }
}
