using System.Collections.Generic;
using Rootborn.Game.Characters.Spum;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.MainMenu
{
    public sealed class SpumCharacterCreatorPreview : MonoBehaviour
    {
        public void Build(IReadOnlyList<SpumPartDefinition> weaponParts)
        {
            ClearGeneratedChildren();
            if (weaponParts == null || weaponParts.Count == 0)
                return;

            var weaponPreview = new GameObject("WeaponPreview", typeof(RectTransform), typeof(Image));
            weaponPreview.transform.SetParent(transform, false);
            var rect = (RectTransform)weaponPreview.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-24f, 0f);
            rect.sizeDelta = new Vector2(96f, 96f);

            var image = weaponPreview.GetComponent<Image>();
            image.sprite = weaponParts[0] != null ? weaponParts[0].PreviewSprite : null;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private void ClearGeneratedChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }
    }
}
