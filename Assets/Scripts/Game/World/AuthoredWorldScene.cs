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
                if (_locations[i] != location) continue;
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
                if (_npcLocations[i] != location) continue;
                if (current++ != slot) continue;
                if (!IsLocal(_npcAnchors[i])) return false;
                position = _npcAnchors[i].position; return true;
            }
            return false;
        }
        private bool IsLocal(Transform value) => value != null && value.gameObject.scene == gameObject.scene;
    }
}
