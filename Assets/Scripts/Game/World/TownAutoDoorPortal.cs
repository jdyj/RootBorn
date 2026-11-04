using System.Collections;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Game.World
{
    [DisallowMultipleComponent]
    public sealed class TownAutoDoorPortal : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        private const float DefaultOpeningDuration = 0.35f;
        private const int OverlaySortingOrder = 260;

        [SerializeField] private float _openingDuration = DefaultOpeningDuration;

        private WorldPortal _portal;
        private SpriteRenderer _openOverlay;
        private bool _isOpening;
        private bool _hasTravelled;

        public int InteractionPriority => 110;
        public string InteractionPrompt => string.Empty;
        public Vector3 InteractionPromptOffset => Vector3.zero;
        public Transform InteractionTransform => transform;
        public bool IsOpening => _isOpening;
        public bool OpenOverlayVisible => _openOverlay != null && _openOverlay.gameObject.activeSelf && _openOverlay.enabled && _openOverlay.color.a > 0.01f;

        private void Awake()
        {
            _portal = GetComponent<WorldPortal>();
            EnsureOverlay();
        }

        public void Configure(float openingDuration)
        {
            _openingDuration = Mathf.Max(0.05f, openingDuration);
        }

        public bool CanInteract(GameObject player)
        {
            return !_hasTravelled;
        }

        public bool TryInteract(GameObject player)
        {
            return false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isOpening || _hasTravelled || !IsPlayer(other))
            {
                return;
            }

            _portal = _portal != null ? _portal : GetComponent<WorldPortal>();
            if (_portal == null || !_portal.CanTravel)
            {
                return;
            }

            var actor = other.GetComponentInParent<PlayerController>();
            if (actor != null) StartCoroutine(OpenThenTravel(actor.gameObject));
        }

        private IEnumerator OpenThenTravel(GameObject actor)
        {
            _isOpening = true;
            EnsureOverlay();
            _openOverlay.gameObject.SetActive(true);
            _openOverlay.enabled = true;

            float elapsed = 0f;
            while (elapsed < _openingDuration)
            {
                elapsed += UnityEngine.Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _openingDuration);
                float width = Mathf.Lerp(0.25f, 1.0f, t);
                _openOverlay.transform.localScale = new Vector3(width, 1f, 1f);
                Color color = _openOverlay.color;
                color.a = Mathf.Lerp(0.55f, 1f, t);
                _openOverlay.color = color;
                yield return null;
            }

            _hasTravelled = _portal.TryInteract(actor);
            _isOpening = false;
            if (!_hasTravelled && _openOverlay != null) _openOverlay.gameObject.SetActive(false);
        }

        private static bool IsPlayer(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            return other.GetComponent<PlayerController>() != null || other.GetComponentInParent<PlayerController>() != null;
        }

        private void EnsureOverlay()
        {
            if (_openOverlay != null)
            {
                return;
            }

            Transform child = transform.Find("[DoorOpenOverlay]");
            GameObject overlayObject;
            if (child == null)
            {
                overlayObject = new GameObject("[DoorOpenOverlay]");
                overlayObject.transform.SetParent(transform, false);
            }
            else
            {
                overlayObject = child.gameObject;
            }

            overlayObject.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            overlayObject.transform.localRotation = Quaternion.identity;
            overlayObject.transform.localScale = new Vector3(0.25f, 1f, 1f);

            _openOverlay = overlayObject.GetComponent<SpriteRenderer>();
            if (_openOverlay == null)
            {
                _openOverlay = overlayObject.AddComponent<SpriteRenderer>();
            }

            if (_openOverlay.sprite == null)
            {
                _openOverlay.sprite = CreateOverlaySprite();
            }
            _openOverlay.sortingOrder = OverlaySortingOrder;
            _openOverlay.color = new Color(1f, 1f, 1f, 0.45f);
            _openOverlay.gameObject.SetActive(false);
        }

        private static Sprite CreateOverlaySprite()
        {
            const int width = 18;
            const int height = 26;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var transparent = new Color(0f, 0f, 0f, 0f);
            var dark = new Color(0.025f, 0.02f, 0.018f, 1f);
            var edge = new Color(0.95f, 0.58f, 0.16f, 1f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool roundedTop = y > height - 4 && (x < 2 || x > width - 3);
                    bool border = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                    texture.SetPixel(x, y, roundedTop ? transparent : border ? edge : dark);
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 16f);
        }
    }
}
