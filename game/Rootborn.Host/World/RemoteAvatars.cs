using System;
using System.Collections.Generic;
using Rootborn.Core.Characters;
using Rootborn.Core.World;
using Rootborn.Net.Session;
using Rootborn.World;

namespace Rootborn.Host.World
{
    /// <summary>다른 참가자의 겉모습을 그릴 시트: 성장 단계의 몸 + 입은 옷(그리는 순서).</summary>
    public sealed class RemoteLook
    {
        public RemoteLook(CharacterSheet body, IReadOnlyList<CharacterSheet> layers)
        {
            Body = body;
            Layers = layers;
        }

        public CharacterSheet Body { get; }
        public IReadOnlyList<CharacterSheet> Layers { get; }
    }

    /// <summary>같은 맵에 있는 다른 참가자 한 명(그릴 때 쓰는 값).</summary>
    public sealed class RemoteAvatar
    {
        internal RemoteAvatar(string playerId) { PlayerId = playerId; }

        public string PlayerId { get; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public Facing Facing { get; internal set; }
        public bool Walking { get; internal set; }
        public string LookCode { get; internal set; }
        /// <summary>겉모습을 풀지 못했으면 null(그리지 않는다).</summary>
        public RemoteLook Look { get; internal set; }
        public CharacterAnimator Animator { get; } = new CharacterAnimator();
        internal bool Seen;
    }

    /// <summary>
    /// 같은 맵의 다른 참가자들(Unity 판처럼 이름표 없이 몸만 그린다. 몸은 걷기를 막지 않는다 — 충돌 없음).
    /// Host가 프레임마다 Net이 메운 위치(<see cref="SessionClient.PlayersOn"/>)를 넘기면 사람마다 걷기 칸을 돌리고, 맵에 없는 사람은 뺀다.
    /// 겉모습 코드(Core WearableLookCode)는 처음 볼 때 한 번 풀어 둔다. 사람 수만큼의 객체는 들어올 때 한 번만 만든다(프레임마다 새로 만들지 않는다).
    /// </summary>
    public sealed class RemoteAvatars
    {
        private readonly CharacterArt _art;
        private readonly WearableCatalog _wearables;
        private readonly Dictionary<ushort, RemoteAvatar> _byNetId = new Dictionary<ushort, RemoteAvatar>(8);
        private readonly Dictionary<string, RemoteLook> _looks = new Dictionary<string, RemoteLook>(StringComparer.Ordinal);
        private readonly List<RemoteAvatar> _visible = new List<RemoteAvatar>(8);
        private readonly List<ushort> _gone = new List<ushort>(8);

        public RemoteAvatars(CharacterArt art, WearableCatalog wearables)
        {
            _art = art ?? throw new ArgumentNullException(nameof(art));
            _wearables = wearables;
        }

        /// <summary>지금 그릴 사람(겉모습을 푼 사람만).</summary>
        public IReadOnlyList<RemoteAvatar> Visible => _visible;

        /// <summary>이번 프레임에 같은 맵에 있는 남. 돌려준 목록(Visible)은 다음 부를 때 다시 쓴다.</summary>
        public void Update(IReadOnlyList<RemotePlayerView> players, float dt)
        {
            foreach (RemoteAvatar a in _byNetId.Values) a.Seen = false;
            _visible.Clear();
            for (int i = 0; i < players.Count; i++)
            {
                RemotePlayerView p = players[i];
                if (!_byNetId.TryGetValue(p.NetId, out RemoteAvatar a))
                {
                    a = new RemoteAvatar(p.PlayerId);
                    _byNetId[p.NetId] = a;
                }
                a.Seen = true;
                a.X = p.X;
                a.Y = p.Y;
                a.Facing = p.Facing;
                a.Walking = p.Walking;
                if (!ReferenceEquals(a.LookCode, p.Look) && a.LookCode != p.Look)
                {
                    a.LookCode = p.Look;
                    a.Look = Resolve(p.Look);
                }
                a.Animator.Advance(p.Walking, p.Facing, dt);
                if (a.Look != null) _visible.Add(a);
            }

            _gone.Clear();
            foreach (var pair in _byNetId)
                if (!pair.Value.Seen) _gone.Add(pair.Key);
            for (int i = 0; i < _gone.Count; i++) _byNetId.Remove(_gone[i]);
        }

        public void Clear()
        {
            _byNetId.Clear();
            _visible.Clear();
        }

        /// <summary>겉모습 코드 → 시트(처음 한 번만). 모르는 단계·옷이면 null.</summary>
        private RemoteLook Resolve(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            if (_looks.TryGetValue(code, out RemoteLook cached)) return cached;
            RemoteLook look = null;
            if (WearableLookCode.TryDecode(_wearables, code, out string stage, out List<string> ids))
            {
                CharacterSheet body = _art.BodyFor(stage);
                if (body != null)
                {
                    var layers = new List<CharacterSheet>(ids.Count);
                    foreach (string id in ids)
                    {
                        CharacterSheet sheet = _art.WearableSheet(id, stage);
                        if (sheet != null) layers.Add(sheet);
                    }
                    look = new RemoteLook(body, layers);
                }
            }
            _looks[code] = look;
            return look;
        }
    }
}
