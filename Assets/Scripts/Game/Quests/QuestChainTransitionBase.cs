using UnityEngine;

namespace Rootborn.Game.Quests
{
    public abstract class QuestChainTransitionBase : ScriptableObject
    {
        [SerializeField] private string _id;
        public string Id => _id;
        public virtual string ResolveNextStepId(QuestChainDefinition chain, QuestChainProgress progress) => string.Empty;

        public void ConfigureForTests(string id)
        {
            _id = id;
        }
    }
}
