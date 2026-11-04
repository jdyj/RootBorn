using NUnit.Framework;
using Rootborn.Game.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public sealed class UrbanInterestEntryTests {
 [Test] public void TownCleanup_ClosesStalePanelsButKeepsInterestEntryReachable(){
 var scene=EditorSceneManager.NewPreviewScene();
 try {var button=new GameObject("CareerInterestOpenButton");SceneManager.MoveGameObjectToScene(button,scene); var panel=new GameObject("ObjectiveJournalRoot");SceneManager.MoveGameObjectToScene(panel,scene); typeof(TownReturnViewCleanup).GetMethod("HideReturnUi",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{scene}); Assert.That(panel.activeSelf,Is.False); Assert.That(button.activeSelf,Is.True,"Interest must remain reachable after entering Town");}
 finally {EditorSceneManager.ClosePreviewScene(scene);}
 }
}


