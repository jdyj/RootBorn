using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Editor.Tools;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernFarmSliceSetupTests
    {
        [Test]
        public void Catalog_ContainsCoreFarmSheets()
        {
            Assert.AreEqual(7, ModernFarmSliceSetup.Targets.Count);
        }

        [Test]
        public void Catalog_PointsOnlyToModernFarmPack()
        {
            foreach (var target in ModernFarmSliceSetup.Targets)
            {
                StringAssert.StartsWith("Assets/Modern_Farm_v1.2/16x16/", target.AssetPath);
                Assert.IsFalse(target.AssetPath.Contains("Pixelwood"));
            }
        }

        [Test]
        public void Catalog_AssetsExistOnDisk()
        {
            var missing = new List<string>();
            foreach (var target in ModernFarmSliceSetup.Targets)
            {
                if (!File.Exists(target.AssetPath))
                {
                    missing.Add(target.AssetPath);
                }
            }

            Assert.IsEmpty(missing, "Missing Modern Farm sheet files: " + string.Join(", ", missing));
        }

        [Test]
        public void Catalog_ExpectedGridSizesMatchPngDimensions()
        {
            foreach (var target in ModernFarmSliceSetup.Targets)
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
        public void BuildSpriteName_UsesStableFarmConvention()
        {
            var target = ModernFarmSliceSetup.Targets[0];
            Assert.AreEqual($"{target.LabelPrefix}_r2_c3", ModernFarmSliceSetup.BuildSpriteName(target, 2, 3));
        }

        [Test]
        public void SlicedSheets_ExposeFirstAndLastNamedSubSprites()
        {
            var missing = new List<string>();
            foreach (var target in ModernFarmSliceSetup.Targets)
            {
                var spriteNames = AssetDatabase.LoadAllAssetsAtPath(target.AssetPath)
                    .OfType<Sprite>()
                    .Select(sprite => sprite.name)
                    .ToHashSet();

                string first = ModernFarmSliceSetup.BuildSpriteName(target, 0, 0);
                string last = ModernFarmSliceSetup.BuildSpriteName(target, target.Rows - 1, target.Columns - 1);
                if (!spriteNames.Contains(first))
                {
                    missing.Add($"{target.AssetPath}:{first}");
                }

                if (!spriteNames.Contains(last))
                {
                    missing.Add($"{target.AssetPath}:{last}");
                }
            }

            Assert.IsEmpty(missing, "Modern Farm sheets not sliced with expected sub-sprite names: " + string.Join(", ", missing));
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
