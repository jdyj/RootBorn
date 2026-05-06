using NUnit.Framework;
using Rootborn.Game.Resources;
using Rootborn.Game.WorldGeneration;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.WorldGeneration
{
    public sealed class SeededWorldGeneratorTests
    {
        [Test]
        public void Generate_SameSeeds_ProducesSameTilesAndProps()
        {
            var def = MakeDefinition();
            var a = SeededWorldGenerator.Generate(def, 10, 20);
            var b = SeededWorldGenerator.Generate(def, 10, 20);
            Assert.AreEqual(a.Signature(), b.Signature());
        }

        [Test]
        public void Generate_DifferentSeeds_ProducesDifferentTilesOrProps()
        {
            var def = MakeDefinition();
            var a = SeededWorldGenerator.Generate(def, 10, 20);
            var b = SeededWorldGenerator.Generate(def, 11, 21);
            Assert.AreNotEqual(a.Signature(), b.Signature());
        }

        [Test]
        public void Generate_RespectsTilePatternCoordinates()
        {
            var def = MakeDefinition();
            var world = SeededWorldGenerator.Generate(def, 10, 20);
            Assert.AreSame(world.GetTile(0, 0), world.GetTile(2, 0));
            Assert.AreSame(world.GetTile(1, 0), world.GetTile(3, 0));
        }

        [Test]
        public void Generate_DoesNotPlacePropsInsideReservedArea()
        {
            var def = MakeDefinition();
            var world = SeededWorldGenerator.Generate(def, 10, 20);
            foreach (var prop in world.Props)
            {
                Assert.IsFalse(prop.Cell.x >= 0 && prop.Cell.x <= 3 && prop.Cell.y >= 0 && prop.Cell.y <= 3);
            }
        }

        private static TerrainGenerationDefinition MakeDefinition()
        {
            var tileA = ScriptableObject.CreateInstance<Tile>();
            var tileB = ScriptableObject.CreateInstance<Tile>();
            var setA = ScriptableObject.CreateInstance<TileVariantSetDefinition>();
            setA.SetTestData(new[] { new WeightedTileVariant(tileA, 1) });
            var setB = ScriptableObject.CreateInstance<TileVariantSetDefinition>();
            setB.SetTestData(new[] { new WeightedTileVariant(tileB, 1) });

            var pattern = ScriptableObject.CreateInstance<TilePatternDefinition>();
            pattern.SetTestData(2, 1, new[] { setA, setB });

            var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var spawn = ScriptableObject.CreateInstance<NaturalPropSpawnDefinition>();
            spawn.SetTestData(resource, 8, new RectInt(0, 0, 10, 10), 1, 64);

            var def = ScriptableObject.CreateInstance<TerrainGenerationDefinition>();
            def.SetTestData(10, 10, pattern, new[] { new TerrainReservedArea(new RectInt(0, 0, 4, 4), 0) }, new[] { spawn });
            return def;
        }
    }
}
