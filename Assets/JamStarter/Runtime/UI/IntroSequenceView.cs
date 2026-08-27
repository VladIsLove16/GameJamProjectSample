using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JamStarter
{
    [DisallowMultipleComponent]
    public sealed class IntroSequenceView : MonoBehaviour
    {
        [SerializeField] private GameObject[] panels = Array.Empty<GameObject>();
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button startButton;

        private int currentIndex;
        private bool isOpen;
        private bool buttonsBound;

        public event Action Completed;

        public bool IsOpen => isOpen;

        public void Configure(
            GameObject[] introPanels,
            TMP_Text progress,
            Button next,
            Button back,
            Button skip,
            Button start)
        {
            panels = introPanels ?? Array.Empty<GameObject>();
            progressLabel = progress;
            nextButton = next;
            backButton = back;
            skipButton = skip;
            startButton = start;
            BindButtons();
        }

        private void Awake()
        {
            BindButtons();
            isOpen = false;
        }

        private void Start()
        {
            // An inactive tutorial receives Awake only when Show activates it.
            // Hiding it from Awake would immediately cancel that first Show call.
            if (!isOpen)
            {
                Hide();
            }
        }

        private void BindButtons()
        {
            if (buttonsBound)
            {
                return;
            }

            nextButton?.onClick.AddListener(Next);
            backButton?.onClick.AddListener(Back);
            skipButton?.onClick.AddListener(Skip);
            startButton?.onClick.AddListener(Complete);
            buttonsBound = true;
        }

        public void Show()
        {
            isOpen = true;
            currentIndex = 0;
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            isOpen = false;
            gameObject.SetActive(false);
        }

        public void Next()
        {
            if (!isOpen)
            {
                return;
            }

            if (currentIndex >= panels.Length - 1)
            {
                Complete();
                return;
            }

            currentIndex++;
            Refresh();
        }

        public void Back()
        {
            if (isOpen && currentIndex > 0)
            {
                currentIndex--;
                Refresh();
            }
        }

        public void Skip()
        {
            if (isOpen)
            {
                Complete();
            }
        }

        private void Complete()
        {
            if (!isOpen)
            {
                return;
            }

            Hide();
            Completed?.Invoke();
        }

        private void Refresh()
        {
            for (int index = 0; index < panels.Length; index++)
            {
                if (panels[index] != null)
                {
                    panels[index].SetActive(index == currentIndex);
                }
            }

            if (progressLabel != null)
            {
                progressLabel.text = panels.Length == 0
                    ? string.Empty
                    : $"{currentIndex + 1}/{panels.Length}";
            }

            if (backButton != null)
            {
                backButton.interactable = currentIndex > 0;
            }

            bool lastPanel = currentIndex >= panels.Length - 1;
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(!lastPanel);
            }

            if (startButton != null)
            {
                startButton.gameObject.SetActive(lastPanel);
            }
        }

        private void OnDestroy()
        {
            nextButton?.onClick.RemoveListener(Next);
            backButton?.onClick.RemoveListener(Back);
            skipButton?.onClick.RemoveListener(Skip);
            startButton?.onClick.RemoveListener(Complete);
        }
    }
}
