using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
namespace Rootborn.Game.Placement
{
    // Captures authored geometry in the original grid coordinates; never writes to source layers.
    public sealed class AuthoredHousePlacementModel
    {
        private readonly HashSet<Vector3Int> _ground;
        private readonly HashSet<Vector3Int> _blocked;
        private HashSet<Vector3Int> _solid;
        private Vector3Int[] _approaches;
        public BoundsInt Bounds { get; }
        public Vector3Int Spawn { get; }
        public IReadOnlyCollection<Vector3Int> GroundCells => new List<Vector3Int>(_ground);
        private AuthoredHousePlacementModel(HashSet<Vector3Int> ground, HashSet<Vector3Int> blocked, BoundsInt bounds, Vector3Int spawn)
        { _ground=ground; _blocked=blocked; Bounds=bounds; Spawn=spawn; }
        public static AuthoredHousePlacementModel Capture(Tilemap ground, Tilemap collision, Vector3Int[] fixedCells, Vector3Int spawn, Vector3Int[] approaches)
        {
            if(ground==null) throw new ArgumentNullException(nameof(ground));
            var cells=new HashSet<Vector3Int>(); var blocked=new HashSet<Vector3Int>();
            foreach(var cell in ground.cellBounds.allPositionsWithin) if(ground.HasTile(cell)) cells.Add(cell);
            if(collision!=null) foreach(var cell in collision.cellBounds.allPositionsWithin) if(collision.HasTile(cell)) blocked.Add(cell);
            if(fixedCells!=null) foreach(var cell in fixedCells) blocked.Add(cell);
            var solid=new HashSet<Vector3Int>(blocked);
            blocked.Add(spawn);
            if(approaches!=null) foreach(var cell in approaches) blocked.Add(cell);
            return new AuthoredHousePlacementModel(cells,blocked,ground.cellBounds,spawn){_solid=solid,_approaches=approaches==null?Array.Empty<Vector3Int>():(Vector3Int[])approaches.Clone()};
        }
        public bool PreservesAccess(Vector3Int[] additionalBlocking)
        {
            var blocks=new HashSet<Vector3Int>(_solid);
            if(additionalBlocking!=null) foreach(var cell in additionalBlocking) blocks.Add(cell);
            var before=Reachable(_solid);var after=Reachable(blocks);
            if(before.Contains(Spawn) && !after.Contains(Spawn))return false;
            foreach(var point in _approaches) if(before.Contains(point) && !after.Contains(point))return false;
            return true;
        }
        private HashSet<Vector3Int> Reachable(HashSet<Vector3Int> blocks)
        {
            var visited=new HashSet<Vector3Int>();if(!_ground.Contains(Spawn)||blocks.Contains(Spawn))return visited;
            var queue=new Queue<Vector3Int>();queue.Enqueue(Spawn);visited.Add(Spawn);
            var dirs=new[]{Vector3Int.up,Vector3Int.right,Vector3Int.down,Vector3Int.left};
            while(queue.Count>0){var cell=queue.Dequeue();foreach(var direction in dirs){var next=cell+direction;if(_ground.Contains(next)&&!blocks.Contains(next)&&visited.Add(next))queue.Enqueue(next);}}
            return visited;
        }
        public bool CanOccupy(Vector3Int[] cells)
        {
            if(cells==null || cells.Length==0) return false;
            foreach(var cell in cells) if(!_ground.Contains(cell) || _blocked.Contains(cell)) return false;
            return true;
        }
    }
}
