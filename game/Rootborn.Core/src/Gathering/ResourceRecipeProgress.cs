using System;
using System.Collections.Generic;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Gathering
{
    /// <summary>resource-recipe-progress*.json의 모양. 필드 이름은 Unity 판 ResourceRecipeProgressSaveData와 같다.</summary>
    [Serializable]
    public sealed class ResourceRecipeProgressSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public string SaveSlot;
        public string PlayerId;
        public string[] SeenResourceIds = Array.Empty<string>();
        public string[] RevealedRecipeIds = Array.Empty<string>();
        public string[] SourceActivityIds = Array.Empty<string>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>본 자원과 공개된 레시피(원본 Assets/Scripts/Game/Crafting/ResourceRecipeProgress.cs). 목록은 글자 순서로 쓴다.</summary>
    public sealed class ResourceRecipeProgress
    {
        private readonly HashSet<string> _seenResourceIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _revealedRecipeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _sourceActivityIds = new HashSet<string>(StringComparer.Ordinal);

        public ResourceRecipeProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public string[] SeenResourceIds => Sorted(_seenResourceIds);
        public string[] RevealedRecipeIds => Sorted(_revealedRecipeIds);
        public string[] SourceActivityIds => Sorted(_sourceActivityIds);

        /// <summary>자원을 본 것으로, 레시피를 공개한 것으로 남긴다. 무엇이든 새로 바뀌었으면 true.</summary>
        public bool TryReveal(string resourceId, string recipeId, string sourceActivityId)
        {
            bool changed = !string.IsNullOrEmpty(resourceId) && _seenResourceIds.Add(resourceId);
            return TryRevealRecipe(recipeId, sourceActivityId) | changed;
        }

        /// <summary>새로 공개했으면 true. 출처 활동은 레시피가 있을 때만 남긴다.</summary>
        public bool TryRevealRecipe(string recipeId, string sourceActivityId)
        {
            if (string.IsNullOrEmpty(recipeId)) return false;
            bool changed = _revealedRecipeIds.Add(recipeId);
            if (!string.IsNullOrEmpty(sourceActivityId)) _sourceActivityIds.Add(sourceActivityId);
            return changed;
        }

        public bool IsRecipeRevealed(string recipeId) => !string.IsNullOrEmpty(recipeId) && _revealedRecipeIds.Contains(recipeId);
        public bool IsResourceSeen(string resourceId) => !string.IsNullOrEmpty(resourceId) && _seenResourceIds.Contains(resourceId);

        public ResourceRecipeProgressSaveData ToSaveData()
        {
            return new ResourceRecipeProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                SeenResourceIds = SeenResourceIds,
                RevealedRecipeIds = RevealedRecipeIds,
                SourceActivityIds = SourceActivityIds,
            };
        }

        /// <summary>기록의 슬롯·플레이어가 비면 받은 값을 쓴다. 빈 id는 버린다.</summary>
        public static ResourceRecipeProgress FromSaveData(ResourceRecipeProgressSaveData saveData, string fallbackSaveSlot, string fallbackPlayerId)
        {
            var progress = new ResourceRecipeProgress(
                saveData != null && !string.IsNullOrEmpty(saveData.SaveSlot) ? saveData.SaveSlot : fallbackSaveSlot,
                saveData != null && !string.IsNullOrEmpty(saveData.PlayerId) ? saveData.PlayerId : fallbackPlayerId);
            if (saveData == null) return progress;
            AddRange(progress._seenResourceIds, saveData.SeenResourceIds);
            AddRange(progress._revealedRecipeIds, saveData.RevealedRecipeIds);
            AddRange(progress._sourceActivityIds, saveData.SourceActivityIds);
            return progress;
        }

        private static void AddRange(HashSet<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }

        private static string[] Sorted(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }
    }

    /// <summary>레시피 진행 파일 읽기·쓰기. 파일 이름은 SaveFileNames.ResourceRecipe.</summary>
    public static class ResourceRecipeSaveFiles
    {
        public static ResourceRecipeProgress LoadOrCreate(ISaveStore store, string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            if (store == null || !store.TryRead(slot, SaveFileNames.ResourceRecipe(player), out ResourceRecipeProgressSaveData data))
                return new ResourceRecipeProgress(slot, player);
            return ResourceRecipeProgress.FromSaveData(data, slot, player);
        }

        public static void Save(ISaveStore store, ResourceRecipeProgress progress)
        {
            if (store == null || progress == null) return;
            store.Write(progress.SaveSlot, SaveFileNames.ResourceRecipe(progress.PlayerId), progress.ToSaveData());
        }
    }

    /// <summary>
    /// 장소 활동의 레시피 공개 결과가 쓰는 실행 서비스(원본 LocationActivityResourceRecipeOutcome.Apply의 불러오기·저장).
    /// 학생 진행 상태의 슬롯·플레이어 파일을 읽고, 바뀌었을 때만 저장한다.
    /// </summary>
    public sealed class ResourceRecipeReveal : ILocationActivityRecipeReveal
    {
        private readonly ISaveStore _store;

        public ResourceRecipeReveal(ISaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public bool TryReveal(StudentLifeProgress progress, string resourceId, string recipeId, string sourceActivityId)
        {
            if (progress == null) return false;
            var recipes = ResourceRecipeSaveFiles.LoadOrCreate(_store, progress.SaveSlot, progress.PlayerId);
            if (!recipes.TryReveal(resourceId, recipeId, sourceActivityId)) return false;
            ResourceRecipeSaveFiles.Save(_store, recipes);
            return true;
        }
    }
}
