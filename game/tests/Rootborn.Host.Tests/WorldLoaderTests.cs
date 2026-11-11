using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Rootborn.Host.Tests
{
    /// <summary>월드는 정의 로더가 만든 것만 받는다: Rootborn.World에 data/를 다시 읽는 옛 진입점(Obsolete)이 남지 않는다.</summary>
    public sealed class WorldLoaderTests
    {
        [Test]
        public void RootbornWorld_HasNoObsoleteEntryPoints()
        {
            Assembly world = typeof(Rootborn.World.WorldTravelData).Assembly;
            string[] obsolete = world.GetTypes()
                .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttribute<ObsoleteAttribute>() != null)
                    .Select(m => t.Name + "." + m.Name))
                .ToArray();
            Assert.That(obsolete, Is.Empty);
        }
    }
}
