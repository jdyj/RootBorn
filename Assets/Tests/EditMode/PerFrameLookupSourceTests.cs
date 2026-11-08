using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    /// <summary>Components that run for a whole scene visit must not search the scene or look up components every frame.</summary>
    public sealed class PerFrameLookupSourceTests
    {
        [TestCase("Assets/Scripts/UI/Interiors/FurniturePlacementStyle2Skin.cs")]
        public void LateUpdate_DoesNotSearchOrAllocateEveryFrame(string sourcePath)
        {
            Assert.IsTrue(File.Exists(sourcePath), "Missing source: " + sourcePath);
            string source = File.ReadAllText(sourcePath);
            var match = Regex.Match(source, @"private void LateUpdate\(\)\s*\{(?<body>.*?)\n        \}", RegexOptions.Singleline);
            Assert.IsTrue(match.Success, "LateUpdate not found in " + sourcePath);
            string body = match.Groups["body"].Value;
            StringAssert.DoesNotContain("FindFirstObjectByType", body);
            StringAssert.DoesNotContain("FindObjectOfType", body);
            StringAssert.DoesNotContain("GameObject.Find", body);
            StringAssert.DoesNotContain("GetComponent", body);
            StringAssert.DoesNotContain("new[]", body);
        }
    }
}
