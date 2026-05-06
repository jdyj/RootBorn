using System.Collections.Generic;

namespace Rootborn.Game.Story
{
    public sealed class StoryFlagSet
    {
        private readonly HashSet<StoryFlagDefinition> _flags = new HashSet<StoryFlagDefinition>();

        public bool IsSet(StoryFlagDefinition flag)
        {
            return flag != null && _flags.Contains(flag);
        }

        public bool Set(StoryFlagDefinition flag)
        {
            return flag != null && _flags.Add(flag);
        }
    }
}
