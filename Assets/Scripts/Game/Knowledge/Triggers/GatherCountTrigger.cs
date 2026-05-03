using UnityEngine;

namespace Rootborn.Game.Knowledge.Triggers
{
    [CreateAssetMenu(fileName = "Trigger_GatherCount", menuName = "Rootborn/Knowledge/Triggers/Gather Count")]
    public sealed class GatherCountTrigger : KnowledgeTriggerBase
    {
        [SerializeField] private string _expectedTargetResourceId;

        public override bool Evaluate(in KnowledgeContext ctx)
        {
            if (ctx.Action != KnowledgeAction.Gather) return false;
            if (!string.IsNullOrEmpty(_expectedTargetResourceId) && ctx.TargetResourceId != _expectedTargetResourceId) return false;
            return ctx.RepeatCount >= RequiredRepeats;
        }
    }
}
