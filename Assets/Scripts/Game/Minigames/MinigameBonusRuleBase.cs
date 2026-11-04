using Rootborn.Game.StudentLife;
using UnityEngine;
namespace Rootborn.Game.Minigames
{
 public abstract class MinigameBonusRuleBase:ScriptableObject { public abstract void Apply(StudentLifeProgress progress,ref MinigameTuning tuning); }
}
