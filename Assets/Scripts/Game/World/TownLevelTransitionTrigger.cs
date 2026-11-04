using System;
using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Game.World
{
    public sealed class TownLevelTransitionTrigger : MonoBehaviour
    {
        [SerializeField] private string _destinationScene = "Beach";
        [SerializeField] private string _destinationSpawnId = "beach-top-entry";
        [SerializeField] private float _fadeSeconds = 0.25f;
        [SerializeField] private float _cooldownSeconds = 0.75f;
        private Func<GameObject, string, string, string, bool> _travelHandler;
        private bool _transitioning;
        private float _lastTransitionTime = -100f;
        private SpriteRenderer _overlayRenderer;

        public bool HasTransitioned { get; private set; }
        public bool TransitionOverlayVisible => _overlayRenderer != null && _overlayRenderer.enabled;
        public string DestinationScene => _destinationScene;
        public string DestinationSpawnId => _destinationSpawnId;

        public void Configure(string destinationScene, string destinationSpawnId, float fadeSeconds, float cooldownSeconds)
        {
            _destinationScene = destinationScene; _destinationSpawnId = destinationSpawnId;
            _fadeSeconds = Mathf.Max(0f, fadeSeconds); _cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        }
        public void ConfigureTravelHandler(Func<GameObject, string, string, string, bool> handler) => _travelHandler = handler;
        public bool TryTravel(GameObject actor)
        {
            if (actor == null || HasTransitioned || UnityEngine.Time.time < _lastTransitionTime + _cooldownSeconds
                || !GameDataRegistryProvider.TryGet(out var registry)
                || WorldEntryService.ResolvePair(registry.WorldTravelCatalog, _destinationScene, _destinationSpawnId) == null)
                return RejectTravel();
            bool accepted = (_travelHandler ?? WorldTravelRuntime.TryRequestTravel)(actor, _destinationScene, _destinationSpawnId, _destinationScene);
            if (!accepted) return RejectTravel();
            HasTransitioned = true; _transitioning = true; _lastTransitionTime = UnityEngine.Time.time;
            return true;
        }
        private bool RejectTravel()
        {
            if (!HasTransitioned) _transitioning = false;
            if (_overlayRenderer != null) _overlayRenderer.enabled = false;
            return false;
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || _transitioning || HasTransitioned || UiGameplayInputLock.IsBlocked
                || UnityEngine.Time.time < _lastTransitionTime + _cooldownSeconds) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || string.IsNullOrWhiteSpace(_destinationScene) || string.IsNullOrWhiteSpace(_destinationSpawnId)) return;
            StartCoroutine(Transition(player.gameObject));
        }
        private IEnumerator Transition(GameObject player)
        {
            _transitioning = true;
            EnsureOverlay(); _overlayRenderer.enabled = true;
            yield return new WaitForSeconds(_fadeSeconds);
            TryTravel(player);
        }
        private void EnsureOverlay()
        {
            if (_overlayRenderer != null) return;
            var overlay = new GameObject("LevelTransitionOverlay");
            overlay.transform.SetParent(transform, false); overlay.transform.localPosition = Vector3.zero; overlay.transform.localScale = new Vector3(200f, 200f, 1f);
            _overlayRenderer = overlay.AddComponent<SpriteRenderer>(); _overlayRenderer.sprite = MakeSolidSprite();
            _overlayRenderer.color = new Color(0f, 0f, 0f, 0.82f); _overlayRenderer.sortingOrder = 5000; _overlayRenderer.enabled = false;
        }
        private static Sprite MakeSolidSprite()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) texture.SetPixel(x, y, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }
        private void OnDestroy()
        {
            var sprite = _overlayRenderer != null ? _overlayRenderer.sprite : null;
            if (sprite == null) return;
            var texture = sprite.texture;
            if (Application.isPlaying) { Destroy(sprite); if (texture != null) Destroy(texture); }
            else { DestroyImmediate(sprite); if (texture != null) DestroyImmediate(texture); }
        }
    }
}
