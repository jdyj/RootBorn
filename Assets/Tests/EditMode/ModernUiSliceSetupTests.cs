using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Editor.Tools;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernUiSliceSetupTests
    {
        [Test]
        public void Catalog_ContainsThreeRuntimeModernUi16Sheets()
        {
            Assert.AreEqual(3, ModernUiSliceSetup.Targets.Count);
        }

        [Test]
        public void Catalog_PointsOnlyToModernUiPack()
        {
            foreach (var target in ModernUiSliceSetup.Targets)
            {
                StringAssert.StartsWith("Assets/modernuserinterface-win/16x16/", target.AssetPath);
                Assert.IsFalse(target.AssetPath.Contains("Pixelwood"));
                Assert.AreEqual(16, target.CellSize, target.AssetPath);
            }
        }

        [Test]
        public void Catalog_AssetsExistOnDisk()
        {
            var missing = new List<string>();
            foreach (var target in ModernUiSliceSetup.Targets)
            {
                if (!File.Exists(target.AssetPath))
                {
                    missing.Add(target.AssetPath);
                }
            }

            Assert.IsEmpty(missing, "Missing Modern UI sheet files: " + string.Join(", ", missing));
        }

        [Test]
        public void Catalog_DimensionsMatchCellSize()
        {
            var mismatches = new List<string>();
            foreach (var target in ModernUiSliceSetup.Targets)
            {
                var texture = LoadTexture(target.AssetPath);
                try
                {
                    if (texture.width % target.CellSize != 0 || texture.height % target.CellSize != 0)
                    {
                        mismatches.Add($"{target.AssetPath} {texture.width}x{texture.height} cell {target.CellSize}");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }

            Assert.IsEmpty(mismatches, "Modern UI sheets with non-grid dimensions: " + string.Join(", ", mismatches));
        }

        [Test]
        public void Catalog_ExpectedGridSizesMatchPngDimensions()
        {
            foreach (var target in ModernUiSliceSetup.Targets)
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
        public void BuildSpriteName_UsesStableRowColumnConvention()
        {
            var target = ModernUiSliceSetup.Targets[0];
            Assert.AreEqual($"{target.LabelPrefix}_r2_c3", ModernUiSliceSetup.BuildSpriteName(target, 2, 3));
        }

        [Test]
        public void SlicedSheets_ExposeFirstAndLastNamedSubSprites()
        {
            var missing = new List<string>();
            foreach (var target in ModernUiSliceSetup.Targets)
            {
                var spriteNames = AssetDatabase.LoadAllAssetsAtPath(target.AssetPath)
                    .OfType<Sprite>()
                    .Select(sprite => sprite.name)
                    .ToHashSet();

                string first = ModernUiSliceSetup.BuildSpriteName(target, 0, 0);
                string last = ModernUiSliceSetup.BuildSpriteName(target, target.Rows - 1, target.Columns - 1);
                if (!spriteNames.Contains(first))
                {
                    missing.Add($"{target.AssetPath}:{first}");
                }

                if (!spriteNames.Contains(last))
                {
                    missing.Add($"{target.AssetPath}:{last}");
                }
            }

            Assert.IsEmpty(missing, "Modern UI sheets not sliced with expected sub-sprite names: " + string.Join(", ", missing));
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
