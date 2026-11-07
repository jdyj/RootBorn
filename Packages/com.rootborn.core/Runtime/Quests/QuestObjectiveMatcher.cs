using System;
using System.Collections.Generic;

namespace Rootborn.Core.Quests
{
    /// <summary>What happened, reduced to ids: the event kind, the id of what it happened to, and how many times.</summary>
    public readonly struct QuestEventFacts
    {
        public QuestEventFacts(int kind, string subjectId, int count = 1, string surfaceId = null)
        {
            Kind = kind;
            SubjectId = subjectId;
            Count = count;
            SurfaceId = surfaceId;
        }

        public int Kind { get; }
        public string SubjectId { get; }
        public int Count { get; }
        public string SurfaceId { get; }
    }

    public enum QuestSubjectRule
    {
        /// <summary>The event subject must be one of the accepted ids. No accepted ids means nothing matches.</summary>
        Listed,
        /// <summary>Any event of the kind that has a subject.</summary>
        AnySubject,
        /// <summary>Any event of the kind, with or without a subject.</summary>
        AnyEvent
    }

    /// <summary>What an objective accepts, by id only, so copies of the same definition loaded from different places match.</summary>
    public sealed class QuestObjectiveSpec
    {
        private readonly HashSet<string> _acceptedIds;

        private QuestObjectiveSpec(int kind, QuestSubjectRule rule, IEnumerable<string> acceptedIds, string requiredSurfaceId)
        {
            Kind = kind;
            Rule = rule;
            RequiredSurfaceId = string.IsNullOrEmpty(requiredSurfaceId) ? null : requiredSurfaceId;
            _acceptedIds = new HashSet<string>(StringComparer.Ordinal);
            if (acceptedIds == null) return;
            foreach (string id in acceptedIds)
                if (!string.IsNullOrEmpty(id)) _acceptedIds.Add(id);
        }

        public int Kind { get; }
        public QuestSubjectRule Rule { get; }
        public string RequiredSurfaceId { get; }
        public IReadOnlyCollection<string> AcceptedIds => _acceptedIds;

        public static QuestObjectiveSpec Listed(int kind, params string[] acceptedIds) => new QuestObjectiveSpec(kind, QuestSubjectRule.Listed, acceptedIds, null);
        public static QuestObjectiveSpec Listed(int kind, IEnumerable<string> acceptedIds) => new QuestObjectiveSpec(kind, QuestSubjectRule.Listed, acceptedIds, null);
        public static QuestObjectiveSpec AnySubject(int kind) => new QuestObjectiveSpec(kind, QuestSubjectRule.AnySubject, null, null);
        public static QuestObjectiveSpec AnyEvent(int kind) => new QuestObjectiveSpec(kind, QuestSubjectRule.AnyEvent, null, null);

        public QuestObjectiveSpec WithRequiredSurface(string surfaceId) => new QuestObjectiveSpec(Kind, Rule, _acceptedIds, surfaceId);

        internal bool Accepts(string subjectId) => !string.IsNullOrEmpty(subjectId) && _acceptedIds.Contains(subjectId);
    }

    /// <summary>Decides whether an event counts toward an objective. Empty ids never count as the same entry.</summary>
    public static class QuestObjectiveMatcher
    {
        public static bool Matches(QuestObjectiveSpec spec, in QuestEventFacts facts)
        {
            if (spec == null || facts.Kind != spec.Kind) return false;
            if (spec.RequiredSurfaceId != null && !string.Equals(spec.RequiredSurfaceId, facts.SurfaceId, StringComparison.Ordinal)) return false;
            switch (spec.Rule)
            {
                case QuestSubjectRule.AnyEvent: return true;
                case QuestSubjectRule.AnySubject: return !string.IsNullOrEmpty(facts.SubjectId);
                default: return spec.Accepts(facts.SubjectId);
            }
        }

        public static int Delta(QuestObjectiveSpec spec, in QuestEventFacts facts)
            => Matches(spec, facts) ? Math.Max(1, facts.Count) : 0;
    }
}
