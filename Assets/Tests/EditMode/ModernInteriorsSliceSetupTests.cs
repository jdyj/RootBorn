using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Editor.Tools;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernInteriorsSliceSetupTests
    {
        [Test]
        public void Catalog_ContainsCoreInteriorSheets()
        {
            Assert.AreEqual(3, ModernInteriorsSliceSetup.Targets.Count);
        }

        [Test]
        public void Catalog_PointsOnlyToModernInteriorsPack()
        {
            foreach (var target in ModernInteriorsSliceSetup.Targets)
            {
                StringAssert.StartsWith("Assets/moderninteriors-win/", target.AssetPath);
                Assert.IsFalse(target.AssetPath.Contains("Pixelwood"));
            }
        }

        [Test]
        public void Catalog_AssetsExistOnDisk()
        {
            var missing = new List<string>();
            foreach (var target in ModernInteriorsSliceSetup.Targets)
            {
                if (!File.Exists(target.AssetPath))
                {
                    missing.Add(target.AssetPath);
                }
            }

            Assert.IsEmpty(missing, "Missing Modern Interiors sheet files: " + string.Join(", ", missing));
        }

        [Test]
        public void Catalog_ExpectedGridSizesMatchPngDimensions()
        {
            foreach (var target in ModernInteriorsSliceSetup.Targets)
            {
                var texture = LoadTexture(target.AssetPath);
                try
                {
                    Assert.AreEqual(texture.width / target.CellSize, target.Columns, target.AssetPath);
                    Assert.AreEqual(texture.height / target.CellSize, target.Rows, target.AssetPath);
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        [Test]
        public void SlicedSheets_ExposeFirstAndLastNamedSubSprites()
        {
            var missing = new List<string>();
            foreach (var target in ModernInteriorsSliceSetup.Targets)
            {
                var spriteNames = AssetDatabase.LoadAllAssetsAtPath(target.AssetPath)
                    .OfType<Sprite>()
                    .Select(sprite => sprite.name)
                    .ToHashSet();

                string first = ModernInteriorsSliceSetup.BuildSpriteName(target, 0, 0);
                string last = ModernInteriorsSliceSetup.BuildSpriteName(target, target.Rows - 1, target.Columns - 1);
                if (!spriteNames.Contains(first))
                {
                    missing.Add($"{target.AssetPath}:{first}");
                }

                if (!spriteNames.Contains(last))
                {
                    missing.Add($"{target.AssetPath}:{last}");
                }
            }

            Assert.IsEmpty(missing, "Modern Interiors sheets not sliced with expected sub-sprite names: " + string.Join(", ", missing));
        }

        private static Texture2D LoadTexture(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2);
            Assert.IsTrue(texture.LoadImage(bytes), path);
            return texture;
        }
    }
}
