using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.UI.Careers
{
    [DisallowMultipleComponent]
    public sealed class CareerFacilityInteractor : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        [SerializeField] private FacilityDefinition _facility;
        public FacilityDefinition Facility => _facility;
        public string InteractionPrompt => GameText.Get("facility.interact");
        public Vector3 InteractionPromptOffset => new Vector3(0, 1.1f, 0);
        public Transform InteractionTransform => transform;
        public int InteractionPriority => 55;
        public void Bind(FacilityDefinition facility) => _facility = facility;
        public bool CanInteract(GameObject player)
        { return isActiveAndEnabled && _facility != null && _facility.IsValid && player != null && player.GetComponent<PlayerInventory>() != null; }
        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player) || !GameDataRegistryProvider.TryGet(out var registry)) return false;
            // Scene references and bundle dependencies can be different instances of one authored facility.
            FacilityDefinition registered = null;
            foreach (var candidate in registry.FacilityDefinitions)
                if (candidate != null && candidate.Id == _facility.Id) { registered = candidate; break; }
            if (registered == null || !registered.IsValid) return false;
            var owner = AdultCareerRuntimeComponent.Ensure(player, registry); if (owner == null) return false;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var canvas = SelectManagementCanvas(canvases, gameObject.scene);
            if (canvas == null) return false;
            var panel = FacilityManagementPanel.EnsureInScene(canvas);
            if (panel == null) return false;
            panel.Show(registered, owner);
            if (!panel.IsOpen || !panel.gameObject.activeInHierarchy) return false;
            if (owner.CanMutate && registered.Location != null)
                QuestGameplayEventBridge.Record(new QuestEvent(QuestEventKind.LocationVisit,
                    QuestGameplayEventBridge.CreateRequestKey("facility-visit"), location: registered.Location), owner.StudentComponent.Progress);
            return true;
        }

        private static Canvas SelectManagementCanvas(Canvas[] candidates, Scene scene)
        {
            Canvas selected = null;
            bool selectedShared = false;
            if (candidates == null) return null;
            for (int i = 0; i < candidates.Length; i++)
            {
                var canvas = candidates[i];
                if (canvas == null || !canvas.isActiveAndEnabled || !canvas.gameObject.activeInHierarchy
                    || canvas.gameObject.scene != scene || canvas.rootCanvas != canvas
                    || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                bool shared = canvas.GetComponentInChildren<ModernUiInventoryPanel>(true) != null
                    || canvas.GetComponentInChildren<ModernUiPanelInputRouter>(true) != null;
                if (selected == null || (shared && !selectedShared)
                    || (shared == selectedShared && canvas.sortingOrder > selected.sortingOrder))
                {
                    selected = canvas;
                    selectedShared = shared;
                }
            }
            return selected;
        }
    }
}
