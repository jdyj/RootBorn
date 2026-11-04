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
        private Vector3 ReturnPosition()=>All<WorldSpawnPoint>().Single(s=>s.SpawnId=="town-home-entry").transform.position;
        [Test] public void HomeReturn_PlayerBodyHasMarginFromAutomaticDoorTrigger()
        {
            var point=ReturnPosition();
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
        [Test] public void HomeReturn_FootprintRestsOnGroundAwayFromBuildingTiles()
        {
            var point=ReturnPosition(); var maps=All<Tilemap>();
            var ground=maps.Single(m=>m.name=="Town2LdtkGroundTilemap");
            var buildings=maps.Where(m=>m.name=="Town2LdtkBuildingTilemap" || m.name=="Town2LdtkModularBuildingTilemap").ToArray();
            foreach(var offset in new[]{new Vector3(-.49f,-.49f),new Vector3(.49f,-.49f),new Vector3(-.49f,.49f),new Vector3(.49f,.49f)})
            {
                var p=point+offset; Assert.That(ground.HasTile(ground.WorldToCell(p)),Is.True,"Missing ground");
                foreach(var building in buildings) Assert.That(building.HasTile(building.WorldToCell(p)),Is.False,"Return body overlaps building");
            }
        }
    }
}
