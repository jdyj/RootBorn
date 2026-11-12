using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Screens;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Storage;

namespace Rootborn.Simulation.Rooms
{
    /// <summary>
    /// 같이 하는 방의 정책 표: 혼자 하기의 정책(PlayPolicyCatalog) + 나누는 사람(<see cref="SharerPolicy"/>). 자리마다 씨앗으로 섞는다 —
    /// 첫 자리는 늘 나누는 사람(상자·가구·지갑이 실제로 쓰이게), 나머지는 무작위·메인 체인 우선·학교 밖·안 해 본 것·밤샘을 돌려 가며.
    /// </summary>
    public static class RoomPolicies
    {
        public const string Sharer = "sharer";

        private static readonly string[] Others =
        {
            PlayPolicyCatalog.MainChain, PlayPolicyCatalog.Random, PlayPolicyCatalog.OffSchool, PlayPolicyCatalog.Novelty, PlayPolicyCatalog.StayUp, Sharer,
        };

        public static string[] Mix(int seed, int players)
        {
            var names = new string[players];
            for (int i = 0; i < players; i++) names[i] = i == 0 ? Sharer : Others[(seed + i - 1) % Others.Length];
            return names;
        }

        public static IPlayPolicy Create(string name, PlayAtlas atlas) => name == Sharer ? new SharerPolicy(atlas) : PlayPolicyCatalog.Default().Create(name, atlas);
    }

    /// <summary>
    /// 나누는 사람: 놓을 수 있는 면이 있는 맵에서 상자를 놓고(배치 화면 검색 칸에 상자인 가구의 id를 친다 — 데이터가 상자라고 한 가구), 놓인 상자 앞에서
    /// 상자 화면을 열어 가방의 물건을 넣고 상자의 물건을 꺼낸다. 그 밖에는 학교 밖 정책처럼 마을 활동을 한다(물건과 돈이 생긴다).
    /// 화면 요소 id·행동 분류·상호작용 종류만 본다. 엔티티 id로 분기하지 않는다.
    /// </summary>
    public sealed class SharerPolicy : IPlayPolicy
    {
        // 물건이 생기는 곳(상호작용 종류): 탐색과 알바. 종류 이름은 상호작용 데이터의 것이다.
        private readonly PreferKindsPolicy _inner = new PreferKindsPolicy(RoomPolicies.Sharer, new[] { "exploration", "part-time-work" });
        private readonly PlayAtlas _atlas;
        private int _moves, _away;
        private bool _typed, _placed;

        /// <param name="atlas">집(놓을 수 있는 면이 있는 맵)으로 돌아가는 문을 찾는다. 없으면 돌아가지 않는다.</param>
        public SharerPolicy(PlayAtlas atlas = null) { _atlas = atlas; }

        public string Name => RoomPolicies.Sharer;

        public PlayAction Choose(PlayView view, PlayMemory memory, Random random)
        {
            if (view.TopScreen == ScreenCatalog.Storage) return Storage(view, random);
            _moves = 0;
            if (view.TopScreen == ScreenCatalog.FurniturePlacement) return Placement(view, random);
            _typed = false;
            _placed = false;
            if (view.InWorld && view.TopScreen == null)
            {
                PlayAction chest = Pick.Any(view.Actions.Where(a => a.Category == "interaction" && a.InteractionKind == StorageScreen.InteractionKind).ToList(), random);
                PlayAction place = view.Find("button:" + ScreenCatalog.HudButtons + "/hud.button.placement");
                if (place != null) _away = 0; else _away++;
                if (chest != null && random.NextDouble() < 0.35 && memory.Tried(chest.Key) < 30) return chest;
                if (chest == null && place != null && memory.Tried(place.Key) < 6) return place;
                // 한동안 집 밖에 있었으면 가끔 집으로(가져온 물건을 넣는다).
                if (place == null && _away > 8 && _atlas != null && random.NextDouble() < 0.5)
                {
                    string door = _atlas.FirstDoorToward(view.MapId, map => !string.IsNullOrEmpty(_atlas.Interactions.Map(map).PlacementSurface));
                    PlayAction home = string.IsNullOrEmpty(door) ? null : view.Find("door:" + door);
                    if (home != null) return home;
                }
            }
            return _inner.Choose(view, memory, random);
        }

        /// <summary>상자 화면: 넣거나 꺼내기를 몇 번 하고 닫는다. 기다리는 동안(방의 답)은 기다린다.</summary>
        private PlayAction Storage(PlayView view, Random random)
        {
            if (_moves >= 3) return DayEndRoutine.Close(view);
            var bag = view.Actions.Where(a => a.Screen == ScreenCatalog.Storage && a.Element == StorageScreen.BagGridId && a.Subject != null).ToList();
            var chest = view.Actions.Where(a => a.Screen == ScreenCatalog.Storage && a.Element == StorageScreen.ChestGridId && a.Subject != null).ToList();
            List<PlayAction> from = chest.Count > 0 && (bag.Count == 0 || random.NextDouble() < 0.5) ? chest : bag;
            if (from.Count == 0) return DayEndRoutine.Close(view);
            _moves++;
            return Pick.Any(from, random);
        }

        /// <summary>배치 화면: 상자를 고르고(안 보이면 검색 칸에 친다) 놓을 수 있는 칸을 누른다. 한 번 놓으면 닫는다.</summary>
        private PlayAction Placement(PlayView view, Random random)
        {
            if (_placed || view.StorageFurniture.Count == 0) return DayEndRoutine.Close(view);
            var cells = view.Actions.Where(a => a.Category == "cell" && view.StorageFurniture.Contains(a.Subject)).ToList();
            // 놓을 칸: 반은 첫 칸(사람들이 같은 자리를 노리기 쉽다 — 같은 칸에 동시에 놓으면 먼저 온 요청만 된다), 반은 아무 칸.
            if (cells.Count > 0) { _placed = true; return random.NextDouble() < 0.5 ? cells[0] : Pick.Any(cells, random); }
            PlayAction card = view.Actions.FirstOrDefault(a => a.Screen == ScreenCatalog.FurniturePlacement && a.Element != null
                && a.Element.StartsWith("furniture.slot.", StringComparison.Ordinal) && view.StorageFurniture.Contains(a.Subject));
            if (card != null) return card;
            if (!_typed)
            {
                _typed = true;
                PlayAction type = view.TypeInto(ScreenCatalog.FurniturePlacement, FurniturePlacementScreen.SearchId, view.StorageFurniture[0]);
                if (type != null) return type;
            }
            return DayEndRoutine.Close(view);
        }
    }
}
