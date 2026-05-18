using System;
        using System.Collections.Generic;
        using UnityEngine;
        
        namespace Rootborn.Game.StudentLife
        {
            [CreateAssetMenu(fileName = "CareerCandidateRoute_New", menuName = "Rootborn/Student Life/Career Candidates/Route")]
            public sealed class CareerCandidateRouteDefinition : StudentLifeDefinitionBase
            {
                [SerializeField] private string _descriptionKey;
                [SerializeField] private LocationDefinition[] _locations = Array.Empty<LocationDefinition>();
                [SerializeField] private LifeActivityDefinition[] _activities = Array.Empty<LifeActivityDefinition>();
                [SerializeField] private string[] _recommendedActionKeys = Array.Empty<string>();
        
                public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? DisplayNameKey : _descriptionKey;
                public IReadOnlyList<LocationDefinition> Locations => _locations;
                public IReadOnlyList<LifeActivityDefinition> Activities => _activities;
                public IReadOnlyList<string> RecommendedActionKeys => _recommendedActionKeys;
        
                public void ConfigureForTests(string id, string displayNameKey, string descriptionKey, LocationDefinition[] locations = null, LifeActivityDefinition[] activities = null, string[] recommendedActionKeys = null)
                {
                    ConfigureForTests(id, displayNameKey);
                    _descriptionKey = descriptionKey;
                    _locations = locations ?? Array.Empty<LocationDefinition>();
                    _activities = activities ?? Array.Empty<LifeActivityDefinition>();
                    _recommendedActionKeys = recommendedActionKeys ?? Array.Empty<string>();
                }
            }
        }
        