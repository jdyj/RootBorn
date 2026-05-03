namespace Rootborn.Game.Status
{
    public sealed class StatusValue
    {
        public StatusEffectDefinition Definition { get; }
        public float Current { get; private set; }

        public StatusValue(StatusEffectDefinition definition, float initial = 0f)
        {
            Definition = definition;
            Current = initial;
        }

        public void Tick(float deltaSeconds, float decayMul = 1f)
        {
            Current += Definition.DecayPerSecond * decayMul * deltaSeconds;
            if (Current < 0f) Current = 0f;
            if (Current > Definition.MaxValue) Current = Definition.MaxValue;
        }

        public void Restore(float amount)
        {
            Current -= amount;
            if (Current < 0f) Current = 0f;
        }

        public float Penalty01() => Definition.EvaluatePenalty01(Current);
    }
}
