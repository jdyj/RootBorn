using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_Defeat", menuName = "Rootborn/Quests/Objectives/Defeat")]
    public sealed class DefeatQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private ScriptableObject _target;

        public ScriptableObject Target => _target;

        public override bool Matches(in QuestEvent questEvent)
        {
            return questEvent.Kind == QuestEventKind.Defeat
                && _target != null
                && questEvent.DefeatTarget == _target;
        }
    }
}
