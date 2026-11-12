using System;
using System.Collections.Generic;
using Rootborn.Core.Interaction;
using Rootborn.Core.Save;
using Rootborn.Data.Save;
using Rootborn.Screens.Start;
using Rootborn.Screens.Storage;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// 그냥 Open(id)로는 내용이 없어 바로 닫히거나 비는 화면을 실제로 여는 길(상호작용, 예약, 인자). 긴 문구 시험과 키보드 전수 검사가 같이 쓴다.
    /// 나머지 화면은 Open(id)로 연다.
    /// </summary>
    internal static class ScreenOpeners
    {
        /// <param name="menu">시작 화면의 메뉴(필요할 때만 만든다).</param>
        /// <param name="placeChest">상자를 놓고 그 id를 돌려준다(상자 화면에만, 없으면 그 화면은 열지 않는다).</param>
        public static bool TryOpen(string id, ScreenStack stack, ScreenServices services, Func<StartMenu> menu, Func<string> placeChest = null)
        {
            switch (id)
            {
                case ScreenCatalog.Dialogue: stack.Interaction(new InteractionEvent("House", "npc", "npc.first-guide")); return true;
                case ScreenCatalog.Location: stack.Interaction(new InteractionEvent("Town", "location-activity", "location.library")); return true;
                case ScreenCatalog.Minigame: stack.Interaction(new InteractionEvent("Town", "part-time-work", "work.shift.corner-store")); return true;
                case ScreenCatalog.SaveSlotSelect: stack.Open(ScreenCatalog.SaveSlotSelect, argument: menu()); return true;
                case ScreenCatalog.CharacterCreator: stack.Open(ScreenCatalog.CharacterCreator, argument: new CreatorOpen(menu(), "slot-2")); return true;
                case ScreenCatalog.InteractionRefusal: Rootborn.Screens.Hud.InteractionRefusals.Show(services, "ui.prompt.no-time"); return true;
                case ScreenCatalog.RoomSharesNotice:
                    stack.Open(ScreenCatalog.RoomSharesNotice, argument: new RoomSharesNoticeOpen(Rootborn.Core.Room.RoomPieceCatalog.CreateDefault().Names));
                    return true;
                case ScreenCatalog.Storage when placeChest != null:
                    stack.Interaction(new InteractionEvent("House", StorageScreen.InteractionKind, placeChest()));
                    return true;
                default: return false;
            }
        }
    }

    /// <summary>시작 화면 시험용 메뉴: 그 판의 세이브 폴더에 슬롯 하나(이야기를 써 둬 이어 하기·지우기 칸도 보인다)를 만든다.</summary>
    internal static class StartMenus
    {
        public static StartMenu Create(string savesDirectory)
        {
            var store = new FileSaveStore(savesDirectory);
            var looks = CreatorLooks.Load(TestData.DataDirectory);
            var model = new CharacterCreatorModel(looks);
            long now = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc).Ticks;
            store.Write("slot-1", SaveFileNames.Metadata, SaveSlots.CreateMetadata("slot-1", "Name", model.Snapshot(), now, 1));
            return new StartMenu(new SaveSlots(store, _ => { }), looks, new Rootborn.Screens.Tests.Start.FakePreviewArt(), _ => true, () => { }, () => now, () => 1);
        }

        public static StartMenu Create(TestSession t) => Create(t.SavesDirectory);
    }
}
