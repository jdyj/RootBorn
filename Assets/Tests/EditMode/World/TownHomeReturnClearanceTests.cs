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
        // A Town already open in the editor plus this preview copy logs "more than one global light"; geometry is all these tests read.
        [SetUp] public void SetUp(){UnityEngine.TestTools.LogAssert.ignoreFailingMessages=true;_scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Town.unity");}
        [TearDown] public void TearDown(){EditorSceneManager.ClosePreviewScene(_scene);}
        // LDtk "Collision" layer: 1 Block, 2 Open; a cell centre is (x + .5, -(y + .5)) cells in world units.
        [Test] public void PaintedCollision_ReadsBlockAndOpenCellCentres()
        {
            var blocks=new System.Collections.Generic.List<Vector2>(); var opens=new System.Collections.Generic.List<Vector2>();
            Rootborn.Editor.LDtk.LdtkPaintedCollision.Read(new[]{0,1,2,0},2,32,32f,blocks,opens);
            Assert.That(blocks,Is.EqualTo(new[]{new Vector2(1.5f,-.5f)}));
            Assert.That(opens,Is.EqualTo(new[]{new Vector2(.5f,-1.5f)}));
        }
        // Saving a level in LDtk bakes the scene it feeds; levels that feed no scene are left alone.
        [Test] public void PaintedLevel_MapsToTheSceneItFeeds()
        {
            Assert.That(Rootborn.Editor.LDtk.InteriorDoorPortalBaker.SceneForPaintedLevel("Assets/LDtk/Town/town/Level_0.ldtkl"),Is.EqualTo("Town"));
            Assert.That(Rootborn.Editor.LDtk.InteriorDoorPortalBaker.SceneForPaintedLevel("Assets/LDtk/Interiors/interiors/Shop.ldtkl"),Is.EqualTo("ShopInterior"));
            Assert.That(Rootborn.Editor.LDtk.InteriorDoorPortalBaker.SceneForPaintedLevel("Assets/LDtk/Town/town/Level_1.ldtkl"),Is.Null);
            Assert.That(Rootborn.Editor.LDtk.InteriorDoorPortalBaker.SceneForPaintedLevel("Assets/LDtk/Interiors/interiors/Shop_Base.ldtkl"),Is.Null);
        }
        // What is painted in LDtk must be what the saved Town scene has. If this fails, run Rootborn/World/Bake Town Painted Collision.
        [Test] public void TownScene_MatchesThePaintedLdtkCollisionLayer()
        {
            var blocks=new System.Collections.Generic.List<Vector2>(); var opens=new System.Collections.Generic.List<Vector2>();
            Assert.That(Rootborn.Editor.LDtk.LdtkPaintedCollision.TryRead(Rootborn.Editor.LDtk.InteriorDoorPortalBaker.TownLevelPath,Rootborn.Editor.LDtk.LdtkPaintedCollision.PixelsPerUnit,blocks,opens),Is.True,"Town LDtk level has no Collision layer");
            string problem=Rootborn.Editor.LDtk.InteriorDoorPortalBaker.ApplyTownPaintedCollision(_scene,blocks,opens,out bool changed,out int blockCount,out int openCount);
            Assert.That(problem,Is.Null);
            Assert.That(changed,Is.False,"Town scene is out of date with the painted LDtk Collision layer ("+blockCount+" block, "+openCount+" open). Run Rootborn/World/Bake Town Painted Collision.");
        }
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
                    // Arriving right against an exit trigger sends the player straight back out (the shooting range did).
                    foreach(var exit in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldPortal>(true)))
                    {
                        var box=exit.GetComponent<BoxCollider2D>(); Assert.That(box,Is.Not.Null,sceneName+" exit "+exit.name+" has no trigger box"); var center=(Vector2)exit.transform.position+box.offset; var half=box.size*.5f;
                        var p=(Vector2)spawn.transform.position;
                        float dx=Mathf.Max(center.x-half.x-p.x,0f,p.x-(center.x+half.x)); float dy=Mathf.Max(center.y-half.y-p.y,0f,p.y-(center.y+half.y));
                        Assert.That(Mathf.Sqrt(dx*dx+dy*dy),Is.GreaterThanOrEqualTo(.9f),sceneName+" spawn "+spawn.SpawnId+" hugs exit "+exit.name);
                    }
                }
                foreach(var portal in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldPortal>(true)))
                    Assert.That(collision.HasTile(collision.WorldToCell(portal.transform.position)),Is.False,sceneName+" exit "+portal.name+" is walled off");
                // Counters, tables and cell bars must stop the player like walls (their raised overdraw parts do not).
                // Fixtures touching an exit's trigger box (the police gate) are left open so the exit stays reachable.
                var nearExit=new System.Collections.Generic.HashSet<Vector3Int>();
                foreach(var pt in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldPortal>(true)))
                {
                    var box=pt.GetComponent<BoxCollider2D>(); var center=(Vector2)pt.transform.position+box.offset; var half=box.size*.5f;
                    for(int x=Mathf.FloorToInt(center.x-half.x+.01f)-1;x<=Mathf.FloorToInt(center.x+half.x-.01f)+1;x++)
                        for(int y=Mathf.FloorToInt(center.y-half.y+.01f)-1;y<=Mathf.FloorToInt(center.y+half.y-.01f)+1;y++) nearExit.Add(new Vector3Int(x,y,0));
                }
                foreach(var prop in maps.Where(m=>{var layer=m.name.Substring(sceneName.Length);return new[]{"Police","Kitchen","Shop","Shooting"}.Any(k=>layer.StartsWith(k))&&!layer.Contains("Overdraw");}))
                    foreach(var c in prop.cellBounds.allPositionsWithin)
                        if(prop.HasTile(c)&&!nearExit.Contains(c))
                            Assert.That(collision.HasTile(c),Is.True,sceneName+" lets the player walk through "+prop.name+" at "+c);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
    }

    // Painting a cell in LDtk and saving re-imports the level; the Town scene must pick it up with no menu click.
    public sealed class LdtkPaintedCollisionAutoBakeTests
    {
        private const string ScenePath = "Assets/Scenes/Town.unity";

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator SavingThePaintedTownLevel_BakesTheTownSceneByItself()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            string levelPath = Rootborn.Editor.LDtk.InteriorDoorPortalBaker.TownLevelPath;
            byte[] originalLevel = System.IO.File.ReadAllBytes(levelPath);
            byte[] originalScene = System.IO.File.ReadAllBytes(ScenePath);
            int before = PaintedBlockCount();
            try
            {
                System.IO.File.WriteAllText(levelPath, PaintLastCollisionCell(System.Text.Encoding.UTF8.GetString(originalLevel)));
                UnityEditor.AssetDatabase.ImportAsset(levelPath, UnityEditor.ImportAssetOptions.ForceUpdate);
                yield return WaitForCount(before + 1);
                Assert.That(PaintedBlockCount(), Is.EqualTo(before + 1), "Saving the painted level did not bake the new Block cell into Town");

                System.IO.File.WriteAllBytes(levelPath, originalLevel);
                UnityEditor.AssetDatabase.ImportAsset(levelPath, UnityEditor.ImportAssetOptions.ForceUpdate);
                yield return WaitForCount(before);
                Assert.That(PaintedBlockCount(), Is.EqualTo(before), "Erasing the cell and saving did not bake it back out");
            }
            finally
            {
                System.IO.File.WriteAllBytes(levelPath, originalLevel);
                System.IO.File.WriteAllBytes(ScenePath, originalScene);
                UnityEditor.AssetDatabase.ImportAsset(levelPath, UnityEditor.ImportAssetOptions.ForceUpdate);
                UnityEditor.AssetDatabase.ImportAsset(ScenePath, UnityEditor.ImportAssetOptions.ForceUpdate);
            }
        }

        // The bottom-right cell is the last value of the Collision layer's csv; the test paint lives top-left.
        private static string PaintLastCollisionCell(string level)
        {
            int layer = level.IndexOf("\"__identifier\": \"Collision\"", System.StringComparison.Ordinal);
            Assert.That(layer, Is.GreaterThanOrEqualTo(0));
            int csv = level.IndexOf("\"intGridCsv\": [", layer, System.StringComparison.Ordinal);
            int close = level.IndexOf(']', csv);
            int last = close - 1;
            while (char.IsWhiteSpace(level[last])) last--;
            Assert.That(level[last], Is.EqualTo('0'), "bottom-right Collision cell is already painted");
            return level.Substring(0, last) + "1" + level.Substring(last + 1);
        }

        // Waits by time, not frames: in a full run frames pass quickly while the LDtk import and the
        // delayed bake can take tens of seconds, so a 600-frame wait gave up before the bake landed.
        private static System.Collections.IEnumerator WaitForCount(int count)
        {
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 90.0;
            double nextCheck = 0.0;
            while (UnityEditor.EditorApplication.timeSinceStartup < deadline)
            {
                if (UnityEditor.EditorApplication.timeSinceStartup >= nextCheck)
                {
                    if (PaintedBlockCount() == count) yield break;
                    nextCheck = UnityEditor.EditorApplication.timeSinceStartup + 1.0;
                }
                yield return null;
            }
        }

        private static int PaintedBlockCount()
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
                {
                    if (map.name != Rootborn.Editor.LDtk.InteriorDoorPortalBaker.TownPaintedBlockName) continue;
                    map.CompressBounds();
                    int n = 0;
                    foreach (var cell in map.cellBounds.allPositionsWithin) if (map.HasTile(cell)) n++;
                    return n;
                }
                return 0;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
