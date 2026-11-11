using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Core.Characters;
using Rootborn.Core.Time;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.World;

namespace Rootborn.Host.World
{
    /// <summary>
    /// 월드 화면이 쓰는 데이터: 정의 로더(data/)로 만든 규칙 정의와, 로더가 만들지 않는 그림 데이터(캐릭터 시트).
    /// 정의는 바뀌지 않으므로 같은 data 폴더는 프로세스에서 한 번만 읽는다(창을 여러 번 띄우는 테스트). 상태가 있는 것
    /// (시계 등)은 여기서 꺼내 쓰지 말고 값만 읽어 새로 만든다.
    /// </summary>
    public sealed class GameContent
    {
        private static readonly Dictionary<string, GameContent> Cache = new Dictionary<string, GameContent>(StringComparer.OrdinalIgnoreCase);

        public readonly GameDefinitions Definitions;
        public readonly CharacterArt Characters;
        public readonly WearableCatalog Wearables;
        public readonly DayNightLighting Lighting;
        public readonly NpcArt Npcs;
        private readonly DayClock _timeDefinition;

        private GameContent(GameDefinitions definitions, CharacterArt characters, DayNightLighting lighting, NpcArt npcs)
        {
            Npcs = npcs;
            Definitions = definitions;
            Characters = characters;
            Lighting = lighting;
            IReadOnlyList<DayClock> times = definitions.All<DayClock>();
            if (times.Count != 1) throw new InvalidOperationException("시간 정의(TimeDefinition)가 하나가 아니다: " + times.Count);
            _timeDefinition = times[0];
            Wearables = new WearableCatalog(definitions.All<WearableSlotRule>(), definitions.All<WearableRule>(), definitions.All<GrowthStageRule>());
        }

        /// <summary>시간 정의의 하루 길이와 시작 날로 새 시계를 만든다(정의의 시계 자체는 함께 쓰므로 흘리지 않는다).</summary>
        public DayClock NewClock() => new DayClock(_timeDefinition.RealSecondsPerGameDay, _timeDefinition.Day - CoreDaysElapsed(_timeDefinition));

        private static int CoreDaysElapsed(DayClock clock) => (int)Math.Floor(clock.ElapsedGameDays);

        public static GameContent Load(string repoRoot)
        {
            string data = Path.GetFullPath(Path.Combine(repoRoot, "data"));
            lock (Cache)
            {
                if (Cache.TryGetValue(data, out GameContent cached)) return cached;
                LoadResult result = GameDataLoader.Load(data, RootbornBuilders.Register);
                result.ThrowIfFailed();
                var content = new GameContent(result.Definitions, CharacterArt.Load(repoRoot), DayNightLighting.Load(repoRoot), NpcArt.Load(repoRoot));
                foreach (string problem in content.Npcs.Problems) Console.Error.WriteLine("[NPC 그림] " + problem);
                foreach (string problem in content.Characters.Problems) Console.Error.WriteLine("[캐릭터 그림] " + problem);
                Cache[data] = content;
                return content;
            }
        }
    }
}
