using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Interiors
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SortingGroup))]
    public sealed class GroundDepthSort : MonoBehaviour
    {
        [SerializeField] private Vector2 _footPointOffset;
        [SerializeField, Min(1)] private int _unitsPerWorld = 48;
        [SerializeField] private int _baseSortingOrder;
        [SerializeField] private string _activeSceneName;
        private SortingGroup _group;
        [SerializeField, HideInInspector] private bool _hasScopedFallback;
        [SerializeField, HideInInspector] private bool _fallbackGroupEnabled;
        [SerializeField, HideInInspector] private int _fallbackSortingOrder;

        private void OnEnable()
        {
            _group = GetComponent<SortingGroup>();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            RefreshSorting();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (!string.IsNullOrEmpty(_activeSceneName)) RestoreFallback();
        }

        private void LateUpdate() => RefreshSorting();
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshSorting();
        private void OnActiveSceneChanged(Scene previous, Scene next) => RefreshSorting();

        public void Configure(Vector2 footPointOffset, int unitsPerWorld, int baseSortingOrder = 0)
        {
            _footPointOffset = footPointOffset;
            _unitsPerWorld = Mathf.Max(1, unitsPerWorld);
            _baseSortingOrder = baseSortingOrder;
            RefreshSorting();
        }

        // A configured scene owns the world depth band and foot anchor. Legacy scoped callers
        // retain their existing behavior until the scene policy is explicitly applied again.
        public void ConfigureScenePolicy(Vector2 footPointOffset, int unitsPerWorld, int baseSortingOrder)
        {
            _activeSceneName = string.Empty;
            _hasScopedFallback = false;
            if (_group == null) _group = GetComponent<SortingGroup>();
            if (_group != null) _group.enabled = true;
            Configure(footPointOffset, unitsPerWorld, baseSortingOrder);
        }

        public void SetActiveSceneScope(string activeSceneName, bool fallbackGroupEnabled, int fallbackSortingOrder)
        {
            // The caller snapshots before AddComponent can invoke OnEnable and change an existing group.
            // Subsequent reinforcement must never capture the temporary scoped depth band as the fallback.
            if (!_hasScopedFallback)
            {
                _hasScopedFallback = true;
                _fallbackGroupEnabled = fallbackGroupEnabled;
                _fallbackSortingOrder = fallbackSortingOrder;
            }
            _activeSceneName = activeSceneName ?? string.Empty;
            RefreshSorting();
        }

        public void RefreshSorting()
        {
            if (_group == null) _group = GetComponent<SortingGroup>();
            if (_group == null) return;
            if (!string.IsNullOrEmpty(_activeSceneName))
            {
                if (!_hasScopedFallback)
                {
                    // A serialized scope may use the authored group state as its outside-scene fallback.
                    _hasScopedFallback = true;
                    _fallbackGroupEnabled = _group.enabled;
                    _fallbackSortingOrder = _group.sortingOrder;
                }
                // Travelling players can belong to DontDestroyOnLoad during portal handoff.
                if (!string.Equals(SceneManager.GetActiveScene().name, _activeSceneName, System.StringComparison.Ordinal))
                {
                    RestoreFallback();
                    return;
                }
                if (!_group.enabled) _group.enabled = true;
            }

            var footY = transform.TransformPoint(_footPointOffset).y;
            int order = Mathf.Clamp(_baseSortingOrder - Mathf.RoundToInt(footY * _unitsPerWorld), short.MinValue, short.MaxValue);
            if (_group.sortingOrder != order) _group.sortingOrder = order;
        }

        private void RestoreFallback()
        {
            if (_group == null || !_hasScopedFallback) return;
            if (_group.sortingOrder != _fallbackSortingOrder) _group.sortingOrder = _fallbackSortingOrder;
            if (_group.enabled != _fallbackGroupEnabled) _group.enabled = _fallbackGroupEnabled;
        }
    }
}
