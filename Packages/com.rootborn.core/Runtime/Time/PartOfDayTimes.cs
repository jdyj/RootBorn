using System;
using Rootborn.Core.Network;

namespace Rootborn.Core.Time
{
    /// <summary>
    /// 방의 시계에서 "때 → 대표 시각(분)". 하루의 때가 n개면 낮 시각 목록의 마지막 n개를 순서대로 쓴다(평일 2 = 낮·저녁,
    /// 주말 3 = 아침·낮·저녁). 때가 목록보다 많으면 모자란 앞쪽은 첫 시각을 다시 쓴다. 때를 다 쓴 뒤(하루가 끝남)는 밤 시각.
    /// 달력 정의(<see cref="StudentLife.LifeCalendar"/>)가 하나 갖는다. 평일·주말을 여기서 가르지 않는다.
    /// </summary>
    public sealed class PartOfDayTimes
    {
        private readonly int[] _dayPartMinutes;

        public PartOfDayTimes(int[] dayPartMinutes, int dayOverMinutes)
        {
            _dayPartMinutes = dayPartMinutes == null || dayPartMinutes.Length == 0 ? new[] { 0 } : (int[])dayPartMinutes.Clone();
            for (int i = 0; i < _dayPartMinutes.Length; i++) _dayPartMinutes[i] = Clamp(_dayPartMinutes[i]);
            DayOverMinutes = Clamp(dayOverMinutes);
        }

        /// <summary>
        /// 기본값: 공용 시간의 대표 시각(<see cref="WorldTimeHours"/>)과 같다 — 아침 9:00, 낮 14:00, 저녁 18:30, 밤 22:00.
        /// 데이터(달력)가 대표 시각을 주기 전까지 쓴다.
        /// </summary>
        public static PartOfDayTimes Default { get; } = new PartOfDayTimes(
            new[] { Minutes(WorldPartOfDay.Morning), Minutes(WorldPartOfDay.Afternoon), Minutes(WorldPartOfDay.Evening) },
            Minutes(WorldPartOfDay.Night));

        public int DayOverMinutes { get; }

        /// <param name="partIndex">0부터. partsInDay 이상이면 하루가 끝난 것.</param>
        public int MinutesFor(int partIndex, int partsInDay)
        {
            if (partsInDay <= 0 || partIndex >= partsInDay) return DayOverMinutes;
            int index = Math.Max(0, partIndex) + _dayPartMinutes.Length - partsInDay;
            return _dayPartMinutes[index < 0 ? 0 : index];
        }

        private static int Minutes(int partOfDay) => (int)Math.Round(WorldTimeHours.Representative(partOfDay) * 60f);

        private static int Clamp(int minutes) => minutes < 0 ? 0 : minutes >= 24 * 60 ? 24 * 60 - 1 : minutes;
    }
}
