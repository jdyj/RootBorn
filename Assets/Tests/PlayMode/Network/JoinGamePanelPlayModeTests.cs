using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
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
    /// NET-JOIN: the main menu's "Join game" opens an address panel over the menu. Buttons are pressed with a mouse device.
    /// Typing into the fields is preparation (field text is set directly); the OS keyboard path is checked on FEVM by hand.
    /// </summary>
    public sealed class JoinGamePanelPlayModeTests
    {
        // Nothing listens here during tests, so a connect attempt must come back as a failure the player can read.
        private const string UnusedPort = "7979";
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
        public IEnumerator NET_JOIN_001_ClientButtonOpensAddressPanelOverTheMenu()
        {
            yield return PlayerFlowE2EDriver.ClickButton(MenuButton("ClientButton"));
            yield return null;

            var panel = Object.FindFirstObjectByType<JoinGamePanel>();
            Assert.IsNotNull(panel, "Join game should open the address panel.");
            Assert.IsTrue(panel.IsShown);
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name, "Joining is set up on the menu, not in an empty lobby scene.");
            Assert.AreEqual(GameBootstrap.Config.JoinIp, Field(panel, "JoinHostField").text);
            Assert.AreEqual(GameBootstrap.Config.Port.ToString(), Field(panel, "JoinPortField").text);

            yield return PlayerFlowE2EDriver.ClickButton(PanelButton(panel, "JoinBackButton"));
            yield return null;
            Assert.IsFalse(panel.IsShown, "Back should close the panel.");
            yield return PlayerFlowE2EDriver.ClickButton(MenuButton("ClientButton"));
            Assert.IsTrue(panel.IsShown, "The menu button should be reachable again after Back.");
        }

        [UnityTest]
        public IEnumerator NET_JOIN_002_InvalidPortIsExplainedWithoutStartingAClient()
        {
            yield return PlayerFlowE2EDriver.ClickButton(MenuButton("ClientButton"));
            var panel = Object.FindFirstObjectByType<JoinGamePanel>();
            Field(panel, "JoinPortField").text = "0"; // preparation: typed value

            yield return PlayerFlowE2EDriver.ClickButton(PanelButton(panel, "JoinConnectButton"));
            yield return null;

            Assert.AreEqual(JoinGamePanel.JoinState.Failed, panel.State);
            StringAssert.Contains("65535", panel.StatusText, "The status should say what a valid port is.");
            Assert.IsFalse(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening, "A rejected address must not start the client.");
            Assert.IsTrue(PanelButton(panel, "JoinConnectButton").interactable, "The player can fix the port and try again.");
        }

        [UnityTest]
        public IEnumerator NET_JOIN_003_UnreachableServerFailsVisiblyAndAllowsRetry()
        {
            Assert.IsNotNull(NetworkManager.Singleton, "Boot should provide the NetworkManager.");
            yield return PlayerFlowE2EDriver.ClickButton(MenuButton("ClientButton"));
            var panel = Object.FindFirstObjectByType<JoinGamePanel>();
            Field(panel, "JoinHostField").text = "127.0.0.1"; // preparation: typed values
            Field(panel, "JoinPortField").text = UnusedPort;

            yield return PlayerFlowE2EDriver.ClickButton(PanelButton(panel, "JoinConnectButton"));
            yield return null;
            Assert.AreEqual(JoinGamePanel.JoinState.Connecting, panel.State, panel.StatusText);
            Assert.IsFalse(PanelButton(panel, "JoinConnectButton").interactable, "Connect should not be pressed twice while connecting.");

            float deadline = Time.unscaledTime + 30f;
            while (panel.State == JoinGamePanel.JoinState.Connecting && Time.unscaledTime < deadline) yield return null;
            Assert.AreEqual(JoinGamePanel.JoinState.Failed, panel.State, "No server answered, so the attempt should fail within the shortened timeout. Status: " + panel.StatusText);
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);

            deadline = Time.unscaledTime + 5f;
            while (NetworkManager.Singleton.ShutdownInProgress && Time.unscaledTime < deadline) yield return null;
            Assert.IsFalse(NetworkManager.Singleton.IsListening, "The failed client should be shut down.");

            // Retrying starts a new attempt instead of being stuck on the old session.
            yield return PlayerFlowE2EDriver.ClickButton(PanelButton(panel, "JoinConnectButton"));
            yield return null;
            Assert.AreEqual(JoinGamePanel.JoinState.Connecting, panel.State, panel.StatusText);
            yield return PlayerFlowE2EDriver.ClickButton(PanelButton(panel, "JoinBackButton"));
            yield return null;
            Assert.IsFalse(panel.IsShown);
            deadline = Time.unscaledTime + 5f;
            while (NetworkManager.Singleton.ShutdownInProgress && Time.unscaledTime < deadline) yield return null;
            Assert.IsFalse(NetworkManager.Singleton.IsListening, "Back while connecting should cancel the attempt.");
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

        private static InputField Field(JoinGamePanel panel, string name)
        {
            var child = PlayerFlowE2EDriver.FindChild(panel.transform, name);
            Assert.IsNotNull(child, name + " not found.");
            return child.GetComponent<InputField>();
        }

        private static void ShutdownClient()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening) nm.Shutdown();
        }
    }
}
