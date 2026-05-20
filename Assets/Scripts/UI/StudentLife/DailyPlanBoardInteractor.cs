using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.UI.StudentLife
{
    public sealed class DailyPlanBoardInteractor : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        [SerializeField] private DailyPlanPanel _panel;
        [SerializeField] private DailyPlanRouteSetDefinition[] _routeSets = System.Array.Empty<DailyPlanRouteSetDefinition>();
        [SerializeField] private DailyPlanProgressComponent _dailyProgress;
        [SerializeField] private StudentLifeProgressComponent _studentProgress;

        public string InteractionPrompt => "[E] Plan Day";
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.1f, 0f);
        public Transform InteractionTransform => transform;
        public int InteractionPriority => 80;

        public void Bind(
            DailyPlanPanel panel,
            DailyPlanRouteSetDefinition[] routeSets,
            DailyPlanProgressComponent dailyProgress,
            StudentLifeProgressComponent studentProgress)
        {
            _panel = panel;
            _routeSets = routeSets ?? System.Array.Empty<DailyPlanRouteSetDefinition>();
            _dailyProgress = dailyProgress;
            _studentProgress = studentProgress;
        }

        public bool CanInteract(GameObject player)
        {
            return _panel != null;
        }

        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player)) return false;

            var daily = _dailyProgress != null ? _dailyProgress : player != null ? player.GetComponent<DailyPlanProgressComponent>() : null;
            var student = _studentProgress != null ? _studentProgress : player != null ? player.GetComponent<StudentLifeProgressComponent>() : null;
            _panel.gameObject.SetActive(true);
            _panel.Bind(GetAvailableRoutes(student), daily, student);
            return true;
        }

        private DailyPlanRouteDefinition[] GetAvailableRoutes(StudentLifeProgressComponent student)
        {
            var progress = student != null ? student.Progress : null;
            if (progress == null) return System.Array.Empty<DailyPlanRouteDefinition>();
            return new DailyPlanRouteRunner().GetAvailableRoutes(_routeSets, progress);
        }
    }
}
