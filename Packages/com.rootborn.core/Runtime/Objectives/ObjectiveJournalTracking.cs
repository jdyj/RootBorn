using System;
using Rootborn.Core.Save;

namespace Rootborn.Core.Objectives
{
    /// <summary>
    /// 목표 수첩에서 추적하는 항목(Unity ObjectiveJournalTrackingStore의 파일 모양, 필드 이름 그대로).
    /// 파일은 슬롯마다 하나다(플레이어와 무관, Unity 판과 같음).
    /// </summary>
    [Serializable]
    public sealed class ObjectiveJournalTrackingSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public string CategoryId;
        public string StableKey;

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>추적 지정 하나. 안정 키가 비면 아무것도 추적하지 않는다. default 값도 빈 글자를 준다.</summary>
    public readonly struct ObjectiveTracking
    {
        public static readonly ObjectiveTracking None = default;

        private readonly string _categoryId;
        private readonly string _stableKey;

        public ObjectiveTracking(string categoryId, string stableKey)
        {
            _categoryId = categoryId ?? string.Empty;
            _stableKey = stableKey ?? string.Empty;
        }

        /// <summary>목표 수첩의 분류 id(빈 글자일 수 있다).</summary>
        public string CategoryId => _categoryId ?? string.Empty;
        /// <summary>목표 수첩 항목의 안정 키.</summary>
        public string StableKey => _stableKey ?? string.Empty;
        public bool IsTracking => StableKey.Length > 0;
    }

    /// <summary>
    /// 추적 지정 파일 읽기·쓰기(Unity ObjectiveJournalTrackingStore와 같은 규칙).
    /// 쓰기: 슬롯이나 안정 키가 비면 쓰지 않는다. 읽기: 파일이 없거나 안정 키가 비면 "추적 없음".
    /// </summary>
    public static class ObjectiveJournalTrackingSaveFiles
    {
        public static bool TryLoad(ISaveStore store, string saveSlot, out ObjectiveTracking tracking)
        {
            tracking = ObjectiveTracking.None;
            if (store == null || string.IsNullOrEmpty(saveSlot)) return false;
            if (!store.TryRead<ObjectiveJournalTrackingSaveData>(saveSlot, SaveFileNames.ObjectiveJournalTracking, out var data)) return false;
            if (data == null || string.IsNullOrEmpty(data.StableKey)) return false;
            tracking = new ObjectiveTracking(data.CategoryId, data.StableKey);
            return true;
        }

        public static bool Save(ISaveStore store, string saveSlot, string categoryId, string stableKey)
        {
            if (store == null || string.IsNullOrEmpty(saveSlot) || string.IsNullOrEmpty(stableKey)) return false;
            store.Write(saveSlot, SaveFileNames.ObjectiveJournalTracking, new ObjectiveJournalTrackingSaveData
            {
                Version = ObjectiveJournalTrackingSaveData.KnownVersion,
                CategoryId = categoryId ?? string.Empty,
                StableKey = stableKey,
            });
            return true;
        }
    }
}
