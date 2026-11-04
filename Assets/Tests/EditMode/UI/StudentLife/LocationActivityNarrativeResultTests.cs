using System.Reflection;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Save;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    public sealed class LocationActivityNarrativeResultTests
    {
        [Test]
        public void SuccessfulClueAction_ShowsItsAuthoredResultText_AlongsideTheTimeBudget()
        {
            var field = typeof(LocationActivityDefinition).GetField("_resultTextKey", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Clue activities need an authored outcome message, not only TP and stat numbers.");
            string temp = Path.Combine(Path.GetTempPath(), "rootborn-narrative", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp); SaveService.SetRootDirectoryForTests(temp);
            var canvas = new GameObject("NarrativeCanvas", typeof(RectTransform), typeof(Canvas));
            var panelGo = new GameObject("NarrativePanel"); panelGo.transform.SetParent(canvas.transform, false);
            var panel = panelGo.AddComponent<LocationIdentityPanel>();
            var player = new GameObject("NarrativePlayer", typeof(StudentLifeProgressComponent));
            player.GetComponent<StudentLifeProgressComponent>().ConfigureForTests("narrative-slot", "narrative-player", 20, 20, 0, 480);
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests("fixture.library", "Library", Vector2.zero, null);
            var activity = ScriptableObject.CreateInstance<LocationActivityDefinition>();
            activity.ConfigureForTests("fixture.clue", "Inspect", location, LocationGrowthRoute.Exploration, 0, 0, 0, 0, null, null);
            const string message = "A faded photograph sits behind the locked Archive door.";
            field.SetValue(activity, message); // Unregistered text falls back to itself via GameText.
            var identity = ScriptableObject.CreateInstance<LocationIdentityDefinition>();
            identity.ConfigureForTests("fixture.identity", "Library", location, "Library", new[] { activity }, null, 480, 1140);
            try
            {
                panel.Show(identity, player); panel.ClickActivityForTests(0);
                var result = (Text)typeof(LocationIdentityPanel).GetField("_result", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                StringAssert.Contains(message, result.text);
                Assert.That(result.text.Length, Is.GreaterThan(message.Length), "Time budget feedback remains visible alongside the story result.");
            }
            finally
            {
                Object.DestroyImmediate(canvas); Object.DestroyImmediate(player); Object.DestroyImmediate(identity);
                Object.DestroyImmediate(activity); Object.DestroyImmediate(location);
                SaveService.SetRootDirectoryForTests(null); if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }
    }
}
