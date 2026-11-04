using System;
using UnityEngine;
namespace Rootborn.Game.Minigames
{
 [CreateAssetMenu(fileName="Choice",menuName="Rootborn/Minigames/Choice")]
 public sealed class ChoiceChallengeDefinition:ChallengeDefinitionBase
 {
  [SerializeField] private int[] _answers=Array.Empty<int>();
  [SerializeField] private string[] _prompts=Array.Empty<string>();
  [SerializeField] private string[] _symbols=Array.Empty<string>();
  public override bool IsValid {get{if(!base.IsValid||_answers==null||_answers.Length==0||_prompts==null||_prompts.Length!=_answers.Length||_symbols==null||_symbols.Length<2)return false;foreach(int a in _answers)if(a<0||a>=_symbols.Length)return false;return true;}}
  internal override ChallengeSession Create(MinigameTuning tuning,int seed){var s=new ChoiceSession(_answers,_prompts,_symbols);Bind(s,tuning);return s;}
  private sealed class ChoiceSession:ChallengeSession
  {
   private readonly int[] _answers;private readonly string[] _prompts,_symbols;private int _index,_correct;
   public ChoiceSession(int[] answers,string[] prompts,string[] symbols){_answers=(int[])answers.Clone();_prompts=(string[])prompts.Clone();_symbols=(string[])symbols.Clone();}
   public override MinigameMechanic Mechanic=>MinigameMechanic.Choice;
   public override string[] Symbols=>_symbols;
   public override string PromptKey=>_prompts[Mathf.Min(_index,_prompts.Length-1)];
   public override int ProgressIndex=>_index;
   public override int TotalSteps=>_answers.Length;
   public override bool IsHintChoice(int index)=>HintCount>0&&!IsComplete&&index==_answers[_index];
   public override bool Choose(int index){if(IsComplete||index<0||index>=_symbols.Length)return false;if(index==_answers[_index])_correct++;else Mistakes++;_index++;if(_index==_answers.Length)Complete(Percent(_correct,_answers.Length));return true;}
   protected override void FinishTimeout()=>Complete(Percent(_correct,_answers.Length));
  }
 }
}
