using System.Collections;
using NUnit.Framework;
using Rootborn.UI.MainMenu;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode
{
    public sealed class SaveSlotUiFlowTests
    {
        [UnityTest]
        public IEnumerator SaveSlotSelectPanel_Show_DisplaysThreeSlotCards()
        {
            var panel = SaveSlotSelectPanel.EnsureInScene();
            panel.Show();
            yield return null;

            Assert.IsNotNull(GameObject.Find("SaveSlotCard_slot-0"));
            Assert.IsNotNull(GameObject.Find("SaveSlotCard_slot-1"));
            Assert.IsNotNull(GameObject.Find("SaveSlotCard_slot-2"));
        }
    }
}
