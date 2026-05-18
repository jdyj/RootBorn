using System;
using System.Globalization;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class LocationActivityInteractor : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        private const int LocationIdentityPriority = 45;

        [SerializeField] private LocationIdentityDefinition _identity;

        public static event Action<LocationActivityInteractor, GameObject> OnAnyInteracted;

        public LocationIdentityDefinition Identity => _identity;
        public int InteractionPriority => LocationIdentityPriority;
        public string InteractionPrompt => "[E] Explore " + Humanize(_identity != null && _identity.Location != null ? _identity.Location.DisplayNameKey : string.Empty);
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.25f, 0f);
        public Transform InteractionTransform => transform;

        public void Bind(LocationIdentityDefinition identity)
        {
            _identity = identity;
        }

        public bool CanInteract(GameObject player)
        {
            var progress = player != null ? player.GetComponent<StudentLifeProgressComponent>()?.EnsureProgress() : null;
            return _identity != null && _identity.Location != null && progress != null && _identity.Location.CanVisit(progress);
        }

        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player)) return false;
            var component = player.GetComponent<StudentLifeProgressComponent>();
            var progress = component.EnsureProgress();
            var visitProgress = new LocationVisitProgress(progress);
            if (!visitProgress.HasVisited(_identity.Location))
            {
                visitProgress.TryRecordLocationVisit(_identity.Location, out _);
                StudentLifeProgressPersistence.Save(component);
            }

            OnAnyInteracted?.Invoke(this, player);
            return true;
        }

        private static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Location";
            string text = key.Replace("location.", string.Empty).Replace(".name", string.Empty).Replace('-', ' ').Replace('_', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);
        }
    }
}
