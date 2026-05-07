using Rootborn.Game.Family;
using UnityEngine;

namespace Rootborn.Game.Tools.Effects
{
    public enum FishingAnimationPhase
    {
        ThrowHook = 0,
        WaitingIdle = 1,
        PullHook = 2,
        Caught = 3,
    }

    [CreateAssetMenu(fileName = "Effect_FishingAnimation", menuName = "Rootborn/Tools/Effects/Fishing Animation")]
    public sealed class FishingAnimationEffect : ToolEffectBase
    {
        [SerializeField] private FishingAnimationPhase _phase = FishingAnimationPhase.ThrowHook;

        public FishingAnimationPhase Phase => _phase;

        public override void Apply(in ToolUseContext ctx)
        {
            if (ctx.Target == null)
            {
                return;
            }

            var controller = ctx.Target.GetComponent<FishingAnimationController>();
            if (controller == null)
            {
                return;
            }

            switch (_phase)
            {
                case FishingAnimationPhase.ThrowHook:
                    controller.PlayThrowHook();
                    break;
                case FishingAnimationPhase.WaitingIdle:
                    controller.PlayWaitingIdle();
                    break;
                case FishingAnimationPhase.PullHook:
                    controller.PlayPullHook(false);
                    break;
                case FishingAnimationPhase.Caught:
                    controller.PlayPullHook(true);
                    break;
            }
        }
    }
}
