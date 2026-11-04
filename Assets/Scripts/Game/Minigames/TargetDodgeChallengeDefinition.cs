using System;
using UnityEngine;
namespace Rootborn.Game.Minigames
{
 [CreateAssetMenu(fileName="TargetDodge",menuName="Rootborn/Minigames/Target and Dodge")]
 public sealed class TargetDodgeChallengeDefinition:ChallengeDefinitionBase
 {
  [SerializeField] private Vector2[] _targets=Array.Empty<Vector2>();
  [SerializeField] private int[] _obstacleLanes=Array.Empty<int>();
  [SerializeField,Min(.1f)] private float _obstacleIntervalSeconds=2;
  [SerializeField,Range(.01f,.3f)] private float _targetRadius=.08f;
  [SerializeField,Range(0,.2f)] private float _targetWobble=.03f;
  [SerializeField,Range(2,5)] private int _laneCount=3;
  public override bool IsValid {get{if(!base.IsValid||_targets==null||_obstacleLanes==null||_targets.Length+_obstacleLanes.Length==0||_laneCount<2||_targetRadius<=0||_obstacleIntervalSeconds<=0||_obstacleLanes.Length*_obstacleIntervalSeconds>_timeLimitSeconds)return false;foreach(int lane in _obstacleLanes)if(lane<0||lane>=_laneCount)return false;foreach(var p in _targets)if(p.x<0||p.x>1||p.y<0||p.y>1)return false;return true;}}
  internal override ChallengeSession Create(MinigameTuning tuning,int seed){var s=new TargetSession(_targets,_obstacleLanes,_obstacleIntervalSeconds,_targetRadius,_targetWobble/(1+tuning.Steadiness),_laneCount,seed);Bind(s,tuning);return s;}
  private sealed class TargetSession:ChallengeSession
  {
   private readonly Vector2[] _targets;private readonly int[] _obstacles;private readonly float _interval,_radius,_wobble,_phase;private readonly int _laneCount;private int _targetIndex,_obstacleIndex,_avoided,_lane;
   public TargetSession(Vector2[] targets,int[] obstacles,float interval,float radius,float wobble,int laneCount,int seed){_targets=(Vector2[])targets.Clone();_obstacles=(int[])obstacles.Clone();_interval=interval;_radius=radius;_wobble=wobble;_laneCount=laneCount;_phase=(seed&255)/255f;}
   public override MinigameMechanic Mechanic=>MinigameMechanic.TargetDodge;
   public override bool HasTarget=>_targetIndex<_targets.Length;
   public override Vector2 TargetPosition=>HasTarget?_targets[_targetIndex]+new Vector2(Mathf.Sin(Elapsed*2+_phase)*_wobble,0):Vector2.zero;
   public override float TargetRadius=>_radius;
   public override int Lane=>_lane;
   public override int LaneCount=>_laneCount;
   public override int NextObstacleLane=>_obstacleIndex<_obstacles.Length?_obstacles[_obstacleIndex]:-1;
   public override float NextObstacleFraction=>NextObstacleLane<0?1:Mathf.Clamp01((Elapsed-_obstacleIndex*_interval)/_interval);
   public override int ProgressIndex=>_targetIndex+_obstacleIndex;
   public override int TotalSteps=>_targets.Length+_obstacles.Length;
   public override bool Aim(Vector2 position){if(IsComplete||!HasTarget||float.IsNaN(position.x)||float.IsNaN(position.y))return false;if(Vector2.Distance(position,TargetPosition)>_radius){Wrong();return false;}_targetIndex++;CheckDone();return true;}
   public override bool MoveLane(int lane){if(IsComplete||lane<0||lane>=_laneCount)return false;_lane=lane;return true;}
   protected override void OnAdvanced(float before,float after){while(!IsComplete&&_obstacleIndex<_obstacles.Length&&after>=(_obstacleIndex+1)*_interval){if(_lane==_obstacles[_obstacleIndex])Wrong();else _avoided++;_obstacleIndex++;}CheckDone();}
   private void CheckDone(){if(!HasTarget&&_obstacleIndex==_obstacles.Length)FinishTimeout();}
   protected override void FinishTimeout()=>Complete(Percent(_targetIndex+_avoided,TotalSteps+Mistakes));
  }
 }
}
