using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Placement;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.House
{
    public sealed class StoryPhotoComfyAssetTests
    {
        const string SpritePath = "Assets/Art/HousePixels/Story31/story_town_photo.png";
        const string LegacyPath = "Assets/Art/StudentLifePixels/Sprites/town_record_photo.png";
        const string TilePath = "Assets/Data/Interiors/Furniture/Story/Tiles/Story_PhotoBoard_Tile.asset";
        const string FurniturePath = "Assets/Data/Interiors/Furniture/Story/Furniture_Story_PhotoBoard.asset";
        const string BuilderPath = "Assets/Scripts/Editor/StudentLife/MainStoryOpeningDataBuilder.cs";

        [Test]
        public void DedicatedPhotoHasOriginalCanvasPivotAndCrispImportWithOwnAddress()
        {
            var sprite = Required<Sprite>(SpritePath);
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, 40, 32)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(48f));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(20, 2)));
            var legacy = Required<Sprite>(LegacyPath);
            Assert.That(sprite.rect.size, Is.EqualTo(legacy.rect.size));
            Assert.That(sprite.pivot, Is.EqualTo(legacy.pivot));
            Assert.That(AssetDatabase.AssetPathToGUID(LegacyPath), Is.EqualTo("2d00e7969402e704fb6377571e0fdde8"));
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(addressables, Is.Not.Null);
            var entry = addressables.FindAssetEntry(AssetDatabase.AssetPathToGUID(SpritePath));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.address, Is.EqualTo("sprites/house/comfy31/story-town-photo"));
            Assert.That(entry.parentGroup.Name, Is.EqualTo("Sprites"));
        }

        [Test]
        public void ReviewedPhotoUsesBinaryAlphaTwentyOneColorsAndExactTwoPixelClusters()
        {
            Required<Sprite>(SpritePath);
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(raw.LoadImage(File.ReadAllBytes(SpritePath)), Is.True);
                Assert.That(new Vector2Int(raw.width, raw.height), Is.EqualTo(new Vector2Int(40, 32)));
                var pixels = raw.GetPixels32();
                Assert.That(pixels.All(p => p.a == 0 || p.a == 255), Is.True);
                Assert.That(pixels.Where(p => p.a == 0).All(p => p.r == 0 && p.g == 0 && p.b == 0), Is.True);
                Assert.That(new HashSet<uint>(pixels.Where(p => p.a != 0).Select(Key)).Count, Is.EqualTo(21));
                var occupied = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].a != 0).ToArray();
                Assert.That(occupied, Is.Not.Empty);
                // Unity raw pixel rows are bottom-up. Reviewed top-left bbox is[4,6,36,30].
                Assert.That(new[] { occupied.Min(i => i % 40), occupied.Min(i => i / 40), occupied.Max(i => i % 40) + 1, occupied.Max(i => i / 40) + 1 }, Is.EqualTo(new[] { 4, 2, 36, 26 }));
                for (int y = 0; y < 32; y += 2)
                    for (int x = 0; x < 40; x += 2)
                        Assert.That(new[] { Key(pixels[y * 40 + x + 1]), Key(pixels[(y + 1) * 40 + x]), Key(pixels[(y + 1) * 40 + x + 1]) }, Is.All.EqualTo(Key(pixels[y * 40 + x])), "Nonuniform2x2 at " + x + "," + y);
            }
            finally { Object.DestroyImmediate(raw); }
        }

        [Test]
        public void ExistingPhotoTileReferencesDedicatedSpriteWithoutColliderTintOrTransformChanges()
        {
            var tile = Required<Tile>(TilePath);
            Assert.That(tile.sprite, Is.SameAs(Required<Sprite>(SpritePath)));
            Assert.That(tile.colliderType, Is.EqualTo(Tile.ColliderType.None));
            Assert.That(tile.color, Is.EqualTo(Color.white));
            Assert.That(tile.transform, Is.EqualTo(Matrix4x4.identity));
            Assert.That(tile.flags, Is.EqualTo(TileFlags.LockColor));
            Assert.That(tile.gameObject, Is.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(TilePath), Is.EqualTo("400773d50a5677746aa313cdc3b4302a"));
        }

        [Test]
        public void ExistingFurnitureKeepsOneHouseCellRegistryMembershipAndEarnedUnlockGate()
        {
            var definition = Required<FurnitureDefinition>(FurniturePath);
            Assert.That(definition.StableId, Is.EqualTo("story.photo-board"));
            Assert.That(definition.Category, Is.EqualTo("Decor"));
            Assert.That(definition.AllowedSurfaceIds, Is.EqualTo(new[] { "house" }));
            Assert.That(definition.FootprintCells, Is.EqualTo(new[] { Vector2Int.zero }));
            Assert.That(definition.BlocksMovement, Is.False);
            Assert.That(definition.PlacementPreference, Is.EqualTo(FurniturePlacementPreference.Any));
            Assert.That(definition.UnlockToken, Is.EqualTo("unlock.furniture.story.photo-board"));
            Assert.That(definition.TileParts.Count, Is.EqualTo(1));
            Assert.That(definition.TileParts[0].LocalCell, Is.EqualTo(Vector2Int.zero));
            Assert.That(definition.TileParts[0].Tile, Is.SameAs(Required<Tile>(TilePath)));
            var registry = Required<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.That(registry.FurnitureDefinitions.Count(x => x == definition), Is.EqualTo(1));
            Assert.That(registry.FurnitureDefinitions.Count(x => x != null && x.StableId == definition.StableId), Is.EqualTo(1));
            var objective = new SerializedObject(Required<ScriptableObject>("Assets/Data/StudentLife/MainStory/Objectives/Objective_MyRoom.asset"));
            var accepted = objective.FindProperty("_acceptedFurniture");
            Assert.That(accepted.arraySize, Is.EqualTo(2));
            Assert.That(accepted.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(definition));
            Assert.That(accepted.GetArrayElementAtIndex(1).objectReferenceValue, Is.SameAs(Required<FurnitureDefinition>("Assets/Data/Interiors/Furniture/Story/Furniture_Story_StudyDesk.asset")));
            Assert.That(objective.FindProperty("_requiredSurfaceId").stringValue, Is.EqualTo("house"));
            Assert.That(objective.FindProperty("_requiredCount").intValue, Is.EqualTo(1));
        }

        [Test]
        public void FutureOpeningDataBuildRequiresDedicatedPhotoAndCannotRestoreLegacySprite()
        {
            string source = File.ReadAllText(BuilderPath);
            Assert.That(source, Does.Contain("var photoSprite = Required<Sprite>(\"" + SpritePath + "\");"));
            Assert.That(source, Does.Not.Contain("Required<Sprite>(\"" + LegacyPath + "\")"));
        }

        static T Required<T>(string path) where T : Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(value, Is.Not.Null, "Missing actual asset: " + path);
            return value;
        }
        static uint Key(Color32 p) => (uint)(p.r | p.g << 8 | p.b << 16 | p.a << 24);
    }
}
