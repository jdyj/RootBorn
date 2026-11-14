using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Core.Room
{
    /// <summary>
    /// 같이 쓰는 집에서 돈을 낸 꾸밈 잡기들(<see cref="SaveFileNames.RoomHouseBuildPaid"/>, 플레이어마다 하나). 필드 이름과 형은 바꾸지 않는다.
    /// 보낼 목록(<see cref="RoomOutboxSaveData"/>)과 따로 둔다: 꾸밈 조각을 모르는 옛 빌드는 보낼 목록의 쓰기 줄을 옛 방에 보내고 답(Invalid)을 받아 지우지만,
    /// 이 파일은 모르는 파일이라 건드리지 않는다. 그래서 새 프로그램으로 돌아와도 "이 잡기에 돈을 냈다"가 남는다.
    /// 잡기 id만 적는다 — 방이 잡기마다 꾸밈·칸을 갖고 있고, 쓰기 요청은 잡기 id 하나로 만든다(<see cref="RoomHouseBuildClient.AddUse"/>).
    /// </summary>
    [Serializable]
    public sealed class RoomHouseBuildPaidSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public string[] Holds = Array.Empty<string>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>떠도는 내 잡기(방의 상태에 내 잡기로 있다)를 어떻게 다룰지.</summary>
    public enum RoomHoldStep
    {
        /// <summary>이 판이 이미 다룬다(답을 기다림, 돈을 내는 중, 쓰기·풀기를 보냄) — 아무것도 하지 않는다.</summary>
        Leave = 0,

        /// <summary>돈을 내지 않은 잡기 — 풀기를 적는다(잃는 것이 없다).</summary>
        Release = 1,

        /// <summary>돈을 냈는데 쓰기가 보낼 목록에 없다(옛 빌드가 지웠다) — 쓰기를 새 id로 다시 적는다.</summary>
        SendUse = 2,
    }

    /// <summary>
    /// 돈을 낸 꾸밈 잡기의 표시. 돈을 내는 바꾸기 안에서 <see cref="Mark"/>하고 같은 저장 묶음에 쓴다(돈 빠짐과 표시가 한 묶음).
    /// 방의 상태에서 그 잡기가 없어졌고(쓰기가 적용됐거나 풀렸다) 그 잡기의 요청도 보낼 목록에 없으면 <see cref="ForgetGone"/>이 지운다 — 끝없이 자라지 않게.
    /// 지우는 것은 표시만 바꾸고 저장은 다음 저장 묶음과 함께 간다(읽기만으로 쓰지 않는다).
    /// </summary>
    public sealed class RoomHouseBuildPaidHolds
    {
        private readonly List<string> _holds = new List<string>(2);

        /// <summary>마지막 저장 뒤로 바뀌었다(다음 저장 묶음에 쓴다).</summary>
        public bool Dirty { get; private set; }

        public int Count => _holds.Count;

        public bool Contains(string holdId) => !string.IsNullOrEmpty(holdId) && _holds.Contains(holdId);

        /// <summary>그 잡기에 돈을 냈다. 이미 있으면 그대로.</summary>
        public void Mark(string holdId)
        {
            if (string.IsNullOrEmpty(holdId) || _holds.Contains(holdId)) return;
            _holds.Add(holdId);
            Dirty = true;
        }

        /// <summary>방에 없고(<paramref name="heldInRoom"/>가 false) 보낼 요청도 없는(<paramref name="hasRequestOut"/>가 false) 잡기를 지운다. 지웠으면 true.</summary>
        public bool ForgetGone(Func<string, bool> heldInRoom, Func<string, bool> hasRequestOut)
        {
            if (heldInRoom == null) throw new ArgumentNullException(nameof(heldInRoom));
            if (hasRequestOut == null) throw new ArgumentNullException(nameof(hasRequestOut));
            int removed = _holds.RemoveAll(h => !heldInRoom(h) && !hasRequestOut(h));
            if (removed == 0) return false;
            Dirty = true;
            return true;
        }

        /// <summary>저장 묶음이 커밋됐다.</summary>
        public void Saved() => Dirty = false;

        /// <summary>파일이 없으면(옛 세이브) null — 돈 낸 잡기 없음(지금까지의 동작).</summary>
        public void Load(RoomHouseBuildPaidSaveData data)
        {
            _holds.Clear();
            Dirty = false;
            if (data?.Holds == null) return;
            foreach (var id in data.Holds)
                if (!string.IsNullOrEmpty(id) && !_holds.Contains(id)) _holds.Add(id);
        }

        public RoomHouseBuildPaidSaveData ToSaveData()
            => new RoomHouseBuildPaidSaveData { Version = RoomHouseBuildPaidSaveData.KnownVersion, Holds = _holds.ToArray() };

        /// <summary>
        /// 방의 상태에 있는 내 잡기 하나를 어떻게 다룰지. 이 판이 이미 다루는 잡기(답을 기다림, 돈을 내는 중, 이 판에서 쓰기·풀기를 보냄, 보낼 목록에 그 잡기의
        /// 요청이 있음)는 그대로 둔다. 아니면 돈을 낸 표시가 있으면 쓰기를 다시 적고(돈은 다시 내지 않는다), 없으면 푼다.
        /// </summary>
        public static RoomHoldStep StepFor(bool waiting, bool paying, bool settled, bool requestOut, bool paid)
        {
            if (waiting || paying || settled || requestOut) return RoomHoldStep.Leave;
            return paid ? RoomHoldStep.SendUse : RoomHoldStep.Release;
        }
    }
}
