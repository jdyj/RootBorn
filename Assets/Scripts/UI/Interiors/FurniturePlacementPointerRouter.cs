using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.Interiors
{
    [DefaultExecutionOrder(-20000)]
    public sealed class FurniturePlacementPointerRouter : MonoBehaviour
    {
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
        private bool _wasPressed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoaded()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAfterInitialSceneLoad()
        {
            Ensure(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Ensure(scene);
        }

        private static void Ensure(Scene scene)
        {
            if (scene.name != "House" || FindFirstObjectByType<FurniturePlacementPointerRouter>() != null)
            {
                return;
            }

            var go = new GameObject("[FurniturePlacementPointerRouter]", typeof(FurniturePlacementPointerRouter));
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                _wasPressed = false;
                return;
            }

            bool pressed = mouse.leftButton.isPressed;
            if (!pressed)
            {
                _wasPressed = false;
                return;
            }

            if (_wasPressed)
            {
                return;
            }

            _wasPressed = true;
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            var eventData = new PointerEventData(eventSystem)
            {
                position = mouse.position.ReadValue(),
                button = PointerEventData.InputButton.Left,
                pointerId = -1
            };

            _raycastResults.Clear();
            eventSystem.RaycastAll(eventData, _raycastResults);
            for (int i = 0; i < _raycastResults.Count; i++)
            {
                var hit = _raycastResults[i].gameObject;
                if (hit == null || hit.GetComponentInParent<InteriorFurniturePlacementPanel>() == null)
                {
                    continue;
                }

                var button = hit.GetComponentInParent<Button>();
                if (button == null || !button.IsActive() || !button.interactable)
                {
                    continue;
                }

                ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerClickHandler);
                eventSystem.SetSelectedGameObject(button.gameObject, eventData);
                return;
            }
        }
    }
}
