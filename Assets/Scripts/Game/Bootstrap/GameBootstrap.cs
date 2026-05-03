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

            string nextScene = Config.Mode switch
            {
                SessionMode.None => _mainMenuScene,
                SessionMode.Client => _hostLobbyScene,
                _ => _farmScene
            };

            Debug.Log($"[ROOTBORN] Bootstrap mode={Config.Mode} port={Config.Port} maxPlayers={Config.MaxPlayers} saveSlot={Config.SaveSlot} → loading scene '{nextScene}'");
            SceneManager.LoadScene(nextScene);
        }
    }
}
