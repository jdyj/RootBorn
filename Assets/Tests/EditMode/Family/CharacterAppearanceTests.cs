using NUnit.Framework;
using Rootborn.Game.Family;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class CharacterAppearanceTests
    {
        [Test]
        public void Appearance_StoresStablePartIdsByCategoryAndRoundTripsThroughJson()
        {
            var appearance = new CharacterAppearance();
            appearance.SetSelectedPart("body", "character.body.01");
            appearance.SetSelectedPart("hair", "character.hair.short.blonde");
            appearance.SetSelectedPart("body", "character.body.02");

            Assert.AreEqual("character.body.02", appearance.GetSelectedPartId("body"));
            Assert.AreEqual("character.hair.short.blonde", appearance.GetSelectedPartId("hair"));

            string json = JsonUtility.ToJson(appearance);
            StringAssert.Contains("character.body.02", json);
            Assert.IsFalse(json.Contains("UnityEngine.Object"));

            var loaded = JsonUtility.FromJson<CharacterAppearance>(json);
            Assert.AreEqual("character.body.02", loaded.GetSelectedPartId("body"));
            Assert.AreEqual("character.hair.short.blonde", loaded.GetSelectedPartId("hair"));
        }
    }
}
