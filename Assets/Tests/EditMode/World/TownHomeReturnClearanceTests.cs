using System.Linq;
using NUnit.Framework;
using Rootborn.Game.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
namespace Rootborn.Tests.EditMode.World
{
    public sealed class TownHomeReturnClearanceTests
    {
        private Scene _scene;
        [SetUp] public void SetUp(){_scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Town.unity");}
        [TearDown] public void TearDown(){EditorSceneManager.ClosePreviewScene(_scene);}
        private T[] All<T>() where T:Component => _scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        private Vector3 ReturnPosition(string spawnId)=>All<WorldSpawnPoint>().Single(s=>s.SpawnId==spawnId).transform.position;
        // Each building returns the player to its own door; every return spot must be clear of the doors and buildings.
        private static readonly string[] ReturnSpawnIds={"town-home-entry","town-shop-entry","town-hospital-entry","town-police-entry","town-kitchen-entry"};
        [TestCaseSource(nameof(ReturnSpawnIds))] public void HomeReturn_PlayerBodyHasMarginFromAutomaticDoorTrigger(string spawnId)
        {
            var point=ReturnPosition(spawnId);
            foreach(var automatic in All<TownAutoDoorPortal>().Where(p=>p.enabled && p.gameObject.activeInHierarchy))
            {
                var box=automatic.GetComponent<BoxCollider2D>(); Assert.That(box,Is.Not.Null);
                var center=box.transform.TransformPoint(box.offset);
                var half=Vector2.Scale(box.size, new Vector2(Mathf.Abs(box.transform.lossyScale.x),Mathf.Abs(box.transform.lossyScale.y)))*.5f;
                // Actual saved House actor has a 1x1 body; retain an extra .25 contact margin.
                bool separated=Mathf.Abs(point.x-center.x)>half.x+.75f || Mathf.Abs(point.y-center.y)>half.y+.75f;
                Assert.That(separated,Is.True,automatic.name+" overlaps the returning player body/contact margin");
            }
        }
        [TestCaseSource(nameof(ReturnSpawnIds))] public void HomeReturn_FootprintRestsOnGroundAwayFromBuildingTiles(string spawnId)
        {
            var point=ReturnPosition(spawnId); var maps=All<Tilemap>();
            var ground=maps.Single(m=>m.name=="Town2LdtkGroundTilemap");
            var buildings=maps.Where(m=>m.name=="Town2LdtkBuildingTilemap" || m.name=="Town2LdtkModularBuildingTilemap").ToArray();
            foreach(var offset in new[]{new Vector3(-.49f,-.49f),new Vector3(.49f,-.49f),new Vector3(-.49f,.49f),new Vector3(.49f,.49f)})
            {
                var p=point+offset; Assert.That(ground.HasTile(ground.WorldToCell(p)),Is.True,"Missing ground under "+spawnId);
                foreach(var building in buildings) Assert.That(building.HasTile(building.WorldToCell(p)),Is.False,"Return body overlaps building at "+spawnId);
                // Physics shoves a player who arrives inside a solid tile, which threw the hospital return two tiles off.
                foreach(var solid in maps.Where(m=>{var c=m.GetComponent<TilemapCollider2D>(); return c!=null && c.enabled && !c.isTrigger && m.gameObject.activeInHierarchy;}))
                    Assert.That(solid.HasTile(solid.WorldToCell(p)),Is.False,"Return body at "+spawnId+" lands inside solid "+solid.name+" cell "+solid.WorldToCell(p));
            }
        }
        // Player builds carry no LDtk level files, so each interior's street exit must be saved into its scene.
        [TestCase("HospitalInterior","town-hospital-entry")]
        [TestCase("ShopInterior","town-shop-entry")]
        [TestCase("PoliceInterior","town-police-entry")]
        [TestCase("KitchenInterior","town-kitchen-entry")]
        public void InteriorScene_SavesItsStreetExitToTheBuildingsOwnDoor(string sceneName,string spawnId)
        {
            var text=System.IO.File.ReadAllText("Assets/Scenes/"+sceneName+".unity");
            Assert.That(text,Does.Contain("_destinationScene: Town"),sceneName+" has no saved exit to Town");
            Assert.That(text,Does.Contain("_destinationSpawnId: "+spawnId),sceneName+" exit must lead to "+spawnId);
            Assert.That(text,Does.Not.Contain("_destinationSpawnId: town-entry"),sceneName+" still sends players to the generic town entry");
        }
        // Interiors were saved without any collision, so players walked through walls and off the map in the build.
        [TestCase("HospitalInterior")]
        [TestCase("ShopInterior")]
        [TestCase("PoliceInterior")]
        [TestCase("PoliceShootingInterior")]
        [TestCase("KitchenInterior")]
        [TestCase("Kitchen2Interior")]
        public void InteriorScene_BlocksEverythingButFloorAndKeepsSpawnsAndExitsOpen(string sceneName)
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/"+sceneName+".unity");
            try
            {
                var maps=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Tilemap>(true)).ToArray();
                var collision=maps.SingleOrDefault(m=>m.name==sceneName+"CollisionTilemap");
                Assert.That(collision,Is.Not.Null,sceneName+" has no collision tilemap");
                var collider=collision.GetComponent<TilemapCollider2D>();
                Assert.That(collider!=null && collider.enabled && !collider.isTrigger,Is.True,sceneName+" collision tilemap must have a solid collider");
                var ground=maps.Where(m=>m.name.Contains("Ground")).ToArray();
                Assert.That(ground,Is.Not.Empty);
                bool IsFloor(Vector3Int c)=>ground.Any(g=>g.HasTile(c));
                // A cell just outside the floor's bounds must be blocked on every side.
                var b=ground[0].cellBounds;
                foreach(var probe in new[]{new Vector3Int(b.xMin-1,(b.yMin+b.yMax)/2,0),new Vector3Int(b.xMax,(b.yMin+b.yMax)/2,0)})
                    if(!IsFloor(probe)) Assert.That(collision.HasTile(probe),Is.True,sceneName+" leaves off-map cell "+probe+" open");
                foreach(var spawn in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldSpawnPoint>(true)))
                {
                    var c=collision.WorldToCell(spawn.transform.position);
                    Assert.That(IsFloor(c),Is.True,sceneName+" spawn "+spawn.SpawnId+" is off the floor at "+c);
                    Assert.That(collision.HasTile(c),Is.False,sceneName+" spawn "+spawn.SpawnId+" is inside collision at "+c);
                }
                foreach(var portal in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldPortal>(true)))
                    Assert.That(collision.HasTile(collision.WorldToCell(portal.transform.position)),Is.False,sceneName+" exit "+portal.name+" is walled off");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
