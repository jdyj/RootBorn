using System;

namespace Rootborn.Game.StudentLife
{
    public readonly struct DailyEventResultSummary
    {
        public readonly string EventId;
        public readonly string State;
        public readonly string SelectedChoiceId;
        public readonly string CompletionRequestId;
        public readonly string[] ResultSummaryLogIds;

        public DailyEventResultSummary(string eventId, string state, string selectedChoiceId, string completionRequestId, string[] resultSummaryLogIds)
        {
            EventId = string.IsNullOrEmpty(eventId) ? string.Empty : eventId;
            State = string.IsNullOrEmpty(state) ? DailyEventStates.Unseen : state;
            SelectedChoiceId = string.IsNullOrEmpty(selectedChoiceId) ? string.Empty : selectedChoiceId;
            CompletionRequestId = string.IsNullOrEmpty(completionRequestId) ? string.Empty : completionRequestId;
            ResultSummaryLogIds = resultSummaryLogIds ?? Array.Empty<string>();
        }

        public static DailyEventResultSummary FromRecord(DailyEventRecord record)
        {
            if (record == null) return new DailyEventResultSummary(string.Empty, DailyEventStates.Unseen, string.Empty, string.Empty, Array.Empty<string>());
            return new DailyEventResultSummary(record.EventId, record.State, record.SelectedChoiceId, record.CompletionRequestId, record.ResultSummaryLogIds);
        }
    }
}
