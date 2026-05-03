using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Bootstrap
{
    public sealed class SceneDiagnostics : MonoBehaviour
    {
        private void Start()
        {
            var scene = SceneManager.GetActiveScene();
            int rootCount = scene.rootCount;
            int cameraCount = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;
            int rendererCount = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Length;
            int tilemapCount = Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None).Length;

            Debug.Log($"[ROOTBORN] Scene '{scene.name}' loaded — rootObjects={rootCount}, cameras={cameraCount}, spriteRenderers={rendererCount}, tilemaps={tilemapCount}");

            if (rendererCount == 0 && tilemapCount == 0)
            {
                Debug.LogWarning($"[ROOTBORN] '{scene.name}' has no visual content. Run 'Rootborn → Setup Everything (One Click)' in Edit mode.");
            }
        }
    }
}
