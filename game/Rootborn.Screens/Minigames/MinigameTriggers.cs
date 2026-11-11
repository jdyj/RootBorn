using System;
using System.Collections.Generic;
using Rootborn.Core.Careers;
using Rootborn.Core.Interaction;
using Rootborn.Core.StudentLife;
using Rootborn.Game;
using Rootborn.Screens.Hud;

namespace Rootborn.Screens.Minigames
{
    /// <summary>
    /// 예약된 미니게임이 생기면 미니게임 화면을 연다(Unity MinigamePresentationBridge.Begin: 예약 직후 패널을 띄웠다).
    /// 예약하는 쪽(장소 화면, 일터 지점, 시설 화면)은 GameSession 명령만 부르고, 여기는 "게임 진행이 바뀜" 알림에서 새 예약을 본다.
    /// 화면 쌓기가 만들어질 때(불러온 직후) 이미 있던 예약은 같은 날이면 바로 연다. 날이 지난 예약은 열지 않는다 — Core가 다음 날
    /// 시작 때 포기 처리한다(사용자 결정 2026-11-12. Unity 판은 다시 불러올 때 그 판을 포기 처리했다).
    /// </summary>
    public sealed class MinigameTrigger
    {
        private readonly ScreenServices _services;
        private readonly string _screenId;
        private string _handledRequest;

        public MinigameTrigger(ScreenServices services, string screenId)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _screenId = screenId;
            _handledRequest = MinigameModel.PendingRequestId(services.Session);
            if (_services.Session != null) _services.Session.Changed += OnChanged;
            if (_handledRequest.Length > 0 && MinigameModel.CanResumeOnLoad(services.Session) && _services.Screens != null)
                _services.Screens.Open(_screenId);
        }

        private void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language || topic == ChangeTopics.Map) return;
            string request = MinigameModel.PendingRequestId(_services.Session);
            if (string.IsNullOrEmpty(request) || request == _handledRequest || _services.Screens.IsOpen(_screenId)) return;
            _handledRequest = request;
            _services.Screens.Open(_screenId);
        }
    }

    /// <summary>
    /// 일터 지점(Unity PartTimeWorkInteractor)과 진로 연습 지점(StudentLifeSceneInteraction의 연습): 상호작용하면 그 알바·연습을 시작한다.
    /// 미니게임이 있으면 예약하고(그러면 MinigameTrigger가 화면을 연다), 없으면 바로 한다. 어느 상호작용 종류가 어느 쪽인지는 등록할 때 넘긴다.
    /// 지점의 대상 id가 알바·연습 id다(엔티티 id로 분기하지 않는다).
    /// 거절되면 이유를 실패 안내(InteractionRefusalNoticeScreen)에 보인다(Unity PartTimeWorkInteractor.ExplainFailure). 이유는 명령의 결과 종류와
    /// Core 판정(StudentActivityRefusal)에서 온다.
    /// </summary>
    public sealed class MinigameWorkPoints
    {
        private readonly ScreenServices _services;
        private readonly HashSet<string> _workKinds, _practiceKinds;
        private readonly Func<int> _seeds;

        public MinigameWorkPoints(ScreenServices services, string[] workKinds, string[] practiceKinds, Func<int> seeds = null)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _workKinds = new HashSet<string>(workKinds ?? Array.Empty<string>(), StringComparer.Ordinal);
            _practiceKinds = new HashSet<string>(practiceKinds ?? Array.Empty<string>(), StringComparer.Ordinal);
            _seeds = seeds ?? (() => Guid.NewGuid().GetHashCode());
            _services.Screens.Interacted += OnInteracted;
        }

        /// <summary>마지막으로 일터·연습 지점에서 한 일의 결과(테스트와 로그용). 없으면 null.</summary>
        public object LastResult { get; private set; }

        private void OnInteracted(InteractionEvent interaction, GameScreen opened)
        {
            GameSession session = _services.Session;
            if (opened != null || session == null || _services.Screens.MovementBlocked) return;
            string kind = interaction.Kind ?? string.Empty, id = interaction.TargetId ?? string.Empty;
            if (_workKinds.Contains(kind)) Work(session, id);
            else if (_practiceKinds.Contains(kind)) Practice(session, id);
        }

        private void Work(GameSession session, string workId)
        {
            string request = "part-time-work:" + Guid.NewGuid().ToString("N");
            bool known = session.Definitions.TryGet(workId, out IMinigamePartTimeWork work);
            bool hasMinigame = known && work.Minigame != null;
            int seed = _seeds();
            if (hasMinigame)
            {
                AdultCareerResult result = session.BeginPartTimeWork(workId, request, seed);
                LastResult = result;
                Refused(result.Kind, known ? Why(session, work.TimePolicy, work.EnergyCost, work.FocusCost) : StudentActivityRefusalReason.None);
            }
            else
            {
                PartTimeWorkResult result = session.PerformPartTimeWork(workId, request);
                LastResult = result;
                Refused(result.Kind, known ? Why(session, work.TimePolicy, work.EnergyCost, work.FocusCost) : StudentActivityRefusalReason.None);
            }
        }

        private void Practice(GameSession session, string practiceId)
        {
            string request = "career-practice:" + Guid.NewGuid().ToString("N");
            bool known = session.Definitions.TryGet(practiceId, out CareerPractice practice);
            bool hasMinigame = known && practice.Minigame != null;
            int seed = _seeds();
            LifeActivity activity = known ? practice.Activity : null;
            StudentActivityRefusalReason why = activity != null ? Why(session, activity.TimePolicy, activity.EnergyCost, activity.FocusCost) : StudentActivityRefusalReason.None;
            if (hasMinigame)
            {
                AdultCareerResult result = session.BeginPractice(practiceId, request, seed);
                LastResult = result;
                Refused(result.Kind, why);
            }
            else
            {
                LifeActivityResult result = session.PerformPractice(practiceId, request);
                LastResult = result;
                Refused(result.Kind, why);
            }
        }

        /// <summary>마지막 거절로 보인 실패 안내의 문구 키(없으면 null).</summary>
        public string LastRefusalKey { get; private set; }

        // 이유는 명령을 부른 뒤의 상태로 판정한다: 거절이면 상태가 그대로다(Core는 거절할 때 바꾸지 않는다).
        private static StudentActivityRefusalReason Why(GameSession session, ActivityTimePolicy policy, int energy, int focus) =>
            StudentActivityRefusal.Explain(session.Student, policy, energy, focus);

        private void Refused(AdultCareerResultKind kind, StudentActivityRefusalReason why) => LastRefusalKey = InteractionRefusals.Show(_services, InteractionRefusals.KeyFor(kind, why));

        private void Refused(LifeActivityResultKind kind, StudentActivityRefusalReason why) => LastRefusalKey = InteractionRefusals.Show(_services, InteractionRefusals.KeyFor(kind, why));
    }
}
