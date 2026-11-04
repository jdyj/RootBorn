using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace Rootborn.Game.Placement
{
    public enum AuthoredFurnitureReplayFailure { None, MissingDefinition, WrongSurface, Blocked, DuplicateFootprint, AmbiguousDefinition, InvalidDirection }
    public sealed class AuthoredFurnitureReplayEntry
    {
        public int SourceIndex { get; }
        public FurniturePlacementSaveData Record { get; }
        public FurnitureDefinition Definition { get; }
        public AuthoredFurnitureReplayFailure Reason { get; }
        public AuthoredFurnitureReplayEntry(int index,FurniturePlacementSaveData record,FurnitureDefinition definition,AuthoredFurnitureReplayFailure reason)
        {SourceIndex=index;Record=Clone(record);Definition=definition;Reason=reason;}
        public static FurniturePlacementSaveData Clone(FurniturePlacementSaveData record)
        {return record==null?null:new FurniturePlacementSaveData{FurnitureId=record.FurnitureId,SurfaceId=record.SurfaceId,AnchorX=record.AnchorX,AnchorY=record.AnchorY,Direction=record.Direction,StateJson=record.StateJson,TileName=record.TileName};}
    }
    public sealed class AuthoredFurnitureReplayPlan
    {
        public IReadOnlyList<AuthoredFurnitureReplayEntry> Accepted { get; }
        public IReadOnlyList<AuthoredFurnitureReplayEntry> Unresolved { get; }
        internal AuthoredFurnitureReplayPlan(List<AuthoredFurnitureReplayEntry> accepted,List<AuthoredFurnitureReplayEntry> unresolved)
        {Accepted=accepted.AsReadOnly();Unresolved=unresolved.AsReadOnly();}
    }
    public static class AuthoredHouseFurnitureReplay
    {
        public static AuthoredFurnitureReplayPlan Plan(AuthoredHousePlacementModel model,string surfaceId,FurniturePlacementSaveData[] records,FurnitureDefinition[] catalog)
        {
            if(model==null) throw new ArgumentNullException(nameof(model));
            var accepted=new List<AuthoredFurnitureReplayEntry>();var unresolved=new List<AuthoredFurnitureReplayEntry>();var occupied=new HashSet<Vector3Int>();
            var definitions=new Dictionary<string,FurnitureDefinition>(StringComparer.Ordinal);
            var ambiguous=new HashSet<string>(StringComparer.Ordinal);var blocking=new HashSet<Vector3Int>();
            if(catalog!=null) foreach(var definition in catalog) if(definition!=null && !string.IsNullOrWhiteSpace(definition.StableId)){if(definitions.ContainsKey(definition.StableId))ambiguous.Add(definition.StableId);else definitions.Add(definition.StableId,definition);}
            if(records!=null) for(int i=0;i<records.Length;i++)
            {
                var record=records[i];if(record==null) continue;
                definitions.TryGetValue(record.FurnitureId??string.Empty,out var definition);
                var reason=AuthoredFurnitureReplayFailure.None; Vector3Int[] cells=null;
                if(!string.Equals(record.SurfaceId,surfaceId,StringComparison.Ordinal)) reason=AuthoredFurnitureReplayFailure.WrongSurface;
                else if(definition==null) reason=AuthoredFurnitureReplayFailure.MissingDefinition;
                else if(ambiguous.Contains(record.FurnitureId)) reason=AuthoredFurnitureReplayFailure.AmbiguousDefinition;
                else if(!Enum.IsDefined(typeof(FurniturePlacementDirection),record.Direction)) reason=AuthoredFurnitureReplayFailure.InvalidDirection;
                else if(definition.AllowedSurfaceIds.Count>0 && !definition.AllowedSurfaceIds.Contains(surfaceId)) reason=AuthoredFurnitureReplayFailure.WrongSurface;
                else
                {
                    var footprint=definition.GetFootprintCells(record.Direction);cells=new Vector3Int[footprint.Count];
                    for(int j=0;j<cells.Length;j++) cells[j]=record.ToAnchorCell()+new Vector3Int(footprint[j].x,footprint[j].y,0);
                    if(!model.CanOccupy(cells)) reason=AuthoredFurnitureReplayFailure.Blocked;
                    else foreach(var cell in cells) if(occupied.Contains(cell)){reason=AuthoredFurnitureReplayFailure.DuplicateFootprint;break;}
                }
                if(reason==AuthoredFurnitureReplayFailure.None && definition.BlocksMovement && !model.PreservesAccess(blocking.Concat(cells).ToArray())) reason=AuthoredFurnitureReplayFailure.Blocked;
                var entry=new AuthoredFurnitureReplayEntry(i,record,definition,reason);
                if(reason==AuthoredFurnitureReplayFailure.None){accepted.Add(entry);foreach(var cell in cells){occupied.Add(cell);if(definition.BlocksMovement)blocking.Add(cell);}}
                else unresolved.Add(entry);
            }
            return new AuthoredFurnitureReplayPlan(accepted,unresolved);
        }
    }
}
