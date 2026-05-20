using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DailyPlanRouteSet_New", menuName = "Rootborn/Student Life/Daily Plan/Route Set")]
    public sealed class DailyPlanRouteSetDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private DailyPlanRouteDefinition[] _routes = Array.Empty<DailyPlanRouteDefinition>();
        [SerializeField] private int _maxSelectionsPerDay = 1;

        public IReadOnlyList<DailyPlanRouteDefinition> Routes => _routes;
        public int MaxSelectionsPerDay => Mathf.Max(1, _maxSelectionsPerDay);

        public DailyPlanRouteDefinition[] GetAvailableRoutes(StudentLifeProgress progress)
        {
            if (progress == null) return Array.Empty<DailyPlanRouteDefinition>();
            var routes = new List<DailyPlanRouteDefinition>();
            for (int i = 0; i < _routes.Length; i++)
            {
                var route = _routes[i];
                if (route != null && route.IsAvailable(progress)) routes.Add(route);
            }

            return routes.ToArray();
        }

        public void ConfigureForTests(string id, string displayNameKey, DailyPlanRouteDefinition[] routes, int maxSelectionsPerDay)
        {
            ConfigureForTests(id, displayNameKey);
            _routes = routes ?? Array.Empty<DailyPlanRouteDefinition>();
            _maxSelectionsPerDay = Mathf.Max(1, maxSelectionsPerDay);
        }
    }
}
