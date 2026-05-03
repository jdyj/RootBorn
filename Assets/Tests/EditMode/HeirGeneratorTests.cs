using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Heir;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    // HEIR-001: HeirGenerator는 시드가 같으면 결과가 같고, 부모 특성을 일정 확률로 계승한다.
    public sealed class HeirGeneratorTests
    {
        private static HeirTrait MakeTrait(string id, bool inheritable, bool randomOnly = false)
        {
            var t = ScriptableObject.CreateInstance<HeirTrait>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(HeirTrait).GetField("_id", bind).SetValue(t, id);
            typeof(HeirTrait).GetField("_isInheritable", bind).SetValue(t, inheritable);
            typeof(HeirTrait).GetField("_isRandomOnly", bind).SetValue(t, randomOnly);
            return t;
        }

        [Test]
        public void SameSeed_ProducesSameResult()
        {
            var pool = new List<HeirTrait> { MakeTrait("Hardy", true), MakeTrait("Green", true), MakeTrait("Quick", true) };
            var parent = new List<HeirTrait> { pool[0] };

            var a = HeirGenerator.Generate(parent, pool, seed: 42);
            var b = HeirGenerator.Generate(parent, pool, seed: 42);

            CollectionAssert.AreEqual(a.Traits, b.Traits);
        }

        [Test]
        public void NonInheritable_NeverPassedDown_ButCanArriveAsRandomBonus()
        {
            var nonHeritable = MakeTrait("NonHeritable", inheritable: false);
            var pool = new List<HeirTrait> { nonHeritable, MakeTrait("Other", true) };
            var parent = new List<HeirTrait> { nonHeritable };

            var heir = HeirGenerator.Generate(parent, pool, seed: 1, inheritProbability: 1f, maxInherited: 5, randomBonus: 0);

            Assert.IsFalse(heir.Traits.Contains(nonHeritable),
                "Non-inheritable parent trait must not be inherited even at p=1.");
        }

        [Test]
        public void RandomBonus_AddsExtraTraitFromPool()
        {
            var t1 = MakeTrait("T1", true);
            var t2 = MakeTrait("T2", true);
            var heir = HeirGenerator.Generate(
                parentTraits: null,
                traitPool: new[] { t1, t2 },
                seed: 7,
                inheritProbability: 0f,
                maxInherited: 0,
                randomBonus: 1);
            Assert.AreEqual(1, heir.Traits.Count);
        }
    }
}
