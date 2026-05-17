using System;

namespace Rootborn.Game.Housing
{
    public enum HouseUpgradeRouteKind
    {
        None,
        HireConstruction,
        DirectConstruction
    }

    [Serializable]
    public sealed class HouseCurrencySaveData
    {
        public int Balance;
    }

    public sealed class HouseCurrencyWallet
    {
        public HouseCurrencyWallet(int balance)
        {
            Balance = Math.Max(0, balance);
        }

        public int Balance { get; private set; }

        public bool CanSpend(int amount)
        {
            return amount >= 0 && Balance >= amount;
        }

        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount)) return false;
            Balance -= amount;
            return true;
        }

        public void Grant(int amount)
        {
            if (amount > 0) Balance += amount;
        }
    }
}