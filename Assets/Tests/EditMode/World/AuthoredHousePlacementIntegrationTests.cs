using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Placement;
using Rootborn.Game.Interiors;
using Rootborn.Game.World;
using Rootborn.UI.Interiors;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
namespace Rootborn.Tests.EditMode.World
{
    public sealed class AuthoredHousePlacementIntegrationTests
    {
        private Scene _scene; private Tilemap _ground,_collision,_objects,_occupancy; private Tile _tile;private FurnitureDefinition _chair;private TilePlacementSurface _surface;
        [SetUp] public void SetUp()
        {
            _scene=EditorSceneManager.NewPreviewScene();_scene.name="House";
            var grid=Go("Grid");grid.AddComponent<Grid>();_ground=Map(grid,"Ground");_collision=Map(grid,"Collision");_objects=Map(grid,"UserObjects");_occupancy=Map(grid,"UserOccupancy");
            _tile=ScriptableObject.CreateInstance<Tile>();for(int x=0;x<5;x++)for(int y=0;y<3;y++)_ground.SetTile(new Vector3Int(x,y,0),_tile);
            _collision.SetTile(new Vector3Int(2,1,0),_tile);
            _surface=_objects.gameObject.AddComponent<TilePlacementSurface>();_surface.ConfigureForTests("house",_ground,_objects,_occupancy,null,new BoundsInt(0,0,0,5,3,1),TilePlacementRuleFlags.RequireGround|TilePlacementRuleFlags.RejectOccupiedCells|TilePlacementRuleFlags.StayInsideBounds);
            _chair=FurnitureDefinition.CreateForTests("chair","Chair","Seating",new[]{"house"},new[]{new FurnitureTilePart(Vector2Int.zero,_tile)},new[]{Vector2Int.zero},true,FurniturePlacementPreference.Any);
        }
        private GameObject Go(string n){var go=new GameObject(n);SceneManager.MoveGameObjectToScene(go,_scene);return go;}
        private Tilemap Map(GameObject grid,string n){var go=Go(n);go.transform.SetParent(grid.transform,false);return go.AddComponent<Tilemap>();}
        [TearDown] public void TearDown(){EditorSceneManager.ClosePreviewScene(_scene);UnityEngine.Object.DestroyImmediate(_chair);UnityEngine.Object.DestroyImmediate(_tile);}
        private object Session()
        {
            var assembly=typeof(TilePlacementSurface).Assembly;var modelType=assembly.GetType("Rootborn.Game.Placement.AuthoredHousePlacementModel");Assert.That(modelType,Is.Not.Null);
            var model=modelType.GetMethod("Capture").Invoke(null,new object[]{_ground,_collision,Array.Empty<Vector3Int>(),Vector3Int.zero,new[]{new Vector3Int(4,0,0)}});
            var type=assembly.GetType("Rootborn.Game.Placement.AuthoredHouseFurnitureSession");Assert.That(type,Is.Not.Null,"Scene-owned authored session must preserve base layers and retained records.");
            var session=Go("Session").AddComponent(type);Invoke(session,"Bind",model,_surface);return session;
        }
        private object Invoke(object target,string method,params object[] args)
        {var m=target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.Public);Assert.That(m,Is.Not.Null,method);return m.Invoke(target,args);}
        private FurniturePlacementSaveData Save(string id,int x,int y)=>new FurniturePlacementSaveData{FurnitureId=id,SurfaceId="house",AnchorX=x,AnchorY=y,Direction=FurniturePlacementDirection.North,StateJson="{\"quality\":42}",TileName="retained-original"};
        private void Restore(object session,params FurniturePlacementSaveData[] records){Invoke(session,"Restore",records,new[]{_chair});}
        private FurniturePlacementSaveData[] Export(object session)=>(FurniturePlacementSaveData[])Invoke(session,"BuildSaveRecords");
        [Test] public void Restore_OnlyWritesDedicatedUserLayers()
        {var session=Session();Restore(session,Save("chair",1,1));Assert.That(_objects.GetTile(new Vector3Int(1,1,0)),Is.SameAs(_tile));Assert.That(_collision.GetTile(new Vector3Int(2,1,0)),Is.SameAs(_tile));Assert.That(_ground.GetUsedTilesCount(),Is.EqualTo(1));Assert.That(_ground.HasTile(new Vector3Int(1,1,0)),Is.True);}
        [Test] public void RestoreAndExport_RetainsUnknownAndBlockedRecordsWithMetadata()
        {var session=Session();Restore(session,Save("chair",1,1),Save("missing",3,1),Save("chair",2,1));var records=Export(session);Assert.That(records.Length,Is.EqualTo(3));Assert.That(records.All(r=>r.StateJson=="{\"quality\":42}" && r.TileName=="retained-original"),Is.True);}
        [Test] public void RepeatedRestore_DoesNotDuplicateUserFurniture()
        {var session=Session();var records=new[]{Save("chair",1,1)};Restore(session,records);Restore(session,records);Assert.That(Export(session).Length,Is.EqualTo(1));Assert.That(_objects.GetTile(new Vector3Int(1,1,0)),Is.SameAs(_tile));}
        [Test] public void ClearUserFurniture_PreservesBaseAndUnresolvedRecords()
        {var session=Session();Restore(session,Save("chair",1,1),Save("missing",3,1));Invoke(session,"ClearUserFurniture");Assert.That(_objects.GetUsedTilesCount(),Is.Zero);Assert.That(_collision.GetTile(new Vector3Int(2,1,0)),Is.SameAs(_tile));Assert.That(_ground.GetTile(new Vector3Int(1,1,0)),Is.SameAs(_tile));Assert.That(Export(session).Single().FurnitureId,Is.EqualTo("missing"));}
        [Test] public void RestoredBlockingFurniture_HasDedicatedPhysicalCollision()
        {
            var session=Session();Restore(session,Save("chair",1,1));
            var property=session.GetType().GetProperty("BlockingTilemap");Assert.That(property,Is.Not.Null);
            var map=property.GetValue(session) as Tilemap;Assert.That(map,Is.Not.Null);Assert.That(map.GetComponent<TilemapCollider2D>(),Is.Not.Null);
            Assert.That(map.HasTile(new Vector3Int(1,1,0)),Is.True);Invoke(session,"ClearUserFurniture");Assert.That(map.HasTile(new Vector3Int(1,1,0)),Is.False);
        }
        [Test] public void ManualPlacement_RejectsAuthoredCollision()
        {var session=Session();Assert.That((bool)Invoke(session,"TryPlace",_chair,new Vector3Int(2,1,0),FurniturePlacementDirection.North),Is.False);Assert.That(_collision.GetTile(new Vector3Int(2,1,0)),Is.SameAs(_tile));}
        [Test] public void OverlayManualPlacement_UsesSessionWithoutGeneratedMapOrBaseApply()
        {
            var session=Session();var overlay=Go("Preview").AddComponent<InteriorPlacementPreviewOverlay>();
            var bind=typeof(InteriorPlacementPreviewOverlay).GetMethod("BindAuthoredSession",BindingFlags.Public|BindingFlags.Instance);Assert.That(bind,Is.Not.Null);bind.Invoke(overlay,new[]{session});
            var furniture=new InteriorFurnitureDefinition("chair","Chair",InteriorObjectKind.Chair,true,InteriorPlacementPreference.AvoidCorridor,new[]{new InteriorFurnitureTile(Vector2Int.zero,_tile)},new[]{Vector2Int.zero},_chair);
            overlay.RefreshFurniture(null,null,null,furniture);
            Assert.That(overlay.TryManualPlaceFirstValidForTests(out var message),Is.True,message);
            Assert.That(Export(session).Length,Is.EqualTo(1));Assert.That(_collision.GetTile(new Vector3Int(2,1,0)),Is.SameAs(_tile));Assert.That(_ground.GetTile(new Vector3Int(1,1,0)),Is.SameAs(_tile));
        }
        [Test] public void ClosingPreview_DoesNotDiscardSessionRecords()
        {
            var session=Session();Restore(session,Save("chair",1,1),Save("missing",3,1));var overlay=Go("Preview").AddComponent<InteriorPlacementPreviewOverlay>();
            var bind=typeof(InteriorPlacementPreviewOverlay).GetMethod("BindAuthoredSession",BindingFlags.Public|BindingFlags.Instance);Assert.That(bind,Is.Not.Null);bind.Invoke(overlay,new[]{session});
            UnityEngine.Object.DestroyImmediate(overlay.gameObject);Assert.That(Export(session).Length,Is.EqualTo(2));Assert.That(_objects.GetTile(new Vector3Int(1,1,0)),Is.SameAs(_tile));
        }
        [Test] public void MoveRetainsMetadataAndClearsOldCollisionOnlyAfterCommit()
        {var session=Session();Restore(session,Save("chair",1,1));var typed=(AuthoredHouseFurnitureSession)session;var instance=typed.Registry.Instances.Single();
        var method=typed.GetType().GetMethod("TryMove");Assert.That(method,Is.Not.Null);
        Assert.That((bool)method.Invoke(typed,new object[]{instance.InstanceId,new Vector3Int(3,1,0),FurniturePlacementDirection.East}),Is.True);
        Assert.That(Export(session).Single().StateJson,Is.EqualTo("{\"quality\":42}"));Assert.That(Export(session).Single().AnchorX,Is.EqualTo(3));
        Assert.That(typed.BlockingTilemap.HasTile(new Vector3Int(1,1,0)),Is.False);Assert.That(typed.BlockingTilemap.HasTile(new Vector3Int(3,1,0)),Is.True);}
        [Test] public void RejectedMoveKeepsOldVisualCollisionAndRecord()
        {var session=Session();Restore(session,Save("chair",1,1));var typed=(AuthoredHouseFurnitureSession)session;var instance=typed.Registry.Instances.Single();
        var method=typed.GetType().GetMethod("TryMove");Assert.That(method,Is.Not.Null);
        Assert.That((bool)method.Invoke(typed,new object[]{instance.InstanceId,new Vector3Int(2,1,0),FurniturePlacementDirection.North}),Is.False);
        Assert.That(_objects.GetTile(new Vector3Int(1,1,0)),Is.SameAs(_tile));Assert.That(Export(session).Single().AnchorX,Is.EqualTo(1));}
        [Test] public void FailedMoveWriteDoesNotPublishCandidatePlacement()
        {var session=Session();Restore(session,Save("chair",1,1));var typed=(AuthoredHouseFurnitureSession)session;var instance=typed.Registry.Instances.Single();typed.ConfigureCommit(()=>true,records=>false);
        var method=typed.GetType().GetMethod("TryMove");Assert.That(method,Is.Not.Null);
        Assert.That((bool)method.Invoke(typed,new object[]{instance.InstanceId,new Vector3Int(3,1,0),FurniturePlacementDirection.North}),Is.False);
        Assert.That(typed.BlockingTilemap.HasTile(new Vector3Int(1,1,0)),Is.True);Assert.That(Export(session).Single().AnchorX,Is.EqualTo(1));}
        [Test] public void AuthoredRoom_RegenerateActionIsUnavailable()
        {Go("AuthoredWorld").AddComponent<AuthoredWorldScene>();var m=typeof(InteriorFurniturePlacementPanel).GetMethod("CanRegenerateRoom",BindingFlags.Public|BindingFlags.Static);Assert.That(m,Is.Not.Null);Assert.That((bool)m.Invoke(null,new object[]{_scene}),Is.False);}
        [Test] public void LegacyRoom_RegenerateActionRemainsAvailable()
        {var m=typeof(InteriorFurniturePlacementPanel).GetMethod("CanRegenerateRoom",BindingFlags.Public|BindingFlags.Static);Assert.That(m,Is.Not.Null);Assert.That((bool)m.Invoke(null,new object[]{_scene}),Is.True);}
        [Test] public void PaletteSelection_InAuthoredRoom_CreatesPreviewWithoutGeneratedMap()
        {
            Session();
            var panel=Go("PlacementPanel").AddComponent<InteriorFurniturePlacementPanel>();
            var furniture=new InteriorFurnitureDefinition("chair","Chair",InteriorObjectKind.Chair,true,InteriorPlacementPreference.Any,new[]{new InteriorFurnitureTile(Vector2Int.zero,_tile)},new[]{Vector2Int.zero},_chair);
            Assert.IsNull(panel.Model.CurrentMap);
            typeof(InteriorFurniturePlacementPanel).GetMethod("SelectFurnitureDefinition",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,new object[]{furniture});
            var overlay=_scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<InteriorPlacementPreviewOverlay>(true)).SingleOrDefault();
            Assert.IsNotNull(overlay,"An authored room uses its scene-owned session rather than a generated room map.");
            Assert.Greater(overlay.ValidCellCount,0);
        }
    }
}
