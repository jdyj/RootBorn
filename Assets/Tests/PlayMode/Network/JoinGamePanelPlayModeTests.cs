using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Network.Steam;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using Rootborn.UI.MainMenu;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Network
{
    /// <summary>
    /// NET-JOIN: the main menu's "Join a friend" opens the Steam friend room panel over the menu (buttons pressed with a
    /// mouse device). Real Steam joins need two logged-in Steam accounts and are checked by hand later.
    /// </summary>
    public sealed class JoinGamePanelPlayModeTests
    {
        private SessionMode _mode;
        private string _joinIp;
        private ushort _port;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ShutdownClient();
            yield return PlayerFlowE2EDriver.LoadScene("Boot");
            yield return PlayerFlowE2EDriver.WaitForScene("MainMenu", 15f, "Boot did not reach the main menu.");
            var config = GameBootstrap.Config;
            Assert.IsNotNull(config, "Boot should create the app config.");
            _mode = config.Mode;
            _joinIp = config.JoinIp;
            _port = config.Port;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ShutdownClient();
            var config = GameBootstrap.Config;
            if (config != null)
            {
                config.Mode = _mode;
                config.JoinIp = _joinIp;
                config.Port = _port;
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator NET_JOIN_001_JoinAFriendOpensTheRoomPanelOverTheMenu()
        {
            yield return PlayerFlowE2EDriver.ClickButton(MenuButton("ClientButton"));
            yield return null;

            var panel = Object.FindFirstObjectByType<JoinGamePanel>();
            Assert.IsNotNull(panel, "Join a friend should open the room panel.");
            Assert.IsTrue(panel.IsShown);
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name, "Joining is set up on the menu, not in an empty lobby scene.");
            Assert.IsNull(PlayerFlowE2EDriver.FindChild(panel.transform, "JoinHostField"), "Players pick a friend's room; there is no address to type.");
            if (SteamPlatform.IsReady)
            {
                Assert.AreEqual(JoinGamePanel.JoinState.Idle, panel.State, panel.StatusText);
            }
            else
            {
                // Test machines run without Steam: the panel must say why no rooms can be listed.
                Assert.AreEqual(JoinGamePanel.JoinState.SteamUnavailable, panel.State, panel.StatusText);
                StringAssert.Contains("Steam", panel.StatusText);
                Assert.IsFalse(PlayerFlowE2EDriver.FindChild(panel.transform, "JoinRefreshButton").gameObject.activeInHierarchy);
                Assert.AreEqual(0, panel.RoomRowCount);
            }

            yield return PlayerFlowE2EDriver.ClickButton(PanelButton(panel, "JoinBackButton"));
            yield return null;
            Assert.IsFalse(panel.IsShown, "Back should close the panel.");
            yield return PlayerFlowE2EDriver.ClickButton(MenuButton("ClientButton"));
            Assert.IsTrue(panel.IsShown, "The menu button should be reachable again after Back.");
        }

        [UnityTest]
        public IEnumerator NET_JOIN_002_WithoutSteamAJoinRequestExplainsInsteadOfConnecting()
        {
            if (SteamPlatform.IsReady) Assert.Ignore("Steam is running on this machine; this case covers machines without it.");
            yield return PlayerFlowE2EDriver.ClickButton(MenuButton("ClientButton"));
            var panel = Object.FindFirstObjectByType<JoinGamePanel>();

            panel.JoinLobby(109775241021923456UL); // preparation: a lobby picked in Steam's friends list

            Assert.AreEqual(JoinGamePanel.JoinState.SteamUnavailable, panel.State);
            Assert.IsFalse(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening, "No client may start without Steam.");
        }

        private static Button MenuButton(string name)
        {
            var menu = Object.FindFirstObjectByType<ModeSelectPanel>();
            Assert.IsNotNull(menu, "The main menu should be showing.");
            var button = PlayerFlowE2EDriver.FindButton(menu.transform, name);
            Assert.IsNotNull(button, name + " not found. " + PlayerFlowE2EDriver.DescribeButtons(menu.transform));
            return button;
        }

        private static Button PanelButton(JoinGamePanel panel, string name)
        {
            var button = PlayerFlowE2EDriver.FindButton(panel.transform, name);
            Assert.IsNotNull(button, name + " not found. " + PlayerFlowE2EDriver.DescribeButtons(panel.transform));
            return button;
        }

        private static void ShutdownClient()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening) nm.Shutdown();
        }
    }
}
