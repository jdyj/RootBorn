using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.Game.Player;
using Rootborn.Game.World;
using Rootborn.UI.StudentLife;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class HouseSceneAssetTests
    {
        private Scene _scene;
        [SetUp] public void SetUp() { _scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/House.unity"); }
        [TearDown] public void TearDown() { if(_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); }
        private T[] All<T>() where T:Component => _scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        [Test] public void TOWN_House_HasCanonicalEntryAndExactTownReturn()
        {
            Assert.That(All<WorldSpawnPoint>().Count(s=>s.SpawnId=="house-entry"),Is.EqualTo(1));
            Assert.That(All<WorldPortal>().Count(p=>p.DestinationScene=="Town" && p.DestinationSpawnId=="town-home-entry"),Is.EqualTo(1));
        }
        [Test] public void LIFE_House_HasVisibleBedAndDiaryWithSeparatedInteractions()
        {
            var bed=All<StudentDayEndInteractor>().SingleOrDefault(); var plan=All<DailyPlanBoardInteractor>().SingleOrDefault();
            Assert.That(bed,Is.Not.Null); Assert.That(plan,Is.Not.Null);
            Assert.That(bed.DayEndRules,Is.Not.Null);
            Assert.That(Vector2.Distance(bed.transform.position,plan.transform.position),Is.GreaterThan(3.5f));
            Assert.That(bed.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.sprite!=null),Is.True);
            Assert.That(plan.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.sprite!=null),Is.True);
        }
        [Test] public void TOWN_House_HasAuthoredMembershipAndExistingCameraBoundsPolicy()
        {
            Assert.That(All<AuthoredWorldScene>().Length,Is.EqualTo(1));
            var policy=All<ScenePlayerRenderPolicy>().SingleOrDefault(); Assert.That(policy,Is.Not.Null);
            Assert.That(policy.CameraBounds,Is.Not.Null); Assert.That(policy.CameraBounds.name,Is.EqualTo("HouseCameraBounds"));
            Assert.That(policy.CameraBounds.size,Is.EqualTo(new Vector2(32,20)));
            Assert.That(All<Canvas>(),Is.Not.Empty);
        }
        [Test] public void TOWN_House_AuthoredSetupPreservesExistingFurnitureAndTileFootprint()
        {
            foreach(var pair in new[]{("HouseGroundTilemap",380),("HouseWallTilemap",127),("HouseDecorationTilemap",14),("HouseCollisionTilemap",133)})
            {
                var map=All<Tilemap>().Single(m=>m.name==pair.Item1); int count=0;
                foreach(var cell in map.cellBounds.allPositionsWithin) if(map.HasTile(cell)) count++;
                Assert.That(count,Is.EqualTo(pair.Item2),pair.Item1);
            }
            Assert.That(All<InteriorObjectInteractor>().Length,Is.EqualTo(3));
            Assert.That(All<InteriorTilemapApplier>().Length,Is.EqualTo(1));
        }
    }
}
