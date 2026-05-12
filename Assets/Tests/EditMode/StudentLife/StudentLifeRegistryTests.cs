using NUnit.Framework;
using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentLifeRegistryTests
    {
        [Test]
        public void LIFE_STUDENT_001_GameDataRegistryExposesStudentLifeDataArrays()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();

            Assert.IsNotNull(registry.LifeActivities);
            Assert.IsNotNull(registry.StudentLifeChoices);
            Assert.IsNotNull(registry.StudentLifeTraits);
            Assert.IsNotNull(registry.StudentLifeSkills);
            Assert.IsNotNull(registry.Careers);
            Assert.IsNotNull(registry.CareerPractices);
            Assert.IsNotNull(registry.PracticeSteps);
        }
    }
}
