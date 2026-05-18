using System;
using System.Threading.Tasks;
using Rootborn.Game.Managers;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Bootstrap
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string _mainMenuScene = "MainMenu";

        public static AppConfig Config { get; private set; }
        public static event Action<AppConfig> OnBootstrapped;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            Config = ArgsParser.Parse(Environment.GetCommandLineArgs());
            ApplyCommandLineSaveSlot(Config);
            OnBootstrapped?.Invoke(Config);

            await Managers.Managers.BootstrapAsync();

            if (Config.Mode != SessionMode.None)
            {
                Debug.Log($"[ROOTBORN] Bootstrap mode={Config.Mode} port={Config.Port} maxPlayers={Config.MaxPlayers} saveSlot={Config.SaveSlot} -> waiting for network scene sync");
                return;
            }

            Debug.Log($"[ROOTBORN] Bootstrap mode={Config.Mode} port={Config.Port} maxPlayers={Config.MaxPlayers} saveSlot={Config.SaveSlot} -> loading scene '{_mainMenuScene}'");
            SceneManager.LoadScene(_mainMenuScene);
        }

        private static void ApplyCommandLineSaveSlot(AppConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.SaveSlot)) return;
            ActiveSaveContext.Set(new SaveSlotMetadata
            {
                SlotId = config.SaveSlot,
                DisplayName = config.SaveSlot,
                CreatedAtUtcTicks = DateTime.UtcNow.Ticks,
                UpdatedAtUtcTicks = DateTime.UtcNow.Ticks,
            });
        }
    }
}
