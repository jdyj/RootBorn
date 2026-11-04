using System;
using UnityEngine;
namespace Rootborn.Game.Minigames
{
 [CreateAssetMenu(fileName="Sequence",menuName="Rootborn/Minigames/Sequence")]
 public sealed class SequenceChallengeDefinition:ChallengeDefinitionBase
 {
  [SerializeField] private int[] _answers=Array.Empty<int>();
  [SerializeField] private string[] _symbols=Array.Empty<string>();
  [SerializeField,Min(0)] private float _revealSeconds=3;
  public override bool IsValid {get{if(!base.IsValid||_answers==null||_answers.Length==0||_symbols==null||_symbols.Length<2||_revealSeconds<0||_revealSeconds>=_timeLimitSeconds)return false;foreach(int a in _answers)if(a<0||a>=_symbols.Length)return false;return true;}}
  internal override ChallengeSession Create(MinigameTuning tuning,int seed){var s=new SequenceSession(_answers,_symbols,_revealSeconds);Bind(s,tuning);return s;}
  private sealed class SequenceSession:ChallengeSession
  {
   private readonly int[] _answers;private readonly string[] _symbols;private readonly float _reveal;private int _index;
   public SequenceSession(int[] answers,string[] symbols,float reveal){_answers=(int[])answers.Clone();_symbols=(string[])symbols.Clone();_reveal=reveal;}
   public override MinigameMechanic Mechanic=>MinigameMechanic.Sequence;
   public override string[] Symbols=>_symbols;
   public override bool PreviewVisible=>Elapsed<_reveal;
   public override int[] PreviewSequence=>PreviewVisible?(int[])_answers.Clone():Array.Empty<int>();
   public override int ProgressIndex=>_index;
   public override int TotalSteps=>_answers.Length;
   public override bool IsHintChoice(int index)=>HintCount>0&&!PreviewVisible&&!IsComplete&&index==_answers[_index];
   public override bool Choose(int index){if(IsComplete||PreviewVisible||index<0||index>=_symbols.Length)return false;if(index!=_answers[_index])return Wrong();_index++;if(_index==_answers.Length)Complete(Percent(_index,_answers.Length+Mistakes));return true;}
  }
 }
}
