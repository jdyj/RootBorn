using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Careers;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Careers
{
    public sealed class StudioLevel2ExteriorArtTests
    {
        private const string SpritePath = "Assets/Art/CareerFacilities/Sprites/interior_lv2.png";
        private const string VisualPath = "Assets/Data/Careers/FacilityVisuals/interior_lv2.asset";
        private const string Address = "sprites/career-facilities/interior_lv2";

        [Test]
        public void StudioPixelsUseBinaryAlphaAtMost31VisibleColorsAndExactTwoPixelClusters()
        {
            var raw = DecodeSource();
            try
            {
                var pixels = raw.GetPixels32();
                Assert.That(pixels.All(p => p.a == 0 || p.a == 255), Is.True,
                    "The small Town facade must retain crisp binary transparency.");
                var colors = new HashSet<uint>(pixels.Where(p => p.a != 0).Select(VisibleColor));
                Assert.That(colors.Count, Is.LessThanOrEqualTo(31),
                    "The level1 studio palette must remain readable at native Town scale.");
                string firstMismatch = null;
                for (int y = 0; y < raw.height && firstMismatch == null; y += 2)
                    for (int x = 0; x < raw.width; x += 2)
                    {
                        uint expected = VisibleColor(pixels[y * raw.width + x]);
                        if (VisibleColor(pixels[y * raw.width + x + 1]) != expected
                            || VisibleColor(pixels[(y + 1) * raw.width + x]) != expected
                            || VisibleColor(pixels[(y + 1) * raw.width + x + 1]) != expected)
                        {
                            firstMismatch = "First nonuniform 2x2 cluster at source bottom-left pixel " + x + "," + y;
                            break;
                        }
                    }
                Assert.That(firstMismatch, Is.Null,
                    "All visible color/alpha boundaries must use exact 2x2 pixel clusters. " + firstMismatch);
            }
            finally { Object.DestroyImmediate(raw); }
        }

        [Test]
        public void StudioSilhouetteFitsTheOriginalLowBuildingEnvelopeAndKeepsItsFloor()
        {
            var raw = DecodeSource();
            try
            {
                var pixels = raw.GetPixels32();
                int minX = raw.width, minTop = raw.height, maxX = -1, maxTop = -1;
                for (int y = 0; y < raw.height; y++)
                    for (int x = 0; x < raw.width; x++)
                    {
                        if (pixels[y * raw.width + x].a == 0) continue;
                        int topY = raw.height - 1 - y;
                        minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                        minTop = Mathf.Min(minTop, topY); maxTop = Mathf.Max(maxTop, topY);
                    }
                Assert.That(maxX, Is.GreaterThanOrEqualTo(minX), "Facade must have a visible silhouette.");
                Assert.That(minX, Is.GreaterThanOrEqualTo(12));
                Assert.That(maxX + 1, Is.LessThanOrEqualTo(244));
                Assert.That(maxX - minX + 1, Is.GreaterThanOrEqualTo(224),
                    "Retain the broad facade covering the established five-cell collision site.");
                Assert.That(minTop, Is.GreaterThanOrEqualTo(60), "Do not add a new floor or taller roof above the established facade.");
                Assert.That(maxTop + 1, Is.EqualTo(188), "Keep the original top-origin, bottom-exclusive floor line at188.");
                Assert.That(maxTop - minTop + 1, Is.GreaterThanOrEqualTo(124),
                    "Retain the original low building's apparent height instead of shrinking its silhouette.");
                Assert.That((minX + maxX + 1) * .5f, Is.EqualTo(128f).Within(2f),
                    "Keep the facade centered over its existing entrance/site anchors.");
            }
            finally { Object.DestroyImmediate(raw); }
        }

        [Test]
        public void CosmeticStudioReplacementPreservesSpriteIdentityImportAndDataDefinedSite()
        {
            Assert.That(AssetDatabase.AssetPathToGUID(SpritePath), Is.EqualTo("b8758e130342e164b9911ba73f04c853"));
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0f, 0f, 256f, 192f)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(48f));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(128f, 4f)));
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.isReadable, Is.False, "Decode a temporary source texture instead of changing importer readability.");
            var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(SpritePath));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.address, Is.EqualTo(Address));
            Assert.That(entry.parentGroup.Name, Is.EqualTo("Sprites"));

            var visual = AssetDatabase.LoadAssetAtPath<FacilityVisualDefinition>(VisualPath);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.Facility, Is.SameAs(AssetDatabase.LoadAssetAtPath<FacilityDefinition>("Assets/Data/Careers/Facilities/Interior/Facility_Interior.asset")));
            Assert.That(visual.Level, Is.EqualTo(2));
            Assert.That(visual.SpriteAddress, Is.EqualTo(Address));
            Assert.That(visual.SiteSize, Is.EqualTo(new Vector2Int(5, 3)));
            Assert.That(visual.SpriteOffset, Is.EqualTo(new Vector2(2f, -.5f)));
            Assert.That(visual.EntranceLocalPosition, Is.EqualTo(new Vector2(2f, -.5f)));
            Assert.That(visual.CollisionCells, Is.EqualTo(new[] {
                new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(3,0),new Vector2Int(4,0),
                new Vector2Int(0,1),new Vector2Int(1,1),new Vector2Int(2,1),new Vector2Int(3,1),new Vector2Int(4,1),
                new Vector2Int(0,2),new Vector2Int(1,2),new Vector2Int(2,2),new Vector2Int(3,2),new Vector2Int(4,2) }));
            Assert.That(visual.EntranceCells, Is.EqualTo(new[] {new Vector2Int(2,0)}));
            Assert.That(visual.ApproachCells, Is.EqualTo(new[] {new Vector2Int(2,-1)}));
            Assert.That(visual.IsValid, Is.True);
        }

        private static Texture2D DecodeSource()
        {
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(raw.LoadImage(File.ReadAllBytes(SpritePath)), Is.True);
                Assert.That(raw.width, Is.EqualTo(256));
                Assert.That(raw.height, Is.EqualTo(192));
                return raw;
            }
            catch { Object.DestroyImmediate(raw); throw; }
        }

        private static uint VisibleColor(Color32 p)
        {
            // Invisible RGB bytes do not form visible pixel clusters or palette entries.
            return p.a == 0 ? 0u : ((uint)p.a << 24) | ((uint)p.r << 16) | ((uint)p.g << 8) | p.b;
        }
    }
}
