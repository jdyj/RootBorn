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
    }
}
