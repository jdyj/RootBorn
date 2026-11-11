using System;
using Rootborn.Core.Save;

namespace Rootborn.Core.Inventory
{
    /// <summary>
    /// 돈을 주는 쪽. 지금은 플레이어 개인 지갑(<see cref="PersonalWallet"/>) 하나다. 멀티플레이에서 돈을 방이 같이 쓰면
    /// 방 지갑으로 바꿔 끼운다(멀티플레이 설계 4절). 규칙 코드와 화면은 어느 쪽인지 모른다.
    /// 가방(<see cref="ItemInventory"/>)에 통화 아이템을 주고받으면 이 지갑이 바뀐다.
    /// </summary>
    public interface IWallet
    {
        int Balance { get; }

        /// <summary>cost를 빼고 reward를 넣을 수 있는지. 음수, 잔액 부족, 상한 넘침이면 false.</summary>
        bool CanExchange(int cost, int reward);

        /// <summary>
        /// 전부 아니면 전무: 잔액을 바꾸고 commit을 부른다. commit이 던지면 잔액을 되돌리고 다시 던진다.
        /// 바뀌었으면 commit 뒤에 <see cref="Changed"/>를 알린다. 알림 중에는 다시 바꿀 수 없다.
        /// </summary>
        bool TryExchange(int cost, int reward, Action commit);

        event Action Changed;
    }

    /// <summary>플레이어 한 명의 지갑. 잔액은 0 이상 <see cref="MaxBalance"/> 이하다.</summary>
    public sealed class PersonalWallet : IWallet
    {
        /// <summary>
        /// 상한은 int 범위다(넘침 방지). 데이터에 정한 상한은 없다. 가방에 있을 때는 칸 수 × 9,999가 사실상의 상한이었다.
        /// </summary>
        public const int MaxBalance = int.MaxValue;

        private int _balance;
        private bool _inProgress;

        public int Balance => _balance;

        public event Action Changed;

        public bool CanExchange(int cost, int reward)
            => !_inProgress && cost >= 0 && reward >= 0 && cost <= _balance && (long)_balance - cost + reward <= MaxBalance;

        public bool TryExchange(int cost, int reward, Action commit)
        {
            if (!CanExchange(cost, reward)) return false;
            _inProgress = true;
            int previous = _balance;
            try
            {
                _balance = previous - cost + reward;
                try { commit?.Invoke(); }
                catch
                {
                    _balance = previous;
                    throw;
                }

                if (_balance != previous) Changed?.Invoke();
                return true;
            }
            finally { _inProgress = false; }
        }

        public WalletSaveData ToSaveData() => new WalletSaveData { Version = WalletSaveData.KnownVersion, Balance = _balance };

        /// <summary>저장된 잔액으로 바꾼다. null이면 0이다(지갑이 없던 세이브). 음수는 0으로 읽는다.</summary>
        public void LoadFromSaveData(WalletSaveData data)
        {
            int balance = data == null ? 0 : Math.Max(0, data.Balance);
            if (balance == _balance) return;
            _balance = balance;
            Changed?.Invoke();
        }
    }

    /// <summary>지갑의 세이브 모양. 성인 직업 스냅샷(<c>AdultCareerSnapshotSaveData.Wallet</c>) 안에 들어간다.</summary>
    [Serializable]
    public sealed class WalletSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public int Balance;

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }
}
