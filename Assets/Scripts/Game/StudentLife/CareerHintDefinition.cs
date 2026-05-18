using System;
        using System.Collections.Generic;
        using UnityEngine;
        
        namespace Rootborn.Game.StudentLife
        {
            [CreateAssetMenu(fileName = "CareerHint_New", menuName = "Rootborn/Student Life/Career Candidates/Hint")]
            public sealed class CareerHintDefinition : StudentLifeDefinitionBase
            {
                [SerializeField] private CareerCandidateDefinition _candidate;
                [SerializeField] private CareerCandidateRouteDefinition _route;
                [SerializeField] private string _insightKey;
                [SerializeField] private int _understandingDelta = 1;
                [SerializeField] private CareerHintSourceBase[] _sources = Array.Empty<CareerHintSourceBase>();
        
                public CareerCandidateDefinition Candidate => _candidate;
                public CareerCandidateRouteDefinition Route => _route;
                public string InsightKey => string.IsNullOrEmpty(_insightKey) ? DisplayNameKey : _insightKey;
                public int UnderstandingDelta => Mathf.Max(1, _understandingDelta);
                public IReadOnlyList<CareerHintSourceBase> Sources => _sources;
        
                public bool Matches(CareerCandidateEvaluationContext context)
                {
                    if (_candidate == null || _sources == null || _sources.Length == 0) return false;
                    for (int i = 0; i < _sources.Length; i++)
                    {
                        if (_sources[i] != null && _sources[i].Matches(context)) return true;
                    }
        
                    return false;
                }
        
                public void ConfigureForTests(
                    string id,
                    string displayNameKey,
                    CareerCandidateDefinition candidate,
                    CareerCandidateRouteDefinition route,
                    string insightKey,
                    int understandingDelta,
                    CareerHintSourceBase[] sources)
                {
                    ConfigureForTests(id, displayNameKey);
                    _candidate = candidate;
                    _route = route;
                    _insightKey = insightKey;
                    _understandingDelta = understandingDelta;
                    _sources = sources ?? Array.Empty<CareerHintSourceBase>();
                }
            }
        }
        