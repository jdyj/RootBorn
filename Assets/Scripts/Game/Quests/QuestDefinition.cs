using UnityEngine;

namespace Rootborn.Game.Quests
{
    [CreateAssetMenu(fileName = "Quest_New", menuName = "Rootborn/Quests/Quest Definition")]
    public sealed class QuestDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private string _descriptionKey;
        [SerializeField] private QuestConditionBase[] _providerConditions = System.Array.Empty<QuestConditionBase>();
        [SerializeField] private QuestConditionBase[] _prerequisites = System.Array.Empty<QuestConditionBase>();
        [SerializeField] private QuestObjectiveBase[] _objectives = System.Array.Empty<QuestObjectiveBase>();
        [SerializeField] private QuestRewardBase[] _rewards = System.Array.Empty<QuestRewardBase>();
        [SerializeField] private QuestCompletionEffectBase[] _completionEffects = System.Array.Empty<QuestCompletionEffectBase>();

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public string DescriptionKey => _descriptionKey;
        public QuestConditionBase[] ProviderConditions => _providerConditions;
        public QuestConditionBase[] Prerequisites => _prerequisites;
        public QuestObjectiveBase[] Objectives => _objectives;
        public QuestRewardBase[] Rewards => _rewards;
        public QuestCompletionEffectBase[] CompletionEffects => _completionEffects;
    }
}
