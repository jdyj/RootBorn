using System;
using UnityEngine;
namespace Rootborn.Game.Minigames
{
 [CreateAssetMenu(fileName="Timing",menuName="Rootborn/Minigames/Timing")]
 public sealed class TimingChallengeDefinition:ChallengeDefinitionBase
 {
  [SerializeField] private float[] _beatTimes=Array.Empty<float>();
  [SerializeField,Min(.01f)] private float _windowSeconds=.15f;
  public override bool IsValid {get{if(!base.IsValid||_beatTimes==null||_beatTimes.Length==0||_windowSeconds<=0)return false;float previous=0;foreach(float b in _beatTimes){if(b<=previous||float.IsNaN(b)||float.IsInfinity(b))return false;previous=b;}return previous+_windowSeconds<=_timeLimitSeconds;}}
  internal override ChallengeSession Create(MinigameTuning tuning,int seed){var s=new TimingSession(_beatTimes,_windowSeconds+tuning.TimingWindow);Bind(s,tuning);return s;}
  private sealed class TimingSession:ChallengeSession
  {
   private readonly float[] _beats;private readonly bool[] _hit;private readonly float _window;private int _correct;
   public TimingSession(float[] beats,float window){_beats=(float[])beats.Clone();_hit=new bool[beats.Length];_window=window;}
   public override MinigameMechanic Mechanic=>MinigameMechanic.Timing;
   public override float TimingWindow=>_window;
   public override int ProgressIndex=>_correct;
   public override int TotalSteps=>_beats.Length;
   public override int[] PreviewSequence=>Array.Empty<int>();
   public override float[] BeatTimes=>(float[])_beats.Clone();
   public override bool Tap(){if(IsComplete)return false;for(int i=0;i<_beats.Length;i++)if(!_hit[i]&&Mathf.Abs(Elapsed-_beats[i])<=_window){_hit[i]=true;_correct++;return true;}Mistakes++;return false;}
   protected override void OnAdvanced(float before,float after){if(after>=_beats[_beats.Length-1]+_window)Complete(Percent(_correct,_beats.Length+Mistakes));}
   protected override void FinishTimeout()=>Complete(Percent(_correct,_beats.Length+Mistakes));
  }
 }
}
