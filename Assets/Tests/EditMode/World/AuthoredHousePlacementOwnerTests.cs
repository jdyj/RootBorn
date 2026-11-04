using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.Game.Placement;
using Rootborn.Game.Save;
using Rootborn.Tests.EditMode.Dialogue;
using Rootborn.UI.Careers;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
namespace Rootborn.Tests.EditMode.World
{
    public sealed class AuthoredHousePlacementOwnerTests
    {
        private NpcDialogueContextTests _fixture;
        private AdultCareerRuntimeComponent _owner;
        private GameObject _grid;
        private Tile _tile;
        private FurnitureDefinition _chair;
        private const string FileName="house-furniture-layout-stage-0.json";
        [SetUp] public void Setup()
        {
            _fixture=new NpcDialogueContextTests();_fixture.Setup();
            _owner=(AdultCareerRuntimeComponent)typeof(NpcDialogueContextTests).GetField("_owner",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_fixture);
            _grid=new GameObject("OwnedHouse",typeof(Grid),typeof(InteriorTilemapApplier));SceneManager.MoveGameObjectToScene(_grid,_owner.gameObject.scene);
            var ground=Map("HouseGroundTilemap");Map("HouseCollisionTilemap");
            _tile=ScriptableObject.CreateInstance<Tile>();for(int x=0;x<4;x++)for(int y=0;y<4;y++)ground.SetTile(new Vector3Int(x,y,0),_tile);
            _chair=FurnitureDefinition.CreateForTests("owner-chair","Chair","Seat",new[]{"house"},new[]{new FurnitureTilePart(Vector2Int.zero,_tile)},new[]{Vector2Int.zero},true,FurniturePlacementPreference.Any);
        }
        private Tilemap Map(string name){var go=new GameObject(name,typeof(Tilemap));go.transform.SetParent(_grid.transform,false);return go.GetComponent<Tilemap>();}
        private AuthoredHouseFurnitureSession Session=>_grid.GetComponent<AuthoredHouseFurnitureSession>();
        private SaveService Storage=>new SaveService(_owner.Progress.SaveSlot);
        [TearDown] public void Cleanup(){if(_grid!=null)UnityEngine.Object.DestroyImmediate(_grid);if(_chair!=null)UnityEngine.Object.DestroyImmediate(_chair);if(_tile!=null)UnityEngine.Object.DestroyImmediate(_tile);_fixture.Cleanup();}
        [Test] public void OwnerBindingCreatesDedicatedSessionAndPlacementPersists()
        {
            Assert.That(AuthoredHousePlacementBinding.BindOwner(_owner.gameObject.scene,_owner),Is.True);
            Assert.That(Session.TryPlace(_chair,new Vector3Int(1,1,0),FurniturePlacementDirection.North),Is.True);
            var saved=JsonUtility.FromJson<FurniturePlacementLayoutSaveData>(Storage.ReadJson(FileName));
            Assert.That(saved.Items.Single().FurnitureId,Is.EqualTo("owner-chair"));
            Assert.That(Session.BlockingTilemap.HasTile(new Vector3Int(1,1,0)),Is.True);
        }
        [Test] public void CommittedPlacementPublishesOneQuestEventForExactOwner_RestoreAndMoveStaySilent()
        {
            Assert.That(AuthoredHousePlacementBinding.BindOwner(_owner.gameObject.scene,_owner),Is.True);
            int count=0; Rootborn.Game.Quests.QuestEvent captured=default;
            Rootborn.Game.StudentLife.StudentLifeProgress actor=null;
            Action<Rootborn.Game.Quests.QuestEvent,Rootborn.Game.StudentLife.StudentLifeProgress> listener=(e,p)=>{count++;captured=e;actor=p;
                Assert.That(Storage.ReadJson(FileName),Is.Not.Null.And.Not.Empty,"Layout must be durable before notification.");};
            Rootborn.Game.Quests.QuestGameplayEventBridge.OnRecorded+=listener;
            try {
                Assert.That(Session.TryPlace(_chair,new Vector3Int(1,1,0),FurniturePlacementDirection.North),Is.True);
                Assert.That(count,Is.EqualTo(1)); Assert.That(captured.Kind,Is.EqualTo(Rootborn.Game.Quests.QuestEventKind.FurniturePlaced));
                Assert.That(captured.Furniture,Is.SameAs(_chair));Assert.That(captured.SurfaceId,Is.EqualTo("house"));
                Assert.That(actor,Is.SameAs(_owner.StudentComponent.Progress));Assert.That(captured.EventKey,Is.Not.Empty);
                var saved=Session.BuildSaveRecords();Session.Restore(saved,new[]{_chair});
                var commitField=typeof(AuthoredHouseFurnitureSession).GetField("_commit",BindingFlags.Instance|BindingFlags.NonPublic);
                var guardField=typeof(AuthoredHouseFurnitureSession).GetField("_canMutate",BindingFlags.Instance|BindingFlags.NonPublic);
                var reloadField=typeof(AuthoredHouseFurnitureSession).GetField("_reload",BindingFlags.Instance|BindingFlags.NonPublic);
                var originalCommit=(Func<FurniturePlacementSaveData[],bool>)commitField.GetValue(Session);
                var originalGuard=(Func<bool>)guardField.GetValue(Session);
                var originalReload=(Func<bool>)reloadField.GetValue(Session);
                var trace=new System.Collections.Generic.List<string>();bool moved;
                try{
                    Session.ConfigureCommit(originalGuard,records=>{bool okay=originalCommit(records);trace.Add("commit="+okay+" guard="+Session.CanMutate+" records="+records.Length);return okay;},originalReload);
                    moved=Session.TryMove(Session.Registry.Instances.Single().InstanceId,new Vector3Int(2,2,0),FurniturePlacementDirection.North);
                }finally{Session.ConfigureCommit(originalGuard,originalCommit,originalReload);}
                if(!moved){
                    var cell=new Vector3Int(2,2,0);var probe=AuthoredFurnitureReplayEntry.Clone(saved.Single());probe.AnchorX=2;probe.AnchorY=2;
                    var plan=AuthoredHouseFurnitureReplay.Plan(Session.Model,Session.Surface.SurfaceId,new[]{probe},new[]{_chair});
                    trace.Add("guard="+Session.CanMutate+" planAccepted="+plan.Accepted.Count+" unresolved="+string.Join(",",plan.Unresolved.Select(x=>x.Reason.ToString()))+" ground="+Session.Surface.GroundTilemap.HasTile(cell)+" occupied="+Session.Surface.OccupancyTilemap.HasTile(cell)+" object="+Session.Surface.ObjectTilemap.HasTile(cell)+" bounds="+Session.Surface.Bounds+" spawn="+Session.Model.Spawn);
                    string path=System.IO.Path.Combine(Storage.DirectoryPath,FileName);
                    try{using(var h=System.IO.File.Open(path,System.IO.FileMode.Open,System.IO.FileAccess.ReadWrite,System.IO.FileShare.None)){trace.Add("exclusive-open=okay length="+h.Length);}}catch(Exception ex){trace.Add("exclusive-open="+ex.GetType().Name+" hresult="+ex.HResult+" "+ex.Message);}
                }
                Assert.That(moved,Is.True,string.Join(" | ",trace));
                Assert.That(count,Is.EqualTo(1),"Reloading and rearranging are not new furniture placements.");
                Session.ConfigureCommit(()=>true,records=>false);
                Assert.That(Session.TryPlace(_chair,new Vector3Int(1,1,0),FurniturePlacementDirection.North),Is.False);
                Assert.That(count,Is.EqualTo(1));
            } finally {Rootborn.Game.Quests.QuestGameplayEventBridge.OnRecorded-=listener;}
        }
        [Test] public void CorruptLayoutCannotBeBypassedByRetryingOwnerBinding()
        {
            Storage.WriteJson(FileName,"broken-layout");
            Assert.That(AuthoredHousePlacementBinding.BindOwner(_owner.gameObject.scene,_owner),Is.False);
            Assert.That(AuthoredHousePlacementBinding.BindOwner(_owner.gameObject.scene,_owner),Is.False,"Retries must not mark an unrestored session ready.");
            Assert.That(Storage.ReadJson(FileName),Is.EqualTo("broken-layout"));
        }
        [Test] public void WriteFailureLeavesFurnitureRecordsAndPhysicalCollisionUnchanged()
        {
            Assert.That(AuthoredHousePlacementBinding.BindOwner(_owner.gameObject.scene,_owner),Is.True);
            Session.ConfigureCommit(()=>true,records=>false);
            Assert.That(Session.TryPlace(_chair,new Vector3Int(1,1,0),FurniturePlacementDirection.North),Is.False);
            Assert.That(Session.BuildSaveRecords(),Is.Empty);Assert.That(Session.BlockingTilemap.HasTile(new Vector3Int(1,1,0)),Is.False);
        }
        [Test] public void LostOwnerLeaseCannotPlaceOrClearFurniture()
        {
            Assert.That(AuthoredHousePlacementBinding.BindOwner(_owner.gameObject.scene,_owner),Is.True);
            Assert.That(Session.TryPlace(_chair,new Vector3Int(1,1,0),FurniturePlacementDirection.North),Is.True);
            string before=Storage.ReadJson(FileName);Rootborn.Game.Careers.AdultCareerPersistenceRouter.Unregister(_owner);
            Assert.That(Session.TryPlace(_chair,new Vector3Int(2,2,0),FurniturePlacementDirection.North),Is.False);
            Assert.That(Session.ClearUserFurniture(),Is.False);Assert.That(Session.BuildSaveRecords().Length,Is.EqualTo(1));
            Assert.That(Storage.ReadJson(FileName),Is.EqualTo(before));
        }
    }
}
