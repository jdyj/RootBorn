using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
namespace Rootborn.UI.Interiors
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
    public sealed class PlacedFurnitureDepthPresentation : MonoBehaviour
    {
        private readonly Dictionary<Vector3Int, SpriteRenderer> _views = new Dictionary<Vector3Int, SpriteRenderer>();
        private Tilemap _source;
        private TilemapRenderer _renderer;
        private bool _capturedRendererState;
        private bool _originalRendererEnabled;
        private bool _dirty = true;
        private Matrix4x4 _lastMatrix;

        private void EnsureSource()
        {
            if (_source == null) _source = GetComponent<Tilemap>();
            if (_renderer == null) _renderer = GetComponent<TilemapRenderer>();
            if (!_capturedRendererState && _renderer != null)
            {
                _originalRendererEnabled = _renderer.enabled;
                _capturedRendererState = true;
            }
        }

        private void OnEnable()
        {
            EnsureSource();
            Tilemap.tilemapTileChanged -= OnTilesChanged;
            Tilemap.tilemapTileChanged += OnTilesChanged;
            _dirty = true;
        }

        private void OnTilesChanged(Tilemap source, Tilemap.SyncTile[] changes)
        {
            if (source == _source) _dirty = true;
        }

        private void LateUpdate()
        {
            if (_dirty || transform.localToWorldMatrix != _lastMatrix) RefreshPresentation();
        }

        public void RefreshPresentation()
        {
            EnsureSource();
            if (_source == null || _renderer == null) return;
            _renderer.enabled = false;
            var active = new HashSet<Vector3Int>();
            foreach (var cell in _source.cellBounds.allPositionsWithin)
            {
                var sprite = _source.GetSprite(cell);
                if (sprite == null) continue;
                active.Add(cell);
                if (!_views.TryGetValue(cell, out var view) || view == null)
                {
                    var go = new GameObject("FurnitureDepthView_" + cell.x + "_" + cell.y, typeof(SpriteRenderer));
                    go.transform.SetParent(transform, false);
                    view = go.GetComponent<SpriteRenderer>();
                    _views[cell] = view;
                }
                var anchor = _source.CellToLocalInterpolated((Vector3)cell + _source.tileAnchor);
                var matrix = Matrix4x4.Translate(anchor) * _source.orientationMatrix * _source.GetTransformMatrix(cell);
                view.transform.localPosition = matrix.GetColumn(3);
                view.transform.localRotation = matrix.rotation;
                view.transform.localScale = matrix.lossyScale;
                view.sprite = sprite;
                view.color = _source.color * _source.GetColor(cell);
                view.sharedMaterials = _renderer.sharedMaterials;
                view.sortingLayerID = _renderer.sortingLayerID;
                view.sortingOrder = 10000 - Mathf.RoundToInt(_source.GetCellCenterWorld(cell).y * 48f);
                view.enabled = true;
            }
            var stale = new List<Vector3Int>();
            foreach (var pair in _views) if (!active.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (var cell in stale)
            {
                var view = _views[cell];
                if (view != null)
                {
                    view.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(view.gameObject); else DestroyImmediate(view.gameObject);
                }
                _views.Remove(cell);
            }
            _lastMatrix = transform.localToWorldMatrix;
            _dirty = false;
        }

        private void OnDisable()
        {
            Tilemap.tilemapTileChanged -= OnTilesChanged;
            foreach (var view in _views.Values) if (view != null) view.enabled = false;
            if (_renderer != null && _capturedRendererState) _renderer.enabled = _originalRendererEnabled;
        }
    }
}
