using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Workspaces;

namespace Rootborn.Core.Tests
{
    public sealed class WorkspaceDefinitionTests
    {
        private static StudentLifeProgress Student(int skill)
        {
            var s = new StudentLifeProgress("slot", "player", 10, 10);
            if (skill > 0) s.AddSkill("craft", skill);
            return s;
        }

        private static Workspace Make(IDecorationRule[] rules, int[] caps = null, int repeat = 50)
            => new Workspace("ws", "fac", "surf", new[] { "surf" }, caps ?? new[] { 4, 6, 8 }, repeat, rules, "tpl");

        [Test]
        public void Capacity_IsLevelValuePlusReachedTierBonuses()
        {
            var tier = new SkillTier("t1", "craft", 20, "k");
            var w = Make(new IDecorationRule[] { new SkillTierCapacityRule(tier, 2) });
            Assert.AreEqual(6, w.Capacity(2, Student(0)));
            Assert.AreEqual(8, w.Capacity(2, Student(20)));
        }

        [Test]
        public void ScorePercent_AddsOnlyReachedTiers()
        {
            var tier = new SkillTier("t1", "craft", 20, "k");
            var w = Make(new IDecorationRule[] { new SkillTierScoreRule(tier, 25) });
            Assert.AreEqual(100, w.ScorePercent(Student(19)));
            Assert.AreEqual(125, w.ScorePercent(Student(20)));
        }

        [Test]
        public void CanAdd_FalseAtCapacity()
        {
            var w = Make(new IDecorationRule[0]);
            Assert.IsTrue(w.CanAdd(3, 1, Student(0)));
            Assert.IsFalse(w.CanAdd(4, 1, Student(0)));
        }

        [Test]
        public void Capacity_ZeroOutsideLevels()
        {
            var w = Make(new IDecorationRule[0]);
            Assert.AreEqual(0, w.Capacity(0, Student(0)));
            Assert.AreEqual(0, w.Capacity(4, Student(0)));
            Assert.IsFalse(w.CanAdd(0, 0, Student(0)));
        }

        [Test]
        public void NegativeBonus_IsTreatedAsZero()
        {
            var tier = new SkillTier("t1", "craft", 1, "k");
            var w = Make(new IDecorationRule[] { new SkillTierCapacityRule(tier, -5), new SkillTierScoreRule(tier, -9) });
            Assert.AreEqual(4, w.Capacity(1, Student(5)));
            Assert.AreEqual(100, w.ScorePercent(Student(5)));
        }

        [Test]
        public void Workspace_InvalidWithoutThreeLevels()
        {
            Assert.IsTrue(Make(new IDecorationRule[0]).IsValid);
            Assert.IsFalse(Make(new IDecorationRule[0], new[] { 4, 6 }).IsValid);
            Assert.IsFalse(Make(new IDecorationRule[0], new[] { 4, -1, 8 }).IsValid);
            Assert.IsFalse(Make(new IDecorationRule[0], null, 101).IsValid);
        }
    }
}
