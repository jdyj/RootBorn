using System;
using System.Globalization;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class OutsideSchoolActivityInteractor : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private OutsideSchoolActivityDefinition _activity;

        private readonly OutsideSchoolActivityRunner _runner = new OutsideSchoolActivityRunner();
        private int _requestSequence;

        public OutsideSchoolActivityDefinition Activity => _activity;
        public OutsideSchoolActivityResult LastResult { get; private set; }
        public string InteractionPrompt => "[E] " + HumanizeDisplayKey(_activity != null ? _activity.DisplayNameKey : string.Empty);
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.05f, 0f);
        public Transform InteractionTransform => transform;

        public void Bind(OutsideSchoolActivityDefinition activity)
        {
            _activity = activity;
            _requestSequence = 0;
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
            string requestId = _activity != null ? _activity.Id + ":" + _requestSequence++ : "outside-school:" + _requestSequence++;
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

        private static string HumanizeDisplayKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "Outside Activity";
            }

            int dot = key.LastIndexOf('.');
            string tail = dot >= 0 && dot + 1 < key.Length ? key.Substring(dot + 1) : key;
            string[] parts = tail.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return key;
            }

            var textInfo = CultureInfo.InvariantCulture.TextInfo;
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = textInfo.ToTitleCase(parts[i]);
            }

            return string.Join(" ", parts);
        }
    }
}
