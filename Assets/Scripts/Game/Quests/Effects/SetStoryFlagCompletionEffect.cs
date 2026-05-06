using Rootborn.Game.Story;
using UnityEngine;

namespace Rootborn.Game.Quests.Effects
{
    [CreateAssetMenu(fileName = "Effect_SetStoryFlag", menuName = "Rootborn/Quests/Effects/Set Story Flag")]
    public sealed class SetStoryFlagCompletionEffect : QuestCompletionEffectBase
    {
        [SerializeField] private StoryFlagDefinition _flag;

        public override bool CanApply(in RewardRuntimeContext context)
        {
            return context.StoryFlags != null && _flag != null;
        }

        public override void Apply(in RewardRuntimeContext context)
        {
            if (!CanApply(in context)) return;

            context.StoryFlags.Set(_flag);
        }
    }
}
