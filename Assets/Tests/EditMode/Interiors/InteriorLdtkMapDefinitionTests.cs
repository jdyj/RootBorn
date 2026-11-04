using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class InteriorLdtkMapDefinitionTests
    {
        [Test]
        public void INTERIOR_LDTK_EDIT_001_GeneratedTilesUseOriginalTilesetPixelCoordinates()
        {
            AssertLdtkTileRect("Assets/LDtk/Generated/InteriorTiles/_Bedroom_Black_Shadow_32x32_x448_y640_f0.asset",
                "Assets/moderninteriors-win/1_Interiors/32x32/Theme_Sorter_Black_Shadow_32x32/4_Bedroom_Black_Shadow_32x32.png",
                448, 640, 32, 32);

            AssertLdtkTileRect("Assets/LDtk/Generated/InteriorTiles/Room_Builder_borders_32x322_x224_y192_f0.asset",
                "Assets/moderninteriors-win/1_Interiors/32x32/Room_Bulder_subfiles_32x32/Room_Builder_borders_32x32.png",
                224, 192, 32, 32);
        }

        private static void AssertLdtkTileRect(string tilePath, string texturePath, int sourceX, int sourceY, int width, int height)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            Assert.IsNotNull(tile, tilePath + " must exist.");
            Assert.IsNotNull(tile.sprite, tilePath + " must keep a generated sprite.");

            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            Assert.IsNotNull(importer, texturePath + " must import through TextureImporter.");
            var dimensions = ReadPngDimensions(texturePath);
            Assert.GreaterOrEqual(importer.maxTextureSize, Math.Max(dimensions.x, dimensions.y), texturePath + " must not be downscaled before LDtk sprite rect generation.");

            Rect rect = tile.sprite.rect;
            Assert.AreEqual(sourceX, rect.x, 0.001f, tilePath + " rect.x must match LDtk src x.");
            Assert.AreEqual(dimensions.y - sourceY - height, rect.y, 0.001f, tilePath + " rect.y must convert LDtk top-left src y from the original PNG height.");
            Assert.AreEqual(width, rect.width, 0.001f, tilePath + " rect.width must match LDtk tile width.");
            Assert.AreEqual(height, rect.height, 0.001f, tilePath + " rect.height must match LDtk tile height.");
        }

        private static Vector2Int ReadPngDimensions(string assetPath)
        {
            byte[] bytes = File.ReadAllBytes(assetPath);
            if (bytes.Length < 24 || bytes[12] != (byte)'I' || bytes[13] != (byte)'H' || bytes[14] != (byte)'D' || bytes[15] != (byte)'R')
            {
                throw new InvalidOperationException(assetPath + " is not a PNG with an IHDR header.");
            }

            int width = ReadBigEndianInt(bytes, 16);
            int height = ReadBigEndianInt(bytes, 20);
            return new Vector2Int(width, height);
        }

        private static int ReadBigEndianInt(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
        }
    }
}
