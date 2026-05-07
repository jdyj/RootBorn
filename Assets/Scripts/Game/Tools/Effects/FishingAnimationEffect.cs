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
        [SerializeField] private string _requiredSurface = "Water";

        public FishingAnimationPhase Phase => _phase;
        public string RequiredSurface => _requiredSurface;

        public override void Apply(in ToolUseContext ctx)
        {
            if (!MatchesSurface(ctx.Surface) || ctx.Target == null)
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

        private bool MatchesSurface(string surface)
        {
            return string.IsNullOrEmpty(_requiredSurface) || string.Equals(_requiredSurface, surface, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
