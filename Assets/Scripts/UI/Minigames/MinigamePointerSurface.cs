using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Rootborn.UI.Minigames
{
    [DisallowMultipleComponent]
    public sealed class MinigamePointerSurface : MonoBehaviour, IPointerClickHandler
    {
        private RectTransform _rect;
        public Action<Vector2> Clicked;
        private void Awake() => _rect=(RectTransform)transform;
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || eventData.button!=PointerEventData.InputButton.Left) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect,eventData.position,eventData.pressEventCamera,out var local)) return;
            var bounds=_rect.rect;
            if (!bounds.Contains(local)) return;
            Clicked?.Invoke(new Vector2((local.x-bounds.xMin)/bounds.width,(local.y-bounds.yMin)/bounds.height));
        }
    }
}
