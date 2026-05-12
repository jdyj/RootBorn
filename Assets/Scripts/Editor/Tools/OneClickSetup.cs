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
                Debug.Log("[ROOTBORN/OneClick] Step 1/6 - slicing Modern UI/Farm/Interiors sprite sheets...");
                EditorUtility.DisplayProgressBar("ROOTBORN", "Slicing Modern sprite sheets...", 0.1f);
                ModernUiSliceSetup.SliceAll();
                ModernFarmSliceSetup.SliceCore16();
                ModernInteriorsSliceSetup.SliceCore16();

                Debug.Log("[ROOTBORN/OneClick] Step 2/6 - generating default data SOs...");
                EditorUtility.DisplayProgressBar("ROOTBORN", "Generating default data SOs...", 0.3f);
                GenerateDefaultData.Generate();
                ModernWorldGenerationDataSetup.WireRegistry();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[ROOTBORN/OneClick] Step 3/6 - setting up scenes...");
                EditorUtility.DisplayProgressBar("ROOTBORN", "Setting up scenes (Boot/MainMenu/HostLobby/Farm)...", 0.5f);
                SceneSetup.SetupAll();

                Debug.Log("[ROOTBORN/OneClick] Step 4/6 - building Farm tilemap + resource nodes...");
                EditorUtility.DisplayProgressBar("ROOTBORN", "Building Farm tilemap + resource nodes...", 0.75f);
                FarmSceneBuilder.Build();

                Debug.Log("[ROOTBORN/OneClick] Step 5/6 - building Player prefab + Animator...");
                EditorUtility.DisplayProgressBar("ROOTBORN", "Building Player prefab + Animator...", 0.85f);
                PlayerSetup.Setup();

                Debug.Log("[ROOTBORN/OneClick] Step 6/6 - wiring Modern Addressables groups...");
                EditorUtility.DisplayProgressBar("ROOTBORN", "Wiring Modern Addressables groups...", 0.95f);
                ModernUiAddressablesSetup.WireSheets();
                ModernFarmAddressablesSetup.WireSheets();
                ModernInteriorsAddressablesSetup.WireSheets();

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
                "ROOTBORN - Play mode detected",
                "Setup tools run in Edit mode only.\n\nStop Play mode and continue?",
                "Stop and continue",
                "Cancel");

            if (!stop)
            {
                Debug.LogWarning("[ROOTBORN] Setup canceled because the editor is still in Play mode.");
                return false;
            }

            EditorApplication.isPlaying = false;
            Debug.LogWarning("[ROOTBORN] Play mode stopped. Click 'Setup Everything' again now that the editor is in Edit mode.");
            return false;
        }
    }
}
