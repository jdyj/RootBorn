using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.World;
using Rootborn.UI.Careers;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class TownAuthoredBindingsAssetTests
    {
        private Scene _scene;
        private GameDataRegistry _registry;
        [SetUp] public void SetUp()
        {
            _registry=AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            _scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Town.unity");
        }
        [TearDown] public void TearDown() { if(_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); }
        private T[] All<T>() where T:Component => _scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        [Test] public void TOWN_SavedBindings_AllLocationsHaveAuthoredMembership()
        {
            var config=All<AuthoredWorldScene>().SingleOrDefault(); Assert.That(config,Is.Not.Null);
            Assert.That(_registry.Locations.Count(l=>l!=null),Is.EqualTo(9));
            foreach(var location in _registry.Locations.Where(l=>l!=null)) Assert.That(config.ContainsLocation(location),Is.True,location.Id);
        }
        [Test] public void TOWN_SavedBindings_FacilityLocationDataMatchesActualEntrances()
        {
            var doors=All<CareerFacilityInteractor>(); Assert.That(doors.Length,Is.EqualTo(4));
            foreach(var door in doors)
            {
                Assert.That(door.Facility,Is.Not.Null); Assert.That(door.Facility.Location,Is.Not.Null);
                Assert.That(Vector2.Distance(door.Facility.Location.WorldPosition,door.transform.position),Is.LessThan(.01f),door.Facility.Id);
            }
        }
        [Test] public void TOWN_SavedBindings_FiveNpcActorsUseWholeBodySprites()
        {
            foreach(var npc in _registry.Npcs.Where(n=>n!=null))
            {
                var actors=All<NpcInteractor>().Where(n=>n.Npc==npc).ToArray(); Assert.That(actors.Length,Is.EqualTo(1),npc.Id);
                Assert.That(actors[0].name,Is.EqualTo(LocationNpcRuntimeInstaller.NpcObjectName(npc.Id)));
                Assert.That(actors[0].GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.sprite!=null && r.sprite.rect.height>=64),Is.True,npc.Id);
            }
        }
        [Test] public void TOWN_SavedBindings_HomeAndSchoolHaveExactRoundTripSpawns()
        {
            Assert.That(All<WorldPortal>().Count(p=>p.DestinationScene=="House" && p.DestinationSpawnId=="house-entry"),Is.EqualTo(1));
            Assert.That(All<WorldPortal>().Count(p=>p.DestinationScene=="School" && p.DestinationSpawnId=="school-entry"),Is.EqualTo(1));
            Assert.That(All<WorldSpawnPoint>().Count(p=>p.SpawnId=="town-home-entry"),Is.EqualTo(1));
            Assert.That(All<WorldSpawnPoint>().Count(p=>p.SpawnId=="town-school-entry"),Is.EqualTo(1));
        }
    }
}
