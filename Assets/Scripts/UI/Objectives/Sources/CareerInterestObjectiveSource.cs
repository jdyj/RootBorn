using System;
using System.Collections.Generic;
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
                    summary.DisplayName,
                    summary.Description,
                    summary.State.ToString(),
                    BuildCareerInterestProgressText(summary, progress),
                    BuildCareerInterestHintText(summary),
                    selected,
                    selected ? 10 : 40 + i));
            }

            return items.ToArray();
        }

        private static string BuildCareerInterestProgressText(CareerInterestSummary summary, CareerInterestProgress progress)
        {
            string current = progress != null && !string.IsNullOrEmpty(progress.CurrentInterestId) ? progress.CurrentInterestId : "none";
            string text = "Current: " + current;
            if (!string.IsNullOrEmpty(summary.Reason))
            {
                text += " | " + summary.Reason;
            }

            return text;
        }

        private static string BuildCareerInterestHintText(CareerInterestSummary summary)
        {
            string recommendations = ObjectiveJournalSourceText.JoinNonEmpty(summary.RecommendedActionKeys, " | ");
            return string.IsNullOrEmpty(recommendations) ? "Explore town activities, relationships, work, study, or rest to refine this interest." : recommendations;
        }
    }
}
