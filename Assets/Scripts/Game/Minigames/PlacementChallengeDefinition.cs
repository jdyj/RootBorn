using System;
using System.Collections.Generic;
using UnityEngine;
namespace Rootborn.Game.Minigames
{
 [CreateAssetMenu(fileName="Placement",menuName="Rootborn/Minigames/Placement")]
 public sealed class PlacementChallengeDefinition:ChallengeDefinitionBase
 {
  [SerializeField] private Vector2Int _boardSize=new Vector2Int(6,4);
  [SerializeField] private Vector2Int[] _pieceSizes=Array.Empty<Vector2Int>();
  [SerializeField] private Vector2Int[] _blockedCells=Array.Empty<Vector2Int>();
  [SerializeField] private Vector2Int[] _requiredCells=Array.Empty<Vector2Int>();
  public override bool IsValid {get{if(!base.IsValid||_boardSize.x<1||_boardSize.y<1||_boardSize.x>16||_boardSize.y>16||_pieceSizes==null||_pieceSizes.Length==0)return false;foreach(var p in _pieceSizes)if(p.x<1||p.y<1)return false;return true;}}
  internal override ChallengeSession Create(MinigameTuning tuning,int seed){var s=new PlacementSession(_boardSize,_pieceSizes,_blockedCells,_requiredCells,tuning.Harmony);Bind(s,tuning);return s;}
  private sealed class PlacementSession:ChallengeSession
  {
   private readonly Vector2Int _size;private readonly Vector2Int[] _pieces,_blocked,_required;private readonly Dictionary<int,RectInt> _placed=new Dictionary<int,RectInt>();private readonly float _harmony;
   public PlacementSession(Vector2Int size,Vector2Int[] pieces,Vector2Int[] blocked,Vector2Int[] required,float harmony){_size=size;_pieces=(Vector2Int[])pieces.Clone();_blocked=blocked==null?Array.Empty<Vector2Int>():(Vector2Int[])blocked.Clone();_required=required==null?Array.Empty<Vector2Int>():(Vector2Int[])required.Clone();_harmony=harmony;}
   public override MinigameMechanic Mechanic=>MinigameMechanic.Placement;
   public override Vector2Int BoardSize=>_size;
   public override Vector2Int[] PieceSizes=>(Vector2Int[])_pieces.Clone();
   public override Vector2Int[] BlockedCells=>(Vector2Int[])_blocked.Clone();
   public override int ProgressIndex=>_placed.Count;
   public override int TotalSteps=>_pieces.Length;
   public override bool TryGetPieceRect(int index,out RectInt rect)=>_placed.TryGetValue(index,out rect);
   public override bool Place(int piece,Vector2Int cell,int rotation)
   {
    if(IsComplete||piece<0||piece>=_pieces.Length||rotation<0||rotation>3)return false;
    var size=_pieces[piece];if((rotation&1)!=0)size=new Vector2Int(size.y,size.x);var rect=new RectInt(cell,size);
    if(rect.xMin<0||rect.yMin<0||rect.xMax>_size.x||rect.yMax>_size.y)return false;
    foreach(var b in _blocked)if(rect.Contains(b))return false;
    foreach(var p in _placed)if(p.Key!=piece&&rect.Overlaps(p.Value))return false;
    _placed[piece]=rect;
    if(_placed.Count==_pieces.Length){foreach(var required in _required){bool covered=false;foreach(var p in _placed)covered|=p.Value.Contains(required);if(!covered)return true;}Complete(100);}
    return true;
   }
   protected override void FinishTimeout()=>Complete(Mathf.Min(99,Percent(_placed.Count,_pieces.Length)+Mathf.RoundToInt(_harmony)));
  }
 }
}
