using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Core.Careers;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Game;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>판을 어디서 시작하는지(시작 상태 종류 하나). 새 종류는 클래스 하나와 <see cref="RunStarts"/> 등록 한 줄로 더한다.</summary>
    internal interface IRunStart
    {
        string Kind { get; }

        /// <summary>저장소 위에 판을 시작한다. target은 종류마다 다르다(시설, 없음 등).</summary>
        GameSession Start(ISaveStore store, object target);
    }

    /// <summary>시작 상태 종류의 등록부.</summary>
    internal static class RunStarts
    {
        private static readonly Dictionary<string, IRunStart> Kinds = Register(new NewGameStart(), new AdultStart());

        public static bool Knows(string kind) => kind != null && Kinds.ContainsKey(kind);

        public static IRunStart Get(string kind)
        {
            if (kind != null && Kinds.TryGetValue(kind, out var start)) return start;
            throw new ArgumentException("모르는 시작 상태 종류다: " + kind, nameof(kind));
        }

        private static Dictionary<string, IRunStart> Register(params IRunStart[] starts)
        {
            var kinds = new Dictionary<string, IRunStart>(starts.Length, StringComparer.Ordinal);
            foreach (var start in starts) kinds.Add(start.Kind, start);
            return kinds;
        }
    }

    /// <summary>새 게임 1일째. target은 쓰지 않는다.</summary>
    internal sealed class NewGameStart : IRunStart
    {
        public string Kind => "new-game";
        public GameSession Start(ISaveStore store, object target) => TestWorld.Start(store);
    }

    /// <summary>
    /// 그 시설(target은 <see cref="Facility"/>)을 막 고른 성인의 첫날.
    /// 학생 시절을 실제로 지나지 않고 세이브를 고쳐 전환 조건을 채운다 — 확인 대상이 아니라 시작 상태를 만드는 **준비 단계**다
    /// (Game 테스트 SaveEdits.MakeChefCandidateReady를 정의에서 끌어낸 값으로 일반화했다). 시설을 고르는 것만 명령(ChooseFacility)으로 한다.
    /// </summary>
    internal sealed class AdultStart : IRunStart
    {
        private const int FirstDayToTry = 4;
        private const int LastDayToTry = 30;

        public string Kind => "adult";

        public GameSession Start(ISaveStore store, object target)
        {
            if (!(target is Facility facility)) throw new ArgumentException("성인 시작에는 시설이 필요하다", nameof(target));
            if (facility.Candidate == null) throw new InvalidOperationException("시설에 진로 후보가 없다: " + facility.Id);

            // 첫 저장: 시설 관리 화면을 여는 것과 같다(고치려면 세이브가 먼저 있어야 한다).
            var first = TestWorld.Start(store);
            if (!first.VisitFacility(facility.Id)) throw new InvalidOperationException("시설에 장소가 없어 첫 저장을 하지 못했다: " + facility.Id);

            var hintIds = HintIdsFor(first.Definitions, facility.Candidate);
            var last = AdultCareerResultKind.InvalidDefinition;
            for (int day = FirstDayToTry; day <= LastDayToTry; day++)
            {
                EditSnapshot(store, snapshot => MakeCandidateReady(snapshot, facility, hintIds, day));
                var game = TestWorld.Start(store);
                last = game.CanChooseFacility(facility.Id);
                if (last != AdultCareerResultKind.Applied) continue;

                var chosen = game.ChooseFacility(facility.Id, "balance-start:" + facility.Id);
                if (!chosen.Applied) throw new InvalidOperationException("시설을 고르지 못했다: " + facility.Id + " (" + chosen.Kind + ")");
                return game;
            }

            throw new InvalidOperationException(LastDayToTry + "일째까지 시설을 고를 수 없었다: " + facility.Id + " (마지막 결과 " + last + ")");
        }

        // 그 후보를 가리키는 힌트(정의 순서)에서 준비 문턱만큼 고른다.
        private static string[] HintIdsFor(Rootborn.Data.GameDefinitions definitions, CareerCandidate candidate)
            => definitions.All<CareerHint>()
                .Where(h => h.Candidate != null && h.Candidate.Id == candidate.Id)
                .Select(h => h.Id)
                .Take(candidate.ReadyHintThreshold)
                .ToArray();

        private static void EditSnapshot(ISaveStore store, Action<AdultCareerSnapshotSaveData> edit)
        {
            var name = SaveFileNames.AdultCareerSnapshot(TestWorld.Player);
            if (!store.TryRead<AdultCareerSnapshotSaveData>(TestWorld.Slot, name, out var snapshot))
                throw new InvalidOperationException("먼저 한 번 저장해야 한다");
            edit(snapshot);
            store.Write(TestWorld.Slot, name, snapshot);
        }

        private static void MakeCandidateReady(AdultCareerSnapshotSaveData snapshot, Facility facility, string[] hintIds, int day)
        {
            var student = snapshot.Student;
            var candidate = facility.Candidate;
            student.CurrentDay = Math.Max(day, student.CurrentDay);
            student.DayState = StudentDayState.InProgress.ToString();
            student.TimePointsSpentToday = 0;
            student.CareerHintIds = (student.CareerHintIds ?? Array.Empty<string>()).Concat(new[] { facility.CareerId }).Distinct().ToArray();
            student.CareerCandidates ??= new CareerCandidateProgressSaveData { SaveSlot = student.SaveSlot, PlayerId = student.PlayerId };
            student.CareerCandidates.Candidates = new[]
            {
                new CareerCandidateProgressSaveData.CandidateEntry
                {
                    CandidateId = candidate.Id,
                    UnderstandingScore = candidate.ReadyHintThreshold,
                    HintIds = hintIds,
                    LastHintSourceId = hintIds.Length > 0 ? hintIds[hintIds.Length - 1] : string.Empty,
                },
            };
        }
    }
}
