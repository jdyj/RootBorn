using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class OneClickSetup
    {
        [MenuItem("Rootborn/Setup Everything (One Click)", priority = 0)]
        public static void Run()
        {
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
    }
}
