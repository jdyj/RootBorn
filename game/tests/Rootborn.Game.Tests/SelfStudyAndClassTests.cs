// 원래 파일: Assets/Tests/PlayMode/EndToEnd/SelfStudyLibraryGrowthE2EScenarioTests.cs (FREE_PATH_001, FREE_PATH_004)
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 같은 특성(집중)이 학교 밖(도서관 자습)과 학교 안(수업) 두 길로 자란다(자유 경로 성장 원칙). 시간을 다 쓰면 자습은 거절되고 아무것도 바뀌지 않는다.
    /// 자동 테스트다.
    /// </summary>
    public sealed class SelfStudyAndClassTests
    {
        private const string Focus = "trait.focus";
        private const string LibraryStudy = "outside.self-study.library";
        private const string SchoolClass = "location.activity.school-class";

        [Test]
        public void FreePath001_FocusGrowsFromLibrarySelfStudy_AndFromSchoolClass_AndSurvivesReading()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            int focusStart = game.Student.GetTraitValue(Focus);

            Assert.That(game.PerformOutsideSchoolActivity(LibraryStudy, "outside:library:1").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            int afterLibrary = game.Student.GetTraitValue(Focus);
            Assert.That(afterLibrary, Is.GreaterThan(focusStart), "학교 밖 자습이 집중을 키운다");

            game = TestWorld.Start(saves.Store);
            game.VisitLocation(game.Definitions.Get<LocationActivity>(SchoolClass).LocationId);
            Assert.That(game.Student.GetTraitValue(Focus), Is.EqualTo(afterLibrary), "학교에 들어가기만 해서는 바뀌지 않는다");
            var result = game.PerformLocationActivity(SchoolClass, "location-activity:school-class:1");
            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            int afterClass = game.Student.GetTraitValue(Focus);
            Assert.That(afterClass, Is.GreaterThan(afterLibrary), "수업도 집중을 키운다");

            Assert.That(TestWorld.Start(saves.Store).Student.GetTraitValue(Focus), Is.EqualTo(afterClass), "두 길의 성장이 한 번씩 저장된다");
        }

        [Test]
        public void FreePath004_LibraryRefusedWhenSpent_ExplainsWhy_AndChangesNothing()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            int applied = 0;
            LifeActivityResultKind last = LifeActivityResultKind.Applied;
            for (int press = 0; press < 8 && last == LifeActivityResultKind.Applied; press++)
            {
                last = game.PerformOutsideSchoolActivity(LibraryStudy, "outside:library:" + press).Kind;
                if (last == LifeActivityResultKind.Applied) applied++;
                game = TestWorld.Start(saves.Store);
            }

            Assert.That(applied, Is.GreaterThan(0));
            Assert.That(last, Is.EqualTo(LifeActivityResultKind.InsufficientResources), "자습을 " + applied + "번 한 뒤 거절되어야 한다");

            int focus = game.Student.GetTraitValue(Focus);
            int remaining = game.Student.TimePointsRemaining;
            int energy = game.Student.Energy;
            int minutes = game.Student.TimeMinutes;
            Assert.That(game.PerformOutsideSchoolActivity(LibraryStudy, "outside:library:again").Kind, Is.EqualTo(LifeActivityResultKind.InsufficientResources));
            game = TestWorld.Start(saves.Store);
            Assert.That(game.Student.GetTraitValue(Focus), Is.EqualTo(focus));
            Assert.That(game.Student.TimePointsRemaining, Is.EqualTo(remaining));
            Assert.That(game.Student.Energy, Is.EqualTo(energy));
            Assert.That(game.Student.TimeMinutes, Is.EqualTo(minutes));
        }
    }
}
