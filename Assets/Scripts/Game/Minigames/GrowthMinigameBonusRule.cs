using Rootborn.Game.StudentLife;
using UnityEngine;
namespace Rootborn.Game.Minigames
{
 public enum MinigameBonusTarget { TimingWindow,TimeLimit,MistakeAllowance,HintCount,Harmony,Steadiness }
 [CreateAssetMenu(fileName="GrowthBonus",menuName="Rootborn/Minigames/Growth Bonus")]
 public sealed class GrowthMinigameBonusRule:MinigameBonusRuleBase
 {
  [SerializeField] private TraitDefinition _trait;
  [SerializeField] private SkillDefinition _skill;
  [SerializeField] private MinigameBonusTarget _target;
  [SerializeField,Min(0)] private float _amountPerPoint=.01f;
  [SerializeField,Min(0)] private float _maximumBonus=1;
  public override void Apply(StudentLifeProgress progress,ref MinigameTuning tuning)
  {
   if(progress==null)return;int points=Mathf.Max(_trait!=null?progress.GetTraitValue(_trait):0,_skill!=null?progress.GetSkillValue(_skill):0);
   float amount=Mathf.Clamp(points*Mathf.Max(0,_amountPerPoint),0,Mathf.Max(0,_maximumBonus));
   switch(_target){case MinigameBonusTarget.TimingWindow:tuning.TimingWindow+=amount;break;case MinigameBonusTarget.TimeLimit:tuning.ExtraSeconds+=amount;break;case MinigameBonusTarget.MistakeAllowance:tuning.ExtraMistakes+=Mathf.FloorToInt(amount);break;case MinigameBonusTarget.HintCount:tuning.HintCount+=Mathf.FloorToInt(amount);break;case MinigameBonusTarget.Harmony:tuning.Harmony+=amount;break;case MinigameBonusTarget.Steadiness:tuning.Steadiness+=amount;break;}
  }
 }
}
