using System;
using Rootborn.Game.Localization;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
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
        public string InteractionPrompt => "[E] " + GameText.Format("life.location.explore", _identity != null && _identity.Location != null ? GameText.Get(_identity.Location.DisplayNameKey) : GameText.Get("life.location.generic"));
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

            QuestGameplayEventBridge.Record(new QuestEvent(QuestEventKind.LocationVisit,
                QuestGameplayEventBridge.CreateRequestKey("location-visit"), location: _identity.Location), progress);
            OnAnyInteracted?.Invoke(this, player);
            return true;
        }

    }
}
