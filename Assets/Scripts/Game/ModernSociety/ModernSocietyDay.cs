using System.Collections.Generic;
using Rootborn.Game.Story;

namespace Rootborn.Game.ModernSociety
{
    public sealed class ModernSocietyDay
    {
        private readonly StoryFlagSet _flags;
        private readonly ModernRoutineFlagRule[] _rules;
        private readonly List<ModernActivityDefinition> _performed = new List<ModernActivityDefinition>();

        public ModernSocietyDay(ModernSocietyStats stats, StoryFlagSet flags, ModernRoutineFlagRule[] rules)
        {
            Stats = stats;
            _flags = flags;
            _rules = rules ?? System.Array.Empty<ModernRoutineFlagRule>();
        }

        public ModernSocietyStats Stats { get; private set; }

        public bool TryPerform(ModernActivityDefinition activity)
        {
            if (activity == null || Stats.Energy < activity.EnergyCost)
            {
                return false;
            }

            var next = Stats;
            next.Energy -= activity.EnergyCost;
            next.Money += activity.MoneyDelta;
            next.Anxiety += activity.AnxietyDelta;
            next.Knowledge += activity.KnowledgeDelta;
            next.Relationships += activity.RelationshipsDelta;
            Stats = next;
            _performed.Add(activity);
            return true;
        }

        public void EndDay()
        {
            for (int i = 0; i < _rules.Length; i++)
            {
                var rule = _rules[i];
                if (rule != null && rule.IsSatisfied(_performed))
                {
                    rule.Apply(_flags);
                }
            }
        }
    }
}
