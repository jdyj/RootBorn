using System.Collections;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Growing;
using Rootborn.Game.Characters.Wearables;
using Rootborn.Game.Common;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Rootborn.Network.Characters
{
    /// <summary>
    /// The owner publishes which look it wears; everyone else shows that look. Only a look key crosses the network
    /// (a wearable look code, or a legacy appearance id), resolved through the registry, so a client can only show
    /// assets the game ships (codes are checked by Rootborn.Core WearableLookCode).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrowingCharacterNetworkAppearanceSync : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString128Bytes> _appearanceId = new NetworkVariable<FixedString128Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private const float RegistryRetrySeconds = 0.25f;

        private GrowingCharacterVisualView _view;
        private WearableLookResolver _resolver;

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
            if (_view != null) _view.LookChanged -= HandleLocalChange;
            _view = null;
        }

        private void HandleInstalled(GameObject player, GrowingCharacterVisualView view)
        {
            if (player == gameObject) BindView(view);
        }

        private void BindView(GrowingCharacterVisualView view)
        {
            if (view == null || view == _view) return;
            if (_view != null) _view.LookChanged -= HandleLocalChange;
            _view = view;
            _view.IsRemote = !IsOwner;
            _view.LookChanged += HandleLocalChange;
            if (IsOwner) HandleLocalChange();
            else ApplyRemote(_appearanceId.Value.ToString());
        }

        private void HandleLocalChange()
        {
            if (!IsOwner || _view == null) return;
            string key = _view.LookKey;
            if (string.IsNullOrEmpty(key)) return;
            var id = new FixedString128Bytes(key);
            if (!_appearanceId.Value.Equals(id)) _appearanceId.Value = id;
        }

        private void HandleValueChanged(FixedString128Bytes previous, FixedString128Bytes current)
        {
            if (!IsOwner) ApplyRemote(current.ToString());
        }

        private void ApplyRemote(string id)
        {
            if (IsOwner || _view == null || string.IsNullOrEmpty(id)) return;
            if (!GameDataRegistryProvider.TryGet(out var registry)) return;
            if (_resolver == null) _resolver = new WearableLookResolver(registry);
            if (_resolver.TryResolveCode(id, out var wearableLook))
            {
                _view.ApplyLayers(wearableLook.Body, wearableLook.Layers, wearableLook.Code);
                return;
            }

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
