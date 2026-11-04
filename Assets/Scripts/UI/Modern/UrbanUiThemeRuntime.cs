using System;
using Rootborn.Game.Managers;
using UnityEngine;

namespace Rootborn.UI.Modern
{
    /// <summary>Single lifetime owner of the theme's resource handle.</summary>
    [DisallowMultipleComponent]
    public sealed class UrbanUiThemeRuntime : MonoBehaviour
    {
        private static UrbanUiThemeRuntime s_owner;
        public static UrbanUiThemeDefinition Current { get; private set; }
        public static event Action Changed;
        private ResourceManager _resource;
        private bool _destroyed;
        private bool _ownsAddress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_owner = null;
            Current = null;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (s_owner != null || Application.isBatchMode) return;
            var go = new GameObject("[UrbanUiTheme]");
            go.AddComponent<UrbanUiThemeRuntime>();
        }

        private void Awake()
        {
            if (s_owner != null && s_owner != this) { Destroy(gameObject); return; }
            s_owner = this;
            DontDestroyOnLoad(gameObject);
            LoadThemeAsync();
        }

        private async void LoadThemeAsync()
        {
            try
            {
                await Managers.BootstrapAsync();
                if (_destroyed) return;
                _resource = Managers.Resource;
                if (_resource == null) return;
                var theme = await _resource.LoadAsync<UrbanUiThemeDefinition>(UrbanUiThemeDefinition.Address);
                _ownsAddress = theme != null;
                if (_destroyed) { ReleaseOwnedAddress(); return; }
                Current = theme;
            }
            catch (Exception exception)
            {
                if (!_destroyed) Debug.LogWarning("[ROOTBORN] Urban UI theme unavailable; keeping legacy UI. " + exception.Message);
                ReleaseOwnedAddress();
                return;
            }
            NotifyChanged();
        }

        private void OnDestroy()
        {
            _destroyed = true;
            if (s_owner != this) return;
            s_owner = null;
            Current = null;
            try { NotifyChanged(); }
            finally { ReleaseOwnedAddress(); }
        }

        private void NotifyChanged()
        {
            var subscribers = Changed;
            if (subscribers == null) return;
            foreach (Action subscriber in subscribers.GetInvocationList())
            {
                try { subscriber(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
        private void ReleaseOwnedAddress()
        {
            if (!_ownsAddress) return;
            _ownsAddress = false;
            _resource?.Release(UrbanUiThemeDefinition.Address);
        }
    }
}


