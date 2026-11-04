// Design candidate. Pure replay planning only; no save IO, UI, or scene application.
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Placement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
namespace Rootborn.Tests.EditMode.World
{
    public sealed class AuthoredHouseFurnitureReplayTests
    {
        private Scene _scene; private Tilemap _ground, _collision; private Tile _tile; private FurnitureDefinition _chair; private FurnitureDefinition _extra; private FurnitureDefinition[] _catalog;
        [SetUp] public void SetUp()
        {
            _scene=EditorSceneManager.NewPreviewScene();
            var grid=new GameObject("Grid",typeof(Grid)); SceneManager.MoveGameObjectToScene(grid,_scene);
            _ground=Map(grid,"Ground"); _collision=Map(grid,"Collision"); _tile=ScriptableObject.CreateInstance<Tile>();
            for(int x=0;x<5;x++) for(int y=0;y<3;y++) _ground.SetTile(new Vector3Int(x,y,0),_tile);
            _chair=FurnitureDefinition.CreateForTests("chair","Chair","Seating",new[]{"house"},new[]{new FurnitureTilePart(Vector2Int.zero,_tile)},new[]{Vector2Int.zero},true,FurniturePlacementPreference.Any);
            _catalog=new[]{_chair};
        }
        private Tilemap Map(GameObject p,string n){var go=new GameObject(n,typeof(Tilemap));go.transform.SetParent(p.transform,false);return go.GetComponent<Tilemap>();}
        [TearDown] public void TearDown(){EditorSceneManager.ClosePreviewScene(_scene);UnityEngine.Object.DestroyImmediate(_chair);if(_extra!=null)UnityEngine.Object.DestroyImmediate(_extra);UnityEngine.Object.DestroyImmediate(_tile);}
        private FurniturePlacementSaveData Save(string id,int x,int y) => new FurniturePlacementSaveData{FurnitureId=id,SurfaceId="house",AnchorX=x,AnchorY=y,Direction=FurniturePlacementDirection.North,StateJson="{\"durability\":37}",TileName="original-tile"};
        private object Plan(params FurniturePlacementSaveData[] saves)
        {
            var assembly=typeof(TilePlacementSurface).Assembly;
            var modelType=assembly.GetType("Rootborn.Game.Placement.AuthoredHousePlacementModel"); Assert.That(modelType,Is.Not.Null);
            var capture=modelType.GetMethod("Capture",BindingFlags.Public|BindingFlags.Static);
            Assert.That(capture,Is.Not.Null,"AuthoredHousePlacementModel.Capture is required before replay planning.");
            var model=capture.Invoke(null,new object[]{_ground,_collision,new[]{new Vector3Int(2,1,0)},new Vector3Int(0,0,0),new[]{new Vector3Int(4,0,0)}});
            Assert.That(model,Is.Not.Null,"Capture must return a snapshot.");
            var replay=assembly.GetType("Rootborn.Game.Placement.AuthoredHouseFurnitureReplay");Assert.That(replay,Is.Not.Null,"Replay must preflight and retain unresolved source records before scene mutation.");
            var method=replay.GetMethod("Plan",BindingFlags.Public|BindingFlags.Static);Assert.That(method,Is.Not.Null);
            return method.Invoke(null,new object[]{model,"house",saves,_catalog});
        }
        private object[] Entries(object plan,string property)
        {var p=plan.GetType().GetProperty(property);Assert.That(p,Is.Not.Null);return ((IEnumerable)p.GetValue(plan)).Cast<object>().ToArray();}
        [Test] public void ReplayPlan_AcceptsValidAndRetainsFixedPropConflict()
        {var plan=Plan(Save("chair",1,1),Save("chair",2,1));Assert.That(Entries(plan,"Accepted").Length,Is.EqualTo(1));Assert.That(Entries(plan,"Unresolved").Length,Is.EqualTo(1));}
        [Test] public void ReplayPlan_RetainsUnknownDefinitionInsteadOfDroppingIt()
        {var plan=Plan(Save("missing-chair",1,1));Assert.That(Entries(plan,"Accepted"),Is.Empty);Assert.That(Entries(plan,"Unresolved").Length,Is.EqualTo(1));}
        [Test] public void ReplayPlan_DuplicateFootprintDoesNotOverwriteFirstRecord()
        {var plan=Plan(Save("chair",1,1),Save("chair",1,1));Assert.That(Entries(plan,"Accepted").Length,Is.EqualTo(1));Assert.That(Entries(plan,"Unresolved").Length,Is.EqualTo(1));}
        [Test] public void ReplayPlan_RejectsFurnitureWhoseDefinitionDisallowsHouse()
        {
            _extra=FurnitureDefinition.CreateForTests("outdoor","Outdoor","Outdoor",new[]{"farm"},new[]{new FurnitureTilePart(Vector2Int.zero,_tile)},new[]{Vector2Int.zero},true,FurniturePlacementPreference.Any);
            _catalog=new[]{_chair,_extra};var plan=Plan(Save("outdoor",1,1));Assert.That(Entries(plan,"Accepted"),Is.Empty);Assert.That(Entries(plan,"Unresolved").Length,Is.EqualTo(1));
        }
        [Test] public void ReplayPlan_RejectsAmbiguousDuplicateDefinitionIds()
        {
            _extra=FurnitureDefinition.CreateForTests("chair","Different Chair","Seating",new[]{"house"},new[]{new FurnitureTilePart(Vector2Int.zero,_tile)},new[]{Vector2Int.zero},true,FurniturePlacementPreference.Any);
            _catalog=new[]{_chair,_extra};var plan=Plan(Save("chair",1,1));Assert.That(Entries(plan,"Accepted"),Is.Empty);Assert.That(Entries(plan,"Unresolved").Length,Is.EqualTo(1));
        }
        [Test] public void ReplayPlan_RejectsUndefinedDirectionInsteadOfTreatingItAsNorth()
        {var record=Save("chair",1,1);record.Direction=(FurniturePlacementDirection)99;var plan=Plan(record);Assert.That(Entries(plan,"Accepted"),Is.Empty);Assert.That(Entries(plan,"Unresolved").Length,Is.EqualTo(1));}
        [Test] public void ReplayPlan_IgnoresCatalogEntryWithNullIdWithoutThrowing()
        {
            _extra=ScriptableObject.CreateInstance<FurnitureDefinition>();typeof(FurnitureDefinition).GetField("_stableId",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(_extra,null);
            _catalog=new[]{_extra,_chair};var plan=Plan(Save("chair",1,1));Assert.That(Entries(plan,"Accepted").Length,Is.EqualTo(1));
        }
        [Test] public void ReplayPlan_DoesNotMutateSourceRecordsOrBaseTiles()
        {
            var saves=new[]{Save("chair",1,1),Save("chair",2,1)};var source=new FurniturePlacementLayoutSaveData{Items=saves};var before=JsonUtility.ToJson(source);
            Plan(saves);Assert.That(JsonUtility.ToJson(source),Is.EqualTo(before));
            for(int x=0;x<5;x++) for(int y=0;y<3;y++) Assert.That(_ground.GetTile(new Vector3Int(x,y,0)),Is.SameAs(_tile));
            Assert.That(_collision.GetUsedTilesCount(),Is.Zero);
        }
    }
}
