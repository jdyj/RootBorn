using System;
using System.Globalization;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class PartTimeWorkInteractor : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private PartTimeWorkDefinition _work;

        private readonly PartTimeWorkRunner _runner = new PartTimeWorkRunner();
        private int _requestSequence;

        public PartTimeWorkDefinition Work => _work;
        public PartTimeWorkResult LastResult { get; private set; }
        public string InteractionPrompt => "[E] " + HumanizeDisplayKey(_work != null ? _work.DisplayNameKey : string.Empty);
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.05f, 0f);
        public Transform InteractionTransform => transform;

        public void Bind(PartTimeWorkDefinition work)
        {
            _work = work;
            _requestSequence = 0;
        }

        public bool CanInteract(GameObject player)
        {
            return _work != null
                && player != null
                && player.GetComponent<StudentLifeProgressComponent>() != null
                && player.GetComponent<PlayerInventory>() != null;
        }

        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player))
            {
                return false;
            }

            return Interact(player.GetComponent<StudentLifeProgressComponent>(), player.GetComponent<PlayerInventory>());
        }

        public bool Interact(StudentLifeProgressComponent progressComponent, PlayerInventory playerInventory)
        {
            string requestId = _work != null ? _work.Id + ":" + _requestSequence++ : "part-time-work:" + _requestSequence++;
            return Interact(progressComponent, playerInventory, requestId);
        }

        public bool Interact(StudentLifeProgressComponent progressComponent, PlayerInventory playerInventory, string requestId)
        {
            var progress = progressComponent == null ? null : progressComponent.EnsureProgress();
            var inventory = playerInventory == null ? null : playerInventory.Inventory;
            bool applied = _runner.TryPerform(_work, progress, inventory, requestId, out var result);
            LastResult = result;
            Debug.Log($"[ROOTBORN] Part-time work applied={applied} result={result.Kind} work={result.WorkId} workplace={result.WorkplaceId} player={result.PlayerId} request={result.RequestId}");
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
                return "Part-Time Work";
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
