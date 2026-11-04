using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
namespace Rootborn.Game.Placement
{
    public sealed class AuthoredHouseFurnitureSession : MonoBehaviour
    {
        private AuthoredHousePlacementModel _model;
        private FurniturePlacementRegistry _registry = new FurniturePlacementRegistry();
        private readonly Dictionary<string, FurniturePlacementSaveData> _records = new Dictionary<string, FurniturePlacementSaveData>();
        private readonly Dictionary<string, FurnitureDefinition> _definitions = new Dictionary<string, FurnitureDefinition>();
        private readonly List<FurniturePlacementSaveData> _unresolved = new List<FurniturePlacementSaveData>();
        private Tile _blockingTile;
        private Func<bool> _canMutate;
        private Func<bool> _reload;
        private Func<FurniturePlacementSaveData[], bool> _commit;
        public event Action<FurnitureDefinition> PlacementCommitted;
        public TilePlacementSurface Surface { get; private set; }
        public Tilemap BlockingTilemap { get; private set; }
        public AuthoredHousePlacementModel Model => _model;
        public FurniturePlacementRegistry Registry => _registry;
        public int UnresolvedCount => _unresolved.Count;
        public bool CanMutate => _canMutate == null || _canMutate();
        public void ConfigureCommit(Func<bool> canMutate, Func<FurniturePlacementSaveData[], bool> commit, Func<bool> reload=null)
        { _canMutate=canMutate; _commit=commit;_reload=reload; }
        public void Bind(AuthoredHousePlacementModel model, TilePlacementSurface surface)
        {
            if(model==null || surface==null || surface.ObjectTilemap==null || surface.OccupancyTilemap==null
                || surface.ObjectTilemap==surface.GroundTilemap || surface.OccupancyTilemap==surface.GroundTilemap
                || surface.ObjectTilemap==surface.OccupancyTilemap || surface.gameObject.scene!=gameObject.scene)
                throw new ArgumentException("Dedicated same-scene furniture layers are required.");
            if(Surface!=null && Surface!=surface)throw new InvalidOperationException("A live furniture session cannot change surfaces.");
            _model=model;Surface=surface;
            if(BlockingTilemap==null)
            {
                var go=new GameObject("AuthoredFurnitureCollision",typeof(Tilemap),typeof(TilemapCollider2D));
                SceneManager.MoveGameObjectToScene(go,gameObject.scene);
                go.transform.SetParent(surface.ObjectTilemap.transform.parent,false);
                BlockingTilemap=go.GetComponent<Tilemap>();
                _blockingTile=ScriptableObject.CreateInstance<Tile>();_blockingTile.name="AuthoredFurnitureBlocking";
                _blockingTile.colliderType=Tile.ColliderType.Grid;
            }
        }
        public void Restore(FurniturePlacementSaveData[] records,FurnitureDefinition[] catalog)
        {
            var plan=AuthoredHouseFurnitureReplay.Plan(_model,Surface.SurfaceId,records,catalog);
            ResetPlaced();_unresolved.Clear();
            foreach(var entry in plan.Unresolved) _unresolved.Add(AuthoredFurnitureReplayEntry.Clone(entry.Record));
            foreach(var entry in plan.Accepted)
            {
                if(_registry.TryPlace(Surface,entry.Definition,entry.Record.ToAnchorCell(),entry.Record.Direction,out var instance,out _))
                { _records.Add(instance.InstanceId,AuthoredFurnitureReplayEntry.Clone(entry.Record));_definitions.Add(instance.InstanceId,entry.Definition); }
                else _unresolved.Add(AuthoredFurnitureReplayEntry.Clone(entry.Record));
            }
            RebuildCollision();
        }
        public bool CanPlace(FurnitureDefinition definition,Vector3Int anchor,FurniturePlacementDirection direction)
        {
            if(_model==null || Surface==null || definition==null || string.IsNullOrWhiteSpace(definition.StableId)
                || !Enum.IsDefined(typeof(FurniturePlacementDirection),direction))return false;
            if(definition.AllowedSurfaceIds.Count>0 && !definition.AllowedSurfaceIds.Contains(Surface.SurfaceId))return false;
            var cells=definition.GetFootprintCells(direction).Select(p=>anchor+new Vector3Int(p.x,p.y,0)).ToArray();
            if(cells.Any(p=>!Surface.Bounds.Contains(p)||!Surface.GroundTilemap.HasTile(p)))return false;
            if(!_model.CanOccupy(cells) || cells.Any(p=>Surface.OccupancyTilemap.HasTile(p)))return false;
            // Visual tile parts must not overwrite another object's tiles outside its footprint.
            if(definition.GetTileParts(direction).Any(p=>p.Tile==null || Surface.ObjectTilemap.HasTile(anchor+new Vector3Int(p.LocalCell.x,p.LocalCell.y,0))))return false;
            var blocked=_registry.Instances.Where(i=>_definitions[i.InstanceId].BlocksMovement).SelectMany(i=>i.OccupiedCells);
            return !definition.BlocksMovement || _model.PreservesAccess(blocked.Concat(cells).ToArray());
        }
        public bool TryPlace(FurnitureDefinition definition,Vector3Int anchor,FurniturePlacementDirection direction)
        {
            if(!CanMutate || !CanPlace(definition,anchor,direction))return false;
            var record=new FurniturePlacementSaveData{FurnitureId=definition.StableId,SurfaceId=Surface.SurfaceId,AnchorX=anchor.x,AnchorY=anchor.y,Direction=direction};
            if(!Commit(BuildSaveRecords().Concat(new[]{record}).ToArray()))return false;
            if(!_registry.TryPlace(Surface,definition,anchor,direction,out var instance,out _))return false;
            _records.Add(instance.InstanceId,record);_definitions.Add(instance.InstanceId,definition);RebuildCollision();
            PlacementCommitted?.Invoke(definition);return true;
        }
        public bool TryMove(string instanceId,Vector3Int anchor,FurniturePlacementDirection direction)
        {
            if(!CanMutate || !_records.TryGetValue(instanceId,out var oldRecord))return false;
            var record=AuthoredFurnitureReplayEntry.Clone(oldRecord);record.AnchorX=anchor.x;record.AnchorY=anchor.y;record.Direction=direction;
            var records=_records.Where(p=>p.Key!=instanceId).Select(p=>p.Value).Concat(new[]{record}).ToArray();
            var definitions=_definitions.Values.Distinct().ToArray();
            var plan=AuthoredHouseFurnitureReplay.Plan(_model,Surface.SurfaceId,records,definitions);
            if(plan.Accepted.Count!=records.Length)return false;
            var original=_registry.Instances.First(i=>i.InstanceId==instanceId);
            var definition=_definitions[instanceId];
            if(definition.GetTileParts(direction).Any(p=>p.Tile==null || (Surface.ObjectTilemap.HasTile(anchor+new Vector3Int(p.LocalCell.x,p.LocalCell.y,0)) && !original.TileCells.Contains(anchor+new Vector3Int(p.LocalCell.x,p.LocalCell.y,0)))))return false;
            var before=BuildSaveRecords();if(!Commit(records.Concat(_unresolved).Select(AuthoredFurnitureReplayEntry.Clone).ToArray()))return false;
            if(!_registry.TryMove(Surface,instanceId,anchor,direction,out _,out _)){Commit(before);return false;}
            _records[instanceId]=record;RebuildCollision();return true;
        }
        public FurniturePlacementSaveData[] BuildSaveRecords()
        { return _records.Values.Concat(_unresolved).Select(AuthoredFurnitureReplayEntry.Clone).ToArray(); }
        public bool Reload() => CanMutate && _reload!=null && _reload();
        public bool Save() => CanMutate && Commit(BuildSaveRecords());
        public bool ClearUserFurniture()
        {
            if(!CanMutate || !Commit(_unresolved.Select(AuthoredFurnitureReplayEntry.Clone).ToArray()))return false;
            ResetPlaced();return true;
        }
        public bool Delete(string instanceId)
        {
            if(!CanMutate || !_records.ContainsKey(instanceId))return false;
            var next=_records.Where(p=>p.Key!=instanceId).Select(p=>p.Value).Concat(_unresolved).Select(AuthoredFurnitureReplayEntry.Clone).ToArray();
            if(!Commit(next) || !_registry.TryDelete(Surface,instanceId))return false;
            _records.Remove(instanceId);_definitions.Remove(instanceId);RebuildCollision();return true;
        }
        private bool Commit(FurniturePlacementSaveData[] records) => _commit==null || _commit(records);
        private void ResetPlaced()
        { _registry.ClearSurface(Surface);_records.Clear();_definitions.Clear();BlockingTilemap.ClearAllTiles(); }
        private void RebuildCollision()
        {
            BlockingTilemap.ClearAllTiles();
            foreach(var instance in _registry.Instances)if(_definitions[instance.InstanceId].BlocksMovement)
                foreach(var cell in instance.OccupiedCells)BlockingTilemap.SetTile(cell,_blockingTile);
            BlockingTilemap.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
        }
        private void OnDestroy()
        { if(_blockingTile!=null){if(Application.isPlaying)Destroy(_blockingTile);else DestroyImmediate(_blockingTile);} }
    }
}
