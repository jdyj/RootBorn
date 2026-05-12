using System;
using System.Globalization;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class TownHelpActionInteractor : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private TownHelpActionDefinition _action;

        private readonly TownHelpActionRunner _runner = new TownHelpActionRunner();
        private int _requestSequence;

        public TownHelpActionDefinition Action => _action;
        public TownHelpActionResult LastResult { get; private set; }
        public string InteractionPrompt => "[E] " + HumanizeDisplayKey(_action != null ? _action.DisplayNameKey : string.Empty);
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.05f, 0f);
        public Transform InteractionTransform => transform;

        public void Bind(TownHelpActionDefinition action)
        {
            _action = action;
            _requestSequence = 0;
        }

        public bool CanInteract(GameObject player)
        {
            return _action != null && player != null && player.GetComponent<StudentLifeProgressComponent>() != null;
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
            string requestId = _action != null ? _action.Id + ":" + _requestSequence++ : "town-help:" + _requestSequence++;
            return Interact(progressComponent, requestId);
        }

        public bool Interact(StudentLifeProgressComponent progressComponent, string requestId)
        {
            var progress = progressComponent == null ? null : progressComponent.EnsureProgress();
            bool applied = _runner.TryPerform(_action, progress, requestId, out var result);
            LastResult = result;
            Debug.Log($"[ROOTBORN] Town help action applied={applied} result={result.Kind} action={result.ActionId} npc={result.NpcId} location={result.LocationId} player={result.PlayerId} request={result.RequestId}");
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
                return "Town Help";
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
