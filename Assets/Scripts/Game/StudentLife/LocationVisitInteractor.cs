using System;
using System.Globalization;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class LocationVisitInteractor : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        private const int LocationInteractionPriority = 35;

        [SerializeField] private LocationDefinition _location;

        public LocationDefinition Location => _location;
        public LocationVisitResult LastResult { get; private set; }
        public int InteractionPriority => LocationInteractionPriority;
        public string InteractionPrompt => "[E] Visit " + HumanizeDisplayKey(_location != null ? _location.DisplayNameKey : string.Empty);
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.05f, 0f);
        public Transform InteractionTransform => transform;

        public void Bind(LocationDefinition location)
        {
            _location = location;
        }

        public bool CanInteract(GameObject player)
        {
            var progress = player != null ? player.GetComponent<StudentLifeProgressComponent>()?.EnsureProgress() : null;
            return _location != null && progress != null && _location.CanVisit(progress);
        }

        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player)) return false;
            var component = player.GetComponent<StudentLifeProgressComponent>();
            var progress = component.EnsureProgress();
            var visitProgress = new LocationVisitProgress(progress);
            bool applied = visitProgress.TryRecordLocationVisit(_location, out var result);
            LastResult = result;
            if (applied) StudentLifeProgressPersistence.Save(component);
            Debug.Log($"[ROOTBORN] Location visit applied={applied} result={result.Kind} location={result.EntityId} player={result.PlayerId} request={result.RequestId}");
            return true;
        }

        private static string HumanizeDisplayKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Location";
            string[] keyParts = key.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            string tail = key;
            if (keyParts.Length > 0)
            {
                tail = keyParts[keyParts.Length - 1];
                if ((tail == "name" || tail == "display") && keyParts.Length > 1)
                {
                    tail = keyParts[keyParts.Length - 2];
                }
            }

            string[] parts = tail.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return key;
            var textInfo = CultureInfo.InvariantCulture.TextInfo;
            for (int i = 0; i < parts.Length; i++) parts[i] = textInfo.ToTitleCase(parts[i]);
            return string.Join(" ", parts);
        }
    }
}
