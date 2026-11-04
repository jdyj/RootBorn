using UnityEngine;
namespace Rootborn.Game.Minigames
{
 public enum MinigameMechanic { Sequence,Choice,Timing,Placement,TargetDodge }
 public struct MinigameTuning { public float TimingWindow,ExtraSeconds,Harmony,Steadiness; public int ExtraMistakes,HintCount; }
 public abstract class ChallengeDefinitionBase:ScriptableObject
 {
  [SerializeField] private string _instructionKey;
  [SerializeField] private Sprite[] _symbolSprites=System.Array.Empty<Sprite>();
  [SerializeField,Min(1)] protected float _timeLimitSeconds=30;
  [SerializeField,Min(0)] protected int _mistakeAllowance=3;
  public string InstructionKey=>_instructionKey;
  public virtual bool IsValid=>_timeLimitSeconds>0&&!float.IsNaN(_timeLimitSeconds)&&!float.IsInfinity(_timeLimitSeconds)&&_mistakeAllowance>=0;
  internal abstract ChallengeSession Create(MinigameTuning tuning,int seed);
  protected void Bind(ChallengeSession session,MinigameTuning tuning){session.Initialize(_instructionKey,_timeLimitSeconds+tuning.ExtraSeconds,_mistakeAllowance+tuning.ExtraMistakes,tuning.HintCount,_symbolSprites);}
 }
 public abstract class ChallengeSession
 {
  public abstract MinigameMechanic Mechanic {get;}
  public Sprite[] SymbolSprites {get;private set;}=System.Array.Empty<Sprite>();
  public string InstructionKey {get;private set;}
  public float Elapsed {get;private set;}
  public float Duration {get;private set;}
  public float RemainingSeconds=>Mathf.Max(0,Duration-Elapsed);
  public int Mistakes {get;protected set;}
  public int MistakeAllowance {get;private set;}
  public int HintCount {get;private set;}
  public bool IsComplete {get;private set;}
  public int Score {get;private set;}
  public virtual float TimingWindow=>0;
  public virtual float[] BeatTimes=>System.Array.Empty<float>();
  public virtual string[] Symbols=>System.Array.Empty<string>();
  public virtual string PromptKey=>InstructionKey;
  public virtual int ProgressIndex=>0;
  public virtual int TotalSteps=>1;
  public virtual bool PreviewVisible=>false;
  public virtual int[] PreviewSequence=>System.Array.Empty<int>();
  public virtual bool IsHintChoice(int index)=>false;
  public virtual Vector2Int BoardSize=>Vector2Int.zero;
  public virtual Vector2Int[] PieceSizes=>System.Array.Empty<Vector2Int>();
  public virtual Vector2Int[] BlockedCells=>System.Array.Empty<Vector2Int>();
  public virtual bool TryGetPieceRect(int index,out RectInt rect){rect=default;return false;}
  public virtual Vector2 TargetPosition=>Vector2.zero;
  public virtual bool HasTarget=>false;
  public virtual float TargetRadius=>0;
  public virtual int Lane=>0;
  public virtual int LaneCount=>0;
  public virtual int NextObstacleLane=>-1;
  public virtual float NextObstacleFraction=>0;
  internal void Initialize(string instruction,float duration,int allowance,int hints,Sprite[] symbolSprites=null){SymbolSprites=symbolSprites!=null?(Sprite[])symbolSprites.Clone():System.Array.Empty<Sprite>();InstructionKey=instruction;Duration=Mathf.Max(.01f,duration);MistakeAllowance=Mathf.Max(0,allowance);HintCount=Mathf.Max(0,hints);}
  public bool AdvanceTime(float seconds)
  {
   if(IsComplete||seconds<0||float.IsNaN(seconds)||float.IsInfinity(seconds))return false;
   float before=Elapsed;Elapsed=Mathf.Min(Duration,Elapsed+seconds);OnAdvanced(before,Elapsed);
   if(!IsComplete&&Elapsed>=Duration)FinishTimeout();return true;
  }
  protected virtual void OnAdvanced(float before,float after){}
  protected virtual void FinishTimeout()=>Complete(0);
  protected void Complete(int score){if(IsComplete)return;Score=Mathf.Clamp(score,0,100);IsComplete=true;}
  protected bool Wrong(){Mistakes++;if(Mistakes>MistakeAllowance)Complete(0);return true;}
  protected static int Percent(int correct,int total)=>total<=0?0:Mathf.Clamp(Mathf.RoundToInt(100f*correct/total),0,100);
  public virtual bool Choose(int index)=>false;
  public virtual bool Tap()=>false;
  public virtual bool Place(int piece,Vector2Int cell,int rotation)=>false;
  public virtual bool Aim(Vector2 position)=>false;
  public virtual bool MoveLane(int lane)=>false;
 }
}
