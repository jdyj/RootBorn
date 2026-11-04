using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Careers;
using UnityEngine;

namespace Rootborn.UI.StudentLife
{
    /// <summary>Connects diary commands to the same player snapshot as their growth rewards.</summary>
    [DisallowMultipleComponent]
    public sealed class DailyPlanSessionBinding : MonoBehaviour
    {
        private AdultCareerRuntimeComponent _owner;
        private DailyPlanProgressComponent _daily;
        public static void Ensure(GameObject player, GameDataRegistry registry)
        {
            if (player == null || registry == null) return;
            var owner = AdultCareerRuntimeComponent.Ensure(player, registry);
            if (owner == null || !owner.IsBound || owner.RecoveryRequired) return;
            var binding = player.GetComponent<DailyPlanSessionBinding>();
            if (binding == null) binding = player.AddComponent<DailyPlanSessionBinding>();
            binding.Bind(owner);
        }
        private void Bind(AdultCareerRuntimeComponent owner)
        {
            if (_owner != null) _owner.Committed -= HandleCommitted;
            _owner = owner;
            _daily = GetComponent<DailyPlanProgressComponent>();
            if (_daily == null) _daily = gameObject.AddComponent<DailyPlanProgressComponent>();
            _daily.BindSession(() => _owner != null ? _owner.StudentComponent.Progress : null,
                command => _owner != null && _owner.ExecuteSessionCommand((session, service) =>
                    new AdultCareerResult(command(session.Student) ? AdultCareerResultKind.Applied : AdultCareerResultKind.InvalidRequest)).Applied);
            _owner.Committed += HandleCommitted;
        }
        private void HandleCommitted() => _daily?.RefreshBoundProgress();
        private void OnDestroy()
        {
            if (_owner != null) _owner.Committed -= HandleCommitted;
            if (_daily != null) _daily.BindSession(null, null);
        }
    }
}
