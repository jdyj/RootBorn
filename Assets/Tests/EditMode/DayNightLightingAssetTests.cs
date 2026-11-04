using NUnit.Framework;
using Rootborn.Game.Lighting;
using Rootborn.Game.Time;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode
{
    public sealed class DayNightLightingAssetTests
    {
        [Test]
        public void LIGHT_DAYNIGHT_EDIT_004_DefaultProfileAssetExistsAndEvaluatesAllDayParts()
        {
            var profile = AssetDatabase.LoadAssetAtPath<DayNightLightingProfile>("Assets/Data/Time/DayNightLighting_Default.asset");

            Assert.IsNotNull(profile);
            Assert.Greater(profile.EvaluateIntensity(12f), profile.EvaluateIntensity(22f));
            Assert.Greater(profile.EvaluateIntensity(7f), profile.EvaluateIntensity(4f));
        }

        [Test]
        public void LIGHT_DAYNIGHT_EDIT_006_ProjectUsesUrp2DRenderer()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            var renderer2D = AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/PC_2D_Renderer.asset");
            Assert.IsNotNull(pipeline, "Project must use URP for 2D lighting.");
            Assert.IsNotNull(renderer2D, "Project must store a Renderer2DData asset.");

            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            Assert.IsNotNull(list);
            Assert.Greater(list.arraySize, 0);
            Assert.AreSame(renderer2D, list.GetArrayElementAtIndex(0).objectReferenceValue, "URP default renderer must be the 2D Renderer for Light2D to affect sprites.");
        }
        [Test]
        public void LIGHT_DAYNIGHT_EDIT_005_TownSceneStoresGlobalLightManagerAndLitTilemaps()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Town.unity", OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid());

            var clock = Object.FindFirstObjectByType<GameClock>();
            var manager = Object.FindFirstObjectByType<LightingManager>();
            var pixelPerfect = Object.FindFirstObjectByType<PixelPerfectCamera>();
            var lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var tilemapRenderers = Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Assert.IsNotNull(clock, "Town scene must store the GameClock used by lighting.");
            Assert.IsNotNull(manager, "Town scene must store LightingManager.");
            Assert.IsNotNull(pixelPerfect, "Town scene must store PixelPerfectCamera.");
            Assert.AreEqual(16, pixelPerfect.assetsPPU);
            var camera = Camera.main;
            Assert.IsNotNull(camera, "Town scene must store a Main Camera.");
            Assert.AreEqual(9f, camera.orthographicSize, 0.01f, "Pixel Perfect settings must preserve the existing Town camera gameplay framing.");
            Assert.AreEqual(512, pixelPerfect.refResolutionX);
            Assert.AreEqual(288, pixelPerfect.refResolutionY);
            Assert.IsTrue(pixelPerfect.cropFrameX, "Town Pixel Perfect crop must preserve the existing orthographicSize 9 gameplay framing.");
            Assert.IsTrue(pixelPerfect.cropFrameY, "Town Pixel Perfect crop must preserve the existing orthographicSize 9 gameplay framing.");
            Assert.IsTrue(System.Array.Exists(lights, light => light.lightType == Light2D.LightType.Global), "Town scene must store a Global Light 2D.");
            Assert.IsTrue(System.Array.Exists(lights, light => light.lightType == Light2D.LightType.Point), "Town scene must store at least one Point Light 2D local light.");
            Assert.Greater(tilemapRenderers.Length, 0);

            foreach (var renderer in tilemapRenderers)
            {
                Assert.IsNotNull(renderer.sharedMaterial, renderer.name + " must have a lit material assigned.");
                Assert.AreEqual("Sprite-Lit-Default", renderer.sharedMaterial.name, renderer.name + " must receive URP 2D Light2D.");
            }
        }
    }
}