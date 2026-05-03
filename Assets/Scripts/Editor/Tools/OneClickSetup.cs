using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class OneClickSetup
    {
        [MenuItem("Rootborn/Setup Everything (One Click)", priority = 0)]
        public static void Run()
        {
            if (!EnsureNotPlaying()) return;

            try
            {
                EditorUtility.DisplayProgressBar("ROOTBORN", "Slicing Pixelwood sprite sheets...", 0.1f);
                PixelwoodSliceSetup.SliceAll();

                EditorUtility.DisplayProgressBar("ROOTBORN", "Generating default data SOs...", 0.3f);
                GenerateDefaultData.Generate();

                EditorUtility.DisplayProgressBar("ROOTBORN", "Setting up scenes (Boot/MainMenu/HostLobby/Farm)...", 0.5f);
                SceneSetup.SetupAll();

                EditorUtility.DisplayProgressBar("ROOTBORN", "Building Farm tilemap + resource nodes...", 0.75f);
                FarmSceneBuilder.Build();

                EditorUtility.DisplayProgressBar("ROOTBORN", "Building Player prefab + Animator...", 0.9f);
                PlayerSetup.Setup();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[ROOTBORN] One-Click setup complete. Open Assets/Scenes/Boot.unity and Play.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static bool EnsureNotPlaying()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;

            bool stop = EditorUtility.DisplayDialog(
                "ROOTBORN — Play 모드 감지",
                "Setup 도구는 Edit 모드에서만 동작합니다.\n\nPlay 모드를 정지할까요?",
                "정지하고 계속",
                "취소");

            if (!stop)
            {
                Debug.LogWarning("[ROOTBORN] Setup canceled — still in Play mode.");
                return false;
            }

            EditorApplication.isPlaying = false;
            Debug.LogWarning("[ROOTBORN] Play mode stopped. Click 'Setup Everything' again now that the editor is in Edit mode.");
            return false;
        }
    }
}
