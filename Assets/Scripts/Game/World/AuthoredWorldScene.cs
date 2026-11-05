using System;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.World
{
    [DisallowMultipleComponent]
    public sealed class AuthoredWorldScene : MonoBehaviour
    {
        [SerializeField] private LocationDefinition[] _locations = Array.Empty<LocationDefinition>();
        [SerializeField] private Transform[] _locationAnchors = Array.Empty<Transform>();
        [SerializeField] private LocationDefinition[] _npcLocations = Array.Empty<LocationDefinition>();
        [SerializeField] private Transform[] _npcAnchors = Array.Empty<Transform>();
        // Separate tile colliders catch a player sliding along a wall at every seam. Merged when the scene loads:
        // the saved scene's collider layout is pinned by reviewed evidence.
        public static void MergeCollisionTilemaps(Scene scene)
        {
            if (!scene.IsValid()) return;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var collider in root.GetComponentsInChildren<UnityEngine.Tilemaps.TilemapCollider2D>(true))
            {
                if (collider.compositeOperation == Collider2D.CompositeOperation.Merge) continue;
                var body = collider.GetComponent<Rigidbody2D>();
                if (body == null) body = collider.gameObject.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
                var composite = collider.GetComponent<CompositeCollider2D>();
                if (composite == null) composite = collider.gameObject.AddComponent<CompositeCollider2D>();
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                collider.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
        }

        // The saved 512x288 frame only fits some windows; at 1080p it left wide black bars at 3x. The nearest whole zoom with a
        // frame cut to the window keeps pixels crisp and shows nearly the same amount of the world.
        public static Vector2Int FitPixelFrame(int width, int height, int referenceHeight)
        {
            int zoom = Mathf.Max(1, Mathf.RoundToInt(height / (float)Mathf.Max(1, referenceHeight)));
            return new Vector2Int(width / zoom, height / zoom);
        }

        private UnityEngine.Rendering.Universal.PixelPerfectCamera _pixelCamera;
        private int _savedFrameHeight, _fittedWidth, _fittedHeight;

        private void Start()
        {
            var main = Camera.main;
            _pixelCamera = main != null && main.gameObject.scene == gameObject.scene ? main.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>() : null;
            if (_pixelCamera != null) _savedFrameHeight = _pixelCamera.refResolutionY;
            else enabled = false;
        }

        private void LateUpdate()
        {
            if (_pixelCamera == null || (Screen.width == _fittedWidth && Screen.height == _fittedHeight)) return;
            _fittedWidth = Screen.width;
            _fittedHeight = Screen.height;
            var frame = FitPixelFrame(_fittedWidth, _fittedHeight, _savedFrameHeight);
            _pixelCamera.refResolutionX = frame.x;
            _pixelCamera.refResolutionY = frame.y;
        }

        public static AuthoredWorldScene Find(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var config = root.GetComponentInChildren<AuthoredWorldScene>(true);
                if (config != null) return config;
            }
            return null;
        }
        public void ConfigureLocations(LocationDefinition[] locations, Transform[] anchors)
        {
            _locations = locations ?? Array.Empty<LocationDefinition>();
            _locationAnchors = anchors ?? Array.Empty<Transform>();
        }
        public void ConfigureNpcSlots(LocationDefinition[] locations, Transform[] anchors)
        {
            _npcLocations = locations ?? Array.Empty<LocationDefinition>();
            _npcAnchors = anchors ?? Array.Empty<Transform>();
        }
        public bool ContainsLocation(LocationDefinition location) => TryGetLocationAnchor(location,out _);
        public bool TryGetLocationAnchor(LocationDefinition location,out Transform anchor)
        {
            anchor = null;
            if (location == null || _locations == null || _locationAnchors == null || _locations.Length != _locationAnchors.Length) return false;
            int found = -1;
            for (int i=0;i<_locations.Length;i++)
            {
                if (!SameLocation(_locations[i],location)) continue;
                if (found >= 0) return false;
                found = i;
            }
            if (found < 0 || !IsLocal(_locationAnchors[found])) return false;
            anchor = _locationAnchors[found]; return true;
        }
        public bool TryGetLocationPosition(LocationDefinition location,out Vector2 position)
        {
            position = default;
            if (!TryGetLocationAnchor(location,out var anchor)) return false;
            position = anchor.position; return true;
        }
        public bool TryGetNpcPosition(LocationDefinition location,int slot,out Vector2 position)
        {
            position = default;
            if (!ContainsLocation(location) || slot < 0 || _npcLocations == null || _npcAnchors == null || _npcLocations.Length != _npcAnchors.Length) return false;
            int current=0;
            for (int i=0;i<_npcLocations.Length;i++)
            {
                if (!SameLocation(_npcLocations[i],location)) continue;
                if (current++ != slot) continue;
                if (!IsLocal(_npcAnchors[i])) return false;
                position = _npcAnchors[i].position; return true;
            }
            return false;
        }
        // Built-in scenes and the Addressables registry hold separate copies of the same asset outside the editor database.
        private static bool SameLocation(LocationDefinition a,LocationDefinition b) => a == b || (a != null && b != null && string.Equals(a.Id,b.Id,StringComparison.Ordinal));
        private bool IsLocal(Transform value) => value != null && value.gameObject.scene == gameObject.scene;
    }
}
