using Rootborn.Game.Knowledge;
using UnityEngine;

namespace Rootborn.Game.Quests.Effects
{
    [CreateAssetMenu(fileName = "Effect_UnlockKnowledge", menuName = "Rootborn/Quests/Effects/Unlock Knowledge")]
    public sealed class UnlockKnowledgeCompletionEffect : QuestCompletionEffectBase
    {
        [SerializeField] private KnowledgeNode _knowledge;

        public override bool CanApply(in RewardRuntimeContext context)
        {
            return context.KnowledgeProgress != null && _knowledge != null;
        }

        public override void Apply(in RewardRuntimeContext context)
        {
            if (!CanApply(in context)) return;

            context.KnowledgeProgress.Seed(_knowledge);
        }
    }
}
