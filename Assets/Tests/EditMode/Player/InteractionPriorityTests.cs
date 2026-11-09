using System;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Save;
using UnityEngine;
namespace Rootborn.Tests.EditMode.Player
{
 public sealed class InteractionPriorityTests
 {
  private GameObject _player,_location;private LocationDefinition _definition;private LocationIdentityDefinition _identity;private SaveSlotMetadata _save;
  [SetUp]public void SetUp(){_save=ActiveSaveContext.Metadata;ActiveSaveContext.Clear();_player=new GameObject("PriorityPlayer",typeof(StudentLifeProgressComponent),typeof(PlayerInteractionRouter));_player.transform.position=new Vector3(1000,1000);_definition=ScriptableObject.CreateInstance<LocationDefinition>();_definition.ConfigureForTests("priority-place","priority-place",Vector2.zero,Array.Empty<DiscoveryDefinition>());_identity=ScriptableObject.CreateInstance<LocationIdentityDefinition>();_identity.ConfigureForTests("priority-identity","priority-identity",_definition,"",Array.Empty<LocationActivityDefinition>(),null,0,1440);_location=new GameObject("Place",typeof(BoxCollider2D));_location.transform.position=_player.transform.position+Vector3.right*.5f;_location.GetComponent<BoxCollider2D>().isTrigger=true;}
  [TearDown]public void TearDown(){UnityEngine.Object.DestroyImmediate(_player);UnityEngine.Object.DestroyImmediate(_location);UnityEngine.Object.DestroyImmediate(_definition);UnityEngine.Object.DestroyImmediate(_identity);ActiveSaveContext.Set(_save);}
  [Test]public void SameObject_ActivityPriorityWinsRegardlessOfComponentOrder(){_location.AddComponent<LocationVisitInteractor>().Bind(_definition);var activity=_location.AddComponent<LocationActivityInteractor>();activity.Bind(_identity);Physics2D.SyncTransforms();var router=_player.GetComponent<PlayerInteractionRouter>();router.RefreshPromptNow();Assert.That(router.CurrentInteractable,Is.SameAs(activity));}
  [Test]public void SameObject_UnavailableFirstComponentDoesNotHideUsableInteraction(){_location.AddComponent<LocationActivityInteractor>();var visit=_location.AddComponent<LocationVisitInteractor>();visit.Bind(_definition);Physics2D.SyncTransforms();var router=_player.GetComponent<PlayerInteractionRouter>();router.RefreshPromptNow();Assert.That(router.CurrentInteractable,Is.SameAs(visit));}
  // INTERACT-001: standing at a lower-priority point (visit, 35) with a higher-priority one (activity, 45) at the edge of reach,
  // E acts on the one in front; with both about equally close, priority still decides.
  [Test]public void INTERACT_001_TheThingInFrontBeatsAHigherPriorityThingFartherAway()
  {
   var activity=_location.AddComponent<LocationActivityInteractor>();activity.Bind(_identity);
   _location.transform.position=_player.transform.position+Vector3.right*1.5f;
   var spot=new GameObject("VisitSpot",typeof(BoxCollider2D));
   try
   {
    spot.GetComponent<BoxCollider2D>().isTrigger=true;spot.transform.position=_player.transform.position+Vector3.left*.3f;
    var visit=spot.AddComponent<LocationVisitInteractor>();visit.Bind(_definition);
    Physics2D.SyncTransforms();var router=_player.GetComponent<PlayerInteractionRouter>();router.RefreshPromptNow();
    Assert.That(router.CurrentInteractable,Is.SameAs(visit),"the point the player stands at must take E");
    spot.transform.position=_player.transform.position+Vector3.left*1.2f;Physics2D.SyncTransforms();router.RefreshPromptNow();
    Assert.That(router.CurrentInteractable,Is.SameAs(activity),"about equally close: the higher priority wins");
   }
   finally{UnityEngine.Object.DestroyImmediate(spot);}
  }
  [Test]public void NearerPartTimeWorkBeatsAFartherFacilityActivity()
  {
   // A work point beside a facility (errand helper next to the restaurant, 2 cells apart): the one the player stands at wins.
   _player.AddComponent<Rootborn.Game.Player.PlayerInventory>();
   var activity=_location.AddComponent<LocationActivityInteractor>();activity.Bind(_identity);
   _location.transform.position=_player.transform.position+Vector3.right*1.2f;
   var work=ScriptableObject.CreateInstance<PartTimeWorkDefinition>();
   var workPoint=new GameObject("WorkPoint",typeof(BoxCollider2D));
   try
   {
    workPoint.GetComponent<BoxCollider2D>().isTrigger=true;workPoint.transform.position=_player.transform.position+Vector3.left*.4f;
    var job=workPoint.AddComponent<PartTimeWorkInteractor>();job.Bind(work);
    Physics2D.SyncTransforms();var router=_player.GetComponent<PlayerInteractionRouter>();router.RefreshPromptNow();
    Assert.That(router.CurrentInteractable,Is.SameAs(job));
    workPoint.transform.position=_player.transform.position+Vector3.left*1.5f;Physics2D.SyncTransforms();router.RefreshPromptNow();
    Assert.That(router.CurrentInteractable,Is.SameAs(activity),"standing at the facility, the facility wins");
   }
   finally{UnityEngine.Object.DestroyImmediate(workPoint);UnityEngine.Object.DestroyImmediate(work);}
  }
  [Test]public void ChildCollider_UnavailableChildDoesNotHideParentActivity(){var activity=_location.AddComponent<LocationActivityInteractor>();activity.Bind(_identity);UnityEngine.Object.DestroyImmediate(_location.GetComponent<BoxCollider2D>());var child=new GameObject("Child",typeof(BoxCollider2D),typeof(LocationVisitInteractor));child.transform.SetParent(_location.transform,false);child.GetComponent<BoxCollider2D>().isTrigger=true;Physics2D.SyncTransforms();var router=_player.GetComponent<PlayerInteractionRouter>();router.RefreshPromptNow();Assert.That(router.CurrentInteractable,Is.SameAs(activity));}
 }
}
