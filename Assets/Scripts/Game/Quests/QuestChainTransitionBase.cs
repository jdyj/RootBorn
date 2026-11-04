using UnityEngine;

namespace Rootborn.Game.Quests
{
    public abstract class QuestChainTransitionBase : ScriptableObject
    {
        [SerializeField] private string _id;
        public string Id => _id;
        public virtual string ResolveNextStepId(QuestChainDefinition chain, QuestChainProgress progress) => string.Empty;

        public virtual bool TryResolveNextStep(QuestChainDefinition chain, QuestChainProgress progress, in QuestRuntimeContext context, out string nextStepId)
        {
            nextStepId = ResolveNextStepId(chain, progress);
            return !string.IsNullOrEmpty(nextStepId);
        }

        public void ConfigureForTests(string id)
        {
            _id = id;
        }
    }
}
