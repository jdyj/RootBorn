using System;
using System.Globalization;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class DiscoveryPointInteractor : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        [SerializeField] private DiscoveryDefinition _discovery;

        private readonly DiscoveryRunner _runner = new DiscoveryRunner();

        public DiscoveryDefinition Discovery => _discovery;
        public DiscoveryResult LastResult { get; private set; }
        public string InteractionPrompt => "[E] " + HumanizeDisplayKey(_discovery != null ? _discovery.DisplayNameKey : string.Empty);
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.05f, 0f);
        public Transform InteractionTransform => transform;
        public int InteractionPriority => 15;

        public void Bind(DiscoveryDefinition discovery)
        {
            _discovery = discovery;
        }

        public bool CanInteract(GameObject player)
        {
            return _discovery != null && player != null && player.GetComponent<StudentLifeProgressComponent>() != null;
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
            var progress = progressComponent == null ? null : progressComponent.EnsureProgress();
            bool applied = _runner.TryDiscover(_discovery, progress, out var result);
            LastResult = result;
            Debug.Log($"[ROOTBORN] Discovery applied={applied} result={result.Kind} discovery={result.DiscoveryId} scope={result.Scope} player={result.PlayerId} request={result.RequestId}");
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
                return "Discovery";
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
