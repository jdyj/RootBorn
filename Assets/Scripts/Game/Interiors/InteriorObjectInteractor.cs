using Rootborn.Game.Player;
        using UnityEngine;
        
        namespace Rootborn.Game.Interiors
        {
            [DisallowMultipleComponent]
            public sealed class InteriorObjectInteractor : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
            {
                [SerializeField] private InteriorInteractionDefinition _definition;
                [SerializeField] private InteriorObjectKind _objectKind;
                [SerializeField] private Vector2Int _cell;
        
                private GameObject _lastInteractingPlayer;
                private int _interactionCount;
        
                public InteriorObjectKind ObjectKind => _objectKind;
                public Vector2Int Cell => _cell;
                public int InteractionCount => _interactionCount;
                public GameObject LastInteractingPlayer => _lastInteractingPlayer;
                public string InteractionPrompt => _definition != null ? _definition.Prompt : string.Empty;
                public Vector3 InteractionPromptOffset => _definition != null ? _definition.PromptOffset : Vector3.up;
                public Transform InteractionTransform => transform;
                public int InteractionPriority => _definition != null ? _definition.Priority : 0;
        
                public void Bind(InteriorInteractionDefinition definition, InteriorPlacedObject placedObject)
                {
                    _definition = definition;
                    _objectKind = placedObject.ObjectKind;
                    _cell = placedObject.Cell;
                }
        
                public bool CanInteract(GameObject player)
                {
                    return _definition != null && player != null && isActiveAndEnabled;
                }
        
                public bool TryInteract(GameObject player)
                {
                    if (!CanInteract(player))
                    {
                        return false;
                    }
        
                    _lastInteractingPlayer = player;
                    _interactionCount++;
                    return true;
                }
            }
        }
        