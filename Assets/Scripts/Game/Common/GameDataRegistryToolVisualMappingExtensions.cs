using Rootborn.Game.Tools;

namespace Rootborn.Game.Common
{
    public static class GameDataRegistryToolVisualMappingExtensions
    {
        public static ToolVisualMappingDefinition FindToolVisualMapping(this GameDataRegistry registry, ToolDefinition tool)
        {
            if (registry == null || tool == null)
                return null;

            var mappings = registry.ToolVisualMappings;
            for (int i = 0; i < mappings.Length; i++)
            {
                var mapping = mappings[i];
                if (mapping != null && ReferenceEquals(mapping.Tool, tool))
                    return mapping;
            }

            return null;
        }
    }
}
