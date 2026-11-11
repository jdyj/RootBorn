using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Core.Characters;
using Rootborn.Core.Save;
using Rootborn.Game;
using Rootborn.Screens.Records;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.World
{
    /// <summary>
    /// 옷장이 부르는 캐릭터 쪽(Unity GrowingCharacterStageDriver.ChooseOutfit): 옷을 입고(Core WearableRules.Choose), 슬롯에 바로 저장하고,
    /// 월드의 캐릭터 모습을 새로 입힌다. 저장에 실패해도 이번 판에서는 입은 채로 둔다(Unity 판과 같음).
    /// 미리보기는 서 있는 칸(아래 보기, 가운데 칸)의 몸 + 옷 레이어 그림이다.
    /// </summary>
    public sealed class HostWardrobe : IWardrobeBackend
    {
        private readonly WorldScreen _world;
        private readonly GameSession _session;
        private readonly ISaveStore _store;
        private readonly string _slotId, _playerId, _repoRoot;

        public HostWardrobe(WorldScreen world, GameSession session, ISaveStore store, string slotId, string playerId, string repoRoot)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _slotId = slotId; _playerId = playerId; _repoRoot = repoRoot;
        }

        public WearableCatalog Catalog => _world.Content.Wearables;
        public string StageId => _session.GrowthStageId();

        public IReadOnlyCollection<string> WornIds
        {
            get
            {
                var ids = new List<string>();
                foreach (WearableLayer layer in WearableRules.ResolveLayers(Catalog, _world.Wearing, StageId, string.Empty)) ids.Add(layer.WearableId);
                return ids;
            }
        }

        public IReadOnlyList<WardrobeSprite> Preview(string wearableId)
        {
            WearableRule rule = Catalog.FindWearable(wearableId);
            var sprites = new List<WardrobeSprite>();
            if (rule == null) return sprites;
            var state = WearableState.FromEntries(_world.Wearing.ToEntries());
            WearableRules.Choose(Catalog, state, rule.SlotId, wearableId, StageId);
            CharacterLook look = CharacterLook.Resolve(_world.Content.Characters, Catalog, state, StageId, null);
            if (look == null) return sprites;
            Add(sprites, look.Body);
            foreach (CharacterSheet layer in look.Layers) Add(sprites, layer);
            return sprites;
        }

        public bool Choose(string wearableId)
        {
            WearableRule rule = Catalog.FindWearable(wearableId);
            if (rule == null || string.IsNullOrEmpty(wearableId)) return false;
            if (!WearableRules.Choose(Catalog, _world.Wearing, rule.SlotId, wearableId, StageId)) return false;
            try { _store.Write(_slotId, SaveFileNames.Wearables(_playerId), WearableSaveData.From(_world.Wearing, legacyImported: true)); }
            catch (IOException e) { Console.Error.WriteLine("[옷장] 저장하지 못했다(이번 판에서는 입은 채로 둔다): " + e.Message); }
            catch (UnauthorizedAccessException e) { Console.Error.WriteLine("[옷장] 저장하지 못했다(이번 판에서는 입은 채로 둔다): " + e.Message); }
            _world.ApplyPlayerLook(CharacterLook.Resolve(_world.Content.Characters, Catalog, _world.Wearing, StageId, null));
            return true;
        }

        private void Add(List<WardrobeSprite> sprites, CharacterSheet sheet)
        {
            CharacterFrame frame = sheet?.Frame(CharacterAnimator.RowDown, CharacterAnimator.StandingColumn);
            if (frame == null) return;
            string image = Path.GetRelativePath(_repoRoot, frame.Texture).Replace('\\', '/');
            sprites.Add(new WardrobeSprite(image, new PixelRect(frame.X, frame.Y, frame.W, frame.H)));
        }
    }
}
