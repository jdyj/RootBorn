using System.Globalization;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class DailyEventProgressComponent : MonoBehaviour
    {
        [SerializeField] private string _fallbackPlayerId = "local-player";

        public DailyEventProgress Progress { get; private set; }

        private void Awake()
        {
            EnsureProgress();
        }

        public DailyEventProgress EnsureProgress()
        {
            if (Progress != null) return Progress;
            string saveSlot = ActiveSaveContext.Metadata != null && !string.IsNullOrEmpty(ActiveSaveContext.Metadata.SlotId)
                ? ActiveSaveContext.Metadata.SlotId
                : "default";
            Progress = new DailyEventProgress(saveSlot, ResolvePlayerId());
            return Progress;
        }

        public void RestoreFromSaveData(DailyEventProgressSaveData saveData)
        {
            Progress = DailyEventProgress.FromSaveData(saveData);
        }

        private string ResolvePlayerId()
        {
            var identity = GetComponent<PlayerIdentity>();
            if (identity != null && !string.IsNullOrEmpty(identity.PlayerId)) return identity.PlayerId;
            return string.IsNullOrEmpty(_fallbackPlayerId) ? PlayerIdentity.DefaultPlayerId : _fallbackPlayerId;
        }
    }

    public static class DailyEventProgressPersistence
    {
        private const string DailyEventFileName = "daily-event-progress.json";

        public static bool TryLoad(DailyEventProgressComponent component)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (component == null || metadata == null || string.IsNullOrEmpty(metadata.SlotId)) return false;
            string playerId = ResolvePlayerId(component.gameObject);
            string json = new SaveService(metadata.SlotId).ReadJson(FileNameFor(playerId));
            if (string.IsNullOrEmpty(json)) return false;
            component.RestoreFromSaveData(JsonUtility.FromJson<DailyEventProgressSaveData>(json));
            return true;
        }

        public static void Save(DailyEventProgressComponent component)
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
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId) return DailyEventFileName;
            return "daily-event-progress-" + SanitizeFileName(playerId) + ".json";
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
