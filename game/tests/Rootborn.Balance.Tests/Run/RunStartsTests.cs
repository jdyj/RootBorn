using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.StudentLife;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    public sealed class RunStartsTests
    {
        [Test]
        public void EveryFacilityInTheDataCanBeStartedAsAnAdult()
        {
            var facilities = TestWorld.Definitions.All<Facility>().ToList();
            Assert.That(facilities, Is.Not.Empty);
            foreach (var facility in facilities)
            {
                var game = RunStarts.Get("adult").Start(new MemorySaveStore(), facility);
                Assert.That(game.Adult.IsAdult, Is.True, facility.Id);
                Assert.That(game.Adult.FacilityId, Is.EqualTo(facility.Id));
                Assert.That(game.Adult.FacilityLevel, Is.EqualTo(1));
                Assert.That(game.Student.DayState, Is.EqualTo(StudentDayState.InProgress));
                Assert.That(game.FacilityStatus(facility.Id).Works.Any(w => w.CanWork), Is.True, "첫날에 할 수 있는 일이 있다: " + facility.Id);
            }
        }

        [Test]
        public void NewGameStartIsDayOne()
            => Assert.That(RunStarts.Get("new-game").Start(new MemorySaveStore(), null).Student.CurrentDay, Is.EqualTo(1));

        [Test]
        public void UnknownStartKindSaysItsName()
            => Assert.That(() => RunStarts.Get("nope"), Throws.ArgumentException.With.Message.Contains("nope"));
    }
}
