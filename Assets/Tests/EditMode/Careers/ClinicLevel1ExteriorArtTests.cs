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
    public sealed class ClinicLevel1ExteriorArtTests
    {
        private const string SpritePath = "Assets/Art/CareerFacilities/Sprites/emergencycare_lv1.png";
        private const string VisualPath = "Assets/Data/Careers/FacilityVisuals/emergencycare_lv1.asset";
        private const string Address = "sprites/career-facilities/emergencycare_lv1";

        [Test]
        public void ClinicPixelsUseBinaryAlphaAtMost31VisibleColorsAndExactTwoPixelClusters()
        {
            var raw = DecodeSource();
            try
            {
                var pixels = raw.GetPixels32();
                Assert.That(pixels.All(p => p.a == 0 || p.a == 255), Is.True,
                    "The small Town facade must retain crisp binary transparency.");
                var colors = new HashSet<uint>(pixels.Where(p => p.a != 0).Select(VisibleColor));
                Assert.That(colors.Count, Is.LessThanOrEqualTo(31),
                    "The level1 clinic palette must remain readable at native Town scale.");
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
        public void ClinicSilhouetteFitsTheOriginalLowBuildingEnvelopeAndKeepsItsFloor()
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
                Assert.That(minX, Is.GreaterThanOrEqualTo(8));
                Assert.That(maxX + 1, Is.LessThanOrEqualTo(184));
                Assert.That(maxX - minX + 1, Is.GreaterThanOrEqualTo(168),
                    "Retain the broad facade covering the established four-cell collision site.");
                Assert.That(minTop, Is.GreaterThanOrEqualTo(46), "Do not add a new floor or taller roof above the established facade.");
                Assert.That(maxTop + 1, Is.EqualTo(156), "Keep the original top-origin, bottom-exclusive floor line at156.");
                Assert.That(maxTop - minTop + 1, Is.GreaterThanOrEqualTo(106),
                    "Retain the original low building's apparent height instead of shrinking its silhouette.");
                Assert.That((minX + maxX + 1) * .5f, Is.EqualTo(96f).Within(2f),
                    "Keep the facade centered over its existing entrance/site anchors.");
            }
            finally { Object.DestroyImmediate(raw); }
        }

        [Test]
        public void CosmeticClinicReplacementPreservesSpriteIdentityImportAndDataDefinedSite()
        {
            Assert.That(AssetDatabase.AssetPathToGUID(SpritePath), Is.EqualTo("0be17e2eb128c8740a73e1bb446180b2"));
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0f, 0f, 192f, 160f)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(48f));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(96f, 4f)));
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
            Assert.That(visual.Facility, Is.SameAs(AssetDatabase.LoadAssetAtPath<FacilityDefinition>("Assets/Data/Careers/Facilities/EmergencyCare/Facility_EmergencyCare.asset")));
            Assert.That(visual.Level, Is.EqualTo(1));
            Assert.That(visual.SpriteAddress, Is.EqualTo(Address));
            Assert.That(visual.SiteSize, Is.EqualTo(new Vector2Int(4, 2)));
            Assert.That(visual.SpriteOffset, Is.EqualTo(new Vector2(1.5f, -.5f)));
            Assert.That(visual.EntranceLocalPosition, Is.EqualTo(new Vector2(1.5f, -.5f)));
            Assert.That(visual.CollisionCells, Is.EqualTo(new[] {
                new Vector2Int(0, 0), new Vector2Int(3, 0), new Vector2Int(0, 1),
                new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1) }));
            Assert.That(visual.EntranceCells, Is.EqualTo(new[] { new Vector2Int(1, 0), new Vector2Int(2, 0) }));
            Assert.That(visual.ApproachCells, Is.EqualTo(new[] { new Vector2Int(1, -1), new Vector2Int(2, -1) }));
            Assert.That(visual.IsValid, Is.True);
        }

        private static Texture2D DecodeSource()
        {
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(raw.LoadImage(File.ReadAllBytes(SpritePath)), Is.True);
                Assert.That(raw.width, Is.EqualTo(192));
                Assert.That(raw.height, Is.EqualTo(160));
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
