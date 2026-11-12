using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Rootborn.Net.Room;

namespace Rootborn.Server
{
    /// <summary>
    /// 데디케이티드 서버 한 판의 루프 쪽: 방(<see cref="RoomServer"/>)과 고정 간격 루프(<see cref="RoomLoop"/>) 위에 콘솔 명령과 주기 저장을 얹는다.
    /// 콘솔 줄은 어느 스레드에서든 <see cref="Enqueue"/>로 넣고, <see cref="RunDue"/>(루프 스레드)가 틱 전에 꺼내 돌린다 — 방은 한 스레드만 만진다.
    /// 주기 저장은 루프의 틱 수로 잰다(벽시계에 기대지 않는다): <see cref="AutosaveSeconds"/>를 틱 간격으로 나눈 틱마다.
    /// 시간은 넘긴 시계(<see cref="IRoomClock"/>)만 쓴다. 테스트는 손으로 돌리는 시계를 넘긴다.
    /// </summary>
    public sealed class ServerRunner
    {
        private readonly RoomLoop _loop;
        private readonly ObservedSaveStore _store;
        private readonly double _tickSeconds;
        private readonly long _autosaveTicks;
        private readonly Action<string> _log;
        private readonly ConcurrentQueue<string> _pending = new ConcurrentQueue<string>();
        private long _lastSaveTick;

        /// <param name="store">방이 쓰는 저장소(감싼 것 — 마지막 저장 시각을 본다). 없으면 마지막 저장 시각을 모른다.</param>
        /// <param name="autosaveSeconds">주기 저장 간격(초). 0이면 끈다.</param>
        public ServerRunner(RoomServer room, IRoomClock clock, double tickSeconds, string saveSlot, ObservedSaveStore store, double autosaveSeconds,
            Action<string> log, ServerCommands commands = null)
        {
            Room = room ?? throw new ArgumentNullException(nameof(room));
            if (tickSeconds <= 0) throw new ArgumentException("틱 간격은 양수여야 한다", nameof(tickSeconds));
            if (autosaveSeconds < 0 || double.IsNaN(autosaveSeconds)) throw new ArgumentException("주기 저장 간격은 0 이상이다", nameof(autosaveSeconds));
            _loop = new RoomLoop(room, clock, tickSeconds);
            _tickSeconds = tickSeconds;
            _store = store;
            SaveSlot = saveSlot;
            AutosaveSeconds = autosaveSeconds;
            _autosaveTicks = autosaveSeconds > 0 ? Math.Max(1, (long)Math.Round(autosaveSeconds / tickSeconds)) : 0;
            _log = log ?? (_ => { });
            Commands = commands ?? ServerCommands.CreateDefault();
        }

        public RoomServer Room { get; }
        public ServerCommands Commands { get; }
        public string SaveSlot { get; }
        public double AutosaveSeconds { get; }

        /// <summary>돌린 틱 수.</summary>
        public long Ticks => _loop.Ticks;

        /// <summary>가동 시간: 돌린 틱 × 틱 간격(너무 밀려 버린 틱은 세지 않는다).</summary>
        public double UptimeSeconds => _loop.Ticks * _tickSeconds;

        public double SecondsUntilNextTick => _loop.SecondsUntilNextTick;

        public DateTime? LastSaved => _store?.LastWrite;

        /// <summary>stop 명령 등으로 끝내 달라고 했다.</summary>
        public bool StopRequested { get; private set; }

        /// <summary>콘솔 한 줄을 넣는다(어느 스레드에서든). 다음 <see cref="RunDue"/>가 돌린다.</summary>
        public void Enqueue(string line)
        {
            if (line != null) _pending.Enqueue(line);
        }

        /// <summary>넣어 둔 명령을 돌리고, 시계가 지난 틱을 돌리고, 때가 되면 주기 저장을 한다.</summary>
        /// <returns>이번에 돌린 틱 수.</returns>
        public int RunDue()
        {
            while (!StopRequested && _pending.TryDequeue(out var line)) Commands.Run(this, line);
            if (StopRequested || Room.IsShutDown) return 0;
            int ran = _loop.RunDue();
            if (_autosaveTicks > 0 && _loop.Ticks - _lastSaveTick >= _autosaveTicks) SaveNow("주기 저장");
            return ran;
        }

        /// <summary>지금 저장한다(방의 시계와 공유 조각). 쓰지 못하면 로그에 남기고 계속 돈다.</summary>
        /// <returns>저장했으면 true.</returns>
        public bool SaveNow(string why)
        {
            _lastSaveTick = _loop.Ticks;
            try
            {
                Room.Save();
                Log("저장함(" + why + "): 슬롯 " + SaveSlot + ", " + Room.DayAndTime);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log("오류: 저장하지 못했다(" + why + ", 방은 계속 돈다 — 세이브 폴더의 권한·남은 공간을 본다): " + e.Message);
                return false;
            }
        }

        public void RequestStop(string why)
        {
            if (StopRequested) return;
            StopRequested = true;
            Log("끝내 달라고 함: " + why);
        }

        public void Log(string line) => _log(line);

        public void LogLines(IEnumerable<string> lines)
        {
            foreach (var line in lines) _log(line);
        }
    }
}
