using UnityEngine;

namespace Rootborn.Game.Tools
{
    public abstract class ToolEffectBase : ScriptableObject
    {
        public abstract void Apply(in ToolUseContext ctx);
    }

    public readonly struct ToolUseContext
    {
        public readonly ToolDefinition Tool;
        public readonly GameObject Target;
        public readonly string Surface;

        public ToolUseContext(ToolDefinition tool, GameObject target, string surface)
        {
            Tool = tool;
            Target = target;
            Surface = surface;
        }
    }
}
