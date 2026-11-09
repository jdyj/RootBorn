using NUnit.Framework;
using Rootborn.Game.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public sealed class UrbanInterestEntryTests {
 [Test] public void TownCleanup_ClosesStalePanelsButKeepsInterestEntryReachable(){
 var scene=EditorSceneManager.NewPreviewScene();
 try {var button=new GameObject("CareerInterestOpenButton");SceneManager.MoveGameObjectToScene(button,scene); var panel=new GameObject("BookPanel"); /* the journal is no longer swept (f46e7097a); BookPanel is a stale panel the sweep still closes */SceneManager.MoveGameObjectToScene(panel,scene); typeof(TownReturnViewCleanup).GetMethod("HideReturnUi",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{scene,new System.Collections.Generic.HashSet<int>()}); Assert.That(panel.activeSelf,Is.False); Assert.That(button.activeSelf,Is.True,"Interest must remain reachable after entering Town");}
 finally {EditorSceneManager.ClosePreviewScene(scene);}
 }
}


