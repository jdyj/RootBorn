using System;

namespace Rootborn.Core.StudentLife
{
    public enum StudentMinigameKind { Practice, PartTimeWork }

    /// <summary>시작했지만 아직 정산하지 않은 미니게임 한 판. 세이브 파일에 쓰는 모양이라 필드 이름을 바꾸지 않는다.</summary>
    [Serializable]
    public sealed class PendingStudentMinigameSaveData
    {
        public StudentMinigameKind Kind;
        public string RequestId;
        public string DefinitionId;
        public string MinigameId;
        public int Day;
        public int ReservedTimePoints;
        public int Seed;

        /// <summary>세이브 도구가 빈 객체로 채워 둔 것도 "없음"으로 본다.</summary>
        public static bool IsEmpty(PendingStudentMinigameSaveData data) => data == null || (string.IsNullOrEmpty(data.RequestId) && string.IsNullOrEmpty(data.DefinitionId)
            && string.IsNullOrEmpty(data.MinigameId) && data.Day == 0 && data.ReservedTimePoints == 0 && data.Seed == 0);

        public static PendingStudentMinigameSaveData Copy(PendingStudentMinigameSaveData data) => IsEmpty(data) ? null : new PendingStudentMinigameSaveData
        {
            Kind = data.Kind, RequestId = data.RequestId, DefinitionId = data.DefinitionId, MinigameId = data.MinigameId,
            Day = data.Day, ReservedTimePoints = data.ReservedTimePoints, Seed = data.Seed,
        };
    }

    // 미니게임 예약과 진로 후보 진행.
    public sealed partial class StudentLifeProgress
    {
        private PendingStudentMinigameSaveData _pendingMinigame;
        private CareerCandidateProgress _careerCandidates;

        public bool HasPendingMinigame => _pendingMinigame != null;
        public PendingStudentMinigameSaveData PendingMinigame => PendingStudentMinigameSaveData.Copy(_pendingMinigame);
        public CareerCandidateProgress CareerCandidates => _careerCandidates ?? (_careerCandidates = new CareerCandidateProgress(SaveSlot, PlayerId));

        internal void ReserveMinigame(PendingStudentMinigameSaveData data) => _pendingMinigame = PendingStudentMinigameSaveData.Copy(data);

        /// <summary>예약을 지운다. markApplied면 그 요청을 쓴 것으로 표시해 다시 정산하지 못하게 한다.</summary>
        internal void ClearReservedMinigame(bool markApplied)
        {
            if (_pendingMinigame == null) return;
            if (markApplied) MarkRequestApplied(_pendingMinigame.RequestId);
            _pendingMinigame = null;
        }

        /// <summary>같은 세이브 슬롯·플레이어의 진로 후보만 받아들인다.</summary>
        public bool RestoreCareerCandidates(CareerCandidateProgressSaveData data)
        {
            if (data == null || data.SaveSlot != SaveSlot || data.PlayerId != PlayerId) return false;
            _careerCandidates = CareerCandidateProgress.FromSaveData(data);
            return true;
        }

        private void RestoreMinigameDomains(StudentLifeProgressSaveData data)
        {
            _pendingMinigame = PendingStudentMinigameSaveData.Copy(data.PendingMinigame);
            if (!RestoreCareerCandidates(data.CareerCandidates)) _careerCandidates = new CareerCandidateProgress(SaveSlot, PlayerId);
        }

        partial void OnNextDayStarted()
        {
            // 정산하지 않은 미니게임 예약은 날이 바뀌면 포기 처리한다(Unity 판은 화면을 다시 불러올 때에야 포기 처리했다).
            ClearReservedMinigame(true);
            CareerCandidates.ClearTodayHintResults();
            _dailyPlan?.SynchronizeDay(this);
        }
    }
}
