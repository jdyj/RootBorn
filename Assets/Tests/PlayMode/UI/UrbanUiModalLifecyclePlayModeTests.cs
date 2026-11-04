using System.Collections;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;using Rootborn.UI.StudentLife;using Rootborn.Game.StudentLife;using Rootborn.Game.Common;
namespace Rootborn.Tests.PlayMode {
 public sealed class UrbanUiModalLifecyclePlayModeTests {
  [UnityTest] public IEnumerator ResultPanel_ReenableReacquiresInputAndDestroyReleasesIt(){
   var go=new GameObject("ModalLifecycleCanvas",typeof(Canvas));
   try {
    var panel=StudentDayResultPanel.EnsureInScene(go.GetComponent<Canvas>());
    panel.Show(null,new StudentDaySummary(1,new string[0],new string[0],string.Empty));
    Assert.That(UiGameplayInputLock.IsBlocked,Is.True);
    panel.enabled=false;yield return null;Assert.That(UiGameplayInputLock.IsBlocked,Is.False);
    panel.enabled=true;yield return null;Assert.That(UiGameplayInputLock.IsBlocked,Is.True);
    panel.Hide();Assert.That(UiGameplayInputLock.IsBlocked,Is.False);
    panel.Show(null,new StudentDaySummary(1,new string[0],new string[0],string.Empty));
    Object.Destroy(go);yield return null;Assert.That(UiGameplayInputLock.IsBlocked,Is.False);
   } finally {if(go!=null)Object.Destroy(go);}
  }
 }
}
