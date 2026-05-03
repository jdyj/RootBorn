using System.Collections;
using Rootborn.Game.Knowledge;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Knowledge
{
    public sealed class ToolUnlockToast : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _label;
        [SerializeField] private float _duration = 3f;

        private KnowledgeProgress _progress;

        public void Bind(KnowledgeProgress progress)
        {
            if (_progress != null) _progress.OnUnlocked -= HandleUnlocked;
            _progress = progress;
            if (_progress != null) _progress.OnUnlocked += HandleUnlocked;
            if (_root != null) _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_progress != null) _progress.OnUnlocked -= HandleUnlocked;
        }

        private void HandleUnlocked(KnowledgeNode node)
        {
            StopAllCoroutines();
            StartCoroutine(Show(node));
        }

        private IEnumerator Show(KnowledgeNode node)
        {
            if (_root != null) _root.SetActive(true);
            if (_label != null) _label.text = $"새 지식 발견: {node.DisplayKey}";
            yield return new WaitForSeconds(_duration);
            if (_root != null) _root.SetActive(false);
        }
    }
}
