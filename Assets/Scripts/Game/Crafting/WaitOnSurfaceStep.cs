using UnityEngine;

namespace Rootborn.Game.Crafting
{
    [CreateAssetMenu(fileName = "Step_WaitOnSurface", menuName = "Rootborn/Crafting/Step/Wait On Surface")]
    public sealed class WaitOnSurfaceStep : CraftStepBase
    {
        [SerializeField] private string _requiredSurface;
        [SerializeField] private float _requiredSeconds = 5f;

        public override bool IsSatisfied(in CraftAttemptState state)
        {
            if (!string.IsNullOrEmpty(_requiredSurface) && state.CurrentSurface != _requiredSurface) return false;
            return state.ElapsedSec >= _requiredSeconds;
        }
    }
}
