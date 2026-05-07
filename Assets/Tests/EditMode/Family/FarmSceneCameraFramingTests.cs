using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class FarmSceneCameraFramingTests
    {
        [Test]
        public void FarmScene_MainCameraFramesModernFarmFullViewportOrthographic()
        {
            var previousScene = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Farm.unity", OpenSceneMode.Single);
            try
            {
                var camera = Camera.main;
                Assert.IsNotNull(camera);
                Assert.IsTrue(camera.orthographic);
                Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), camera.rect);
                Assert.AreEqual(15f, camera.transform.position.x, 0.01f);
                Assert.AreEqual(10f, camera.transform.position.y, 0.01f);
                Assert.AreEqual(-10f, camera.transform.position.z, 0.01f);
                Assert.AreEqual(8f, camera.orthographicSize, 0.01f);
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousScene) && previousScene != scene.path)
                {
                    EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
                }
            }
        }
    }
}
