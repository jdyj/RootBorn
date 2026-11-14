using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Room;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 같이 쓰는 집에서 돈을 낸 꾸밈 잡기의 표시(Core Room <see cref="RoomHouseBuildPaidHolds"/>)와 떠도는 내 잡기를 어떻게 다룰지의 판정
    /// (<see cref="RoomHouseBuildPaidHolds.StepFor"/>). 생성자로 만든 값만 쓰는 자동 테스트다.
    /// </summary>
    public sealed class RoomHouseBuildPaidHoldsTests
    {
        [Test]
        public void Mark_RemembersOnce_AndIsDirtyUntilSaved()
        {
            var paid = new RoomHouseBuildPaidHolds();
            Assert.That(paid.Dirty, Is.False);
            paid.Mark("h1");
            paid.Mark("h1");
            paid.Mark("");
            paid.Mark(null);
            Assert.That(paid.Count, Is.EqualTo(1));
            Assert.That(paid.Contains("h1"), Is.True);
            Assert.That(paid.Dirty, Is.True);
            paid.Saved();
            Assert.That(paid.Dirty, Is.False);
            paid.Mark("h1");
            Assert.That(paid.Dirty, Is.False, "이미 있는 잡기를 다시 표시해도 바뀐 것이 없다");
        }

        [Test]
        public void ForgetGone_DropsOnlyHoldsTheRoomNoLongerHasAndThatHaveNoRequestOut()
        {
            var paid = new RoomHouseBuildPaidHolds();
            paid.Load(new RoomHouseBuildPaidSaveData { Version = RoomHouseBuildPaidSaveData.KnownVersion, Holds = new[] { "kept-held", "kept-sending", "gone" } });
            Assert.That(paid.Dirty, Is.False, "읽기만으로는 바뀐 것이 없다");

            var held = new HashSet<string> { "kept-held" };
            var sending = new HashSet<string> { "kept-sending" };
            Assert.That(paid.ForgetGone(held.Contains, sending.Contains), Is.True);
            Assert.That(paid.Contains("kept-held") && paid.Contains("kept-sending"), Is.True);
            Assert.That(paid.Contains("gone"), Is.False);
            Assert.That(paid.Dirty, Is.True);

            paid.Saved();
            Assert.That(paid.ForgetGone(held.Contains, sending.Contains), Is.False, "지울 것이 없으면 그대로");
            Assert.That(paid.Dirty, Is.False);
        }

        [Test]
        public void LoadAndSave_RoundTrip_SkipsEmptyAndRepeatedIds_NullIsEmpty()
        {
            var paid = new RoomHouseBuildPaidHolds();
            paid.Load(new RoomHouseBuildPaidSaveData { Version = 1, Holds = new[] { "b", "", null, "a", "b" } });
            var data = paid.ToSaveData();
            Assert.That(data.Version, Is.EqualTo(RoomHouseBuildPaidSaveData.KnownVersion));
            Assert.That(data.Holds, Is.EqualTo(new[] { "b", "a" }), "읽은 순서대로, 빈 id·겹친 id 없이");

            paid.Load(null);
            Assert.That(paid.Count, Is.Zero, "파일이 없는 옛 세이브 = 돈 낸 잡기 없음");
            paid.Load(new RoomHouseBuildPaidSaveData { Holds = null });
            Assert.That(paid.Count, Is.Zero);
        }

        // 떠도는 내 잡기의 판정: 이 판이 이미 다루는 잡기(답을 기다림, 돈을 내는 중, 이 판에서 쓰기·풀기를 보냄, 보낼 목록에 그 잡기의 요청이 있음)는 그대로 둔다.
        // 아니면 돈을 냈다는 표시가 있으면 쓰기를 다시 적고, 없으면 푼다(돈을 내지 않았다 — 잃는 것이 없다).
        [TestCase(true, false, false, false, false, RoomHoldStep.Leave)]
        [TestCase(false, true, false, false, false, RoomHoldStep.Leave)]
        [TestCase(false, false, true, false, true, RoomHoldStep.Leave)]
        [TestCase(false, false, false, true, true, RoomHoldStep.Leave)]
        [TestCase(false, false, false, true, false, RoomHoldStep.Leave)]
        [TestCase(false, false, false, false, true, RoomHoldStep.SendUse)]
        [TestCase(false, false, false, false, false, RoomHoldStep.Release)]
        public void StepFor_StrayHold(bool waiting, bool paying, bool settled, bool outgoing, bool paid, RoomHoldStep expected)
        {
            Assert.That(RoomHouseBuildPaidHolds.StepFor(waiting, paying, settled, outgoing, paid), Is.EqualTo(expected));
        }
    }
}
