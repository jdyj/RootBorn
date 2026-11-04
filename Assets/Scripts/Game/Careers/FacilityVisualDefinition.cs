using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.Careers
{
    [CreateAssetMenu(menuName="Rootborn/Careers/Facility Visual")]
    public sealed class FacilityVisualDefinition : ScriptableObject
    {
        [SerializeField] private FacilityDefinition _facility;
        [SerializeField] private int _level = 1;
        [SerializeField] private string _spriteAddress;
        [SerializeField] private Vector2Int _siteSize;
        [SerializeField] private Vector2 _spriteOffset;
        [SerializeField] private Vector2 _entranceOffset;
        [SerializeField] private Vector2Int[] _collisionCells = Array.Empty<Vector2Int>();
        [SerializeField] private Vector2Int[] _entranceCells = Array.Empty<Vector2Int>();
        [SerializeField] private Vector2Int[] _approachCells = Array.Empty<Vector2Int>();
        public FacilityDefinition Facility => _facility;
        public int Level => _level;
        public string SpriteAddress => _spriteAddress;
        public Vector2Int SiteSize => _siteSize;
        public Vector2 SpriteOffset => _spriteOffset;
        public Vector2 EntranceLocalPosition => _spriteOffset + _entranceOffset;
        public IReadOnlyList<Vector2Int> CollisionCells => _collisionCells ?? Array.Empty<Vector2Int>();
        public IReadOnlyList<Vector2Int> EntranceCells => _entranceCells ?? Array.Empty<Vector2Int>();
        public IReadOnlyList<Vector2Int> ApproachCells => _approachCells ?? Array.Empty<Vector2Int>();
        public bool IsValid
        {
            get
            {
                if (_facility == null || _level < 1 || _level > 3 || string.IsNullOrWhiteSpace(_spriteAddress)
                    || _siteSize.x <= 0 || _siteSize.y <= 0 || EntranceCells.Count == 0 || ApproachCells.Count == 0
                    || !Finite(_spriteOffset) || !Finite(_entranceOffset)) return false;
                var occupied = new HashSet<Vector2Int>();
                foreach (var cell in CollisionCells) if (!Inside(cell) || !occupied.Add(cell)) return false;
                foreach (var cell in EntranceCells) if (!Inside(cell) || occupied.Contains(cell)) return false;
                foreach (var cell in ApproachCells) if (occupied.Contains(cell)) return false;
                return true;
            }
        }
        private bool Inside(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < _siteSize.x && cell.y < _siteSize.y;
        private static bool Finite(Vector2 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
    }
}
