using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rootborn.Game.Careers;
using Rootborn.Game.Interiors;
using Rootborn.Game.Managers;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rootborn.UI.Careers
{
    [DisallowMultipleComponent]
    public sealed class FacilityWorldPresenter : MonoBehaviour
    {
        [SerializeField] private FacilityDefinition _facility;
        [SerializeField] private FacilityVisualDefinition[] _visuals = Array.Empty<FacilityVisualDefinition>();
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private CareerFacilityInteractor _door;
        [SerializeField] private Transform _collisionRoot;
        [SerializeField] private int _sortingBaseOrder = 10000;
        [SerializeField, Min(1)] private int _sortingUnitsPerWorld = 48;

        private sealed class SpriteRequest
        {
            public string Address;
            public Task<Sprite> Task;
            public Action<string> Release;
            public bool Released;
        }
        private readonly Dictionary<string, SpriteRequest> _requests = new Dictionary<string, SpriteRequest>(StringComparer.Ordinal);
        private Func<string, Task<Sprite>> _load;
        private Action<string> _release;
        private AdultCareerRuntimeComponent _owner;
        private SpriteRequest _owned;
        private FacilityVisualDefinition _pendingVisual;
        private Task<bool> _pendingRefresh;
        private int _generation;

        public FacilityDefinition Facility => _facility;
        public FacilityVisualDefinition CurrentVisual { get; private set; }
        public int SelectedLevel => CurrentVisual != null ? CurrentVisual.Level : 0;

        public void ConfigureSpriteLoading(Func<string, Task<Sprite>> load, Action<string> release)
        {
            _load = load;
            _release = release;
        }

        public Task<bool> BindOwner(AdultCareerRuntimeComponent owner)
        {
            Unsubscribe();
            _owner = owner;
            if (isActiveAndEnabled && _owner != null) _owner.Committed += HandleCommitted;
            return RefreshAsync();
        }

        public Task<bool> RefreshAsync()
        {
            if (!isActiveAndEnabled || !TrySelectVisual(out var visual)) return Task.FromResult(false);
            if (CurrentVisual == visual && _owned != null && !_owned.Released && _spriteRenderer.sprite != null)
                return Task.FromResult(true);
            if (_pendingVisual == visual && _pendingRefresh != null && !_pendingRefresh.IsCompleted)
                return _pendingRefresh;

            Func<string, Task<Sprite>> load = _load;
            Action<string> release = _release;
            if (load == null)
            {
                var resources = Managers.Resource;
                if (resources == null) return Task.FromResult(false);
                load = resources.LoadAsync<Sprite>;
                release = resources.Release;
            }
            if (release == null) return Task.FromResult(false);

            if (!_requests.TryGetValue(visual.SpriteAddress, out var request) || request.Released)
            {
                Task<Sprite> task;
                try { task = load(visual.SpriteAddress); }
                catch { return Task.FromResult(false); }
                if (task == null) return Task.FromResult(false);
                request = new SpriteRequest { Address = visual.SpriteAddress, Task = task, Release = release };
                _requests[request.Address] = request;
            }
            _pendingVisual = visual;
            int generation = ++_generation;
            _pendingRefresh = LoadAndApplyAsync(visual, request, generation);
            return _pendingRefresh;
        }

        private async Task<bool> LoadAndApplyAsync(FacilityVisualDefinition visual, SpriteRequest request, int generation)
        {
            Sprite sprite;
            try { sprite = await request.Task; }
            catch { ForgetFailedRequest(request); return false; }
            if (sprite == null) { ForgetFailedRequest(request); return false; }
            if (this == null || !isActiveAndEnabled || generation != _generation || _pendingVisual != visual)
            {
                // A newer request for the same address shares this load and takes ownership.
                if (this == null || !isActiveAndEnabled || (_pendingVisual == null || _pendingVisual.SpriteAddress != request.Address) && !ReferenceEquals(_owned, request))
                    ReleaseRequest(request);
                return false;
            }
            if (!HasReferences()) { ReleaseRequest(request); return false; }

            var previous = _owned;
            ApplyGeometry(visual);
            _spriteRenderer.sprite = sprite;
            _spriteRenderer.enabled = true;
            CurrentVisual = visual;
            _owned = request;
            if (!ReferenceEquals(previous, request)) ReleaseRequest(previous);
            return true;
        }

        private bool TrySelectVisual(out FacilityVisualDefinition selected)
        {
            selected = null;
            if (!HasReferences() || _facility == null || _visuals == null || _visuals.Length != 3) return false;
            int level = 1;
            if (_owner != null && _owner.IsBound && _owner.Progress != null && _owner.Progress.IsAdult
                && _owner.Progress.FacilityId == _facility.Id) level = Mathf.Clamp(_owner.Progress.FacilityLevel, 1, 3);
            int seen = 0;
            for (int i = 0; i < _visuals.Length; i++)
            {
                var visual = _visuals[i];
                if (visual == null || !visual.IsValid || visual.Facility != _facility) return false;
                int bit = 1 << visual.Level;
                if ((seen & bit) != 0) return false;
                seen |= bit;
                if (visual.Level == level) selected = visual;
            }
            return seen == 14 && selected != null;
        }

        private bool HasReferences() => _spriteRenderer != null && _door != null && _collisionRoot != null
            && _spriteRenderer.transform != transform && _door.transform != transform
            && _spriteRenderer.transform.IsChildOf(transform) && _door.transform.IsChildOf(transform)
            && _collisionRoot.IsChildOf(transform);

        private void ApplyGeometry(FacilityVisualDefinition visual)
        {
            _spriteRenderer.transform.position = transform.TransformPoint(visual.SpriteOffset);
            _door.transform.position = transform.TransformPoint(visual.EntranceLocalPosition);
            _door.Bind(_facility);
            var entryCollider = _door.GetComponent<Collider2D>();
            if (entryCollider == null) entryCollider = _door.gameObject.AddComponent<BoxCollider2D>();
            entryCollider.isTrigger = true;
            entryCollider.enabled = true;

            var existing = _collisionRoot.GetComponentsInChildren<BoxCollider2D>(true);
            for (int i = 0; i < visual.CollisionCells.Count; i++)
            {
                BoxCollider2D collider;
                if (i < existing.Length) collider = existing[i];
                else
                {
                    var cell = new GameObject("FacilityCollisionCell");
                    cell.transform.SetParent(_collisionRoot, false);
                    collider = cell.AddComponent<BoxCollider2D>();
                }
                collider.transform.position = transform.TransformPoint((Vector2)visual.CollisionCells[i]);
                collider.transform.localRotation = Quaternion.identity;
                collider.transform.localScale = Vector3.one;
                collider.offset = Vector2.zero;
                collider.size = Vector2.one;
                collider.isTrigger = false;
                collider.gameObject.SetActive(true);
                collider.enabled = true;
            }
            for (int i = visual.CollisionCells.Count; i < existing.Length; i++)
            {
                existing[i].enabled = false;
                if (existing[i].transform == _collisionRoot) Remove(existing[i]);
                else { existing[i].gameObject.SetActive(false); Remove(existing[i].gameObject); }
            }

            var group = GetComponent<SortingGroup>();
            if (group == null) group = gameObject.AddComponent<SortingGroup>();
            group.enabled = true;
            var depth = GetComponent<GroundDepthSort>();
            if (depth == null) depth = gameObject.AddComponent<GroundDepthSort>();
            depth.Configure(visual.EntranceLocalPosition, _sortingUnitsPerWorld, _sortingBaseOrder);
        }

        private void HandleCommitted() { _ = RefreshAsync(); }
        private void OnEnable()
        {
            Unsubscribe();
            if (_owner != null) _owner.Committed += HandleCommitted;
            if (HasReferences()) _ = RefreshAsync();
        }
        private void OnDisable() => StopPresentation();
        private void OnDestroy() => StopPresentation();
        private void Unsubscribe() { if (_owner != null) _owner.Committed -= HandleCommitted; }
        private void StopPresentation()
        {
            Unsubscribe();
            _generation++;
            _pendingVisual = null;
            _pendingRefresh = null;
            if (_spriteRenderer != null) _spriteRenderer.sprite = null;
            ReleaseRequest(_owned);
            _owned = null;
            // Pending tasks release their own captured key when they finish.
        }
        private void ForgetFailedRequest(SpriteRequest request)
        {
            if (_requests.TryGetValue(request.Address, out var current) && ReferenceEquals(current, request)) _requests.Remove(request.Address);
        }
        private void ReleaseRequest(SpriteRequest request)
        {
            if (request == null || request.Released) return;
            request.Released = true;
            ForgetFailedRequest(request);
            request.Release(request.Address);
        }
        private static void Remove(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
