using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rootborn.Game.Common;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class ModernUiStyle2IconCatalogTests
    {
        private const string CatalogJsonPath = "docs/art/modern-ui-style2-icon-catalog.json";
        private const string CatalogMarkdownPath = "docs/art/modern-ui-style2-icon-catalog.md";
        private static readonly Regex EntryRegex = new Regex("\\{\\s*\"row\"\\s*:\\s*(\\d+),\\s*\"column\"\\s*:\\s*(\\d+),\\s*\"coordinate\"\\s*:\\s*\"(r\\d+_c\\d+)\",\\s*\"spriteName\"\\s*:\\s*\"(ModernUI_16_Style2_r\\d+_c\\d+)\",\\s*\"semanticId\"\\s*:\\s*\"([^\"]+)\",\\s*\"enumName\"\\s*:\\s*\"([^\"]+)\",\\s*\"category\"\\s*:\\s*\"([^\"]+)\",\\s*\"stateGroupId\"\\s*:\\s*(null|\"[^\"]*\"),\\s*\"stateRole\"\\s*:\\s*(null|\"[^\"]*\"),\\s*\"confidence\"\\s*:\\s*\"([^\"]+)\",", RegexOptions.Compiled);

        [Test]
        public void IconCatalogJson_CoversEveryStyle2CoordinateExactlyOnce()
        {
            Assert.IsTrue(File.Exists(CatalogJsonPath), "Missing Style2 icon catalog JSON.");
            string json = File.ReadAllText(CatalogJsonPath);

            StringAssert.Contains("\"sourceAddress\": \"" + ModernUISpriteAddresses.Style16Alt + "\"", json);
            StringAssert.Contains("\"rows\": 34", json);
            StringAssert.Contains("\"columns\": 49", json);
            StringAssert.Contains("\"spriteCount\": 1666", json);

            IReadOnlyList<Entry> entries = ReadEntries(json);
            Assert.AreEqual(1666, entries.Count, "Catalog must contain one entry for every 16x16 Style2 coordinate.");

            var seen = new HashSet<string>();
            foreach (Entry entry in entries)
            {
                string expectedCoordinate = "r" + entry.Row + "_c" + entry.Column;
                Assert.AreEqual(expectedCoordinate, entry.Coordinate);
                Assert.AreEqual("ModernUI_16_Style2_" + expectedCoordinate, entry.SpriteName);
                Assert.IsTrue(seen.Add(entry.Coordinate), "Duplicate coordinate: " + entry.Coordinate);
                Assert.IsNotEmpty(entry.SemanticId);
                Assert.IsNotEmpty(entry.EnumName);
                Assert.IsNotEmpty(entry.Category);
                Assert.Contains(entry.Confidence, new[] { "confirmed", "probable", "unknown" });
            }

            for (int row = 0; row < 34; row++)
            {
                for (int column = 0; column < 49; column++)
                {
                    Assert.IsTrue(seen.Contains("r" + row + "_c" + column), "Missing coordinate r" + row + "_c" + column);
                }
            }
        }

        [Test]
        public void IconCatalogMarkdown_ProvidesHumanReviewTableAndUnknownPolicy()
        {
            Assert.IsTrue(File.Exists(CatalogMarkdownPath), "Missing Style2 icon catalog markdown.");
            string markdown = File.ReadAllText(CatalogMarkdownPath);

            StringAssert.Contains("| coordinate | spriteName | semanticId | enumName | category | stateGroupId | stateRole | confidence | note |", markdown);
            StringAssert.Contains("unknown.r{row}.c{column}", markdown);
            StringAssert.Contains("r3_c42", markdown);
            StringAssert.Contains("r3_c43", markdown);
            StringAssert.Contains("r3_c44", markdown);
        }

        [Test]
        public void IconCatalog_GroupsR3C42ToR3C44AsPressedButtonFrames()
        {
            string json = File.ReadAllText(CatalogJsonPath);
            IReadOnlyList<Entry> entries = ReadEntries(json);

            Entry first = entries.Single(entry => entry.Coordinate == "r3_c42");
            Entry second = entries.Single(entry => entry.Coordinate == "r3_c43");
            Entry third = entries.Single(entry => entry.Coordinate == "r3_c44");

            Assert.AreEqual("button.icon.heightFrame", first.StateGroupId);
            Assert.AreEqual(first.StateGroupId, second.StateGroupId);
            Assert.AreEqual(first.StateGroupId, third.StateGroupId);
            Assert.AreEqual("frame0", first.StateRole);
            Assert.AreEqual("frame1", second.StateRole);
            Assert.AreEqual("frame2", third.StateRole);
            Assert.AreEqual("confirmed", first.Confidence);
            Assert.AreEqual("confirmed", second.Confidence);
            Assert.AreEqual("confirmed", third.Confidence);
        }

        [Test]
        public void IconCatalog_ExposesSemanticEnumAndResolverForKnownControls()
        {
            Assert.AreEqual("ModernUI_16_Style2_r3_c42", ModernUiStyle2IconCatalog.GetSpriteKey(ModernUiStyle2Icon.ButtonIconHeightFrame0).SubSpriteName);
            Assert.AreEqual("ModernUI_16_Style2_r3_c43", ModernUiStyle2IconCatalog.GetSpriteKey(ModernUiStyle2Icon.ButtonIconHeightFrame1).SubSpriteName);
            Assert.AreEqual("ModernUI_16_Style2_r3_c44", ModernUiStyle2IconCatalog.GetSpriteKey(ModernUiStyle2Icon.ButtonIconHeightFrame2).SubSpriteName);
            Assert.AreEqual("ModernUI_16_Style2_r1_c13", ModernUiStyle2IconCatalog.GetSpriteKey(ModernUiStyle2Icon.FurnitureChair).SubSpriteName);
            Assert.AreEqual("ModernUI_16_Style2_r1_c14", ModernUiStyle2IconCatalog.GetSpriteKey(ModernUiStyle2Icon.FurnitureBed).SubSpriteName);

            IReadOnlyList<ModernUiStyle2IconCatalog.Entry> entries = ModernUiStyle2IconCatalog.Entries;
            Assert.GreaterOrEqual(entries.Count, 20, "C# catalog should expose confirmed semantic entries for UI use.");
            Assert.IsTrue(entries.Any(entry => entry.Category == "button"));
            Assert.IsTrue(entries.Any(entry => entry.Category == "cursor"));
            Assert.IsTrue(entries.Any(entry => entry.Category == "toggle"));
            Assert.IsTrue(entries.Any(entry => entry.Category == "direction"));
        }

        [Test]
        public void CommonPanelCoordinates_RemainStyle2Baseline()
        {
            Assert.AreEqual("ModernUI_16_Style2_r2_c0", ModernUiStyle2Sprites.CommonPanel.TopLeftName);
            Assert.AreEqual("ModernUI_16_Style2_r2_c1", ModernUiStyle2Sprites.CommonPanel.TopName);
            Assert.AreEqual("ModernUI_16_Style2_r2_c2", ModernUiStyle2Sprites.CommonPanel.TopRightName);
            Assert.AreEqual("ModernUI_16_Style2_r3_c0", ModernUiStyle2Sprites.CommonPanel.LeftName);
            Assert.AreEqual("ModernUI_16_Style2_r3_c1", ModernUiStyle2Sprites.CommonPanel.FillName);
            Assert.AreEqual("ModernUI_16_Style2_r3_c2", ModernUiStyle2Sprites.CommonPanel.RightName);
            Assert.AreEqual("ModernUI_16_Style2_r4_c0", ModernUiStyle2Sprites.CommonPanel.BottomLeftName);
            Assert.AreEqual("ModernUI_16_Style2_r4_c1", ModernUiStyle2Sprites.CommonPanel.BottomName);
            Assert.AreEqual("ModernUI_16_Style2_r4_c2", ModernUiStyle2Sprites.CommonPanel.BottomRightName);
        }

        private static IReadOnlyList<Entry> ReadEntries(string json)
        {
            return EntryRegex.Matches(json).Cast<Match>().Select(match => new Entry(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                match.Groups[3].Value,
                match.Groups[4].Value,
                match.Groups[5].Value,
                match.Groups[6].Value,
                match.Groups[7].Value,
                TrimNullable(match.Groups[8].Value),
                TrimNullable(match.Groups[9].Value),
                match.Groups[10].Value)).ToArray();
        }

        private static string TrimNullable(string value)
        {
            return value == "null" ? null : value.Trim('"');
        }

        private readonly struct Entry
        {
            public Entry(int row, int column, string coordinate, string spriteName, string semanticId, string enumName, string category, string stateGroupId, string stateRole, string confidence)
            {
                Row = row;
                Column = column;
                Coordinate = coordinate;
                SpriteName = spriteName;
                SemanticId = semanticId;
                EnumName = enumName;
                Category = category;
                StateGroupId = stateGroupId;
                StateRole = stateRole;
                Confidence = confidence;
            }

            public int Row { get; }
            public int Column { get; }
            public string Coordinate { get; }
            public string SpriteName { get; }
            public string SemanticId { get; }
            public string EnumName { get; }
            public string Category { get; }
            public string StateGroupId { get; }
            public string StateRole { get; }
            public string Confidence { get; }
        }
    }
}