using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JamStarter
{
    [DisallowMultipleComponent]
    public sealed class UIScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Selectable defaultSelection;

        private Coroutine selectionRoutine;

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            if (canvasGroup == null)
            {
                Debug.LogError($"{nameof(UIScreen)} on '{name}' requires a CanvasGroup reference.", this);
                enabled = false;
            }
        }

        public void Show()
        {
            Show(true);
        }

        public void Show(bool selectDefault)
        {
            if (canvasGroup == null)
            {
                return;
            }

            gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            IsVisible = true;

            if (selectDefault && defaultSelection != null && isActiveAndEnabled)
            {
                if (selectionRoutine != null)
                {
                    StopCoroutine(selectionRoutine);
                }

                selectionRoutine = StartCoroutine(SelectOnNextFrame());
            }
        }

        public void Hide()
        {
            if (selectionRoutine != null)
            {
                StopCoroutine(selectionRoutine);
                selectionRoutine = null;
            }

            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            IsVisible = false;
            gameObject.SetActive(false);
        }

        private IEnumerator SelectOnNextFrame()
        {
            yield return null;

            if (IsVisible && defaultSelection != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(defaultSelection.gameObject);
            }

            selectionRoutine = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (canvasGroup == null)
            {
                Debug.LogWarning($"{nameof(UIScreen)} on '{name}' has no CanvasGroup reference.", this);
            }
        }
#endif
    }
}
