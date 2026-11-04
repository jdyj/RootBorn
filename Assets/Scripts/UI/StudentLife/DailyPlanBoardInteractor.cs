using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.UI.StudentLife
{
    public sealed class DailyPlanBoardInteractor : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable, IPlayerInteractionPromptAppearance
    {
        [SerializeField] private DailyPlanPanel _panel;
        [SerializeField] private Vector3 _interactionPromptOffset = new Vector3(0f, 1.1f, 0f);
        [SerializeField] private bool _overrideInteractionPromptColor;
        [SerializeField] private Color _interactionPromptColor = new Color32(38, 59, 70, 255);
        [SerializeField] private DailyPlanRouteSetDefinition[] _routeSets = System.Array.Empty<DailyPlanRouteSetDefinition>();
        [SerializeField] private DailyPlanProgressComponent _dailyProgress;
        [SerializeField] private StudentLifeProgressComponent _studentProgress;

        public string InteractionPrompt => "[E] Plan Day";
        public Vector3 InteractionPromptOffset => _interactionPromptOffset;
        public Transform InteractionTransform => transform;
        public int InteractionPriority => 80;

        public bool TryGetInteractionPromptColor(out Color color)
        {
            color = _interactionPromptColor;
            return _overrideInteractionPromptColor;
        }

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
            _panel.BindSchedule(GetAvailableRoutes(player, student), daily, student);
            return true;
        }

        private DailyPlanRouteDefinition[] GetAvailableRoutes(GameObject player, StudentLifeProgressComponent student)
        {
            var progress = student != null ? student.Progress : null;
            if (progress == null) return System.Array.Empty<DailyPlanRouteDefinition>();
            var context = DailyPlanRouteContextBuilder.Build(player, progress);
            return new DailyPlanRouteRunner().GetAvailableRoutes(_routeSets, in context);
        }
    }
}