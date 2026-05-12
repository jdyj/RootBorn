using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "Location_New", menuName = "Rootborn/Student Life/Exploration/Location")]
    public sealed class LocationDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private Vector2 _worldPosition;
        [SerializeField] private DiscoveryDefinition[] _discoveries = Array.Empty<DiscoveryDefinition>();

        public Vector2 WorldPosition => _worldPosition;
        public IReadOnlyList<DiscoveryDefinition> Discoveries => _discoveries;

        public void ConfigureForTests(string id, string displayNameKey, Vector2 worldPosition, DiscoveryDefinition[] discoveries)
        {
            ConfigureForTests(id, displayNameKey);
            _worldPosition = worldPosition;
            _discoveries = discoveries ?? Array.Empty<DiscoveryDefinition>();
        }
    }
}
