using System.Collections;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Growing;
using Rootborn.Game.Common;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Rootborn.Network.Characters
{
    /// <summary>
    /// The owner publishes which look it wears; everyone else shows that look. Only an appearance id crosses
    /// the network, and it is resolved through the registry, so a client can only pick assets the game ships.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrowingCharacterNetworkAppearanceSync : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString64Bytes> _appearanceId = new NetworkVariable<FixedString64Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private const float RegistryRetrySeconds = 0.25f;

        private GrowingCharacterVisualView _view;

        public override void OnNetworkSpawn()
        {
            GrowingCharacterRuntimeInstaller.Installed += HandleInstalled;
            _appearanceId.OnValueChanged += HandleValueChanged;
            StartCoroutine(InstallWhenRegistryIsReady());
        }

        private IEnumerator InstallWhenRegistryIsReady()
        {
            var wait = new WaitForSecondsRealtime(RegistryRetrySeconds);
            GameDataRegistry registry;
            while (!GameDataRegistryProvider.TryGet(out registry)) yield return wait;
            // Players joining after Town finished setting up are not visited by its installer.
            GrowingCharacterRuntimeInstaller.TryInstall(gameObject, registry);
            BindView(GetComponentInChildren<GrowingCharacterVisualView>(true));
        }

        public override void OnNetworkDespawn()
        {
            GrowingCharacterRuntimeInstaller.Installed -= HandleInstalled;
            _appearanceId.OnValueChanged -= HandleValueChanged;
            StopAllCoroutines();
            if (_view != null) _view.AppearanceChanged -= HandleLocalChange;
            _view = null;
        }

        private void HandleInstalled(GameObject player, GrowingCharacterVisualView view)
        {
            if (player == gameObject) BindView(view);
        }

        private void BindView(GrowingCharacterVisualView view)
        {
            if (view == null || view == _view) return;
            if (_view != null) _view.AppearanceChanged -= HandleLocalChange;
            _view = view;
            _view.IsRemote = !IsOwner;
            _view.AppearanceChanged += HandleLocalChange;
            if (IsOwner) HandleLocalChange(_view.Appearance);
            else ApplyRemote(_appearanceId.Value.ToString());
        }

        private void HandleLocalChange(GrowingCharacterAppearanceDefinition look)
        {
            if (!IsOwner || look == null) return;
            var id = new FixedString64Bytes(look.Id);
            if (!_appearanceId.Value.Equals(id)) _appearanceId.Value = id;
        }

        private void HandleValueChanged(FixedString64Bytes previous, FixedString64Bytes current)
        {
            if (!IsOwner) ApplyRemote(current.ToString());
        }

        private void ApplyRemote(string id)
        {
            if (IsOwner || _view == null || string.IsNullOrEmpty(id)) return;
            if (!GameDataRegistryProvider.TryGet(out var registry)) return;
            var look = FindById(registry.CharacterAppearances, id);
            if (look != null) _view.Apply(look);
        }

        public static GrowingCharacterAppearanceDefinition FindById(CharacterAppearanceDefinition[] appearances, string id)
        {
            if (appearances == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < appearances.Length; i++)
            {
                if (appearances[i] is GrowingCharacterAppearanceDefinition look && look.Id == id) return look;
            }

            return null;
        }
    }
}
