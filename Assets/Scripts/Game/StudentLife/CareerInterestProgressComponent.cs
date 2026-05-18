using Rootborn.Game.Player;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class CareerInterestProgressComponent : MonoBehaviour
    {
        [SerializeField] private string _fallbackPlayerId = "local-player";
        [SerializeField] private CareerInterestDefinition[] _interests = System.Array.Empty<CareerInterestDefinition>();

        public CareerInterestProgress Progress { get; private set; }
        public CareerInterestDefinition[] Interests => _interests;

        private void Awake()
        {
            if (!CareerInterestProgressPersistence.TryLoad(this)) EnsureProgress();
        }

        public void Bind(CareerInterestDefinition[] interests)
        {
            _interests = interests ?? System.Array.Empty<CareerInterestDefinition>();
            EnsureProgress();
        }

        public void ConfigureForTests(string saveSlot, string playerId)
        {
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot, DisplayName = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot });
            _fallbackPlayerId = string.IsNullOrEmpty(playerId) ? PlayerIdentity.DefaultPlayerId : playerId;
            Progress = null;
        }

        public CareerInterestProgress EnsureProgress()
        {
            if (Progress != null) return Progress;
            string saveSlot = ActiveSaveContext.Metadata != null && !string.IsNullOrEmpty(ActiveSaveContext.Metadata.SlotId) ? ActiveSaveContext.Metadata.SlotId : "default";
            Progress = new CareerInterestProgress(saveSlot, ResolvePlayerId());
            return Progress;
        }

        public void RestoreFromSaveData(CareerInterestProgressSaveData saveData)
        {
            Progress = CareerInterestProgress.FromSaveData(saveData);
        }

        internal string ResolvePlayerId()
        {
            var identity = GetComponent<PlayerIdentity>();
            if (identity != null && !string.IsNullOrEmpty(identity.PlayerId)) return identity.PlayerId;
            return string.IsNullOrEmpty(_fallbackPlayerId) ? PlayerIdentity.DefaultPlayerId : _fallbackPlayerId;
        }
    }

    public static class CareerInterestProgressPersistence
    {
        private const string DefaultFileName = "career-interest-progress.json";

        public static bool TryLoad(CareerInterestProgressComponent component)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (component == null || metadata == null || string.IsNullOrEmpty(metadata.SlotId)) return false;
            string playerId = ResolvePlayerId(component.gameObject);
            string json = new SaveService(metadata.SlotId).ReadJson(FileNameFor(playerId));
            if (string.IsNullOrEmpty(json)) return false;
            component.RestoreFromSaveData(JsonUtility.FromJson<CareerInterestProgressSaveData>(json));
            return component.Progress != null;
        }

        public static void Save(CareerInterestProgressComponent component)
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
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId || playerId == "local-player") return DefaultFileName;
            return "career-interest-progress-" + SanitizeFileName(playerId) + ".json";
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
