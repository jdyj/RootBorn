using System;
using Rootborn.Game.StudentLife;
using UnityEngine;
namespace Rootborn.Game.Minigames
{
 public sealed class MinigameSession
 {
  private readonly MinigameDefinition _definition;private readonly StudentLifeProgress _progress;private readonly int _seed;private int _scoreTotal;
  public MinigameSession(MinigameDefinition definition,StudentLifeProgress progress,int seed)
  {if(definition==null||!definition.IsValid)throw new ArgumentException("A valid authored minigame is required.");_definition=definition;_progress=progress;_seed=seed;Current=definition.Create(0,progress,seed);}
  public ChallengeSession Current {get;private set;}
  public int StageIndex {get;private set;}
  public int StageCount=>_definition.ChallengeCount;
  public bool IsComplete {get;private set;}
  public bool IsCancelled {get;private set;}
  public int Score=>IsCancelled?0:_scoreTotal/StageCount;
  public bool Succeeded=>IsComplete&&!IsCancelled&&Score>=_definition.SuccessScore;
  public bool AdvanceTime(float seconds)=>Input(()=>Current.AdvanceTime(seconds));
  public bool Choose(int index)=>Input(()=>Current.Choose(index));
  public bool Tap()=>Input(()=>Current.Tap());
  public bool Place(int piece,Vector2Int cell,int rotation)=>Input(()=>Current.Place(piece,cell,rotation));
  public bool Aim(Vector2 position)=>Input(()=>Current.Aim(position));
  public bool MoveLane(int lane)=>Input(()=>Current.MoveLane(lane));
  public bool Cancel(){if(IsComplete)return false;IsCancelled=true;IsComplete=true;return true;}
  private bool Input(Func<bool> action)
  {
   if(IsComplete)return false;bool accepted=action();
   if(Current.IsComplete){_scoreTotal+=Current.Score;StageIndex++;if(StageIndex>=StageCount)IsComplete=true;else Current=_definition.Create(StageIndex,_progress,_seed+StageIndex);}
   return accepted;
  }
 }
}
