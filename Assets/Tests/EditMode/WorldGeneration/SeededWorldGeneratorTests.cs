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

        [Test]
        public void Generate_DoesNotPlacePropsInsideStartSafeRadius()
        {
            var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var spawn = ScriptableObject.CreateInstance<NaturalPropSpawnDefinition>();
            spawn.SetTestData(resource, 20, new RectInt(0, 0, 12, 12), 0, 256);

            var def = ScriptableObject.CreateInstance<TerrainGenerationDefinition>();
            var startArea = new TerrainReservedArea(new RectInt(5, 5, 1, 1), 2);
            def.SetTestData(12, 12, null, new[] { startArea }, new[] { spawn });

            var world = SeededWorldGenerator.Generate(def, 10, 20);
            foreach (var prop in world.Props)
            {
                Assert.IsFalse(startArea.Contains(prop.Cell));
            }
        }

        [Test]
        public void Generate_TileSeedControlsTileVariants()
        {
            var def = MakeVariantSensitiveDefinition();
            var a = SeededWorldGenerator.Generate(def, 10, 20);
            var b = SeededWorldGenerator.Generate(def, 10, 21);

            Assert.AreNotSame(a.GetTile(0, 0), b.GetTile(0, 0));
            Assert.AreEqual(PropSignature(a), PropSignature(b));
        }

        [Test]
        public void Generate_WorldSeedControlsNaturalPropLayout()
        {
            var def = MakeVariantSensitiveDefinition();
            var a = SeededWorldGenerator.Generate(def, 10, 20);
            var b = SeededWorldGenerator.Generate(def, 11, 20);

            Assert.AreSame(a.GetTile(0, 0), b.GetTile(0, 0));
            Assert.AreNotEqual(PropSignature(a), PropSignature(b));
        }

        [Test]
        public void Generate_DensityPermilleComputesTargetCountWhenExplicitCountIsZero()
        {
            var def = MakeDensityDefinition();
            var world = SeededWorldGenerator.Generate(def, 100, 200);
            Assert.AreEqual(10, world.Props.Length);
        }

        [Test]
        public void Generate_ClusteredSpawnPlacesNeighboringPropsWithinClusterRadius()
        {
            var def = MakeClusterDefinition();
            var world = SeededWorldGenerator.Generate(def, 100, 200);

            Assert.AreEqual(3, world.Props.Length);
            Assert.IsTrue(HasNeighborWithinChebyshevDistance(world, 1));
        }

        [Test]
        public void Registry_DefaultFarmTerrainGeneration_IsAssigned()
        {
            var registry = UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Common.GameDataRegistry>(
                "Assets/Data/Registry/GameDataRegistry.asset");
            Assert.IsNotNull(registry);
            Assert.IsNotNull(registry.DefaultFarmTerrainGeneration);
        }

        [Test]
        public void ResourcesRegistry_DefaultFarmTerrainGeneration_IsRuntimeReady()
        {
            var registry = UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Common.GameDataRegistry>(
                "Assets/Resources/GameDataRegistry.asset");
            Assert.IsNotNull(registry);
            Assert.IsNotNull(registry.DefaultFarmTerrainGeneration);
            Assert.IsNotNull(registry.DefaultFarmTerrainGeneration.BasePattern);
            Assert.Greater(registry.DefaultFarmTerrainGeneration.NaturalPropSpawns.Length, 0);
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

        private static TerrainGenerationDefinition MakeVariantSensitiveDefinition()
        {
            var tileA = ScriptableObject.CreateInstance<Tile>();
            var tileB = ScriptableObject.CreateInstance<Tile>();
            var set = ScriptableObject.CreateInstance<TileVariantSetDefinition>();
            set.SetTestData(new[] { new WeightedTileVariant(tileA, 1), new WeightedTileVariant(tileB, 1) });

            var pattern = ScriptableObject.CreateInstance<TilePatternDefinition>();
            pattern.SetTestData(1, 1, new[] { set });

            var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var spawn = ScriptableObject.CreateInstance<NaturalPropSpawnDefinition>();
            spawn.SetTestData(resource, 4, new RectInt(0, 0, 12, 12), 1, 64);

            var def = ScriptableObject.CreateInstance<TerrainGenerationDefinition>();
            def.SetTestData(4, 4, pattern, new[] { new TerrainReservedArea(new RectInt(0, 0, 2, 2), 0) }, new[] { spawn });
            return def;
        }

        private static TerrainGenerationDefinition MakeDensityDefinition()
        {
            var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var spawn = ScriptableObject.CreateInstance<NaturalPropSpawnDefinition>();
            spawn.SetTestData(resource, 0, new RectInt(0, 0, 10, 10), 0, 256, 1, 0, 100);

            var def = ScriptableObject.CreateInstance<TerrainGenerationDefinition>();
            def.SetTestData(10, 10, null, System.Array.Empty<TerrainReservedArea>(), new[] { spawn });
            return def;
        }

        private static TerrainGenerationDefinition MakeClusterDefinition()
        {
            var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var spawn = ScriptableObject.CreateInstance<NaturalPropSpawnDefinition>();
            spawn.SetTestData(resource, 3, new RectInt(5, 5, 10, 10), 0, 256, 3, 1, 0);

            var def = ScriptableObject.CreateInstance<TerrainGenerationDefinition>();
            def.SetTestData(20, 20, null, System.Array.Empty<TerrainReservedArea>(), new[] { spawn });
            return def;
        }

        private static string PropSignature(SeededWorldGenerator.GeneratedWorld world)
        {
            var parts = new string[world.Props.Length];
            for (int i = 0; i < world.Props.Length; i++)
            {
                parts[i] = world.Props[i].Cell.x + ":" + world.Props[i].Cell.y;
            }

            System.Array.Sort(parts, System.StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        private static bool HasNeighborWithinChebyshevDistance(SeededWorldGenerator.GeneratedWorld world, int distance)
        {
            for (int i = 0; i < world.Props.Length; i++)
            {
                for (int j = i + 1; j < world.Props.Length; j++)
                {
                    int dx = Mathf.Abs(world.Props[i].Cell.x - world.Props[j].Cell.x);
                    int dy = Mathf.Abs(world.Props[i].Cell.y - world.Props[j].Cell.y);
                    if (dx <= distance && dy <= distance)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
