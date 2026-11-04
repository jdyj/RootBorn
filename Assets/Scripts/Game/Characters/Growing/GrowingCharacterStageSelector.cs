using System.Collections.Generic;

namespace Rootborn.Game.Characters.Growing
{
    /// <summary>Picks the look from progress using only the rules on each appearance asset.</summary>
    public static class GrowingCharacterStageSelector
    {
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
