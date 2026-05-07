using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class PlayerSetupToolPartSourceTests
    {
        private const string PlayerSetupPath = "Assets/Scripts/Editor/Tools/PlayerSetup.cs";

        [Test]
        public void BuildPlayerPrefab_GeneratesDedicatedToolPartRendererAndWiresController()
        {
            string source = File.ReadAllText(PlayerSetupPath);

            StringAssert.Contains("Part_tool", source);
            StringAssert.Contains("var toolRenderer = toolPart.AddComponent<SpriteRenderer>();", source);
            StringAssert.Contains("toolRenderer.sortingOrder = 10", source);
            StringAssert.Contains("FindProperty(\"_toolRenderer\").objectReferenceValue = toolRenderer", source);
        }

        [Test]
        public void BuildPlayerPrefab_KeepsStrictSixteenPixelCharacterScaleAtOne()
        {
            string source = File.ReadAllText(PlayerSetupPath);

            Assert.IsFalse(source.Contains("new Vector3(2.0f, 2.0f, 1f)"));
            StringAssert.Contains("Vector3.one", source);
        }
    }
}
