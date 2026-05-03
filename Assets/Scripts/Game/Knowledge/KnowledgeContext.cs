using Rootborn.Game.Tools;

namespace Rootborn.Game.Knowledge
{
    public enum KnowledgeAction
    {
        None,
        Gather,
        HitGround,
        Plant,
        Harvest,
        Craft,
        UseTool
    }

    public readonly struct KnowledgeContext
    {
        public readonly KnowledgeAction Action;
        public readonly ToolDefinition Tool;
        public readonly string TargetResourceId;
        public readonly string Surface;
        public readonly int RepeatCount;

        public KnowledgeContext(
            KnowledgeAction action,
            ToolDefinition tool,
            string targetResourceId,
            string surface,
            int repeatCount)
        {
            Action = action;
            Tool = tool;
            TargetResourceId = targetResourceId;
            Surface = surface;
            RepeatCount = repeatCount;
        }
    }
}
