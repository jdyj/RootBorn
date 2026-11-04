using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.Game.Minigames;
using Rootborn.UI.Modern;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Rootborn.UI.Minigames
{
    [DisallowMultipleComponent]
    public sealed class MinigamePanel : MonoBehaviour
    {
        private RectTransform _root, _body, _cursor, _target, _obstacle;
        private Text _title, _instruction, _status, _feedback;
        private Action<MinigameSession> _completion;
        private Func<bool> _retrySettlement;
        private Button _retrySave;
        private ChallengeSession _displayed;
        private readonly List<Button> _choices = new List<Button>();
        private readonly List<Text> _choiceLabels = new List<Text>();
        private readonly List<Image> _previewSymbols = new List<Image>();
        private readonly List<Image> _cells = new List<Image>();
        private readonly List<Image> _pieceVisuals = new List<Image>();
        private readonly List<Image> _lanes = new List<Image>();
        private readonly List<RectTransform> _stageObjects = new List<RectTransform>();
        private string[] _symbols=Array.Empty<string>();
        private Vector2Int[] _pieceSizes=Array.Empty<Vector2Int>(), _blockedCells=Array.Empty<Vector2Int>();
        private Vector2Int _boardSize;
        private int _selectedPiece, _rotation, _shownSeconds=-1, _shownProgress=-1, _shownMistakes=-1;
        private bool _delivered, _renderedPreview;
        private float _nextRender, _boardCellSize;
        public MinigameSession Session { get; private set; }
        public bool IsOpen => _root!=null && _root.gameObject.activeSelf;

        public static MinigamePanel EnsureInScene(Canvas canvas)
        {
            if(canvas==null) return null;
            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
            var existing=canvas.GetComponentInChildren<MinigamePanel>(true);
            if(existing!=null)return existing;
            var go=new GameObject("MinigamePanel",typeof(RectTransform));go.transform.SetParent(canvas.transform,false);
            return go.AddComponent<MinigamePanel>();
        }
        private void Awake(){Build();_root.gameObject.SetActive(false);}
        public bool Show(MinigameSession session,Action<MinigameSession> completion)
        {
            Build();
            if(IsOpen||session==null||session.IsComplete||completion==null)return false;
            Session=session;_completion=completion;_delivered=false;_displayed=null;
            _retrySettlement=null;_retrySave.gameObject.SetActive(false);
            _root.gameObject.SetActive(true);transform.SetAsLastSibling();UiGameplayInputLock.Acquire(this);
            RefreshView();return true;
        }
        public void Close()
        {
            if(Session!=null&&!Session.IsComplete)Session.Cancel();
            Deliver();if(_retrySettlement!=null)return;
            if(_root!=null)_root.gameObject.SetActive(false);UiGameplayInputLock.Release(this);
        }
        public void SetSettlementFailure(string message,Func<bool> retry)
        {
            if(Session==null||!Session.IsComplete||retry==null)return;
            _retrySettlement=retry;_feedback.text=message;_retrySave.gameObject.SetActive(true);
        }
        private void RetrySettlement()
        {
            if(!IsOpen||_retrySettlement==null)return;
            if(!_retrySettlement())return;
            _retrySettlement=null;_retrySave.gameObject.SetActive(false);_feedback.text=GameText.Get("minigame.saved");
        }
        private void OnDisable()=>Close();
        private void OnDestroy()=>UiGameplayInputLock.Release(this);
        private void Deliver()
        {
            if(_delivered||Session==null||!Session.IsComplete)return;
            _delivered=true;var handler=_completion;_completion=null;handler?.Invoke(Session);
        }
        private void Update()
        {
            if(!IsOpen||Session==null)return;
            var keyboard=Keyboard.current;
            if(keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame){Close();return;}
            if(Session.IsComplete)return;
            // Time is advanced once per frame; visual strings are refreshed only when values change.
            Session.AdvanceTime(Time.unscaledDeltaTime);
            if(!Session.IsComplete&&keyboard!=null)
            {
                var current=Session.Current;
                if(current.Mechanic==MinigameMechanic.Timing&&keyboard.spaceKey.wasPressedThisFrame)Session.Tap();
                if(current.Mechanic==MinigameMechanic.Placement&&keyboard.rKey.wasPressedThisFrame)Rotate();
                if(current.Mechanic==MinigameMechanic.TargetDodge)
                {
                    if(keyboard.leftArrowKey.wasPressedThisFrame||keyboard.aKey.wasPressedThisFrame)Session.MoveLane(Mathf.Max(0,current.Lane-1));
                    if(keyboard.rightArrowKey.wasPressedThisFrame||keyboard.dKey.wasPressedThisFrame)Session.MoveLane(Mathf.Min(current.LaneCount-1,current.Lane+1));
                }
            }
            Animate();
            if(Time.unscaledTime>=_nextRender||Session.IsComplete||_displayed!=Session.Current){_nextRender=Time.unscaledTime+.1f;RefreshView();}
        }
        public void RefreshView()
        {
            if(Session==null||!IsOpen)return;
            if(_displayed!=Session.Current)BuildStage(Session.Current);
            var c=Session.Current;
            if(Session.IsComplete)
            {
                if(c.Mechanic==MinigameMechanic.Placement)RenderBoard();
                foreach(var b in _choices)b.interactable=false;
                _status.text=GameText.Format(Session.IsCancelled?"minigame.cancelled":"minigame.result",Session.Score);
                _instruction.text=GameText.Get(Session.Succeeded?"minigame.success":"minigame.retry");
                Deliver();return;
            }
            int seconds=Mathf.CeilToInt(c.RemainingSeconds);
            if(seconds!=_shownSeconds||c.ProgressIndex!=_shownProgress||c.Mistakes!=_shownMistakes)
            {
                _shownSeconds=seconds;_shownProgress=c.ProgressIndex;_shownMistakes=c.Mistakes;
                _status.text=GameText.Format("minigame.progress",Session.StageIndex+1,Session.StageCount,c.ProgressIndex,c.TotalSteps,seconds,c.Mistakes);
                _instruction.text=GameText.Get(c.PromptKey);
            }
            if(c.Mechanic==MinigameMechanic.Sequence||c.Mechanic==MinigameMechanic.Choice)
            {
                foreach(var icon in _previewSymbols)if(icon.gameObject.activeSelf!=c.PreviewVisible)icon.gameObject.SetActive(c.PreviewVisible);
                for(int i=0;i<_choices.Count;i++)
                {
                    _choices[i].interactable=!c.PreviewVisible;
                    _choiceLabels[i].text=(c.IsHintChoice(i)?"• ":string.Empty)+GameText.Get(_symbols[i]);
                }
                if(c.PreviewVisible&&!_renderedPreview)
                {
                    var sequence=c.PreviewSequence;var labels=new string[sequence.Length];
                    for(int i=0;i<sequence.Length;i++)labels[i]=GameText.Get(_symbols[sequence[i]]);
                    if(!_renderedPreview){_feedback.text=GameText.Get("minigame.memorize")+"\n"+string.Join(" → ",labels);_renderedPreview=true;}
                }
                else if(!c.PreviewVisible&&_renderedPreview){_feedback.text=GameText.Get("minigame.repeat");_renderedPreview=false;}
            }
            if(c.Mechanic==MinigameMechanic.Placement)RenderBoard();
            if(c.Mechanic==MinigameMechanic.TargetDodge)RenderLanes();
            Animate();
        }
        private void BuildStage(ChallengeSession c)
        {
            for(int i=0;i<_stageObjects.Count;i++){var go=_stageObjects[i].gameObject;go.SetActive(false);if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
            _stageObjects.Clear();_previewSymbols.Clear();_choices.Clear();_choiceLabels.Clear();_cells.Clear();_lanes.Clear();_pieceVisuals.Clear();
            _displayed=c;_cursor=null;_target=null;_obstacle=null;_selectedPiece=0;_rotation=0;
            _shownSeconds=_shownProgress=_shownMistakes=-1;_renderedPreview=false;_feedback.text=string.Empty;
            _symbols=c.Symbols;_title.text=GameText.Get("minigame.title."+c.Mechanic.ToString().ToLowerInvariant());
            if(c.Mechanic==MinigameMechanic.Sequence||c.Mechanic==MinigameMechanic.Choice)
            {
                int columns=Mathf.Min(3,_symbols.Length);
                for(int i=0;i<_symbols.Length;i++)
                {
                    int index=i;var b=StageButton("Choice_"+i,new Vector2((i%columns-(columns-1)*.5f)*210,40-i/columns*86),new Vector2(190,68),GameText.Get(_symbols[i]),()=>Choose(index));
                    var label=b.GetComponentInChildren<Text>();_choices.Add(b);_choiceLabels.Add(label);
                    ModernUiPanelBuilder.ApplyCommonPanel48(b.gameObject);
                    if(i<c.SymbolSprites.Length && c.SymbolSprites[i]!=null)
                    {
                        var icon=Image(b.transform,"SymbolIcon",new Vector2(-62,0),new Vector2(48,48),Color.white);icon.sprite=c.SymbolSprites[i];icon.preserveAspect=true;
                        label.rectTransform.anchoredPosition=new Vector2(27,0);label.rectTransform.sizeDelta=new Vector2(126,60);
                    }
                }
                if(c.Mechanic==MinigameMechanic.Sequence)BuildSequencePreview(c);
            }
            else if(c.Mechanic==MinigameMechanic.Timing)
            {
                StageImage("TimingTrack",new Vector2(0,36),new Vector2(620,24),new Color(.5f,.38f,.24f));
                var beats=c.BeatTimes;
                for(int i=0;i<beats.Length;i++)StageImage("Beat_"+i,new Vector2((beats[i]/c.Duration-.5f)*620,36),new Vector2(Mathf.Max(8,2*c.TimingWindow/c.Duration*620),46),new Color(.35f,.64f,.5f));
                _cursor=StageImage("TimingCursor",new Vector2(-310,36),new Vector2(6,66),new Color(.97f,.82f,.25f)).rectTransform;
                StageButton("Tap",new Vector2(0,-80),new Vector2(270,64),GameText.Get("minigame.tap"),()=>{if(Active()){Session.Tap();RefreshView();}});
            }
            else if(c.Mechanic==MinigameMechanic.Placement)BuildBoard(c);
            else if(c.Mechanic==MinigameMechanic.TargetDodge)BuildTargets(c);
        }
        private void BuildSequencePreview(ChallengeSession c)
        {
            var sequence=c.PreviewSequence;
            if(sequence.Length==0 || sequence.Length>10 || _symbols.Length>6)return;
            foreach(var index in sequence)if(index<0 || index>=c.SymbolSprites.Length || c.SymbolSprites[index]==null)return;
            float spacing=Mathf.Min(64,650f/sequence.Length);
            for(int i=0;i<sequence.Length;i++)
            {
                var icon=StageImage("PreviewSymbol_"+i,new Vector2((i-(sequence.Length-1)*.5f)*spacing,-126),new Vector2(40,40),Color.white);
                icon.sprite=c.SymbolSprites[sequence[i]];icon.preserveAspect=true;_previewSymbols.Add(icon);
            }
        }
        private void BuildBoard(ChallengeSession c)
        {
            _boardSize=c.BoardSize;_pieceSizes=c.PieceSizes;_blockedCells=c.BlockedCells;
            _boardCellSize=Mathf.Min(50,Mathf.Min(470f/_boardSize.x,260f/_boardSize.y));
            float cell=_boardCellSize;
            for(int y=0;y<_boardSize.y;y++)for(int x=0;x<_boardSize.x;x++)
            {
                var point=new Vector2Int(x,y);
                var b=StageButton("Cell_"+x+"_"+y,new Vector2(-95+(x-(_boardSize.x-1)*.5f)*cell,5+(y-(_boardSize.y-1)*.5f)*cell),new Vector2(cell-3,cell-3),string.Empty,()=>Place(point));
                _cells.Add(b.GetComponent<Image>());
            }
            var sprites=c.SymbolSprites;
            for(int i=0;i<_pieceSizes.Length;i++)
            {
                int index=i;
                var button=StageButton("Piece_"+i,new Vector2(285,100-i*48),new Vector2(150,42),GameText.Format("minigame.piece",i+1,_pieceSizes[i].x,_pieceSizes[i].y),()=>{if(Active()){_selectedPiece=index;RenderBoard();}});
                ModernUiPanelBuilder.ApplyCommonPanel48(button.gameObject);
                Image placed=null;
                if(i<sprites.Length&&sprites[i]!=null)
                {
                    var icon=Image(button.transform,"PieceIcon_"+i,new Vector2(-54,0),new Vector2(32,32),Color.white);icon.sprite=sprites[i];icon.preserveAspect=true;
                    var label=button.GetComponentInChildren<Text>();label.rectTransform.anchoredPosition=new Vector2(18,0);label.rectTransform.sizeDelta=new Vector2(106,38);
                    placed=StageImage("PlacedPiece_"+i,Vector2.zero,Vector2.one,Color.white);placed.sprite=sprites[i];placed.preserveAspect=true;placed.gameObject.SetActive(false);
                }
                _pieceVisuals.Add(placed);
            }
            StageButton("Rotate",new Vector2(270,-128),new Vector2(180,48),GameText.Get("minigame.rotate"),Rotate);
        }
        private void RenderBoard()
        {
            var c=Session.Current;
            for(int y=0;y<_boardSize.y;y++)for(int x=0;x<_boardSize.x;x++)
            {
                var point=new Vector2Int(x,y);Color color=new Color(.84f,.81f,.66f);
                for(int j=0;j<_blockedCells.Length;j++)if(_blockedCells[j]==point)color=new Color(.39f,.34f,.29f);
                for(int j=0;j<_pieceSizes.Length;j++)if(c.TryGetPieceRect(j,out var rect)&&rect.Contains(point))color=Color.HSVToRGB((j*.19f+.05f)%1,.42f,.77f);
                _cells[y*_boardSize.x+x].color=color;
            }
            for(int i=0;i<_pieceVisuals.Count;i++)
            {
                var visual=_pieceVisuals[i];if(visual==null)continue;
                bool placed=c.TryGetPieceRect(i,out var rect);
                if(visual.gameObject.activeSelf!=placed)visual.gameObject.SetActive(placed);
                if(!placed)continue;
                var center=new Vector2(rect.x+(rect.width-1)*.5f,rect.y+(rect.height-1)*.5f);
                var position=new Vector2(-95+(center.x-(_boardSize.x-1)*.5f)*_boardCellSize,5+(center.y-(_boardSize.y-1)*.5f)*_boardCellSize);
                var size=new Vector2(rect.width*_boardCellSize-8,rect.height*_boardCellSize-8);
                if(visual.rectTransform.anchoredPosition!=position)visual.rectTransform.anchoredPosition=position;
                if(visual.rectTransform.sizeDelta!=size)visual.rectTransform.sizeDelta=size;
            }
            _feedback.text=GameText.Format("minigame.placement-selection",_selectedPiece+1,_rotation*90);
        }
        private void Place(Vector2Int point)
        {
            if(!Active())return;
            bool placed=Session.Place(_selectedPiece,point,_rotation);
            if(placed&&!Session.IsComplete){for(int i=0;i<_pieceSizes.Length;i++)if(!Session.Current.TryGetPieceRect(i,out _)){_selectedPiece=i;_rotation=0;break;}}
            RefreshView();if(!placed)_feedback.text=GameText.Get("minigame.placement-invalid");
        }
        private void Rotate(){if(!Active())return;_rotation=(_rotation+1)%4;RenderBoard();}
        private void BuildTargets(ChallengeSession c)
        {
            var surface=StageImage("AimSurface",new Vector2(-120,10),new Vector2(400,235),new Color(.78f,.82f,.68f));surface.raycastTarget=true;
            surface.gameObject.AddComponent<MinigamePointerSurface>().Clicked=point=>{if(Active()){Session.Aim(point);RefreshView();}};
            var targetImage=Image(surface.rectTransform,"Target",Vector2.zero,new Vector2(c.TargetRadius*800,c.TargetRadius*470),new Color(.83f,.38f,.24f));_target=targetImage.rectTransform;
            if(c.SymbolSprites.Length>0&&c.SymbolSprites[0]!=null){targetImage.sprite=c.SymbolSprites[0];targetImage.color=Color.white;targetImage.preserveAspect=true;}
            else Text(_target,"TargetMark","◎",Vector2.zero,_target.sizeDelta,30);
            for(int i=0;i<c.LaneCount;i++){int lane=i;var b=StageButton("Lane_"+i,new Vector2(160+i*65,-100),new Vector2(54,54),(i+1).ToString(),()=>{if(Active()){Session.MoveLane(lane);RenderLanes();}});_lanes.Add(b.GetComponent<Image>());}
            var obstacleImage=StageImage("Obstacle",new Vector2(160,100),new Vector2(45,26),new Color(.7f,.4f,.26f));_obstacle=obstacleImage.rectTransform;
            if(c.SymbolSprites.Length>1&&c.SymbolSprites[1]!=null){obstacleImage.sprite=c.SymbolSprites[1];obstacleImage.color=Color.white;obstacleImage.preserveAspect=true;}
            _feedback.text=GameText.Get("minigame.aim-dodge");
        }
        private void RenderLanes(){for(int i=0;i<_lanes.Count;i++)_lanes[i].color=i==Session.Current.Lane?new Color(.35f,.64f,.5f):new Color(.78f,.68f,.49f);}
        private void Animate()
        {
            if(Session==null||Session.IsComplete||_displayed!=Session.Current)return;
            var c=Session.Current;
            if(_cursor!=null)_cursor.anchoredPosition=new Vector2((c.Elapsed/c.Duration-.5f)*620,36);
            if(_target!=null){_target.gameObject.SetActive(c.HasTarget);_target.anchoredPosition=new Vector2((c.TargetPosition.x-.5f)*400,(c.TargetPosition.y-.5f)*235);}
            if(_obstacle!=null){_obstacle.gameObject.SetActive(c.NextObstacleLane>=0);_obstacle.anchoredPosition=new Vector2(160+c.NextObstacleLane*65,110-c.NextObstacleFraction*170);}
        }
        private bool Active()=>IsOpen&&Session!=null&&!Session.IsComplete;
        private void Choose(int index){if(!Active())return;Session.Choose(index);RefreshView();}
        private void Build()
        {
            if(_root!=null)return;
            _root=Rect(transform,"MinigameRoot",Vector2.zero,new Vector2(900,620));
            var tile=_root.gameObject.AddComponent<ModernUiTileImage>();tile.SetRecipe(ModernUiRecipes.CommonPanel);tile.Rebuild();
            _title=Text(_root,"Title",string.Empty,new Vector2(0,250),new Vector2(720,50),30);
            _instruction=Text(_root,"Instruction",string.Empty,new Vector2(0,182),new Vector2(780,76),21);
            _status=Text(_root,"Status",string.Empty,new Vector2(0,-235),new Vector2(800,32),19);
            _feedback=Text(_root,"Feedback",string.Empty,new Vector2(0,-192),new Vector2(800,72),20);
            _body=Rect(_root,"Challenge",Vector2.zero,new Vector2(800,290));
            Button(_root,"Close",new Vector2(390,250),new Vector2(52,48),GameText.Get("ui.close-symbol"),Close);
            _retrySave=Button(_root,"RetrySave",new Vector2(0,-278),new Vector2(220,40),GameText.Get("minigame.save-again"),RetrySettlement);
            _retrySave.gameObject.SetActive(false);
            _root.gameObject.SetActive(false);
        }
        private Button StageButton(string name,Vector2 position,Vector2 size,string label,Action click){var b=Button(_body,name,position,size,label,click);_stageObjects.Add((RectTransform)b.transform);return b;}
        private Image StageImage(string name,Vector2 position,Vector2 size,Color color){var image=Image(_body,name,position,size,color);_stageObjects.Add(image.rectTransform);return image;}
        private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=position;rt.sizeDelta=size;return rt;}
        private static Text Text(Transform parent,string name,string value,Vector2 position,Vector2 size,int fontSize){var rt=Rect(parent,name,position,size);var text=rt.gameObject.AddComponent<Text>();text.font=LifeUiFont.Shared;text.fontSize=fontSize;text.text=value;text.color=new Color(.25f,.18f,.12f);text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;}
        private static Image Image(Transform parent,string name,Vector2 position,Vector2 size,Color color){var rt=Rect(parent,name,position,size);var image=rt.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
        private static Button Button(Transform parent,string name,Vector2 position,Vector2 size,string label,Action click){var image=Image(parent,name,position,size,new Color(.78f,.68f,.49f));image.raycastTarget=true;var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;Text(image.transform,"Label",label,Vector2.zero,size-new Vector2(8,4),19);button.onClick.AddListener(()=>click());return button;}
    }
}
