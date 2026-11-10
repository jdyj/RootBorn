using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Rootborn.Tests.EditMode.World
{
    /// <summary>
    /// Every place the player can travel to needs the same scene setup: the authored-world marker (top status HUD,
    /// quest runtime, owner binding) and a CameraBounds box so the camera never shows the void outside the map.
    /// </summary>
    public sealed class WorldDestinationSceneSetupTests
    {
        private static string[] DestinationScenes()
        {
            return AssetDatabase.FindAssets("t:WorldDestinationDefinition", new[] { "Assets/Data/Locations/WorldTravel" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path)).FindProperty("_sceneName").stringValue)
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct()
                .OrderBy(name => name)
                .ToArray();
        }

        [Test]
        public void EveryTravelDestinationSceneHasTheAuthoredWorldMarkerAndCameraBounds()
        {
            var scenes = DestinationScenes();
            Assert.That(scenes.Length, Is.GreaterThanOrEqualTo(10), "travel destinations were not found");
            string markerGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/Game/World/AuthoredWorldScene.cs");
            Assert.That(markerGuid, Is.Not.Empty);
            var missing = scenes.Where(scene =>
            {
                string text = File.ReadAllText("Assets/Scenes/" + scene + ".unity");
                return !text.Contains("guid: " + markerGuid) || !text.Contains("m_Name: CameraBounds");
            }).ToArray();
            Assert.That(missing, Is.Empty, "destination scenes without the authored-world marker or CameraBounds");
        }
    }
}
