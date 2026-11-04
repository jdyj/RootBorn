using System;
using Rootborn.Game.StudentLife;
using UnityEngine;
namespace Rootborn.Game.Minigames
{
 [CreateAssetMenu(fileName="Minigame",menuName="Rootborn/Minigames/Game")]
 public sealed class MinigameDefinition : ScriptableObject
 {
  [SerializeField] private string _id;
  [SerializeField] private string _displayNameKey;
  [SerializeField] private ChallengeDefinitionBase[] _challenges=Array.Empty<ChallengeDefinitionBase>();
  [SerializeField] private MinigameBonusRuleBase[] _bonuses=Array.Empty<MinigameBonusRuleBase>();
  [SerializeField,Range(1,100)] private int _successScore=60;
  public string Id=>string.IsNullOrEmpty(_id)?name:_id;
  public string DisplayNameKey=>_displayNameKey;
  public int SuccessScore=>Mathf.Clamp(_successScore,1,100);
  public int ChallengeCount=>_challenges.Length;
  public bool IsValid { get { if(_challenges==null||_challenges.Length==0)return false;foreach(var c in _challenges)if(c==null||!c.IsValid)return false;return true; } }
  internal ChallengeSession Create(int index,StudentLifeProgress progress,int seed)
  {
   var tuning=new MinigameTuning();if(_bonuses!=null)foreach(var b in _bonuses)if(b!=null)b.Apply(progress,ref tuning);
   return _challenges[index].Create(tuning,seed);
  }
 }
}
