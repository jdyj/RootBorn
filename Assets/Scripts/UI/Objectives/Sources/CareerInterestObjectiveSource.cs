using System;
using System.Collections.Generic;
using Rootborn.Game.Localization;
using Rootborn.Game.StudentLife;

namespace Rootborn.UI.Objectives
{
    public sealed class CareerInterestObjectiveSource : IObjectiveJournalSource
    {
        private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Hints", "Hints");

        public string SourceId => "career-interests";
        public ObjectiveJournalCategory Category => CategoryValue;

        public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
        {
            var interestsComponent = context.Player != null ? context.Player.GetComponent<CareerInterestProgressComponent>() : null;
            var interests = interestsComponent != null ? interestsComponent.Interests : null;
            if (interestsComponent == null || interests == null || interests.Length == 0)
            {
                return Array.Empty<ObjectiveJournalItem>();
            }

            var student = context.Player.GetComponent<StudentLifeProgressComponent>()?.Progress;
            var candidates = context.Player.GetComponent<CareerCandidateProgressComponent>()?.Progress;
            var progress = interestsComponent.EnsureProgress();
            int day = student != null ? student.CurrentDay : 0;
            var items = new List<ObjectiveJournalItem>();

            for (int i = 0; i < interests.Length; i++)
            {
                var interest = interests[i];
                if (interest == null || string.IsNullOrEmpty(interest.Id))
                {
                    continue;
                }

                var summary = CareerInterestSummaryBuilder.Build(interest, progress, candidates, student, day);
                bool selected = progress.CurrentInterestId == interest.Id;
                items.Add(new ObjectiveJournalItem(
                    "career-interest." + interest.Id,
                    Category.CategoryId,
                    GameText.Get(summary.DisplayName),
                    GameText.Get(summary.Description),
                    ObjectiveJournalSourceText.State(summary.State.ToString()),
                    BuildCareerInterestProgressText(progress, interests),
                    BuildCareerInterestHintText(summary),
                    selected,
                    selected ? 10 : 40 + i));
            }

            return items.ToArray();
        }

        private static string BuildCareerInterestProgressText(CareerInterestProgress progress, CareerInterestDefinition[] interests)
        {
            // A saved dream that is no longer offered reads as "not chosen" instead of showing its internal id.
            string current = GameText.Get("dream.journal.none");
            for (int i = 0; progress != null && i < interests.Length; i++)
                if (interests[i] != null && interests[i].Id == progress.CurrentInterestId) current = GameText.Get(interests[i].DisplayNameKey);
            return GameText.Format("dream.journal.current", current);
        }

        private static string BuildCareerInterestHintText(CareerInterestSummary summary)
        {
            var keys = summary.RecommendedActionKeys;
            var lines = new string[keys.Length];
            for (int i = 0; i < keys.Length; i++) lines[i] = GameText.Get(keys[i]);
            string recommendations = ObjectiveJournalSourceText.JoinNonEmpty(lines, " | ");
            return string.IsNullOrEmpty(recommendations) ? GameText.Get("dream.journal.hint") : recommendations;
        }
    }
}
