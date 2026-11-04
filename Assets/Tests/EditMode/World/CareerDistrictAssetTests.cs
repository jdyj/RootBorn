using System.Linq;
using NUnit.Framework;
using Rootborn.UI.Careers;
using Rootborn.Game.Careers;
using Rootborn.Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
namespace Rootborn.Tests.EditMode.World {
 public sealed class CareerDistrictAssetTests {
 private Scene _scene;
 [SetUp] public void Setup(){_scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Town.unity");}
 [TearDown] public void Cleanup(){if(_scene.IsValid())EditorSceneManager.ClosePreviewScene(_scene);}
 private GameObject District(){var root=_scene.GetRootGameObjects().SingleOrDefault(x=>x.name=="CareerDistrict");Assert.That(root,Is.Not.Null,"Saved north district missing");return root;}
 [Test]public void WORLD_DISTRICT_001_FourSavedSitesUseTwelveRegistryVisualStages(){var root=District();var sites=root.GetComponentsInChildren<FacilityWorldPresenter>(true);Assert.That(sites.Length,Is.EqualTo(4));Assert.That(sites.Select(x=>x.Facility).Distinct().Count(),Is.EqualTo(4));foreach(var site in sites){var so=new SerializedObject(site);var arr=so.FindProperty("_visuals");Assert.That(arr.arraySize,Is.EqualTo(3));for(int i=0;i<3;i++){var v=arr.GetArrayElementAtIndex(i).objectReferenceValue as FacilityVisualDefinition;Assert.That(v,Is.Not.Null);Assert.That(v.IsValid,Is.True);Assert.That(v.Facility,Is.SameAs(site.Facility));Assert.That(v.Level,Is.EqualTo(i+1));}Assert.That(so.FindProperty("_door").objectReferenceValue,Is.Not.Null);Assert.That(so.FindProperty("_spriteRenderer").objectReferenceValue,Is.Not.Null);}}
 [Test]public void WORLD_DISTRICT_002_SavedGroundConnectsExistingNorthExitAndSchool(){var root=District();var map=root.GetComponentsInChildren<Tilemap>(true).Single(x=>x.name=="DistrictGround");for(int y=-2;y<=36;y++)Assert.That(map.HasTile(new Vector3Int(36,y,0)),Is.True,"Corridor y="+y);}
 [Test]public void WORLD_DISTRICT_003_CameraBoundsIncludeNorthAndLegacyBeach(){var policy=_scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ScenePlayerRenderPolicy>(true)).Single();Assert.That(policy.CameraBounds.bounds.Contains(new Vector3(43,39,0)),Is.True);Assert.That(policy.CameraBounds.bounds.Contains(new Vector3(37.5f,-62,0)),Is.True);}
 [Test]public void WORLD_DISTRICT_004_FacilitySitesReserveAllLevelApproachesWithoutOverlap(){var sites=District().GetComponentsInChildren<FacilityWorldPresenter>(true);foreach(var site in sites){var arr=new SerializedObject(site).FindProperty("_visuals");for(int i=0;i<arr.arraySize;i++){var visual=(FacilityVisualDefinition)arr.GetArrayElementAtIndex(i).objectReferenceValue;foreach(var c in visual.ApproachCells){var pos=site.transform.TransformPoint((Vector2)c);Assert.That(pos.y,Is.GreaterThan(0));foreach(var other in sites.Where(x=>x!=site))Assert.That(Vector2.Distance(pos,other.transform.position),Is.GreaterThan(5));}}}}
 }
}
