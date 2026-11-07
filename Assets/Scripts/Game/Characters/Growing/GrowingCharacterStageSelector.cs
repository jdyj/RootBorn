using System.Collections.Generic;

namespace Rootborn.Game.Characters.Growing
{
    /// <summary>Picks the look from progress using only the rules on each appearance asset.</summary>
    public static class GrowingCharacterStageSelector
    {
        /// <summary>Snapshot part category for the starting outfit of one body: "outfit:" + body id.</summary>
        public const string OutfitCategoryPrefix = "outfit:";
        public const string CreatorCatalogId = "growing.creator";

        /// <summary>
        /// Starting-outfit options for the new-game creator: one group per body, in growth order (younger first, adult last).
        /// Facility outfits are earned at work, so they are not offered here.
        /// </summary>
        public static List<List<GrowingCharacterAppearanceDefinition>> CreatorStages(CharacterAppearanceDefinition[] appearances)
        {
            var stages = new List<List<GrowingCharacterAppearanceDefinition>>();
            if (appearances == null) return stages;
            for (int i = 0; i < appearances.Length; i++)
            {
                if (!(appearances[i] is GrowingCharacterAppearanceDefinition look) || look.Body == null || !string.IsNullOrEmpty(look.FacilityId)) continue;
                List<GrowingCharacterAppearanceDefinition> stage = null;
                for (int s = 0; s < stages.Count && stage == null; s++) if (stages[s][0].Body == look.Body) stage = stages[s];
                if (stage == null) stages.Add(stage = new List<GrowingCharacterAppearanceDefinition>());
                stage.Add(look);
            }

            stages.Sort((a, b) => a[0].RequiresAdult != b[0].RequiresAdult ? (a[0].RequiresAdult ? 1 : -1) : a[0].MinimumStudentDay.CompareTo(b[0].MinimumStudentDay));
            return stages;
        }

        /// <summary>The outfit the creator picked for this body, or empty when the save has no creator choice for it.</summary>
        public static string ChoiceFromSnapshot(CharacterAppearanceSnapshot snapshot, string bodyId)
        {
            if (snapshot == null || snapshot.VisualKind != GrowingCharacterAppearanceDefinition.VisualKindName || string.IsNullOrEmpty(bodyId)) return string.Empty;
            return snapshot.GetSelectedPartId(OutfitCategoryPrefix + bodyId);
        }

        public static GrowingCharacterAppearanceDefinition Select(CharacterAppearanceDefinition[] appearances, int studentDay, bool isAdult, string facilityId)
        {
            if (appearances == null) return null;
            return isAdult ? SelectAdult(appearances, facilityId) : SelectStudent(appearances, studentDay);
        }

        /// <summary>Looks wearable on the same body: outfits are drawn per body, so other stages never fit.</summary>
        public static List<GrowingCharacterAppearanceDefinition> Options(CharacterAppearanceDefinition[] appearances, GrowingCharacterAppearanceDefinition current)
        {
            var options = new List<GrowingCharacterAppearanceDefinition>();
            if (appearances == null || current == null) return options;
            for (int i = 0; i < appearances.Length; i++)
            {
                if (appearances[i] is GrowingCharacterAppearanceDefinition look && look.Body == current.Body && look.Head == current.Head) options.Add(look);
            }

            return options;
        }

        public static GrowingCharacterAppearanceDefinition ApplyChoice(CharacterAppearanceDefinition[] appearances, GrowingCharacterAppearanceDefinition look, string chosenId)
        {
            if (look == null || string.IsNullOrEmpty(chosenId)) return look;
            var options = Options(appearances, look);
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Id == chosenId) return options[i];
            }

            return look;
        }

        private static GrowingCharacterAppearanceDefinition SelectStudent(CharacterAppearanceDefinition[] appearances, int studentDay)
        {
            GrowingCharacterAppearanceDefinition best = null;
            for (int i = 0; i < appearances.Length; i++)
            {
                if (!(appearances[i] is GrowingCharacterAppearanceDefinition look) || look.RequiresAdult || look.MinimumStudentDay > studentDay) continue;
                if (best == null || look.MinimumStudentDay > best.MinimumStudentDay) best = look;
            }

            return best;
        }

        private static GrowingCharacterAppearanceDefinition SelectAdult(CharacterAppearanceDefinition[] appearances, string facilityId)
        {
            GrowingCharacterAppearanceDefinition general = null;
            for (int i = 0; i < appearances.Length; i++)
            {
                if (!(appearances[i] is GrowingCharacterAppearanceDefinition look) || !look.RequiresAdult) continue;
                if (string.IsNullOrEmpty(look.FacilityId))
                {
                    if (general == null) general = look;
                }
                else if (look.FacilityId == facilityId)
                {
                    return look;
                }
            }

            return general;
        }
    }
}
