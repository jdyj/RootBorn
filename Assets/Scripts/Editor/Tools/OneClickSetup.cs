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

            Debug.Log("[ROOTBORN/OneClick] === START ===");
            try
            {
                Debug.Log("[ROOTBORN/OneClick] Step 1/5 — slicing Pixelwood sprite sheets...");
                EditorUtility.DisplayProgressBar("ROOTBORN", "Slicing Pixelwood sprite sheets...", 0.1f);
                PixelwoodSliceSetup.SliceAll();

                Debug.Log("[ROOTBORN/OneClick] Step 2/5 — generating default data SOs...");
                EditorUtility.DisplayProgressBar("ROOTBORN", "Generating default data SOs...", 0.3f);
                GenerateDefaultData.Generate();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[ROOTBORN/OneClick] Step 3/5 — setting up scenes...");
                EditorUtility.DisplayProgressBar("ROOTBORN", "Setting up scenes (Boot/MainMenu/HostLobby/Farm)...", 0.5f);
                SceneSetup.SetupAll();

                Debug.Log("[ROOTBORN/OneClick] Step 4/5 — building Farm tilemap + resource nodes...");
                EditorUtility.DisplayProgressBar("ROOTBORN", "Building Farm tilemap + resource nodes...", 0.75f);
                FarmSceneBuilder.Build();

                Debug.Log("[ROOTBORN/OneClick] Step 5/6 — building Player prefab + Animator...");
                EditorUtility.DisplayProgressBar("ROOTBORN", "Building Player prefab + Animator...", 0.85f);
                PlayerSetup.Setup();

                Debug.Log("[ROOTBORN/OneClick] Step 6/6 — wiring Addressables groups...");
                EditorUtility.DisplayProgressBar("ROOTBORN", "Wiring Addressables groups...", 0.95f);
                AddressablesSetup.WireAll();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[ROOTBORN/OneClick] === COMPLETE === Open Assets/Scenes/Boot.unity and Play.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ROOTBORN/OneClick] FAILED: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                throw;
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
