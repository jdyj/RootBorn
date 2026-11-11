using System;
using Rootborn.Core.Inventory;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Careers
{
    /// <summary>
    /// 성인 직업 스냅샷이 생기기 전의 Unity 판 슬롯: 학생 진행, 인벤토리, 일과, 진로 후보가 따로 파일에 있다
    /// (원본 AdultCareerRuntimeComponent가 스냅샷이 없을 때 하던 읽기). 여기서는 읽기만 하고 옛 파일을 지우지 않는다.
    /// 첫 명령이 저장되면 스냅샷이 생기고, 그 뒤로는 스냅샷이 원본이다.
    /// 월드 상태·체인·가구 해금의 독립 파일은 각 영역이 이미 읽는다(WorldStateSaveFiles, QuestChain, FurnitureSaveFiles).
    /// 파일의 슬롯·플레이어가 달라도(복사한 슬롯) 지금 슬롯·플레이어로 읽는다.
    /// </summary>
    public static class PreSnapshotSlotFiles
    {
        /// <summary>학생 진행 파일. 없으면 null.</summary>
        public static StudentLifeProgressSaveData ReadStudent(ISaveStore store, string saveSlot, string playerId)
        {
            if (store == null || !store.TryRead(saveSlot, SaveFileNames.UnityStudentLife(playerId), out StudentLifeProgressSaveData data)) return null;
            data.SaveSlot = saveSlot;
            data.PlayerId = playerId;
            if (data.CareerCandidates != null) Rebind(data.CareerCandidates, saveSlot, playerId);
            if (data.DailyPlan != null && !DailyPlanProgressSaveData.IsEmpty(data.DailyPlan))
            {
                data.DailyPlan.SaveSlot = saveSlot;
                data.DailyPlan.PlayerId = playerId;
            }

            return data;
        }

        /// <summary>인벤토리 파일. 없으면 null. Items가 없는 파일은 읽을 수 없는 파일로 본다(원본은 복구가 필요하다고 멈췄다).</summary>
        public static PlayerInventorySaveData ReadInventory(ISaveStore store, string saveSlot, string playerId)
        {
            if (store == null || !store.TryRead(saveSlot, SaveFileNames.UnityInventory(playerId), out PlayerInventorySaveData data)) return null;
            if (data.Items == null) throw new InvalidOperationException("인벤토리 파일 " + saveSlot + "/" + SaveFileNames.UnityInventory(playerId) + "에 Items가 없다");
            return data;
        }

        /// <summary>
        /// 학생 진행에 따로 있던 일과(4자리 16진 이름, 없으면 정리한 id 이름)와 진로 후보를 넣는다. 하나라도 넣었으면 true.
        /// 일과는 학생이 아직 일과를 갖지 않았을 때만 들어간다(StudentLifeProgress.TryImportLegacyDailyPlan).
        /// 진로 후보 파일이 없으면 학생 진행 안의 후보를 그대로 둔다.
        /// </summary>
        public static bool ImportInto(StudentLifeProgress student, ISaveStore store)
        {
            if (student == null || store == null) return false;
            bool imported = false;
            string slot = student.SaveSlot, player = student.PlayerId;
            if (store.TryRead(slot, SaveFileNames.UnityDailyPlan(player), out DailyPlanProgressSaveData plan)
                || store.TryRead(slot, SaveFileNames.UnityDailyPlanSanitized(player), out plan))
            {
                plan.SaveSlot = slot;
                plan.PlayerId = player;
                imported |= student.TryImportLegacyDailyPlan(plan);
            }

            if (store.TryRead(slot, SaveFileNames.UnityCareerCandidates(player), out CareerCandidateProgressSaveData candidates))
            {
                Rebind(candidates, slot, player);
                imported |= student.RestoreCareerCandidates(candidates);
            }

            return imported;
        }

        private static void Rebind(CareerCandidateProgressSaveData data, string slot, string player)
        {
            data.SaveSlot = slot;
            data.PlayerId = player;
        }
    }
}
