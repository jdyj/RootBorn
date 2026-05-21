using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Game.Resources;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.Crafting
{
    [Serializable]
    public sealed class ResourceRecipeProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public string[] SeenResourceIds = Array.Empty<string>();
        public string[] RevealedRecipeIds = Array.Empty<string>();
        public string[] SourceActivityIds = Array.Empty<string>();
    }

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
        public string[] SeenResourceIds => ToArray(_seenResourceIds);
        public string[] RevealedRecipeIds => ToArray(_revealedRecipeIds);
        public string[] SourceActivityIds => ToArray(_sourceActivityIds);

        public bool TryReveal(ResourceNodeDefinition resource, RecipeDefinition recipe, string sourceActivityId)
        {
            bool changed = false;
            string resourceId = resource != null ? resource.Id : string.Empty;
            if (!string.IsNullOrEmpty(resourceId)) changed |= _seenResourceIds.Add(resourceId);
            changed |= TryRevealRecipe(recipe, sourceActivityId);
            return changed;
        }

        public bool TryRevealRecipe(RecipeDefinition recipe, string sourceActivityId)
        {
            string recipeId = recipe != null ? recipe.Id : string.Empty;
            if (string.IsNullOrEmpty(recipeId)) return false;
            bool changed = _revealedRecipeIds.Add(recipeId);
            if (!string.IsNullOrEmpty(sourceActivityId)) _sourceActivityIds.Add(sourceActivityId);
            return changed;
        }

        public bool IsRecipeRevealed(RecipeDefinition recipe)
        {
            string recipeId = recipe != null ? recipe.Id : string.Empty;
            return !string.IsNullOrEmpty(recipeId) && _revealedRecipeIds.Contains(recipeId);
        }

        public bool IsResourceSeen(ResourceNodeDefinition resource)
        {
            string resourceId = resource != null ? resource.Id : string.Empty;
            return !string.IsNullOrEmpty(resourceId) && _seenResourceIds.Contains(resourceId);
        }

        public ResourceRecipeProgressSaveData ToSaveData()
        {
            return new ResourceRecipeProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                SeenResourceIds = SeenResourceIds,
                RevealedRecipeIds = RevealedRecipeIds,
                SourceActivityIds = SourceActivityIds
            };
        }

        public static ResourceRecipeProgress FromSaveData(ResourceRecipeProgressSaveData saveData)
        {
            if (saveData == null) return new ResourceRecipeProgress("default", "player");
            var progress = new ResourceRecipeProgress(saveData.SaveSlot, saveData.PlayerId);
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

        private static string[] ToArray(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }
    }

    public static class ResourceRecipeProgressPersistence
    {
        private const string DefaultFileName = "resource-recipe-progress.json";

        public static ResourceRecipeProgress LoadOrCreate(string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string id = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            string json = new SaveService(slot).ReadJson(FileNameFor(id));
            if (string.IsNullOrEmpty(json)) return new ResourceRecipeProgress(slot, id);

            try
            {
                var data = JsonUtility.FromJson<ResourceRecipeProgressSaveData>(json);
                return data == null ? new ResourceRecipeProgress(slot, id) : ResourceRecipeProgress.FromSaveData(data);
            }
            catch (ArgumentException)
            {
                return new ResourceRecipeProgress(slot, id);
            }
        }

        public static void Save(ResourceRecipeProgress progress)
        {
            if (progress == null) return;
            new SaveService(progress.SaveSlot).WriteJson(FileNameFor(progress.PlayerId), JsonUtility.ToJson(progress.ToSaveData(), true));
        }

        private static string FileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == "player") return DefaultFileName;
            return "resource-recipe-progress-" + SanitizeFileName(playerId) + ".json";
        }

        private static string SanitizeFileName(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return new string(chars);
        }
    }
}
