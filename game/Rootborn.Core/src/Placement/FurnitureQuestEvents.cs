using System;
using System.Collections.Generic;
using Rootborn.Core.Quests;

namespace Rootborn.Core.Placement
{
    /// <summary>
    /// 가구가 퀘스트에 알리는 이벤트(원본 AuthoredHousePlacementBinding의 RecordPlacement와 AnnouncePlacedFurniture).
    /// 새로 놓으면 한 번, 이미 놓은 가구는 방에 들어오거나 다음 날이 시작될 때 "다시 알림"으로 한 번 더 알린다.
    /// 다시 알림은 퀘스트 로그가 세지 않고 체인만 따라잡는다(QuestEvent.IsReplay).
    /// </summary>
    public static class FurnitureQuestEvents
    {
        /// <summary>이벤트 키를 만들 때 넘기는 행동 이름(원본 CreateRequestKey("furniture-placed")).</summary>
        public const string Action = "furniture-placed";

        public static QuestEvent Placed(FurnitureDefinition furniture, string surfaceId, string eventKey)
            => new QuestEvent(QuestEventKind.FurniturePlaced, eventKey, furniture?.StableId, 1, surfaceId);

        /// <summary>놓인 가구 정의마다 하나(놓인 순서, 같은 정의는 한 번). newEventKey는 <see cref="Action"/>을 받아 키를 만든다.</summary>
        public static List<QuestEvent> Replays(HouseFurnitureLayout layout, Func<string, string> newEventKey)
        {
            var events = new List<QuestEvent>();
            if (layout == null || newEventKey == null) return events;
            var seen = new HashSet<FurnitureDefinition>();
            for (int i = 0; i < layout.Placed.Count; i++)
            {
                var definition = layout.Placed[i].Definition;
                if (!seen.Add(definition)) continue;
                events.Add(new QuestEvent(QuestEventKind.FurniturePlaced, newEventKey(Action), definition.StableId, 1, layout.SurfaceId, true));
            }

            return events;
        }

        /// <summary>
        /// 어느 체인이든 지금 단계에 가구 배치 목표가 있는가. 다시 알림은 처리한 이벤트 키를 세이브에 영구히 남기므로
        /// 기다리는 단계가 없으면 알리지 않는다(원본 AnyCurrentStepWantsFurniture).
        /// </summary>
        public static bool AnyCurrentStepWantsFurniture(QuestChainLog log, IEnumerable<QuestChainDefinition> chains)
        {
            if (log == null || chains == null) return false;
            foreach (var chain in chains)
            {
                var objectives = chain != null ? log.GetCurrentStep(chain)?.Objectives : null;
                if (objectives == null) continue;
                for (int i = 0; i < objectives.Count; i++) if (objectives[i] is FurniturePlacedQuestObjective) return true;
            }

            return false;
        }
    }
}
