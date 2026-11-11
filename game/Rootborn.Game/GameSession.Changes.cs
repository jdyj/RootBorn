using System;
using System.Collections.Generic;

namespace Rootborn.Game
{
    /// <summary>
    /// GameSession.Changed가 알리는 주제 이름. 앞의 일곱은 Rootborn.Screens.ChangeTopics와 같은 글자라서 화면 쪽 알림으로 그대로 넘길 수 있다.
    /// 새 주제는 상수 하나로 더한다.
    /// </summary>
    public static class GameChangeTopics
    {
        public const string Student = "student";
        public const string Inventory = "inventory";
        public const string Quests = "quests";
        public const string Dialogue = "dialogue";
        public const string Day = "day";
        public const string Career = "career";
        /// <summary>월드 상태와 장소 상태.</summary>
        public const string World = "world";
        /// <summary>하루 이벤트 기록(고르기·미루기·지나가기).</summary>
        public const string DailyEvents = "daily-events";
        /// <summary>탐색·발견 단서·단서 해석·도감.</summary>
        public const string Discovery = "discovery";
        /// <summary>집 가구 배치와 가구 해금.</summary>
        public const string Furniture = "furniture";
        /// <summary>목표 수첩의 추적 지정.</summary>
        public const string Objectives = "objectives";
        /// <summary>방의 시계(날짜, 때, 누가 이 때를 끝냈는지).</summary>
        public const string Room = "room";
    }

    /// <summary>변경 알림. 적용된 명령이 저장을 마친 뒤에 주제마다 한 번 알린다. 적용되지 않은 명령은 알리지 않는다.</summary>
    public sealed partial class GameSession
    {
        // 퀘스트·체인에 알린 이벤트와 보상은 가방·생활 수치·월드 상태까지 바꿀 수 있다.
        private static readonly string[] AnnounceTopics = { GameChangeTopics.Quests, GameChangeTopics.Inventory, GameChangeTopics.Student, GameChangeTopics.World };
        private static readonly string[] RewardTopics = AnnounceTopics;

        /// <summary>
        /// 무엇이 바뀌었는지(GameChangeTopics). 저장한 뒤에 부르므로, 듣는 쪽이 예외를 내도 명령의 결과는 이미 저장돼 있다.
        /// 듣는 쪽은 그 주제의 값만 다시 읽는다.
        /// </summary>
        public event Action<string> Changed;

        private void Notify(string[] first, params string[] more)
        {
            var handler = Changed;
            if (handler == null) return;
            var told = new HashSet<string>(StringComparer.Ordinal);
            if (first != null) foreach (var topic in first) if (topic != null && told.Add(topic)) handler(topic);
            if (more != null) foreach (var topic in more) if (topic != null && told.Add(topic)) handler(topic);
        }

        /// <summary>적용되면 저장하고 알린다.</summary>
        private T Saved<T>(T result, bool applied, string[] topics, params string[] more)
        {
            if (!applied) return result;
            Save();
            Notify(topics, more);
            return result;
        }

        private void SaveAndNotify(string[] topics, params string[] more)
        {
            Save();
            Notify(topics, more);
        }
    }
}
