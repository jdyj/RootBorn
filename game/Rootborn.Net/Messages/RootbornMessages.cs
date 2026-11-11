using Rootborn.Core.Characters;
using Rootborn.Core.Network;
using Rootborn.Core.World;

namespace Rootborn.Net.Messages
{
    /// <summary>클라이언트 → 서버: 들어가고 싶다. 신원은 안정된 id 문자열(지금은 넘겨받은 값, 나중에 Steam id).</summary>
    public sealed class HelloMessage
    {
        public string PlayerId;
        public string Look;
    }

    /// <summary>서버 → 클라이언트: 들어왔다. NetId는 이 서버가 그 신원에 붙인 번호(다시 들어와도 같다). 도착 위치는 Core SpawnSpread(칸 단위).</summary>
    public sealed class WelcomeMessage
    {
        public ushort NetId;
        public int JoinIndex;
        public float SpawnOffsetX;
        public float SpawnOffsetY;
    }

    /// <summary>끊는 쪽이 끊김에 싣는 이유(정원 초과는 Core SessionCapacity의 이유 문자열).</summary>
    public sealed class FarewellMessage
    {
        public string Reason;
    }

    /// <summary>서버 → 클라이언트: 다른 사람이 있다(늦게 들어온 사람에게는 지금 있는 사람마다 하나).</summary>
    public sealed class PlayerJoinedMessage
    {
        public ushort NetId;
        public string PlayerId;
        public string Look;
        public bool HasPose;
        public PoseMessage Pose = new PoseMessage();
    }

    public sealed class PlayerLeftMessage
    {
        public ushort NetId;
    }

    /// <summary>
    /// 한 사람의 몸. 주인 → 서버는 NetId를 무시하고(서버가 보낸 연결로 정한다), 서버 → 다른 사람은 주인의 NetId를 채운다.
    /// Sequence와 SenderTime은 주인 것을 그대로 옮긴다(받는 쪽 사이 메우기가 쓴다).
    /// </summary>
    public sealed class PoseMessage
    {
        public ushort NetId;
        public uint Sequence;
        public double SenderTime;
        public string MapId = string.Empty;
        public float X;
        public float Y;
        public byte Facing;
        public bool Walking;

        public PoseSample ToSample() => new PoseSample(Sequence, SenderTime, MapId, X, Y, (Rootborn.Core.World.Facing)Facing, Walking);
    }

    /// <summary>겉모습 키(Core WearableLookCode). 받는 쪽은 그릴 때 등록부로 풀어 게임에 있는 그림만 쓴다.</summary>
    public sealed class LookMessage
    {
        public ushort NetId;
        public string Look;
    }

    /// <summary>서버 → 클라이언트: 같이 쓰는 시간과 하루 끝 준비 현황.</summary>
    public sealed class WorldTimeMessage
    {
        public int Day;
        public byte PartOfDay;
        public byte Phase;
        public byte ReadyCount;
        public byte PlayerCount;
    }

    /// <summary>클라이언트 → 서버: 내 하루를 끝냈다.</summary>
    public sealed class DayEndReadyMessage
    {
    }

    /// <summary>클라이언트 → 서버: 다음 때로(Unity 판 AdvanceTimeOfDayServerRpc, 누구나 부를 수 있었다).</summary>
    public sealed class AdvancePartOfDayMessage
    {
    }

    /// <summary>살아 있다는 표시. 아무것도 보내지 않은 동안만 보낸다.</summary>
    public sealed class HeartbeatMessage
    {
    }

    /// <summary>
    /// ROOTBORN 메시지 등록 표. 종류를 더할 때는 아래에 한 줄을 더한다. 번호는 다시 쓰지 않는다.
    /// 몸통 모양을 바꿀 때는 그 줄의 버전을 올리고 읽기에서 옛 버전을 읽는다.
    /// </summary>
    public static class RootbornMessages
    {
        public const int MaxPlayerIdBytes = 64;
        public const int MaxMapIdBytes = 64;
        public const int MaxReasonBytes = 128;
        public const int MaxLookBytes = WearableLookCode.MaxBytes;

        public static MessageCatalog CreateCatalog(int maxMessageBytes)
        {
            var catalog = new MessageCatalog(maxMessageBytes);
            Register(catalog);
            return catalog;
        }

        public static void Register(MessageCatalog c)
        {
            c.Add<HelloMessage>(1, 1, WriteHello, ReadHello);
            c.Add<WelcomeMessage>(2, 1, WriteWelcome, ReadWelcome);
            c.Add<FarewellMessage>(3, 1, (w, m) => w.WriteString(m.Reason), ReadFarewell);
            c.Add<PlayerJoinedMessage>(4, 1, WriteJoined, ReadJoined);
            c.Add<PlayerLeftMessage>(5, 1, (w, m) => w.WriteUInt16(m.NetId), ReadLeft);
            c.Add<PoseMessage>(6, 1, WritePose, ReadPose);
            c.Add<LookMessage>(7, 1, WriteLook, ReadLook);
            c.Add<WorldTimeMessage>(8, 1, WriteWorldTime, ReadWorldTime);
            c.Add<DayEndReadyMessage>(9, 1, (w, m) => { }, Empty);
            c.Add<AdvancePartOfDayMessage>(10, 1, (w, m) => { }, Empty);
            c.Add<HeartbeatMessage>(11, 1, (w, m) => { }, Empty);
        }

