using System;
using System.Collections.Generic;
using Rootborn.Core.Network;
using Rootborn.Core.Save;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;

namespace Rootborn.Net.Room
{
    /// <summary>방 설정. 값은 실행 인자에서 넘긴다(데디케이티드 서버) 또는 방 만들기 화면에서(호스트 모드, 아직 없음).</summary>
    public sealed class RoomServerSettings
    {
        /// <summary>정원의 상한(설계 10절 1번: 기본 4명, 설정으로 8명까지. 도착 자리 규칙 SpawnSpread의 한계).</summary>
        public const int MaxPlayersLimit = 8;

        public int MaxPlayers { get; set; } = 4;

        /// <summary>틱 간격(초). 받은 것을 처리하고 보낼 것을 보내는 주기. 위치를 보내는 클라이언트 횟수(초당 10번)보다 촘촘하게 둔다.</summary>
        public double TickSeconds { get; set; } = 1.0 / 30.0;

        /// <summary>연결한 뒤 이만큼 버전을 밝히지 않으면 끊는다.</summary>
        public double HandshakeTimeoutSeconds { get; set; } = 5.0;

        /// <summary>세션 쪽 설정(살아 있다는 표시, 시간 초과, 메시지 크기). MaxPlayers는 위 값으로 덮는다.</summary>
        public SessionServerSettings Session { get; set; } = new SessionServerSettings();

        public void Validate()
        {
            if (MaxPlayers < 1 || MaxPlayers > MaxPlayersLimit) throw new ArgumentException("정원은 1~" + MaxPlayersLimit + "명이다: " + MaxPlayers);
            if (TickSeconds <= 0 || HandshakeTimeoutSeconds <= 0) throw new ArgumentException("틱 간격과 접속 확인 시간 초과는 양수여야 한다");
            if (Session == null) throw new ArgumentException("세션 설정이 없다");
        }
    }

    /// <summary>
    /// 방 하나(서버 쪽 논리). 데디케이티드 서버(Rootborn.Server)와, 나중에 같은 프로세스 안의 호스트 모드가 같은 이 코드를 돌린다.
    /// - 시작: 세이브 슬롯의 방 상태(지금은 공용 시간)를 읽는다. 없으면 새 방으로 만들고 바로 써 둔다.
    /// - 접속: 접속 확인 관문(<see cref="HandshakeServerTransport"/>)이 버전을 맞춰 보고, 그 뒤 세션(<see cref="SessionServer"/>)이
    ///   정원·신원을 본 다음 위치·겉모습·공용 시간을 주인으로서 중계한다.
    /// - 틱: <see cref="Tick"/>을 고정 간격으로 부른다(<see cref="RoomLoop"/>). 시간은 넘긴 값만 쓴다.
    ///   공용 시간은 사람이 하루를 끝내야 넘어가므로 아무도 없으면 저절로 멈춘다.
    /// - 끝: <see cref="Shutdown"/>이 방 상태를 저장하고 모두를 이유 코드(<see cref="RoomRefusal.Shutdown"/>)와 함께 끊는다.
    /// 공유 조각(지갑·집·마을 상태)과 손님 저장 받기는 아직 없다(설계 7절 3~5단계). 들어오면 이 클래스가 그것들을 들고 저장한다.
    /// </summary>
    public sealed class RoomServer : IDisposable
    {
        private readonly ISaveStore _store;
        private readonly string _saveSlot;
        private readonly Action<string> _log;
        private readonly SessionServer _session;
        private readonly HashSet<string> _known = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _gone = new List<string>(8);
        private bool _shutDown;

