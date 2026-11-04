using System;
using Rootborn.Game.Localization;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class OutsideSchoolActivityInteractor : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private OutsideSchoolActivityDefinition _activity;

        private readonly OutsideSchoolActivityRunner _runner = new OutsideSchoolActivityRunner();

        public OutsideSchoolActivityDefinition Activity => _activity;
        public OutsideSchoolActivityResult LastResult { get; private set; }
        public string InteractionPrompt => "[E] " + ResolveDisplayKey(_activity != null ? _activity.DisplayNameKey : string.Empty);
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.05f, 0f);
        public Transform InteractionTransform => transform;

        public void Bind(OutsideSchoolActivityDefinition activity)
        {
            _activity = activity;
        }

        public bool CanInteract(GameObject player)
        {
            return _activity != null && player != null && player.GetComponent<StudentLifeProgressComponent>() != null;
        }

        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player))
            {
                return false;
            }

            return Interact(player.GetComponent<StudentLifeProgressComponent>());
        }

        public bool Interact(StudentLifeProgressComponent progressComponent)
        {
            // A fresh user action must stay unique after scene reload; explicit request IDs still deduplicate replays.
            string requestId = (_activity != null ? _activity.Id : "outside-school") + ":" + Guid.NewGuid().ToString("N");
            return Interact(progressComponent, requestId);
        }

        public bool Interact(StudentLifeProgressComponent progressComponent, string requestId)
        {
            var progress = progressComponent == null ? null : progressComponent.EnsureProgress();
            bool applied = _runner.TryPerform(_activity, progress, requestId, out var result);
            LastResult = result;
            Debug.Log($"[ROOTBORN] Outside school activity applied={applied} result={result.Kind} activity={result.ActivityId} category={result.CategoryId} player={result.PlayerId} request={result.RequestId}");
            if (applied)
            {
                StudentLifeProgressPersistence.Save(progressComponent);
            }

            return applied;
        }

        private static string ResolveDisplayKey(string key)
        {
            string localized = GameText.Get(key);
            return !string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, key, StringComparison.Ordinal)
                ? localized
                : GameText.Get("life.outside-activity.generic");
        }
    }
}