        private static bool Empty<T>(NetReader r, int version, out T message) where T : class, new()
        {
            message = new T();
            return true;
        }

        private static void WriteHello(NetWriter w, HelloMessage m)
        {
            w.WriteString(m.PlayerId);
            w.WriteString(m.Look);
        }

        private static bool ReadHello(NetReader r, int version, out HelloMessage m)
        {
            m = new HelloMessage();
            return r.TryReadString(MaxPlayerIdBytes, out m.PlayerId) && r.TryReadString(MaxLookBytes, out m.Look);
        }

        private static void WriteWelcome(NetWriter w, WelcomeMessage m)
        {
            w.WriteUInt16(m.NetId);
            w.WriteInt32(m.JoinIndex);
            w.WriteSingle(m.SpawnOffsetX);
            w.WriteSingle(m.SpawnOffsetY);
        }

        private static bool ReadWelcome(NetReader r, int version, out WelcomeMessage m)
        {
            m = new WelcomeMessage();
            return r.TryReadUInt16(out m.NetId) && r.TryReadInt32(out m.JoinIndex) && r.TryReadSingle(out m.SpawnOffsetX) && r.TryReadSingle(out m.SpawnOffsetY);
        }

        private static bool ReadFarewell(NetReader r, int version, out FarewellMessage m)
        {
            m = new FarewellMessage();
            return r.TryReadString(MaxReasonBytes, out m.Reason);
        }

        private static void WriteJoined(NetWriter w, PlayerJoinedMessage m)
        {
            w.WriteUInt16(m.NetId);
            w.WriteString(m.PlayerId);
            w.WriteString(m.Look);
            w.WriteBool(m.HasPose);
            if (m.HasPose) WritePoseBody(w, m.Pose);
        }

        private static bool ReadJoined(NetReader r, int version, out PlayerJoinedMessage m)
        {
            m = new PlayerJoinedMessage();
            if (!r.TryReadUInt16(out m.NetId) || !r.TryReadString(MaxPlayerIdBytes, out m.PlayerId) || !r.TryReadString(MaxLookBytes, out m.Look)) return false;
            if (!r.TryReadBool(out m.HasPose)) return false;
            if (!m.HasPose) return true;
            m.Pose.NetId = m.NetId;
            return ReadPoseBody(r, m.Pose);
        }

        private static bool ReadLeft(NetReader r, int version, out PlayerLeftMessage m)
        {
            m = new PlayerLeftMessage();
            return r.TryReadUInt16(out m.NetId);
        }

        private static void WritePose(NetWriter w, PoseMessage m)
        {
            w.WriteUInt16(m.NetId);
            WritePoseBody(w, m);
        }

        private static bool ReadPose(NetReader r, int version, out PoseMessage m)
        {
            m = new PoseMessage();
            return r.TryReadUInt16(out m.NetId) && ReadPoseBody(r, m);
        }

        private static void WritePoseBody(NetWriter w, PoseMessage m)
        {
            w.WriteUInt32(m.Sequence);
            w.WriteDouble(m.SenderTime);
            w.WriteString(m.MapId);
            w.WriteSingle(m.X);
            w.WriteSingle(m.Y);
            w.WriteByte(m.Facing);
            w.WriteBool(m.Walking);
        }

        private static bool ReadPoseBody(NetReader r, PoseMessage m)
        {
            return r.TryReadUInt32(out m.Sequence) && r.TryReadDouble(out m.SenderTime) && r.TryReadString(MaxMapIdBytes, out m.MapId)
                   && m.MapId.Length > 0 && r.TryReadSingle(out m.X) && r.TryReadSingle(out m.Y)
                   && r.TryReadByte(out m.Facing) && m.Facing <= (byte)Facing.Right && r.TryReadBool(out m.Walking);
        }

        private static void WriteLook(NetWriter w, LookMessage m)
        {
            w.WriteUInt16(m.NetId);
            w.WriteString(m.Look);
        }

        private static bool ReadLook(NetReader r, int version, out LookMessage m)
        {
            m = new LookMessage();
            return r.TryReadUInt16(out m.NetId) && r.TryReadString(MaxLookBytes, out m.Look);
        }

        private static void WriteWorldTime(NetWriter w, WorldTimeMessage m)
        {
            w.WriteInt32(m.Day);
            w.WriteByte(m.PartOfDay);
            w.WriteByte(m.Phase);
            w.WriteByte(m.ReadyCount);
            w.WriteByte(m.PlayerCount);
        }

        private static bool ReadWorldTime(NetReader r, int version, out WorldTimeMessage m)
        {
            m = new WorldTimeMessage();
            return r.TryReadInt32(out m.Day) && m.Day >= 1 && r.TryReadByte(out m.PartOfDay) && m.PartOfDay < WorldPartOfDay.Count
                   && r.TryReadByte(out m.Phase) && m.Phase < WorldSchedulePhase.Count && r.TryReadByte(out m.ReadyCount) && r.TryReadByte(out m.PlayerCount);
        }
    }
}
