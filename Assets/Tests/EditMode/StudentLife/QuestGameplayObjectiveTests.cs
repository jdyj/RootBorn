using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Placement;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.StudentLife;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class QuestGameplayObjectiveTests
    {
        private readonly List<Object> _objects = new List<Object>();
        [TearDown] public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [Test]
        public void LocationVisitObjective_MatchesTargetLocationByIdAndEventKind()
        {
            // A copy with the same id is the same location (scene copy vs Addressables bundle copy); another id is not.
            var objective = Objective("LocationVisitQuestObjective");
            var location = Own(ScriptableObject.CreateInstance<LocationDefinition>());
            var copy = Own(ScriptableObject.CreateInstance<LocationDefinition>());
            var other = Own(ScriptableObject.CreateInstance<LocationDefinition>());
            location.ConfigureForTests("same-id", "location", Vector2.zero, null);
            copy.ConfigureForTests("same-id", "location", Vector2.zero, null);
            other.ConfigureForTests("other-id", "location", Vector2.zero, null);
            Set(objective, "_targetLocation", location);
            Assert.That(objective.Matches(Event("LocationVisit", "location", location)), Is.True);
            Assert.That(objective.Matches(Event("LocationVisit", "location", copy)), Is.True);
            Assert.That(objective.Matches(Event("LocationVisit", "location", other)), Is.False);
            Assert.That(objective.Matches(new QuestEvent(QuestEventKind.Talk, "wrong-kind")), Is.False);
            Set(objective, "_targetLocation", null);
            Assert.That(objective.Matches(Event("LocationVisit", "location", location)), Is.False);
        }

        [Test]
        public void PartTimeWorkObjective_AcceptsEitherConfiguredWork_AndNeverJustItsLocation()
        {
            var objective = Objective("PartTimeWorkQuestObjective");
            var first = Own(ScriptableObject.CreateInstance<PartTimeWorkDefinition>());
            var second = Own(ScriptableObject.CreateInstance<PartTimeWorkDefinition>());
            var other = Own(ScriptableObject.CreateInstance<PartTimeWorkDefinition>());
            Set(objective, "_acceptedWorks", new[] { first, second });
            Assert.That(objective.Matches(Event("PartTimeWork", "work", first)), Is.True);
            Assert.That(objective.Matches(Event("PartTimeWork", "work", second)), Is.True);
            Assert.That(objective.Matches(Event("PartTimeWork", "work", other)), Is.False);
            Assert.That(objective.Matches(Event("LocationVisit", "location", Own(ScriptableObject.CreateInstance<LocationDefinition>()))), Is.False);
        }

        [Test]
        public void FurnitureObjective_RequiresAcceptedSoReferenceAndAllowedSurface()
        {
            var objective = Objective("FurniturePlacedQuestObjective");
            var furniture = Own(ScriptableObject.CreateInstance<FurnitureDefinition>());
            var other = Own(ScriptableObject.CreateInstance<FurnitureDefinition>());
            Set(objective, "_acceptedFurniture", new[] { furniture });
            Set(objective, "_requiredSurfaceId", "house");
            Assert.That(objective.Matches(Event("FurniturePlaced", "furniture", furniture, "house")), Is.True);
            Assert.That(objective.Matches(Event("FurniturePlaced", "furniture", furniture, "town")), Is.False);
            Assert.That(objective.Matches(Event("FurniturePlaced", "furniture", other, "house")), Is.False);
            Assert.That(objective.Matches(new QuestEvent(QuestEventKind.TilePlaced, "tile", tile: furniture)), Is.False);
        }

        [Test]
        public void AnyOfObjective_UsesEitherChildWithoutMultiplyingProgress_WhenBothMatch()
        {
            var objective = Objective("AnyOfQuestObjective");
            var activity = Own(ScriptableObject.CreateInstance<LocationActivityDefinition>());
            var first = Own(ScriptableObject.CreateInstance<LocationActivityQuestObjective>());
            var second = Own(ScriptableObject.CreateInstance<LocationActivityQuestObjective>());
            first.ConfigureForRuntime("first", 1, activity);
            second.ConfigureForRuntime("second", 1, activity);
            Set(objective, "_alternatives", new QuestObjectiveBase[] { first, second });
            var matching = new QuestEvent(QuestEventKind.LocationActivity, "activity", activity: activity);
            Assert.That(objective.Matches(matching), Is.True);
            Assert.That(objective.GetDelta(matching), Is.EqualTo(1));
            Assert.That(objective.Matches(new QuestEvent(QuestEventKind.Talk, "talk")), Is.False);
            Set(objective, "_alternatives", Array.Empty<QuestObjectiveBase>());
            Assert.That(objective.Matches(matching), Is.False);
        }

        [Test]
        public void DailyPlanObjective_RequiresSuccessfulSelectionEventAndNonNullRoute()
        {
            var objective = Objective("DailyPlanSelectedQuestObjective");
            var route = Own(ScriptableObject.CreateInstance<DailyPlanRouteDefinition>());
            Assert.That(objective.Matches(Event("DailyPlanSelected", "dailyPlanRoute", route)), Is.True);
            Assert.That(objective.Matches(Event("DailyPlanSelected", "dailyPlanRoute", null)), Is.False);
            Assert.That(objective.Matches(new QuestEvent(QuestEventKind.Talk, "talk")), Is.False);
        }

        private QuestObjectiveBase Objective(string name)
        {
            var type = typeof(QuestEvent).Assembly.GetType("Rootborn.Game.Quests.Objectives." + name);
            Assert.That(type, Is.Not.Null, name + " is required to author gameplay objectives without entity-ID branches.");
            var value = Own((QuestObjectiveBase)ScriptableObject.CreateInstance(type));
            value.ConfigureForRuntime("fixture", 1);
            return value;
        }
        private static QuestEvent Event(string kind, string payloadName, object payload, string surface = null)
        {
            Assert.That(Enum.TryParse(kind, out QuestEventKind parsed), Is.True, "Missing event kind " + kind);
            var constructor = typeof(QuestEvent).GetConstructors().OrderByDescending(x => x.GetParameters().Length).First();
            var parameters = constructor.GetParameters();
            Assert.That(parameters.Any(x => x.Name == payloadName), Is.True, "Missing SO payload " + payloadName);
            var arguments = parameters.Select(x => x.HasDefaultValue ? x.DefaultValue : null).ToArray();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].Name == "kind") arguments[i] = parsed;
                else if (parameters[i].Name == "eventKey") arguments[i] = "fixture-event";
                else if (parameters[i].Name == payloadName) arguments[i] = payload;
                else if (parameters[i].Name == "surfaceId") arguments[i] = surface;
            }
            return (QuestEvent)constructor.Invoke(arguments);
        }
        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
        private T Own<T>(T value) where T : Object { _objects.Add(value); return value; }
    }
}
