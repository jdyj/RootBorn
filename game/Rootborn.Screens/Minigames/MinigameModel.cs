using Rootborn.Core.Careers;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;
using Rootborn.Game;

namespace Rootborn.Screens.Minigames
{
    /// <summary>예약된 판의 주인: 학생 연습·알바(StudentLifeProgress.PendingMinigame) 또는 성인 시설 일(AdultCareerProgress.PendingWork).</summary>
    public enum MinigameOwner { None, Student, Facility }

    /// <summary>
    /// 미니게임 화면의 표시 모델: 지금 예약된 한 판(요청 id, 미니게임 정의, 시드)과 그 판을 끝내는 명령.
    /// 판 하나는 GameSession이 예약할 때 정한다. 화면은 그 판을 받아 Core MinigameSession으로 돌리고, 끝나면 여기의 정산 명령을 부른다.
    /// 판정과 점수는 Core가 낸다(Unity MinigamePresentationBridge·AdultCareerRuntimeComponent.TrySettlePendingWork와 같은 순서).
    /// </summary>
    public sealed class MinigameModel
    {
        public static readonly MinigameModel None = new MinigameModel();

        public MinigameOwner Owner { get; private set; }
        public string RequestId { get; private set; } = string.Empty;
        public Minigame Definition { get; private set; }
        public int Seed { get; private set; }
        public bool HasGame => Owner != MinigameOwner.None && Definition != null && Definition.IsValid;

        /// <summary>지금 예약된 판. 없거나 정의를 찾지 못하면 None.</summary>
        public static MinigameModel From(GameSession session)
        {
            if (session == null) return None;
            PendingStudentMinigameSaveData student = session.Student.PendingMinigame;
            if (student != null) return Create(session, MinigameOwner.Student, student.RequestId, student.MinigameId, student.Seed);
            PendingFacilityWorkSaveData work = session.Adult.PendingWork;
            if (work != null) return Create(session, MinigameOwner.Facility, work.RequestId, work.MinigameId, work.Seed);
            return None;
        }

        /// <summary>지금 예약된 판의 요청 id(없으면 빈 글자). 여는 쪽이 "새 예약이 생겼는지"를 볼 때 쓴다.</summary>
        public static string PendingRequestId(GameSession session)
        {
            if (session == null) return string.Empty;
            return session.Student.PendingMinigame?.RequestId ?? session.Adult.PendingWork?.RequestId ?? string.Empty;
        }

        /// <summary>
        /// 불러온 직후 남은 예약을 이어 할지: 예약한 날이 오늘(today, 기본은 학생 날)이면 연다. 날이 지난 예약은 열지 않는다 —
        /// Core가 다음 날 시작 때 포기 처리한다(사용자 결정 2026-11-12).
        /// </summary>
        public static bool CanResumeOnLoad(GameSession session, int? today = null)
        {
            if (session == null) return false;
            int day = today ?? session.StudentDay;
            PendingStudentMinigameSaveData student = session.Student.PendingMinigame;
            if (student != null) return student.Day == day;
            PendingFacilityWorkSaveData work = session.Adult.PendingWork;
            return work != null && work.Day == day;
        }

        private static MinigameModel Create(GameSession session, MinigameOwner owner, string requestId, string minigameId, int seed)
        {
            session.Definitions.TryGet(minigameId ?? string.Empty, out Minigame definition);
            return new MinigameModel { Owner = owner, RequestId = requestId ?? string.Empty, Definition = definition, Seed = seed };
        }

        /// <summary>한 판을 새로 만든다(성장 보너스는 지금 학생 값으로 Core가 정한다).</summary>
        public MinigameSession NewSession(GameSession session) => HasGame ? new MinigameSession(Definition, session.Student, Seed) : null;

        /// <summary>
        /// 끝난 판을 정산한다. 그만둔 판은 포기, 학생 판은 성공 여부, 시설 일은 점수(Unity TrySettlePendingWork).
        /// 같은 요청을 두 번 정산하면 Core가 거절한다.
        /// </summary>
        public AdultCareerResult Settle(GameSession session, MinigameSession played)
        {
            if (session == null || played == null || !played.IsComplete) return new AdultCareerResult(AdultCareerResultKind.InvalidRequest);
            if (Owner == MinigameOwner.Facility)
                return played.IsCancelled ? session.ForfeitFacilityWork(RequestId) : session.SettleFacilityWork(RequestId, played.Score);
            if (Owner == MinigameOwner.Student)
                return played.IsCancelled ? session.ForfeitMinigame(RequestId) : session.SettleMinigame(RequestId, played.Succeeded);
            return new AdultCareerResult(AdultCareerResultKind.InvalidRequest);
        }
    }
}
