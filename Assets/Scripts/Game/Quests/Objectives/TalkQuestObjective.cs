using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_Talk", menuName = "Rootborn/Quests/Objectives/Talk")]
    public sealed class TalkQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private ScriptableObject _targetNpc;

        public ScriptableObject TargetNpc => _targetNpc;

        public override bool Matches(in QuestEvent questEvent)
        {
            return questEvent.Kind == QuestEventKind.Talk
                && _targetNpc != null
                && questEvent.Npc == _targetNpc;
        }
    }
}
