using Rootborn.Game.Common;
using System.Collections.Generic;
using Rootborn.Game.Player;
using Rootborn.Game.Localization;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class CareerInterestPanel : MonoBehaviour
    {

        private GameObject _root;
        private Text _title;
        private Text _details;
        private readonly List<Button> _interestButtons = new List<Button>();
        private RectTransform _cardsContent;
        private RectTransform _detailsContent;
        private ScrollRect _detailsScroll;
        private readonly List<Text> _interestButtonLabels = new List<Text>();
        private CareerInterestDefinition[] _interests = System.Array.Empty<CareerInterestDefinition>();
        private CareerInterestProgress _interestProgress;
        private CareerInterestProgressComponent _interestComponent;
        private CareerCandidateProgress _candidateProgress;
        private StudentLifeProgress _studentProgress;
        private int _day;

        public bool IsOpen => _root != null && _root.activeSelf;
        public event System.Action SelectionChanged;
        public string DetailsTextForTests => _details != null ? _details.text : string.Empty;

        public static CareerInterestPanel EnsureInScene(Canvas canvas)
        {
            var existing = Object.FindFirstObjectByType<CareerInterestPanel>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("CareerInterestPanel");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<CareerInterestPanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
            Hide();
        }

        public void Show(CareerInterestDefinition[] interests, CareerInterestProgress interestProgress, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            _interestComponent = null;
            ShowInternal(interests, interestProgress, candidateProgress, studentProgress, day);
        }

        public void Show(CareerInterestDefinition[] interests, CareerInterestProgressComponent interestComponent, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            _interestComponent = interestComponent;
            ShowInternal(interests, interestComponent != null ? interestComponent.EnsureProgress() : null, candidateProgress, studentProgress, day);
        }

        public void Hide()
        {
            UiGameplayInputLock.Release(this);
            if (_root != null) _root.SetActive(false);
        }

        public Button GetInterestButtonForTests(int index) => index >= 0 && index < _interestButtons.Count ? _interestButtons[index] : null;

        private void ShowInternal(CareerInterestDefinition[] interests, CareerInterestProgress interestProgress, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            BuildIfNeeded();
            _interests = interests ?? System.Array.Empty<CareerInterestDefinition>();
            _interestProgress = interestProgress;
            _candidateProgress = candidateProgress;
            _studentProgress = studentProgress;
            _day = Mathf.Max(0, day);
            _title.text = Ui("ui.career-interest.title", "발견한 관심사", "Career Interests");
            BindButtons();
            EnsureCurrentInterestLinkedQuests();
            ShowDetails(_interests.Length > 0 ? 0 : -1);
            _root.SetActive(true);
            transform.SetAsLastSibling();
            UiGameplayInputLock.Acquire(this);
        }

        private void OnDisable() => UiGameplayInputLock.Release(this);
        private void OnDestroy() => UiGameplayInputLock.Release(this);

        private void BuildIfNeeded()
        {
            if(_root!=null)return;
            _root=new GameObject("CareerInterestRoot",typeof(RectTransform),typeof(ModernUiTileImage));_root.transform.SetParent(transform,false);
            var rt=(RectTransform)_root.transform;rt.anchorMin=new Vector2(.5f,.5f);rt.anchorMax=rt.anchorMin;rt.pivot=rt.anchorMin;rt.anchoredPosition=Vector2.zero;rt.sizeDelta=new Vector2(1440f,840f);
            var tile=_root.GetComponent<ModernUiTileImage>();tile.SetRecipe(ModernUiRecipes.CommonPanel);tile.Rebuild();
            _title=MakeText(rt,"Title",Ui("ui.career-interest.title","발견한 관심사","Career Interests"),new Vector2(-350f,350f),new Vector2(640f,44f),32,TextAnchor.MiddleLeft);
            var cards=MakeContainer(rt,"InterestCardsViewport",new Vector2(0f,105f),new Vector2(1344f,314f));cards.gameObject.AddComponent<RectMask2D>();var hit=cards.gameObject.AddComponent<Image>();hit.color=Color.clear;
            var scroll=cards.gameObject.AddComponent<ScrollRect>();scroll.horizontal=true;scroll.vertical=false;scroll.inertia=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            var content=new GameObject("InterestCardsContent",typeof(RectTransform));content.transform.SetParent(cards,false);_cardsContent=(RectTransform)content.transform;
            _cardsContent.anchorMin=new Vector2(0f,1f);_cardsContent.anchorMax=_cardsContent.anchorMin;_cardsContent.pivot=new Vector2(0f,1f);_cardsContent.sizeDelta=new Vector2(1344f,300f);scroll.viewport=cards;scroll.content=_cardsContent;
            var detailViewport=MakeContainer(rt,"InterestDetailViewport",new Vector2(0f,-220f),new Vector2(1344f,266f));detailViewport.gameObject.AddComponent<RectMask2D>();detailViewport.gameObject.AddComponent<Image>().color=Color.clear;
            _detailsScroll=detailViewport.gameObject.AddComponent<ScrollRect>();_detailsScroll.horizontal=false;_detailsScroll.vertical=true;_detailsScroll.inertia=false;_detailsScroll.movementType=ScrollRect.MovementType.Clamped;
            var detailsGo=new GameObject("InterestDetailContent",typeof(RectTransform));detailsGo.transform.SetParent(detailViewport,false);_detailsContent=(RectTransform)detailsGo.transform;
            _detailsContent.anchorMin=new Vector2(0f,1f);_detailsContent.anchorMax=Vector2.one;_detailsContent.pivot=new Vector2(.5f,1f);_detailsContent.sizeDelta=new Vector2(0f,266f);
            _detailsScroll.viewport=detailViewport;_detailsScroll.content=_detailsContent;
            _details=MakeText(_detailsContent,"Details",string.Empty,Vector2.zero,new Vector2(1304f,246f),20,TextAnchor.UpperLeft);
            var detailRect=_details.rectTransform;detailRect.anchorMin=new Vector2(0f,1f);detailRect.anchorMax=detailRect.anchorMin;detailRect.pivot=new Vector2(0f,1f);detailRect.anchoredPosition=new Vector2(20f,-10f);
            var close=MakeButton(rt,"CloseButton",new Vector2(600f,350f),new Vector2(140f,58f));close.GetComponentInChildren<Text>(true).text=Ui("ui.close","닫기","Close");close.onClick.AddListener(Hide);close.GetComponent<ModernUiTileImage>().Rebuild();
        }

        private void BindButtons()
        {
            while(_interestButtons.Count<_interests.Length)
            {
                int i=_interestButtons.Count;var button=MakeButton(_cardsContent,"InterestButton_"+i,Vector2.zero,new Vector2(300f,300f),true);
                var rt=(RectTransform)button.transform;rt.anchorMin=new Vector2(0f,1f);rt.anchorMax=rt.anchorMin;rt.pivot=new Vector2(0f,1f);rt.anchoredPosition=new Vector2(i*316f,0f);
                var label=button.GetComponentInChildren<Text>(true);label.fontSize=20;label.rectTransform.anchorMin=new Vector2(.5f,1f);label.rectTransform.anchorMax=label.rectTransform.anchorMin;label.rectTransform.pivot=new Vector2(.5f,1f);label.rectTransform.anchoredPosition=new Vector2(0f,-198f);label.rectTransform.sizeDelta=new Vector2(264f,76f);
                var iconGo=new GameObject("InterestIcon",typeof(RectTransform),typeof(Image),typeof(UrbanUiIcon));iconGo.transform.SetParent(button.transform,false);
                var iconRect=(RectTransform)iconGo.transform;iconRect.anchorMin=new Vector2(.5f,1f);iconRect.anchorMax=iconRect.anchorMin;iconRect.pivot=new Vector2(.5f,1f);iconRect.anchoredPosition=new Vector2(0f,-28f);iconRect.sizeDelta=new Vector2(144f,144f);iconGo.GetComponent<Image>().raycastTarget=false;
                _interestButtons.Add(button);_interestButtonLabels.Add(label);
            }
            _cardsContent.sizeDelta=new Vector2(Mathf.Max(1344f,_interests.Length*316f-16f),300f);
            for(int i=0;i<_interestButtons.Count;i++)
            {
                var button=_interestButtons[i];var label=_interestButtonLabels[i];button.onClick.RemoveAllListeners();
                bool visible=i<_interests.Length&&_interests[i]!=null;button.gameObject.SetActive(visible);
                if(!visible){label.text=string.Empty;continue;}
                int index=i;var interest=_interests[i];var summary=CareerInterestSummaryBuilder.Build(interest,_interestProgress,_candidateProgress,_studentProgress,_day);
                var title=string.IsNullOrEmpty(interest.DisplayNameKey)?Ui("ui.career-interest.unknown","관심사","Interest"):GameText.Get(interest.DisplayNameKey);
                label.text=title+"\n"+StateText(summary);
                button.transform.Find("InterestIcon").GetComponent<UrbanUiIcon>().ConfigureFor(interest,"journal");
                button.onClick.AddListener(()=>SelectAndShow(index));
                bool selected=_interestProgress!=null&&!string.IsNullOrEmpty(_interestProgress.CurrentInterestId)&&_interestProgress.CurrentInterestId==interest.Id;
                var chrome=button.GetComponent<ModernUiTileImage>();chrome.SetTint(selected?new Color(1f,.97f,.86f,1f):Color.white);chrome.Rebuild();
            }
        }

        private void SelectAndShow(int index)
        {
            if (index < 0 || index >= _interests.Length || _interestProgress == null) return;
            var result = _interestProgress.TrySelect(_interests[index], _candidateProgress, _studentProgress, _day, "career-interest-ui-" + _interests[index].Id + "-" + _day);
            if (result.Applied)
            {
                if (_interestComponent != null) CareerInterestProgressPersistence.Save(_interestComponent);
                ApplyLinkedQuests(_interests[index]);
                SelectionChanged?.Invoke();
            }
            BindButtons();
            ShowDetails(index);
        }

        private void EnsureCurrentInterestLinkedQuests()
        {
            if (_interestProgress == null || string.IsNullOrEmpty(_interestProgress.CurrentInterestId)) return;
            for (int i = 0; i < _interests.Length; i++)
            {
                var interest = _interests[i];
                if (interest != null && interest.Id == _interestProgress.CurrentInterestId)
                {
                    ApplyLinkedQuests(interest);
                    return;
                }
            }
        }

        private void ApplyLinkedQuests(CareerInterestDefinition interest)
        {
            if (interest == null || interest.LinkedQuests == null || interest.LinkedQuests.Count == 0) return;
            var questPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            if (questPanel == null || questPanel.QuestLog == null) return;
            var inventory = _interestComponent != null ? _interestComponent.GetComponent<PlayerInventory>() : null;
            string playerId = ResolvePlayerId(inventory);
            bool added = false;
            for (int i = 0; i < interest.LinkedQuests.Count; i++)
            {
                var quest = interest.LinkedQuests[i];
                if (quest == null || ContainsQuest(questPanel.Quests, quest.Id)) continue;
                questPanel.QuestLog.AddQuest(quest);
                added = true;
            }

            if (added) LoadQuestLog(questPanel.QuestLog, playerId);

            bool changed = false;
            for (int i = 0; i < interest.LinkedQuests.Count; i++)
            {
                var quest = interest.LinkedQuests[i];
                if (quest == null) continue;
                if (questPanel.QuestLog.GetState(quest) == QuestState.NotStarted && questPanel.QuestLog.Accept(quest)) changed = true;
            }

            var context = new RewardRuntimeContext(questPanel.QuestLog, inventory != null ? inventory.Inventory : null, null, new StoryFlagSet(), _studentProgress);
            questPanel.Bind(questPanel.QuestLog, AppendQuests(questPanel.Quests, interest.LinkedQuests), context);
            if (changed) SaveQuestLog(questPanel.QuestLog, playerId);
        }

        private static QuestDefinition[] AppendQuests(QuestDefinition[] current, IReadOnlyList<QuestDefinition> linked)
        {
            var result = new List<QuestDefinition>();
            if (current != null) for (int i = 0; i < current.Length; i++) if (current[i] != null && !ContainsQuest(result, current[i].Id)) result.Add(current[i]);
            if (linked != null) for (int i = 0; i < linked.Count; i++) if (linked[i] != null && !ContainsQuest(result, linked[i].Id)) result.Add(linked[i]);
            return result.ToArray();
        }

        private static bool ContainsQuest(QuestDefinition[] quests, string questId)
        {
            if (quests == null) return false;
            for (int i = 0; i < quests.Length; i++) if (quests[i] != null && quests[i].Id == questId) return true;
            return false;
        }

        private static bool ContainsQuest(List<QuestDefinition> quests, string questId)
        {
            for (int i = 0; i < quests.Count; i++) if (quests[i] != null && quests[i].Id == questId) return true;
            return false;
        }

        private static void SaveQuestLog(QuestLog questLog, string playerId)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (metadata == null || string.IsNullOrEmpty(metadata.SlotId) || questLog == null) return;
            new SaveService(metadata.SlotId).WriteJson(QuestLogFileNameFor(playerId), JsonUtility.ToJson(questLog.ToSaveData(), true));
        }

        private static void LoadQuestLog(QuestLog questLog, string playerId)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (metadata == null || string.IsNullOrEmpty(metadata.SlotId) || questLog == null) return;
            var json = new SaveService(metadata.SlotId).ReadJson(QuestLogFileNameFor(playerId));
            if (string.IsNullOrEmpty(json)) return;
            questLog.LoadFromSaveData(JsonUtility.FromJson<QuestLogSaveData>(json));
        }

        private static string ResolvePlayerId(PlayerInventory inventory)
        {
            var identity = inventory != null ? inventory.GetComponent<PlayerIdentity>() : null;
            return identity != null ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
        }

        private static string QuestLogFileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId) return "quest-log.json";
            return "quest-log-" + SanitizeFileName(playerId) + ".json";
        }

        private static string SanitizeFileName(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return new string(chars);
        }

        private void ShowDetails(int index)
        {
            if(index<0||index>=_interests.Length||_interests[index]==null)
            {
                _details.text=Ui("ui.career-interest.empty","아직 발견한 관심사가 없어요.","No career interests");LayoutDetails();return;
            }
            var summary=CareerInterestSummaryBuilder.Build(_interests[index],_interestProgress,_candidateProgress,_studentProgress,_day);
            string text=GameText.Get(summary.DisplayName)+"\n"+Ui("ui.career-interest.state","상태","State")+": "+StateText(summary);
            if(!string.IsNullOrEmpty(summary.Reason))text+="\n"+GameText.Get(summary.Reason);
            if(!string.IsNullOrEmpty(summary.Description))text+="\n"+GameText.Get(summary.Description);
            text+=FormatLines("\n"+Ui("ui.career-interest.locations","관련 장소","Related Locations"),summary.RelatedLocationKeys,Ui("ui.career-interest.no-locations","아직 기록된 장소가 없어요.","No locations yet"));
            text+=FormatLines("\n"+Ui("ui.career-interest.activities","관련 활동","Related Activities"),summary.RelatedActivityKeys,Ui("ui.career-interest.no-activities","아직 기록된 활동이 없어요.","No activities yet"));
            text+=FormatLines("\n"+Ui("ui.career-interest.recommended","다음에 해볼 일","Recommended"),summary.RecommendedActionKeys,Ui("ui.career-interest.explore","자유롭게 마을을 둘러보세요.","Explore freely"));
            _details.text=text;LayoutDetails();
        }

        private static string FormatLines(string title, string[] values, string empty)
        {
            string text = title;
            if (values == null || values.Length == 0) return text + "\n" + empty;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) text += "\n" + GameText.Get(values[i]);
            return text;
        }

        private void LayoutDetails()
        {
            float width=Mathf.Max(100f,((RectTransform)_detailsContent.parent).rect.width-40f);
            _details.rectTransform.sizeDelta=new Vector2(width,Mathf.Max(246f,_details.preferredHeight+12f));
            _detailsContent.sizeDelta=new Vector2(0f,_details.rectTransform.sizeDelta.y+20f);_detailsContent.anchoredPosition=Vector2.zero;_detailsScroll.verticalNormalizedPosition=1f;
        }

        private static RectTransform MakeContainer(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.anchorMin=new Vector2(.5f,.5f);rt.anchorMax=rt.anchorMin;rt.pivot=rt.anchorMin;rt.anchoredPosition=position;rt.sizeDelta=size;return rt;
        }

        private string StateText(CareerInterestSummary summary)
        {
            var state=summary.State;
            if(state==CareerInterestSelectionState.Selected)
                return _interestProgress!=null && _interestProgress.CurrentInterestId==summary.InterestId ? Ui("ui.interest-state.selected","선택됨","Selected") : Ui("ui.interest-state.available","선택 가능","Available");
            if(state==CareerInterestSelectionState.ChangePending)return Ui("ui.interest-state.pending","변경 확인 필요","Change pending");
            if(state==CareerInterestSelectionState.ChangedToday)return Ui("ui.interest-state.changed","오늘 변경함","Changed today");
            if(state==CareerInterestSelectionState.LockedByCondition)return Ui("ui.interest-state.locked","조건 확인 필요","Requirements needed");
            return Ui("ui.interest-state.available","선택 가능","Available");
        }

        private static string Ui(string key,string ko,string en)
        {
            var value=GameText.Get(key);return value==key?(GameText.Language=="ko"?ko:en):value;
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = LifeUiFont.Shared;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color32(48,71,83,255);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button MakeButton(RectTransform parent,string name,Vector2 position,Vector2 size,bool card=false)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button),typeof(ModernUiTileImage));go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform;rt.anchorMin=new Vector2(.5f,.5f);rt.anchorMax=rt.anchorMin;rt.pivot=rt.anchorMin;rt.anchoredPosition=position;rt.sizeDelta=size;
            go.GetComponent<Image>().color=Color.clear;go.GetComponent<Image>().raycastTarget=true;
            MakeText(rt,"Label",string.Empty,Vector2.zero,size-new Vector2(24f,12f),20,TextAnchor.MiddleCenter);
            var chrome=go.GetComponent<ModernUiTileImage>();chrome.SetRecipe(card?ModernUiRecipes.SlotPanel:ModernUiRecipes.CommonPanel);chrome.SetTileSize(new Vector2(48f,48f));chrome.Rebuild();return go.GetComponent<Button>();
        }
    }
}
