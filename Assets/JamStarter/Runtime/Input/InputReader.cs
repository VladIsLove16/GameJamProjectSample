using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JamStarter
{
    /// <summary>
    /// Converts the project's Input System actions into a small, stable runtime API.
    /// The configured asset is cloned at runtime, so this component never changes the
    /// enabled state of action maps used by another player or an EventSystem.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InputReader : MonoBehaviour
    {
        private const string GameplayMapName = "Gameplay";
        private const string UiMapName = "UI";

        [Header("Source")]
        [SerializeField] private InputConfiguration configuration;
        [SerializeField] private InputMode initialMode = InputMode.Gameplay;

        private InputActionAsset runtimeActions;
        private InputActionMap gameplayMap;
        private InputActionMap uiMap;

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction primaryAction;
        private InputAction secondaryAction;
        private InputAction interactAction;
        private InputAction pauseAction;

        private InputAction navigateAction;
        private InputAction submitAction;
        private InputAction cancelAction;
        private InputAction pointAction;
        private InputAction clickAction;
        private InputAction scrollWheelAction;

        private bool isSubscribed;
        private bool isInitialized;
        private InputMode mode;

        public event Action<Vector2> MoveChanged;
        public event Action<Vector2> LookChanged;
        public event Action<Vector2> NavigateChanged;
        public event Action<Vector2> PointChanged;
        public event Action<Vector2> ScrollWheelChanged;

        public event Action PrimaryPressed;
        public event Action PrimaryReleased;
        public event Action SecondaryPressed;
        public event Action SecondaryReleased;
        public event Action InteractPressed;
        public event Action PausePressed;
        public event Action SubmitPressed;
        public event Action CancelPressed;
        public event Action ClickPressed;
        public event Action ClickReleased;
        public event Action<InputMode> ModeChanged;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public Vector2 Navigate { get; private set; }
        public Vector2 Point { get; private set; }
        public Vector2 ScrollWheel { get; private set; }

        public bool IsPrimaryPressed { get; private set; }
        public bool IsSecondaryPressed { get; private set; }
        public bool IsClickPressed { get; private set; }
        public InputMode Mode => mode;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (!isInitialized)
            {
                Initialize();
            }

            Subscribe();
            ApplyMode(initialMode, false);
        }

        private void OnDisable()
        {
            Unsubscribe();
            DisableAllMaps();
            ResetState();
        }

        private void OnDestroy()
        {
            if (runtimeActions != null)
            {
                Destroy(runtimeActions);
            }
        }

        /// <summary>Enables only the requested logical map.</summary>
        public void SetMode(InputMode value)
        {
            initialMode = value;
            ApplyMode(value, true);
        }

        public void UseGameplay() => SetMode(InputMode.Gameplay);
        public void UseUI() => SetMode(InputMode.UI);
        public void DisableInput() => SetMode(InputMode.Disabled);

        private void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            InputActionAsset sourceAsset = GetSourceAsset();
            if (sourceAsset == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(InputReader)} on '{name}' requires an action reference backed by an InputActionAsset.");
            }

            runtimeActions = Instantiate(sourceAsset);
            // Fixed: Using this.GetEntityId() for Unity 6.5 compatibility
            runtimeActions.name = $"{sourceAsset.name} (Runtime {this.GetEntityId()})";

            gameplayMap = FindRequiredMap(runtimeActions, GameplayMapName);
            uiMap = FindRequiredMap(runtimeActions, UiMapName);

            moveAction = FindRequiredAction(gameplayMap, "Move");
            lookAction = FindRequiredAction(gameplayMap, "Look");
            primaryAction = FindRequiredAction(gameplayMap, "Primary");
            secondaryAction = FindRequiredAction(gameplayMap, "Secondary");
            interactAction = FindRequiredAction(gameplayMap, "Interact");
            pauseAction = FindRequiredAction(gameplayMap, "Pause");

            navigateAction = FindRequiredAction(uiMap, "Navigate");
            submitAction = FindRequiredAction(uiMap, "Submit");
            cancelAction = FindRequiredAction(uiMap, "Cancel");
            pointAction = FindRequiredAction(uiMap, "Point");
            clickAction = FindRequiredAction(uiMap, "Click");
            scrollWheelAction = FindRequiredAction(uiMap, "ScrollWheel");

            mode = InputMode.Disabled;
            isInitialized = true;
        }

        private void Subscribe()
        {
            if (isSubscribed)
            {
                return;
            }

            SubscribeVector2(moveAction, OnMoveChanged);
            SubscribeVector2(lookAction, OnLookChanged);
            SubscribeVector2(navigateAction, OnNavigateChanged);
            SubscribeVector2(pointAction, OnPointChanged);
            SubscribeVector2(scrollWheelAction, OnScrollWheelChanged);

            primaryAction.started += OnPrimaryStarted;
            primaryAction.performed += OnPrimaryPerformed;
            primaryAction.canceled += OnPrimaryCanceled;
            secondaryAction.started += OnSecondaryStarted;
            secondaryAction.performed += OnSecondaryPerformed;
            secondaryAction.canceled += OnSecondaryCanceled;
            interactAction.performed += OnInteractPerformed;
            pauseAction.performed += OnPausePerformed;

            submitAction.performed += OnSubmitPerformed;
            cancelAction.performed += OnCancelPerformed;
            clickAction.started += OnClickStarted;
            clickAction.performed += OnClickPerformed;
            clickAction.canceled += OnClickCanceled;

            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
            {
                return;
            }

            UnsubscribeVector2(moveAction, OnMoveChanged);
            UnsubscribeVector2(lookAction, OnLookChanged);
            UnsubscribeVector2(navigateAction, OnNavigateChanged);
            UnsubscribeVector2(pointAction, OnPointChanged);
            UnsubscribeVector2(scrollWheelAction, OnScrollWheelChanged);

            primaryAction.started -= OnPrimaryStarted;
            primaryAction.performed -= OnPrimaryPerformed;
            primaryAction.canceled -= OnPrimaryCanceled;
            secondaryAction.started -= OnSecondaryStarted;
            secondaryAction.performed -= OnSecondaryPerformed;
            secondaryAction.canceled -= OnSecondaryCanceled;
            interactAction.performed -= OnInteractPerformed;
            pauseAction.performed -= OnPausePerformed;

            submitAction.performed -= OnSubmitPerformed;
            cancelAction.performed -= OnCancelPerformed;
            clickAction.started -= OnClickStarted;
            clickAction.performed -= OnClickPerformed;
            clickAction.canceled -= OnClickCanceled;

            isSubscribed = false;
        }

        private void ApplyMode(InputMode value, bool notify)
        {
            if (!isInitialized)
            {
                mode = value;
                return;
            }

            DisableAllMaps();

            if (isActiveAndEnabled)
            {
                switch (value)
                {
                    case InputMode.Gameplay:
                        gameplayMap.Enable();
                        break;
                    case InputMode.UI:
                        uiMap.Enable();
                        break;
                    case InputMode.Disabled:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown input mode.");
                }
            }

            bool changed = mode != value;
            mode = value;
            ResetState();

            if (notify && changed)
            {
                ModeChanged?.Invoke(mode);
            }
        }

        private void DisableAllMaps()
        {
            gameplayMap?.Disable();
            uiMap?.Disable();
        }

        private void ResetState()
        {
            Move = Vector2.zero;
            Look = Vector2.zero;
            Navigate = Vector2.zero;
            Point = Vector2.zero;
            ScrollWheel = Vector2.zero;
            IsPrimaryPressed = false;
            IsSecondaryPressed = false;
            IsClickPressed = false;
        }

        private static InputActionMap FindRequiredMap(InputActionAsset asset, string mapName)
        {
            InputActionMap map = asset.FindActionMap(mapName, false);
            if (map == null)
            {
                throw new InvalidOperationException(
                    $"Input action asset '{asset.name}' must contain a '{mapName}' action map.");
            }

            return map;
        }

        private static InputAction FindRequiredAction(InputActionMap map, string actionName)
        {
            InputAction action = map.FindAction(actionName, false);
            if (action == null)
            {
                throw new InvalidOperationException(
                    $"Input action map '{map.name}' must contain a '{actionName}' action.");
            }

            return action;
        }

        private static void SubscribeVector2(
            InputAction action,
            Action<InputAction.CallbackContext> callback)
        {
            action.performed += callback;
            action.canceled += callback;
        }

        private static void UnsubscribeVector2(
            InputAction action,
            Action<InputAction.CallbackContext> callback)
        {
            action.performed -= callback;
            action.canceled -= callback;
        }

        private void OnMoveChanged(InputAction.CallbackContext context)
        {
            Move = context.ReadValue<Vector2>();
            MoveChanged?.Invoke(Move);
        }

        private void OnLookChanged(InputAction.CallbackContext context)
        {
            Look = context.ReadValue<Vector2>();
            LookChanged?.Invoke(Look);
        }

        private void OnNavigateChanged(InputAction.CallbackContext context)
        {
            Navigate = context.ReadValue<Vector2>();
            NavigateChanged?.Invoke(Navigate);
        }

        private void OnPointChanged(InputAction.CallbackContext context)
        {
            Point = context.ReadValue<Vector2>();
            PointChanged?.Invoke(Point);
        }

        private void OnScrollWheelChanged(InputAction.CallbackContext context)
        {
            ScrollWheel = context.ReadValue<Vector2>();
            ScrollWheelChanged?.Invoke(ScrollWheel);
        }

        private void OnPrimaryStarted(InputAction.CallbackContext context) => IsPrimaryPressed = true;

        private void OnPrimaryPerformed(InputAction.CallbackContext context)
        {
            IsPrimaryPressed = true;
            PrimaryPressed?.Invoke();
        }

        private void OnPrimaryCanceled(InputAction.CallbackContext context)
        {
            IsPrimaryPressed = false;
            PrimaryReleased?.Invoke();
        }

        private void OnSecondaryStarted(InputAction.CallbackContext context) => IsSecondaryPressed = true;

        private void OnSecondaryPerformed(InputAction.CallbackContext context)
        {
            IsSecondaryPressed = true;
            SecondaryPressed?.Invoke();
        }

        private void OnSecondaryCanceled(InputAction.CallbackContext context)
        {
            IsSecondaryPressed = false;
            SecondaryReleased?.Invoke();
        }

        private void OnInteractPerformed(InputAction.CallbackContext context) => InteractPressed?.Invoke();
        private void OnPausePerformed(InputAction.CallbackContext context) => PausePressed?.Invoke();
        private void OnSubmitPerformed(InputAction.CallbackContext context) => SubmitPressed?.Invoke();
        private void OnCancelPerformed(InputAction.CallbackContext context) => CancelPressed?.Invoke();
        private void OnClickStarted(InputAction.CallbackContext context) => IsClickPressed = true;

        private void OnClickPerformed(InputAction.CallbackContext context)
        {
            IsClickPressed = true;
            ClickPressed?.Invoke();
        }

        private void OnClickCanceled(InputAction.CallbackContext context)
        {
            IsClickPressed = false;
            ClickReleased?.Invoke();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            InputActionAsset sourceAsset = GetSourceAsset();
            if (sourceAsset == null)
            {
                return;
            }

            ValidateEditorMap(sourceAsset, GameplayMapName,
                "Move", "Look", "Primary", "Secondary", "Interact", "Pause");
            ValidateEditorMap(sourceAsset, UiMapName,
                "Navigate", "Submit", "Cancel", "Point", "Click", "ScrollWheel");
        }

        private void ValidateEditorMap(InputActionAsset asset, string mapName, params string[] actionNames)
        {
            InputActionMap map = asset.FindActionMap(mapName, false);
            if (map == null)
            {
                Debug.LogWarning(
                    $"Input action asset '{asset.name}' has no '{mapName}' map.", this);
                return;
            }

            foreach (string actionName in actionNames)
            {
                if (map.FindAction(actionName, false) == null)
                {
                    Debug.LogWarning(
                        $"Input action map '{mapName}' has no '{actionName}' action.", this);
                }
            }
        }
#endif

        private InputActionAsset GetSourceAsset()
        {
            return configuration != null ? configuration.Actions : null;
        }
    }
}