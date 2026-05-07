using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Editor.Tools;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernUiRecipeManifestTests
    {
        private const string ManifestPath = "docs/art/modern-ui-reconstruction-manifest.json";

        [Test]
        public void Manifest_ExistsForTargetWindows()
        {
            Assert.IsTrue(File.Exists(ManifestPath), "Missing Modern UI reconstruction manifest.");
            string json = File.ReadAllText(ManifestPath);
            StringAssert.Contains("\"settings\"", json);
            StringAssert.Contains("\"inventory\"", json);
            StringAssert.Contains("\"status\"", json);
        }

        [Test]
        public void Manifest_ReferencesExistingSlicedSubSprites()
        {
            var manifest = ModernUiRecipeManifestLoader.Load(ManifestPath);
            var entriesByAddress = ModernUiAddressablesSetup.GetSheetEntries()
                .ToDictionary(entry => entry.address, entry => entry.assetPath);
            var missing = new List<string>();

            foreach (var sprite in manifest.Sprites)
            {
                Assert.IsTrue(entriesByAddress.TryGetValue(sprite.sheetAddress, out string path), sprite.usage);
                var names = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Sprite>()
                    .Select(s => s.name)
                    .ToHashSet();
                if (!names.Contains(sprite.subSpriteName))
                {
                    missing.Add(sprite.usage + ":" + sprite.subSpriteName);
                }
            }

            Assert.IsEmpty(missing, "Manifest references missing sub-sprites: " + string.Join(", ", missing));
        }

        [Test]
        public void CommonPanelRecipe_HasNineDistinctTileRoles()
        {
            var manifest = ModernUiRecipeManifestLoader.Load(ManifestPath);
            CollectionAssert.AreEquivalent(
                new[] { "corner-tl", "edge-t", "corner-tr", "edge-l", "fill", "edge-r", "corner-bl", "edge-b", "corner-br" },
                manifest.CommonPanelRoles);
        }

        [Test]
        public void ManifestSprites_AreIncludedInModernUiPreloadDeclarations()
        {
            var manifest = ModernUiRecipeManifestLoader.Load(ManifestPath);
            var declared = new HashSet<string>();
            foreach (var (sheetAddress, subNames) in Rootborn.Game.Common.ModernUISpriteAddresses.AllSheets)
            {
                foreach (string subName in subNames)
                {
                    declared.Add(sheetAddress + ":" + subName);
                }
            }

            var missing = manifest.Sprites
                .Select(sprite => sprite.sheetAddress + ":" + sprite.subSpriteName)
                .Where(id => !declared.Contains(id))
                .ToArray();

            Assert.IsEmpty(missing, "Manifest sprites missing from preload declarations: " + string.Join(", ", missing));
        }
    }

    internal static class ModernUiRecipeManifestLoader
    {
        public static ModernUiRecipeManifest Load(string path)
        {
            string json = File.ReadAllText(path);
            var sprites = ParseSprites(json);
            var commonRoles = ParseCommonPanelIds(json)
                .Select(id => sprites.Single(sprite => sprite.id == id).role)
                .ToArray();
            return new ModernUiRecipeManifest(sprites, commonRoles);
        }

        private static IReadOnlyList<ModernUiRecipeManifestSprite> ParseSprites(string json)
        {
            var sprites = new List<ModernUiRecipeManifestSprite>();
            foreach (string objectJson in ExtractObjectsFromArray(json, "sprites"))
            {
                sprites.Add(new ModernUiRecipeManifestSprite(
                    ReadString(objectJson, "id"),
                    ReadString(objectJson, "sheetAddress"),
                    ReadString(objectJson, "subSpriteName"),
                    ReadInt(objectJson, "row"),
                    ReadInt(objectJson, "column"),
                    ReadString(objectJson, "role"),
                    ReadString(objectJson, "usage")));
            }

            return sprites;
        }

        private static IReadOnlyList<string> ParseCommonPanelIds(string json)
        {
            const string marker = "\"commonPanel\"";
            int markerIndex = json.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return Array.Empty<string>();
            }

            int start = json.IndexOf('[', markerIndex);
            int end = json.IndexOf(']', start);
            string body = json.Substring(start + 1, end - start - 1);
            return body.Split(',')
                .Select(value => value.Trim().Trim('"'))
                .Where(value => value.Length > 0)
                .ToArray();
        }

        private static IEnumerable<string> ExtractObjectsFromArray(string json, string arrayName)
        {
            string marker = "\"" + arrayName + "\"";
            int markerIndex = json.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                yield break;
            }

            int arrayStart = json.IndexOf('[', markerIndex);
            int depth = 0;
            int objectStart = -1;
            for (int i = arrayStart; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '{')
                {
                    if (depth == 0)
                    {
                        objectStart = i;
                    }

                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objectStart >= 0)
                    {
                        yield return json.Substring(objectStart, i - objectStart + 1);
                        objectStart = -1;
                    }
                }
                else if (c == ']' && depth == 0)
                {
                    yield break;
                }
            }
        }

        private static string ReadString(string json, string name)
        {
            string marker = "\"" + name + "\"";
            int markerIndex = json.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return string.Empty;
            }

            int colon = json.IndexOf(':', markerIndex);
            int firstQuote = json.IndexOf('"', colon + 1);
            int secondQuote = json.IndexOf('"', firstQuote + 1);
            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        private static int ReadInt(string json, string name)
        {
            string marker = "\"" + name + "\"";
            int markerIndex = json.IndexOf(marker, StringComparison.Ordinal);
            int colon = json.IndexOf(':', markerIndex);
            int end = colon + 1;
            while (end < json.Length && (char.IsWhiteSpace(json[end]) || json[end] == ':'))
            {
                end++;
            }

            int start = end;
            while (end < json.Length && char.IsDigit(json[end]))
            {
                end++;
            }

            return int.Parse(json.Substring(start, end - start));
        }
    }

    internal sealed class ModernUiRecipeManifest
    {
        public ModernUiRecipeManifest(IReadOnlyList<ModernUiRecipeManifestSprite> sprites, IReadOnlyList<string> commonPanelRoles)
        {
            Sprites = sprites;
            CommonPanelRoles = commonPanelRoles;
        }

        public IReadOnlyList<ModernUiRecipeManifestSprite> Sprites { get; }
        public IReadOnlyList<string> CommonPanelRoles { get; }
    }

    internal readonly struct ModernUiRecipeManifestSprite
    {
        public ModernUiRecipeManifestSprite(string id, string sheetAddress, string subSpriteName, int row, int column, string role, string usage)
        {
            this.id = id;
            this.sheetAddress = sheetAddress;
            this.subSpriteName = subSpriteName;
            this.row = row;
            this.column = column;
            this.role = role;
            this.usage = usage;
        }

        public readonly string id;
        public readonly string sheetAddress;
        public readonly string subSpriteName;
        public readonly int row;
        public readonly int column;
        public readonly string role;
        public readonly string usage;
    }
}
