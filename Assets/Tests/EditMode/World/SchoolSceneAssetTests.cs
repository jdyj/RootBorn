using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.World;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class SchoolSceneAssetTests
    {
        private const string Path="Assets/Scenes/School.unity";
        private Scene _scene;
        [SetUp] public void SetUp()
        {
            Assert.That(File.Exists(Path),Is.True,"The School interior must be a saved scene.");
            _scene=EditorSceneManager.OpenPreviewScene(Path);
        }
        [TearDown] public void TearDown() { if(_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); }
        private T[] All<T>() where T:Component => _scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        [Test] public void TOWN_School_IsEnabledInPlayerBuild()
        {
            Assert.That(EditorBuildSettings.scenes.Any(s=>s.enabled && s.path==Path),Is.True);
        }
        [Test] public void TOWN_School_ContainsUniqueEntryAndExactExteriorReturn()
        {
            Assert.That(All<WorldSpawnPoint>().Count(s=>s.SpawnId=="school-entry"),Is.EqualTo(1));
            Assert.That(All<WorldPortal>().Count(p=>p.DestinationScene=="Town" && p.DestinationSpawnId=="town-school-entry"),Is.EqualTo(1));
        }
        [Test] public void LIFE_School_TeacherAndPlanAreSavedAndReachableAsSeparateTargets()
        {
            var teacher=All<NpcInteractor>().SingleOrDefault(n=>n.Npc!=null && n.Npc.Id=="npc.teacher");
            Assert.That(teacher,Is.Not.Null);
            var plan=All<DailyPlanBoardInteractor>().SingleOrDefault(); Assert.That(plan,Is.Not.Null);
            Assert.That(Vector2.Distance(teacher.transform.position,plan.transform.position),Is.GreaterThan(3.5f));
            Assert.That(teacher.GetComponent<Collider2D>(),Is.Not.Null);
            Assert.That(plan.GetComponent<Collider2D>(),Is.Not.Null);
            Assert.That(teacher.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.sprite!=null && r.sprite.rect.height>=64),Is.True,"Teacher sprite must contain the body, not a32px head crop.");
        }
        [Test] public void LIFE_School_PlayerBodyCanReachTeacherPlanAndClassFromEntry()
        {
            var blocks=All<BoxCollider2D>().Where(c=>c.enabled&&!c.isTrigger).Select(c=>{
                var center=c.transform.TransformPoint(c.offset);var size=Vector2.Scale(c.size,c.transform.lossyScale);
                return new Rect((Vector2)center-size/2,size);
            }).ToArray();
            var spawn=(Vector2)All<WorldSpawnPoint>().Single(s=>s.SpawnId=="school-entry").transform.position;
            var targets=new[]{(Vector2)All<NpcInteractor>().Single().transform.position,(Vector2)All<DailyPlanBoardInteractor>().Single().transform.position,
                (Vector2)All<Rootborn.Game.StudentLife.LocationActivityInteractor>().Single().transform.position};
            var queue=new System.Collections.Generic.Queue<Vector2Int>();var seen=new System.Collections.Generic.HashSet<Vector2Int>();
            queue.Enqueue(Vector2Int.zero);seen.Add(Vector2Int.zero);var found=new bool[targets.Length];
            var dirs=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
            while(queue.Count>0){var cell=queue.Dequeue();var point=spawn+(Vector2)cell*.25f;
                for(int i=0;i<targets.Length;i++)if(Vector2.Distance(point,targets[i])<1.5f)found[i]=true;
                foreach(var dir in dirs){var next=cell+dir;var pos=spawn+(Vector2)next*.25f;
                    if(pos.x<.5f||pos.x>22.5f||pos.y< -17.5f||pos.y>-.5f||seen.Contains(next))continue;
                    var body=new Rect(pos-Vector2.one*.51f,Vector2.one*1.02f);
                    if(blocks.Any(b=>body.Overlaps(b)))continue;seen.Add(next);queue.Enqueue(next);}
            }
            Assert.That(found,Is.All.True,"Teacher, daily plan and classroom need a traversable route with clearance for the 1-unit player body.");
        }
        [Test] public void TOWN_School_HasSavedBoundsPolicyCanvasAndGround()
        {
            var policy=All<ScenePlayerRenderPolicy>().SingleOrDefault(); Assert.That(policy,Is.Not.Null);
            Assert.That(policy.CameraBounds,Is.Not.Null);
            Assert.That(policy.CameraBounds.size.x,Is.GreaterThan(20));
            Assert.That(All<AuthoredWorldScene>().Length,Is.EqualTo(1));
            Assert.That(All<Canvas>(),Is.Not.Empty);
            Assert.That(All<Camera>().Length,Is.EqualTo(1));
            Assert.That(All<Tilemap>().Any(m=>m.name.Contains("Ground") && m.GetUsedTilesCount()>0),Is.True);
        }
    }
}
