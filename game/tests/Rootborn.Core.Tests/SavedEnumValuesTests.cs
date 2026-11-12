using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 세이브 파일에 숫자로 쓰이는 enum의 값. 순서를 바꾸거나 가운데에 끼워 넣으면 옛 세이브의 뜻이 조용히 바뀐다.
    /// 새 값은 끝에 더하고 여기에 한 줄을 더한다. 이미 있는 값의 숫자는 바꾸지 않는다.
    /// </summary>
    public sealed class SavedEnumValuesTests
    {
        [Test]
        public void DailyPlanActivityKind_KeepsItsSavedNumbers()
        {
            Assert.That((int)DailyPlanActivityKind.LocationActivity, Is.EqualTo(0));
            Assert.That((int)DailyPlanActivityKind.Practice, Is.EqualTo(1));
            Assert.That((int)DailyPlanActivityKind.PartTimeWork, Is.EqualTo(2));
        }

        [Test]
        public void StudentMinigameKind_KeepsItsSavedNumbers()
        {
            Assert.That((int)StudentMinigameKind.Practice, Is.EqualTo(0));
            Assert.That((int)StudentMinigameKind.PartTimeWork, Is.EqualTo(1));
        }

        [Test]
        public void FurniturePlacementDirection_KeepsItsSavedNumbers()
        {
            Assert.That((int)FurniturePlacementDirection.North, Is.EqualTo(0));
            Assert.That(System.Enum.GetValues(typeof(FurniturePlacementDirection)).Length, Is.EqualTo(4));
        }
    }
}
