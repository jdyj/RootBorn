using System;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "Career_New", menuName = "Rootborn/Student Life/Career")]
    public sealed class CareerDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private CareerUnlockRequirementBase[] _requirements = Array.Empty<CareerUnlockRequirementBase>();

        public bool IsUnlocked(StudentLifeProgress progress)
        {
            if (progress == null)
            {
                return false;
            }

            for (int i = 0; i < _requirements.Length; i++)
            {
                var requirement = _requirements[i];
                if (requirement != null && !requirement.IsSatisfied(progress))
                {
                    return false;
                }
            }

            return true;
        }

        public void ConfigureForTests(string id, string displayNameKey, CareerUnlockRequirementBase[] requirements)
        {
            ConfigureForTests(id, displayNameKey);
            _requirements = requirements ?? Array.Empty<CareerUnlockRequirementBase>();
        }
    }
}
