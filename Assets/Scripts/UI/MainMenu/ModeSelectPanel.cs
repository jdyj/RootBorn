using Rootborn.Game.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.MainMenu
{
    public sealed class ModeSelectPanel : MonoBehaviour
    {
        [SerializeField] private Button _singleButton;
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _clientButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private string _farmScene = "Farm";
        [SerializeField] private string _hostLobbyScene = "HostLobby";

        private void Awake()
        {
            if (_singleButton != null) _singleButton.onClick.AddListener(OnSingle);
            if (_hostButton != null) _hostButton.onClick.AddListener(OnHost);
            if (_clientButton != null) _clientButton.onClick.AddListener(OnClient);
            if (_quitButton != null) _quitButton.onClick.AddListener(OnQuit);
        }

        private void OnSingle()
        {
            ApplyMode(SessionMode.Single);
            var slots = SaveSlotSelectPanel.EnsureInScene();
            slots.Show();
        }

        private void OnHost()
        {
            ApplyMode(SessionMode.Host);
            SceneManager.LoadScene(_farmScene);
        }

        private void OnClient()
        {
            ApplyMode(SessionMode.Client);
            SceneManager.LoadScene(_hostLobbyScene);
        }

        private void OnQuit()
        {
            Application.Quit();
        }

        private static void ApplyMode(SessionMode mode)
        {
            if (GameBootstrap.Config == null)
            {
                return;
            }

            GameBootstrap.Config.Mode = mode;
        }
    }
}