        /// <param name="transport">듣는 전송(UDP 또는 같은 프로세스 안). 이 방이 닫을 때 같이 닫는다.</param>
        /// <param name="versions">이 방의 버전. 들어오는 사람의 버전이 이것과 같아야 한다.</param>
        /// <param name="store">방 상태를 읽고 쓸 저장소.</param>
        /// <param name="saveSlot">세이브 슬롯 id.</param>
        /// <param name="log">사람이 읽는 운영 로그(현지화 대상 아님). 없으면 남기지 않는다.</param>
        public RoomServer(ITransport transport, RoomServerSettings settings, RoomVersions versions, ISaveStore store, string saveSlot, Action<string> log)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            settings ??= new RoomServerSettings();
            settings.Validate();
            _store = store ?? throw new ArgumentNullException(nameof(store));
            if (string.IsNullOrWhiteSpace(saveSlot)) throw new ArgumentException("세이브 슬롯이 비어 있다", nameof(saveSlot));
            _saveSlot = saveSlot;
            _log = log;
            Versions = versions;

            IsNewRoom = !_store.TryRead(SaveFileNames.WorldTimeSlot, SaveFileNames.WorldTime(saveSlot), out WorldTimeSaveData _);

            var gate = new HandshakeServerTransport(transport, versions, settings.HandshakeTimeoutSeconds, settings.Session.MaxMessageBytes);
            gate.Refused += (peer, reason, theirs) =>
                Log(theirs.HasValue ? $"접속 거절 {peer}: {reason} (상대 {theirs.Value}, 방 {versions})" : $"접속 거절 {peer}: {reason}");
            settings.Session.MaxPlayers = settings.MaxPlayers;
            _session = new SessionServer(gate, settings.Session, store, saveSlot);

            if (IsNewRoom) SharedWorldTime.Save(_store, _saveSlot, _session.WorldTime);
            Log((IsNewRoom ? "새 방을 만듦" : "방을 이어서 엶") + $": 슬롯 {saveSlot}, {DayText()}, 정원 {settings.MaxPlayers}, {versions}");
        }

        public RoomVersions Versions { get; }

        /// <summary>이 슬롯에 방 상태가 없어서 새로 만들었다.</summary>
        public bool IsNewRoom { get; }

        public SharedWorldTime WorldTime => _session.WorldTime;
        public IReadOnlyCollection<ServerPlayer> Players => _session.Players;
        public IEnumerable<ServerPlayer> PlayersOnMap(string mapId) => _session.PlayersOnMap(mapId);
        public bool IsShutDown => _shutDown;

        /// <summary>한 틱: 받은 것을 처리하고 보낼 것을 보낸다. 들어오고 나간 사람을 로그에 남긴다.</summary>
        public void Tick(double now)
        {
            if (_shutDown) return;
            int day = _session.WorldTime.Day;
            _session.Update(now);
            NoteArrivalsAndDepartures();
            if (_session.WorldTime.Day != day) Log("날이 바뀜: " + DayText());
        }

        /// <summary>방 상태를 저장하고 모두를 끊고 전송을 닫는다. 두 번 불러도 된다.</summary>
        public void Shutdown()
        {
            if (_shutDown) return;
            SharedWorldTime.Save(_store, _saveSlot, _session.WorldTime);
            int count = _session.Players.Count;
            _session.Dispose();
            _shutDown = true;
            Log($"저장하고 닫음: 슬롯 {_saveSlot}, {DayText()}, 끊은 사람 {count}명");
        }

        public void Dispose() => Shutdown();

        private void NoteArrivalsAndDepartures()
        {
            _seen.Clear();
            foreach (var player in _session.Players)
            {
                _seen.Add(player.PlayerId);
                if (_known.Add(player.PlayerId)) Log($"들어옴: {player.PlayerId} (지금 {_session.Players.Count}명)");
            }

            if (_known.Count == _seen.Count) return;
            _gone.Clear();
            foreach (var id in _known)
                if (!_seen.Contains(id)) _gone.Add(id);
            foreach (var id in _gone)
            {
                _known.Remove(id);
                Log($"나감: {id} (지금 {_session.Players.Count}명)");
            }
        }

        private string DayText() => $"{_session.WorldTime.Day}일째 때 {_session.WorldTime.PartOfDay}";

        private void Log(string line) => _log?.Invoke(line);
    }
}
