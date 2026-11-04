using System;

namespace Rootborn.Game.StudentLife
{
    public enum StudentMinigameKind { Practice, PartTimeWork }

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
        public static bool IsEmpty(PendingStudentMinigameSaveData data) => data == null || (string.IsNullOrEmpty(data.RequestId) && string.IsNullOrEmpty(data.DefinitionId) && string.IsNullOrEmpty(data.MinigameId) && data.Day == 0 && data.ReservedTimePoints == 0 && data.Seed == 0);
        public static PendingStudentMinigameSaveData Copy(PendingStudentMinigameSaveData data) => IsEmpty(data) ? null : new PendingStudentMinigameSaveData
        { Kind = data.Kind, RequestId = data.RequestId, DefinitionId = data.DefinitionId, MinigameId = data.MinigameId, Day = data.Day, ReservedTimePoints = data.ReservedTimePoints, Seed = data.Seed };
    }

    public sealed partial class StudentLifeProgress
    {
        private PendingStudentMinigameSaveData _pendingMinigame;
        private CareerCandidateProgress _careerCandidates;
        public bool HasPendingMinigame => _pendingMinigame != null;
        public PendingStudentMinigameSaveData PendingMinigame => PendingStudentMinigameSaveData.Copy(_pendingMinigame);
        public CareerCandidateProgress CareerCandidates => _careerCandidates ??= new CareerCandidateProgress(SaveSlot, PlayerId);
        internal void ReserveMinigame(PendingStudentMinigameSaveData data) => _pendingMinigame = PendingStudentMinigameSaveData.Copy(data);
        internal void ClearReservedMinigame(bool markApplied)
        {
            if (_pendingMinigame == null) return;
            if (markApplied) MarkRequestApplied(_pendingMinigame.RequestId);
            _pendingMinigame = null;
        }
        public bool RestoreCareerCandidates(CareerCandidateProgressSaveData data)
        {
            if (data == null || data.SaveSlot != SaveSlot || data.PlayerId != PlayerId) return false;
            _careerCandidates = CareerCandidateProgress.FromSaveData(data); return true;
        }
        private void RestoreMinigameDomains(StudentLifeProgressSaveData data)
        {
            _pendingMinigame = PendingStudentMinigameSaveData.Copy(data.PendingMinigame);
            if (!RestoreCareerCandidates(data.CareerCandidates)) _careerCandidates = new CareerCandidateProgress(SaveSlot, PlayerId);
        }
    }
}
