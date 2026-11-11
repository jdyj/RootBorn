using NUnit.Framework;
using Rootborn.Core.Network;
using Rootborn.Core.Save;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 같이 쓰는 날짜·때·하루 단계(Unity 판 Assets/Scripts/Network/Time/NetworkWorldTimeState.cs의 규칙,
    /// 원본 테스트 MULTI_TIME_003·004는 소스 문자열을 보던 것이라 여기서 동작으로 고정한다).
    /// </summary>
    public sealed class SharedWorldTimeTests
    {
        private static readonly string[] Two = { "a", "b" };

        [Test]
        public void New_StartsDayOneMorningDayStart()
        {
            var time = new SharedWorldTime();
            Assert.That(time.Day, Is.EqualTo(1));
            Assert.That(time.PartOfDay, Is.EqualTo(WorldPartOfDay.Morning));
            Assert.That(time.Phase, Is.EqualTo(WorldSchedulePhase.DayStart));
            Assert.That(time.WeekdayIndex, Is.EqualTo(0));
        }

        [Test]
        public void MULTI_TIME_004_NextDayOnlyWhenEveryPresentPlayerIsReady()
        {
            var time = new SharedWorldTime();
            Assert.That(time.MarkReady("a", Two), Is.False);
            Assert.That(time.Day, Is.EqualTo(1));
            Assert.That(time.Phase, Is.EqualTo(WorldSchedulePhase.DayEndReady));
            Assert.That(time.IsReady("a"), Is.True);

            Assert.That(time.MarkReady("a", Two), Is.False, "같은 사람이 두 번 눌러도 둘이 되지 않는다");
            Assert.That(time.MarkReady("b", Two), Is.True);
            Assert.That(time.Day, Is.EqualTo(2));
            Assert.That(time.PartOfDay, Is.EqualTo(WorldPartOfDay.Morning));
            Assert.That(time.Phase, Is.EqualTo(WorldSchedulePhase.DayStart));
            Assert.That(time.ReadyCount, Is.EqualTo(0), "다음 날에는 준비가 비워진다");
        }

        [Test]
        public void SinglePlayer_ReadyAdvancesImmediately()
        {
            var time = new SharedWorldTime();
            Assert.That(time.MarkReady("a", new[] { "a" }), Is.True);
            Assert.That(time.Day, Is.EqualTo(2));
        }

        [Test]
        public void ReadyFromSomeoneNotPresent_IsIgnored()
        {
            var time = new SharedWorldTime();
            Assert.That(time.MarkReady("ghost", Two), Is.False);
            Assert.That(time.ReadyCount, Is.EqualTo(0));
            Assert.That(time.Phase, Is.EqualTo(WorldSchedulePhase.DayStart));
        }

        // Unity 판과 달라진 동작: Unity는 준비한 사람이 나가도 준비 수에 남겨 "남은 사람 수"와 비교했다(남은 사람이 준비하지
        // 않았는데 날이 넘어갈 수 있었다). 또 나간 뒤 다시 보지 않아 남은 사람이 모두 준비해도 날이 멈췄다.
        [Test]
        public void ReadyPlayerLeaving_NoLongerCountsForTheRest()
        {
            var time = new SharedWorldTime();
            time.MarkReady("a", Two);
            Assert.That(time.PlayersChanged(new[] { "b" }), Is.False, "b는 아직 준비하지 않았다");
            Assert.That(time.Day, Is.EqualTo(1));
            Assert.That(time.ReadyCount, Is.EqualTo(0));
        }

        [Test]
        public void UnreadyPlayerLeaving_LetsTheReadyOnesGoOn()
        {
            var time = new SharedWorldTime();
            time.MarkReady("a", Two);
            Assert.That(time.PlayersChanged(new[] { "a" }), Is.True);
            Assert.That(time.Day, Is.EqualTo(2));
        }

        [Test]
        public void EveryoneLeaving_DoesNotAdvance()
        {
            var time = new SharedWorldTime();
            time.MarkReady("a", Two);
            Assert.That(time.PlayersChanged(new string[0]), Is.False);
            Assert.That(time.Day, Is.EqualTo(1));
        }

        [Test]
        public void AdvancePartOfDay_GoesThroughTheDayAndNightWrapsToNextMorning()
        {
            var time = new SharedWorldTime();
            Assert.That(time.AdvancePartOfDay(), Is.False);
            Assert.That(time.PartOfDay, Is.EqualTo(WorldPartOfDay.Afternoon));
            Assert.That(time.Phase, Is.EqualTo(WorldSchedulePhase.Activity));
            time.AdvancePartOfDay();
            time.AdvancePartOfDay();
            Assert.That(time.PartOfDay, Is.EqualTo(WorldPartOfDay.Night));
            Assert.That(time.AdvancePartOfDay(), Is.True, "밤 다음은 다음 날 아침");
            Assert.That(time.Day, Is.EqualTo(2));
            Assert.That(time.PartOfDay, Is.EqualTo(WorldPartOfDay.Morning));
            Assert.That(time.Phase, Is.EqualTo(WorldSchedulePhase.DayStart));
        }

        [Test]
        public void Weekday_CyclesEverySevenDays()
        {
            var time = SharedWorldTime.FromSave(new WorldTimeSaveData { CurrentDay = 9 });
            Assert.That(time.WeekdayIndex, Is.EqualTo(1));
        }

        [Test]
        public void FromSave_ClampsOutOfRangeValuesLikeUnity()
        {
            var time = SharedWorldTime.FromSave(new WorldTimeSaveData { CurrentDay = -3, TimeOfDay = 9, SchedulePhase = -1 });
            Assert.That(time.Day, Is.EqualTo(1));
            Assert.That(time.PartOfDay, Is.EqualTo(WorldPartOfDay.Night));
            Assert.That(time.Phase, Is.EqualTo(WorldSchedulePhase.DayStart));
            Assert.That(SharedWorldTime.FromSave(null).Day, Is.EqualTo(1));
        }

        [Test]
        public void SaveRoundTrip_KeepsDayPartAndPhaseButNotReadyPlayers()
        {
            var time = new SharedWorldTime();
            time.AdvancePartOfDay();
            time.MarkReady("a", Two);
            var saved = time.ToSave();
            Assert.That(saved.Version, Is.EqualTo(WorldTimeSaveData.KnownVersion));
            var back = SharedWorldTime.FromSave(saved);
            Assert.That(back.Day, Is.EqualTo(1));
            Assert.That(back.PartOfDay, Is.EqualTo(WorldPartOfDay.Afternoon));
            Assert.That(back.Phase, Is.EqualTo(WorldSchedulePhase.DayEndReady));
            Assert.That(back.ReadyCount, Is.EqualTo(0), "Unity 판도 준비한 사람은 저장하지 않았다");
        }

        [Test]
        public void SaveShape_UsesUnityFieldNamesAndDefaults()
        {
            var data = new WorldTimeSaveData();
            Assert.That(data.CurrentDay, Is.EqualTo(1));
            Assert.That(data.TimeOfDay, Is.EqualTo(0));
            Assert.That(data.SchedulePhase, Is.EqualTo(0));
            Assert.That(data.Version, Is.EqualTo(SaveVersion.Unity));
        }

        [Test]
        public void SaveFile_IsWorldTimeFolderWithSlotName()
        {
            Assert.That(SaveFileNames.WorldTimeSlot, Is.EqualTo("world-time"));
            Assert.That(SaveFileNames.WorldTime("slot-0"), Is.EqualTo("slot-0.json"));
            Assert.That(SaveFileNames.WorldTime(""), Is.EqualTo("default.json"), "Unity 판: 슬롯이 없으면 default");
            Assert.That(SaveFileNames.WorldTime("a/b"), Is.EqualTo("a_b.json"));
        }

        [Test]
        public void SaveAndLoad_ThroughStore()
        {
            var store = new InMemorySaveStore();
            var time = new SharedWorldTime();
            time.MarkReady("a", new[] { "a" });
            SharedWorldTime.Save(store, "slot-0", time);
            Assert.That(store.Contains(SaveFileNames.WorldTimeSlot, "slot-0.json"), Is.True);
            Assert.That(SharedWorldTime.Load(store, "slot-0").Day, Is.EqualTo(2));
            Assert.That(SharedWorldTime.Load(store, "slot-1").Day, Is.EqualTo(1), "없으면 첫날");
        }
    }
}
