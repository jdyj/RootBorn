using Rootborn.Game.Player;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class CareerCandidateProgressComponent : MonoBehaviour
    {
        [SerializeField] private string _fallbackPlayerId = "local-player";
        [SerializeField] private CareerCandidateDefinition[] _candidates = System.Array.Empty<CareerCandidateDefinition>();
        [SerializeField] private CareerHintDefinition[] _hints = System.Array.Empty<CareerHintDefinition>();

        public CareerCandidateProgress Progress { get; private set; }
        public CareerCandidateDefinition[] Candidates => _candidates;
        public CareerHintDefinition[] Hints => _hints;
        public CareerCandidateHintApplyResult LastResult { get; private set; }

        private void Awake()
        {
            if (!CareerCandidateProgressPersistence.TryLoad(this)) EnsureProgress();
        }

        public void Bind(CareerCandidateDefinition[] candidates, CareerHintDefinition[] hints)
        {
            _candidates = candidates ?? System.Array.Empty<CareerCandidateDefinition>();
            _hints = hints ?? System.Array.Empty<CareerHintDefinition>();
        }

        public CareerCandidateProgress EnsureProgress()
        {
            if (Progress != null) return Progress;
            string saveSlot = ActiveSaveContext.Metadata != null && !string.IsNullOrEmpty(ActiveSaveContext.Metadata.SlotId)
                ? ActiveSaveContext.Metadata.SlotId
                : "default";
            Progress = new CareerCandidateProgress(saveSlot, ResolvePlayerId());
            return Progress;
        }

        public void RestoreFromSaveData(CareerCandidateProgressSaveData saveData)
        {
            Progress = CareerCandidateProgress.FromSaveData(saveData);
        }

        public CareerCandidateHintApplyResult ApplyHintsFromResult(StudentLifeProgress studentProgress, string sourceRequestId, string[] resultLogIds)
        {
            var progress = EnsureProgress();
            LastResult = progress.ApplyFirstMatchingHint(_hints, studentProgress, sourceRequestId, resultLogIds);
            if (LastResult.Applied) CareerCandidateProgressPersistence.Save(this);
            return LastResult;
        }

        private string ResolvePlayerId()
        {
            var identity = GetComponent<PlayerIdentity>();
            if (identity != null && !string.IsNullOrEmpty(identity.PlayerId)) return identity.PlayerId;
            return string.IsNullOrEmpty(_fallbackPlayerId) ? PlayerIdentity.DefaultPlayerId : _fallbackPlayerId;
        }
    }

    public static class CareerCandidateProgressPersistence
    {
        private const string DefaultFileName = "career-candidate-progress.json";

        public static bool TryLoad(CareerCandidateProgressComponent component)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (component == null || metadata == null || string.IsNullOrEmpty(metadata.SlotId)) return false;
            string playerId = ResolvePlayerId(component.gameObject);
            string json = new SaveService(metadata.SlotId).ReadJson(FileNameFor(playerId));
            if (string.IsNullOrEmpty(json)) return false;
            component.RestoreFromSaveData(JsonUtility.FromJson<CareerCandidateProgressSaveData>(json));
            return true;
        }

        public static void Save(CareerCandidateProgressComponent component)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (component == null || metadata == null || string.IsNullOrEmpty(metadata.SlotId)) return;
            var progress = component.EnsureProgress();
            string json = JsonUtility.ToJson(progress.ToSaveData(), true);
            new SaveService(metadata.SlotId).WriteJson(FileNameFor(progress.PlayerId), json);
        }

        private static string ResolvePlayerId(GameObject player)
        {
            var identity = player != null ? player.GetComponent<PlayerIdentity>() : null;
            return identity != null && !string.IsNullOrEmpty(identity.PlayerId) ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
        }

        private static string FileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId) return DefaultFileName;
            return "career-candidate-progress-" + SanitizeFileName(playerId) + ".json";
        }

        private static string SanitizeFileName(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_') chars[i] = '_';
            }
            return new string(chars);
        }
    }
}
