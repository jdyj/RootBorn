using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Bootstrap
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string _mainMenuScene = "MainMenu";
        [SerializeField] private string _farmScene = "Farm";
        [SerializeField] private string _hostLobbyScene = "HostLobby";

        public static AppConfig Config { get; private set; }
        public static event Action<AppConfig> OnBootstrapped;

        private void Start()
        {
            Config = ArgsParser.Parse(Environment.GetCommandLineArgs());
            OnBootstrapped?.Invoke(Config);

            Debug.Log($"[ROOTBORN] Bootstrap mode={Config.Mode} port={Config.Port} maxPlayers={Config.MaxPlayers} saveSlot={Config.SaveSlot}");

            switch (Config.Mode)
            {
                case SessionMode.None:
                    SceneManager.LoadScene(_mainMenuScene);
                    break;
                case SessionMode.Single:
                case SessionMode.Host:
                case SessionMode.Server:
                    SceneManager.LoadScene(_farmScene);
                    break;
                case SessionMode.Client:
                    SceneManager.LoadScene(_hostLobbyScene);
                    break;
            }
        }
    }
}
