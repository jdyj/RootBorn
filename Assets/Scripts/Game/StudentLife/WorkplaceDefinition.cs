using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "Workplace_New", menuName = "Rootborn/Student Life/Part-Time Work/Workplace")]
    public sealed class WorkplaceDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private string _locationId;
        [SerializeField] private string _npcId;
        [SerializeField] private PartTimeWorkDefinition[] _availableWorks = Array.Empty<PartTimeWorkDefinition>();

        public string LocationId => string.IsNullOrEmpty(_locationId) ? string.Empty : _locationId;
        public string NpcId => string.IsNullOrEmpty(_npcId) ? string.Empty : _npcId;
        public IReadOnlyList<PartTimeWorkDefinition> AvailableWorks => _availableWorks;

        public void ConfigureForTests(string id, string displayNameKey, string locationId, string npcId, PartTimeWorkDefinition[] availableWorks)
        {
            ConfigureForTests(id, displayNameKey);
            _locationId = string.IsNullOrEmpty(locationId) ? string.Empty : locationId;
            _npcId = string.IsNullOrEmpty(npcId) ? string.Empty : npcId;
            _availableWorks = availableWorks ?? Array.Empty<PartTimeWorkDefinition>();
        }
    }
}
