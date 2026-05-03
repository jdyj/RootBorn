using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Knowledge.Triggers
{
    [CreateAssetMenu(fileName = "Trigger_HitGroundWithRock", menuName = "Rootborn/Knowledge/Triggers/Hit Ground With Rock")]
    public sealed class HitGroundWithRockTrigger : KnowledgeTriggerBase
    {
        [SerializeField] private string _expectedTargetResourceId = "Rock";
        [SerializeField] private string _expectedSurface = "ground";
        [SerializeField] private ToolDefinition _expectedTool;

        public override bool Evaluate(in KnowledgeContext ctx)
        {
            if (ctx.Action != KnowledgeAction.HitGround) return false;
            if (!string.IsNullOrEmpty(_expectedTargetResourceId) && ctx.TargetResourceId != _expectedTargetResourceId) return false;
            if (!string.IsNullOrEmpty(_expectedSurface) && ctx.Surface != _expectedSurface) return false;
            if (_expectedTool != null && ctx.Tool != _expectedTool) return false;
            return ctx.RepeatCount >= RequiredRepeats;
        }
    }
}
