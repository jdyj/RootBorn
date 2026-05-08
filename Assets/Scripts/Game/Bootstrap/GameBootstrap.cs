using System;
using System.Threading.Tasks;
using Rootborn.Game.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Bootstrap
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string _mainMenuScene = "MainMenu";
        [SerializeField] private string _townScene = "Town";
        [SerializeField] private string _hostLobbyScene = "HostLobby";

        public static AppConfig Config { get; private set; }
        public static event Action<AppConfig> OnBootstrapped;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            Config = ArgsParser.Parse(Environment.GetCommandLineArgs());
            OnBootstrapped?.Invoke(Config);

            await Managers.Managers.BootstrapAsync();

            string nextScene = Config.Mode switch
            {
                SessionMode.None => _mainMenuScene,
                SessionMode.Client => _hostLobbyScene,
                _ => _townScene
            };

            Debug.Log($"[ROOTBORN] Bootstrap mode={Config.Mode} port={Config.Port} maxPlayers={Config.MaxPlayers} saveSlot={Config.SaveSlot} -> loading scene '{nextScene}'");
            SceneManager.LoadScene(nextScene);
        }
    }
}