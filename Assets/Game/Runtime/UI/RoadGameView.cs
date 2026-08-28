using System;
using System.Collections.Generic;
using System.Text;
using JamStarter;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace RoadOfLife
{
    /// <summary>
    /// Coordinates authored Canvas objects. It contains presentation logic only;
    /// game rules and input decisions stay in the flow controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadGameView : MonoBehaviour
    {
        [Header("Screen roots")]
        [SerializeField] private GameObject drivingPanel;
        [SerializeField] private GameObject choiceResultPanel;
        [SerializeField] private GameObject upgradePanel;
        [SerializeField] private GameObject endingPanel;

        [Header("Road card")]
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text eventLabel;
        [SerializeField] private TMP_Text leftChoiceLabel;
        [SerializeField] private TMP_Text rightChoiceLabel;
        [SerializeField] private TMP_Text leftPreviewLabel;
        [SerializeField] private TMP_Text rightPreviewLabel;
        [SerializeField] private CanvasGroup leftChoiceGroup;
        [SerializeField] private CanvasGroup rightChoiceGroup;
        [SerializeField] private CardSwipeView cardSwipeView;
        [SerializeField] private RoadCardPresentationView cardPresentationView;
        [SerializeField] private RoadVehicleView vehicleView;
        [SerializeField] private RoadAudioView audioView;
        [SerializeField] private RoadAudioLibrary audioLibrary;
        [SerializeField] private Sprite defaultCardSprite;
        [SerializeField] private Sprite snowCardSprite;
        [SerializeField] private Sprite engineCardSprite;
        [SerializeField] private Sprite iceCardSprite;
        [SerializeField] private Sprite visibilityCardSprite;
        [SerializeField] private Sprite crackCardSprite;
        [SerializeField] private Sprite alarmCardSprite;
        [SerializeField] private Sprite campCardSprite;
        [SerializeField] private Sprite lightingCardSprite;

        [Header("Audio fallback")]
        [SerializeField] private AudioClip cardChoiceSound;
        [SerializeField] private AudioClip consequenceSound;
        [SerializeField] private AudioClip upgradeSound;
        [SerializeField] private AudioClip failureSound;
        [SerializeField] private AudioClip victorySound;
        [SerializeField] private AudioClip startEngineSound;
        [SerializeField] private AudioClip drivingMusic;

        [Header("Stats")]
        [SerializeField] private bool showExactStats = true;
        [SerializeField] private BipolarStatView tempoView;
        [SerializeField] private BipolarStatView engineView;
        [SerializeField] private BipolarStatView visibilityView;
        [SerializeField] private BipolarStatView loadView;

        [Header("Choice result and controls")]
        [SerializeField] private TMP_Text choiceResultHeaderLabel;
        [SerializeField] private TMP_Text choiceResultLabel;
        [SerializeField] private TMP_Text controlsHintLabel;
        [SerializeField] private Button continueButton;

        [Header("Upgrade choice")]
        [SerializeField] private TMP_Text upgradeHeaderLabel;
        [SerializeField] private Button[] upgradeButtons = new Button[4];
        [SerializeField] private TMP_Text[] upgradeNameLabels = new TMP_Text[4];
        [SerializeField] private TMP_Text[] upgradeDescriptionLabels = new TMP_Text[4];
        [SerializeField] private GameObject[] upgradeAcquiredMarks = new GameObject[4];
        [SerializeField] private RoadUpgrade[] upgradeOrder =
        {
            RoadUpgrade.RoadMarkers,
            RoadUpgrade.WarmingPoint,
            RoadUpgrade.PreparedDetour,
            RoadUpgrade.LoadingPost,
        };

        [Header("Ending")]
        [SerializeField] private TMP_Text endingTitleLabel;
        [SerializeField] private TMP_Text endingBodyLabel;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button menuButton;

        [Header("Choice emphasis")]
        [SerializeField, Range(0f, 1f)] private float idleChoiceAlpha = 0.72f;
        [SerializeField, Range(0f, 1f)] private float mutedChoiceAlpha = 0.42f;

        private string leftPreviewContent = string.Empty;
        private string rightPreviewContent = string.Empty;
        private bool buttonsBound;
        private bool swipeBound;
        private bool showChoiceHints = true;
        private SettingsService settingsService;
        private AudioService audioService;
        private Image fallbackCardImage;
        private bool fallbackAudioStarted;
        private AudioSource localSfxSource;
        private AudioSource localMusicSource;

        public event Action RestartRequested;
        public event Action MenuRequested;
        public event Action ContinueRequested;
        public event Action<RoadUpgrade> UpgradeSelected;
        public event Action<ChoiceSide> ChoiceCommitted;
        public event Action<ChoiceSide> PreviewChanged;

        public CardSwipeView SwipeView => cardSwipeView;

        public void ConfigurePresentation(
            RoadCardPresentationView cardPresentation,
            RoadVehicleView vehicle,
            RoadAudioView audio)
        {
            cardPresentationView = cardPresentation;
            vehicleView = vehicle;
            audioView = audio;
        }

        private void Awake()
        {
            BindButtons();
            BindSwipe();
            fallbackCardImage = cardSwipeView != null ? cardSwipeView.GetComponent<Image>() : null;
            SetControlsHint("Мышь/сенсор: потяните карточку  •  Клавиатура/геймпад: ← → и подтвердить");
        }

        [Inject]
        private void Construct(SettingsService settings, AudioService audio)
        {
            settingsService = settings;
            audioService = audio;
            SetExactStatsVisible(settings.Current.ShowExactStats);
            SetChoiceHintsVisible(settings.Current.ShowChoiceHints);
            settingsService.Changed += OnSettingsChanged;
        }

        private void Start()
        {
            if (fallbackAudioStarted || audioView != null)
            {
                return;
            }

            fallbackAudioStarted = true;
            PlaySfxClip(GetStartEngineSound());
            PlayMusicClip(GetDrivingMusic());
        }

        private void OnSettingsChanged(GameSettingsSnapshot settings)
        {
            SetExactStatsVisible(settings.ShowExactStats);
            SetChoiceHintsVisible(settings.ShowChoiceHints);
        }

        /// <summary>
        /// Assigns the complete set of authored Canvas references. This method is
        /// intentionally explicit so an editor scene builder can serialize the setup
        /// without reflection or runtime object construction.
        /// </summary>
        public void Configure(
            GameObject drivingRoot,
            GameObject choiceResultRoot,
            GameObject upgradeRoot,
            GameObject endingRoot,
            TMP_Text progress,
            TMP_Text eventText,
            TMP_Text leftChoice,
            TMP_Text rightChoice,
            TMP_Text leftPreview,
            TMP_Text rightPreview,
            CanvasGroup leftGroup,
            CanvasGroup rightGroup,
            CardSwipeView swipe,
            BipolarStatView tempo,
            BipolarStatView engine,
            BipolarStatView visibility,
            BipolarStatView load,
            TMP_Text choiceResult,
            TMP_Text choiceResultHeader,
            TMP_Text controlsHint,
            Button continueAction,
            TMP_Text upgradeHeader,
            Button[] upgradeActions,
            TMP_Text[] upgradeNames,
            TMP_Text[] upgradeDescriptions,
            GameObject[] acquiredMarks,
            RoadUpgrade[] orderedUpgrades,
            TMP_Text endingTitle,
            TMP_Text endingBody,
            Button restartAction,
            Button menuAction)
        {
            bool shouldRebind = Application.isPlaying && isActiveAndEnabled;
            if (shouldRebind)
            {
                UnbindButtons();
                UnbindSwipe();
            }

            drivingPanel = drivingRoot;
            choiceResultPanel = choiceResultRoot;
            upgradePanel = upgradeRoot;
            endingPanel = endingRoot;
            progressLabel = progress;
            eventLabel = eventText;
            leftChoiceLabel = leftChoice;
            rightChoiceLabel = rightChoice;
            leftPreviewLabel = leftPreview;
            rightPreviewLabel = rightPreview;
            leftChoiceGroup = leftGroup;
            rightChoiceGroup = rightGroup;
            cardSwipeView = swipe;
            tempoView = tempo;
            engineView = engine;
            visibilityView = visibility;
            loadView = load;
            choiceResultLabel = choiceResult;
            choiceResultHeaderLabel = choiceResultHeader;
            controlsHintLabel = controlsHint;
            continueButton = continueAction;
            upgradeHeaderLabel = upgradeHeader;
            upgradeButtons = upgradeActions;
            upgradeNameLabels = upgradeNames;
            upgradeDescriptionLabels = upgradeDescriptions;
            upgradeAcquiredMarks = acquiredMarks;
            upgradeOrder = orderedUpgrades;
            endingTitleLabel = endingTitle;
            endingBodyLabel = endingBody;
            restartButton = restartAction;
            menuButton = menuAction;

            if (shouldRebind)
            {
                BindButtons();
                BindSwipe();
            }

            RefreshChoicePreview(ChoiceSide.None);
        }

        public void ShowRoadCard(
            string eventText,
            string leftChoiceText,
            string rightChoiceText,
            string leftPreviewText,
            string rightPreviewText,
            RoadCard card)
        {
            ShowDriving();
            SetPanelActive(choiceResultPanel, false);
            SetText(eventLabel, eventText);
            SetText(leftChoiceLabel, leftChoiceText);
            SetText(rightChoiceLabel, rightChoiceText);
            if (cardPresentationView != null)
            {
                cardPresentationView.Show(card);
            }
            else
            {
                ShowFallbackCardPresentation(card);
            }
            vehicleView?.PlayEventAnimation();
            leftPreviewContent = leftPreviewText ?? string.Empty;
            rightPreviewContent = rightPreviewText ?? string.Empty;
            RefreshChoicePreview(ChoiceSide.None);

            if (cardSwipeView != null)
            {
                cardSwipeView.ResetToCenter();
                cardSwipeView.SetInteractable(true);
            }
        }

        public void ShowChoiceResult(string resultText, StatDelta appliedDelta)
        {
            if (cardSwipeView != null)
            {
                cardSwipeView.SetInteractable(false);
            }

            SetText(choiceResultLabel, showExactStats
                ? ComposeResult(resultText, appliedDelta)
                : resultText);
            SetText(choiceResultHeaderLabel, "ПОСЛЕДСТВИЯ");
            SetPanelActive(choiceResultPanel, true);
        }

        public void ShowSessionIntroduction(string text)
        {
            if (cardSwipeView != null)
            {
                cardSwipeView.SetInteractable(false);
            }

            SetText(choiceResultLabel, text);
            SetText(choiceResultHeaderLabel, "ВСТУПЛЕНИЕ");
            SetPanelActive(choiceResultPanel, true);
        }

        public void SetProgress(
            int tripNumber,
            int totalTrips,
            JourneyPhase phase,
            int cardNumber,
            int cardsPerLeg)
        {
            string phaseText = phase == JourneyPhase.ToCity ? "В город" : "Из города";
            SetText(progressLabel,
                $"Рейс {Mathf.Max(1, tripNumber)}/{Mathf.Max(1, totalTrips)} • {phaseText} • " +
                $"{Mathf.Max(1, cardNumber)}/{Mathf.Max(1, cardsPerLeg)}");
        }

        public void SetStats(StatSnapshot stats, bool animate = true)
        {
            SetExactStatsVisible(showExactStats);
            tempoView?.SetValue(stats.Tempo, animate);
            engineView?.SetValue(stats.Engine, animate);
            visibilityView?.SetValue(stats.Visibility, animate);
            loadView?.SetValue(stats.Load, animate);
            vehicleView?.SetStats(stats);
        }

        public void SetExactStatsVisible(bool visible)
        {
            showExactStats = visible;
            tempoView?.SetExactValueVisible(visible);
            engineView?.SetExactValueVisible(visible);
            visibilityView?.SetExactValueVisible(visible);
            loadView?.SetExactValueVisible(visible);
        }

        public void SetChoiceHintsVisible(bool visible)
        {
            showChoiceHints = visible;
            RefreshChoicePreview(cardSwipeView != null ? cardSwipeView.CurrentPreview : ChoiceSide.None);
        }

        public void ShowDriving()
        {
            SetPanelActive(drivingPanel, true);
            SetPanelActive(upgradePanel, false);
            SetPanelActive(endingPanel, false);
        }

        public void ShowUpgradeChoice(IReadOnlyCollection<RoadUpgrade> acquiredUpgrades)
        {
            if (cardSwipeView != null)
            {
                cardSwipeView.SetInteractable(false);
            }

            SetPanelActive(drivingPanel, false);
            SetPanelActive(choiceResultPanel, false);
            SetPanelActive(upgradePanel, true);
            SetPanelActive(endingPanel, false);
            SetText(upgradeHeaderLabel, "Подготовка к следующему рейсу");

            int optionCount = GetUpgradeOptionCount();
            for (int index = 0; index < optionCount; index++)
            {
                RoadUpgrade upgrade = GetUpgradeAt(index);
                bool acquired = ContainsUpgrade(acquiredUpgrades, upgrade);
                SetText(GetAt(upgradeNameLabels, index), GetUpgradeName(upgrade));
                SetText(GetAt(upgradeDescriptionLabels, index), GetUpgradeDescription(upgrade));

                Button button = GetAt(upgradeButtons, index);
                if (button != null)
                {
                    button.interactable = !acquired;
                }

                SetPanelActive(GetAt(upgradeAcquiredMarks, index), acquired);
            }
        }

        public void ShowVictory(string bodyText = null, string journalText = null)
        {
            PlayVictory();
            ShowEnding(
                "Дорога пройдена",
                ComposeEndingBody(
                    string.IsNullOrWhiteSpace(bodyText)
                    ? "Три рейса завершены. Груз доставлен через Ладогу."
                    : bodyText,
                    journalText));
        }

        public void ShowDefeat(string bodyText, string journalText = null)
        {
            PlayFailure();
            ShowEnding(
                "Рейс не завершён",
                ComposeEndingBody(
                    string.IsNullOrWhiteSpace(bodyText)
                    ? "Машина не смогла продолжить путь."
                    : bodyText,
                    journalText));
        }

        private static string ComposeEndingBody(string body, string journal)
        {
            return string.IsNullOrWhiteSpace(journal) ? body : body + "\n\n" + journal;
        }

        public void SetControlsHint(string hint)
        {
            SetText(controlsHintLabel, hint);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            cardSwipeView?.SetInteractable(enabled);
        }

        public void FocusFirstAvailableUpgrade()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            int optionCount = GetUpgradeOptionCount();
            for (int index = 0; index < optionCount; index++)
            {
                Button option = GetAt(upgradeButtons, index);
                if (option != null && option.IsActive() && option.IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(option.gameObject);
                    return;
                }
            }

            EventSystem.current.SetSelectedGameObject(null);
        }

        public void FocusContinue()
        {
            FocusButton(continueButton);
        }

        public void FocusRestart()
        {
            FocusButton(restartButton);
        }

        public static string GetUpgradeName(RoadUpgrade upgrade)
        {
            return upgrade switch
            {
                RoadUpgrade.RoadMarkers => "Вешки на льду",
                RoadUpgrade.WarmingPoint => "Пункт обогрева",
                RoadUpgrade.PreparedDetour => "Разведанный объезд",
                RoadUpgrade.LoadingPost => "Погрузочный пост",
                _ => "Улучшение маршрута",
            };
        }

        public static string GetUpgradeDescription(RoadUpgrade upgrade)
        {
            return upgrade switch
            {
                RoadUpgrade.RoadMarkers => "После событий с видимостью возвращает её на 8 пунктов к середине.",
                RoadUpgrade.WarmingPoint => "После снега или проблем с мотором возвращает нагрев на 8 пунктов к середине.",
                RoadUpgrade.PreparedDetour => "После событий на льду возвращает темп на 8 пунктов к середине.",
                RoadUpgrade.LoadingPost => "После событий с грузом возвращает загрузку на 8 пунктов к середине.",
                _ => string.Empty,
            };
        }

        private void ShowEnding(string title, string body)
        {
            if (cardSwipeView != null)
            {
                cardSwipeView.SetInteractable(false);
            }

            SetPanelActive(drivingPanel, false);
            SetPanelActive(choiceResultPanel, false);
            SetPanelActive(upgradePanel, false);
            SetPanelActive(endingPanel, true);
            SetText(endingTitleLabel, title);
            SetText(endingBodyLabel, body);
        }

        private void BindButtons()
        {
            if (buttonsBound)
            {
                return;
            }

            continueButton?.onClick.AddListener(OnContinueClicked);
            restartButton?.onClick.AddListener(OnRestartClicked);
            menuButton?.onClick.AddListener(OnMenuClicked);
            GetAt(upgradeButtons, 0)?.onClick.AddListener(OnUpgradeZeroClicked);
            GetAt(upgradeButtons, 1)?.onClick.AddListener(OnUpgradeOneClicked);
            GetAt(upgradeButtons, 2)?.onClick.AddListener(OnUpgradeTwoClicked);
            GetAt(upgradeButtons, 3)?.onClick.AddListener(OnUpgradeThreeClicked);
            buttonsBound = true;
        }

        private void UnbindButtons()
        {
            if (!buttonsBound)
            {
                return;
            }

            continueButton?.onClick.RemoveListener(OnContinueClicked);
            restartButton?.onClick.RemoveListener(OnRestartClicked);
            menuButton?.onClick.RemoveListener(OnMenuClicked);
            GetAt(upgradeButtons, 0)?.onClick.RemoveListener(OnUpgradeZeroClicked);
            GetAt(upgradeButtons, 1)?.onClick.RemoveListener(OnUpgradeOneClicked);
            GetAt(upgradeButtons, 2)?.onClick.RemoveListener(OnUpgradeTwoClicked);
            GetAt(upgradeButtons, 3)?.onClick.RemoveListener(OnUpgradeThreeClicked);
            buttonsBound = false;
        }

        private void BindSwipe()
        {
            if (swipeBound || cardSwipeView == null)
            {
                return;
            }

            cardSwipeView.ChoiceCommitted += OnChoiceCommitted;
            cardSwipeView.PreviewChanged += OnPreviewChanged;
            swipeBound = true;
        }

        private void UnbindSwipe()
        {
            if (!swipeBound || cardSwipeView == null)
            {
                swipeBound = false;
                return;
            }

            cardSwipeView.ChoiceCommitted -= OnChoiceCommitted;
            cardSwipeView.PreviewChanged -= OnPreviewChanged;
            swipeBound = false;
        }

        private void OnChoiceCommitted(ChoiceSide side)
        {
            PlayCardChoice();
            ChoiceCommitted?.Invoke(side);
        }

        private void OnPreviewChanged(ChoiceSide side)
        {
            RefreshChoicePreview(side);
            PreviewChanged?.Invoke(side);
        }

        private void RefreshChoicePreview(ChoiceSide side)
        {
            bool leftSelected = side == ChoiceSide.Left;
            bool rightSelected = side == ChoiceSide.Right;

            if (leftChoiceGroup != null)
            {
                leftChoiceGroup.alpha = rightSelected ? mutedChoiceAlpha : leftSelected ? 1f : idleChoiceAlpha;
            }

            if (rightChoiceGroup != null)
            {
                rightChoiceGroup.alpha = leftSelected ? mutedChoiceAlpha : rightSelected ? 1f : idleChoiceAlpha;
            }

            if (leftPreviewLabel != null)
            {
                leftPreviewLabel.text = leftPreviewContent;
                leftPreviewLabel.gameObject.SetActive(showChoiceHints && leftSelected && !string.IsNullOrWhiteSpace(leftPreviewContent));
            }

            if (rightPreviewLabel != null)
            {
                rightPreviewLabel.text = rightPreviewContent;
                rightPreviewLabel.gameObject.SetActive(showChoiceHints && rightSelected && !string.IsNullOrWhiteSpace(rightPreviewContent));
            }
        }

        private void OnContinueClicked()
        {
            PlayConsequence();
            ContinueRequested?.Invoke();
        }

        private void OnRestartClicked()
        {
            RestartRequested?.Invoke();
        }

        private void OnMenuClicked()
        {
            MenuRequested?.Invoke();
        }

        private void OnUpgradeZeroClicked()
        {
            SelectUpgradeAt(0);
        }

        private void OnUpgradeOneClicked()
        {
            SelectUpgradeAt(1);
        }

        private void OnUpgradeTwoClicked()
        {
            SelectUpgradeAt(2);
        }

        private void OnUpgradeThreeClicked()
        {
            SelectUpgradeAt(3);
        }

        private void SelectUpgradeAt(int index)
        {
            Button button = GetAt(upgradeButtons, index);
            if (button == null || !button.interactable || index >= GetUpgradeOptionCount())
            {
                return;
            }

            button.interactable = false;
            PlayUpgrade();
            UpgradeSelected?.Invoke(GetUpgradeAt(index));
        }

        private void ShowFallbackCardPresentation(RoadCard card)
        {
            if (fallbackCardImage == null)
            {
                return;
            }

            if (defaultCardSprite != null)
            {
                fallbackCardImage.sprite = defaultCardSprite;
                fallbackCardImage.preserveAspect = true;
                fallbackCardImage.color = Color.white;
            }
        }

        private Sprite GetFallbackCardSprite(RoadCard card)
        {
            if (card == null)
            {
                return defaultCardSprite;
            }

            switch (card.Id)
            {
                case "fresh_ice_crack":
                case "road_repair_timber":
                    return crackCardSprite != null ? crackCardSprite : iceCardSprite;
                case "air_alarm":
                    return alarmCardSprite;
                case "warming_point":
                    return campCardSprite != null ? campCardSprite : engineCardSprite;
                case "road_crew_lighting":
                    return lightingCardSprite != null ? lightingCardSprite : visibilityCardSprite;
            }

            return card.Tag switch
            {
                CardTag.Engine => engineCardSprite,
                CardTag.Ice => iceCardSprite,
                CardTag.Visibility => visibilityCardSprite,
                CardTag.Alarm => alarmCardSprite,
                CardTag.Snow => snowCardSprite,
                _ => defaultCardSprite,
            };
        }

        private void PlayCardChoice()
        {
            if (audioView != null) audioView.PlayCardChoice();
            else PlaySfxClip(GetCardChoiceSound());
        }

        private void PlayConsequence()
        {
            if (audioView != null) audioView.PlayConsequence();
            else PlaySfxClip(GetConsequenceSound());
        }

        private void PlayUpgrade()
        {
            if (audioView != null) audioView.PlayUpgrade();
            else PlaySfxClip(GetUpgradeSound());
        }

        private void PlayFailure()
        {
            if (audioView != null) audioView.PlayFailure();
            else PlaySfxClip(GetFailureSound());
        }

        private void PlayVictory()
        {
            if (audioView != null) audioView.PlayVictory();
            else PlaySfxClip(GetVictorySound());
        }

        private void PlaySfxClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            EnsureLocalAudioSources();
            float volume = audioLibrary != null ? audioLibrary.SfxVolume : 1f;
            localSfxSource.PlayOneShot(clip, volume);
            audioService?.PlaySfx(clip, volume);
        }

        private void PlayMusicClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            EnsureLocalAudioSources();
            localMusicSource.clip = clip;
            localMusicSource.loop = true;
            localMusicSource.volume = audioLibrary != null ? audioLibrary.MusicVolume : 0.45f;
            localMusicSource.spatialBlend = 0f;
            localMusicSource.Play();
            audioService?.PlayMusic(clip, true, 0.2f);
        }

        private void EnsureLocalAudioSources()
        {
            if (localSfxSource == null)
            {
                localSfxSource = gameObject.AddComponent<AudioSource>();
                localSfxSource.playOnAwake = false;
                localSfxSource.loop = false;
                localSfxSource.spatialBlend = 0f;
                localSfxSource.volume = 1f;
            }

            if (localMusicSource == null)
            {
                localMusicSource = gameObject.AddComponent<AudioSource>();
                localMusicSource.playOnAwake = false;
                localMusicSource.loop = true;
                localMusicSource.spatialBlend = 0f;
            }
        }

        private AudioClip GetCardChoiceSound() =>
            audioLibrary != null && audioLibrary.CardChoiceSound != null ? audioLibrary.CardChoiceSound : cardChoiceSound;

        private AudioClip GetConsequenceSound() =>
            audioLibrary != null && audioLibrary.ConsequenceSound != null ? audioLibrary.ConsequenceSound : consequenceSound;

        private AudioClip GetUpgradeSound() =>
            audioLibrary != null && audioLibrary.UpgradeSound != null ? audioLibrary.UpgradeSound : upgradeSound;

        private AudioClip GetFailureSound() =>
            audioLibrary != null && audioLibrary.FailureSound != null ? audioLibrary.FailureSound : failureSound;

        private AudioClip GetVictorySound() =>
            audioLibrary != null && audioLibrary.VictorySound != null ? audioLibrary.VictorySound : victorySound;

        private AudioClip GetStartEngineSound() =>
            audioLibrary != null && audioLibrary.StartEngineSound != null ? audioLibrary.StartEngineSound : startEngineSound;

        private AudioClip GetDrivingMusic() =>
            audioLibrary != null && audioLibrary.DrivingMusic != null ? audioLibrary.DrivingMusic : drivingMusic;

        private int GetUpgradeOptionCount()
        {
            int orderCount = upgradeOrder != null ? upgradeOrder.Length : 0;
            int buttonCount = upgradeButtons != null ? upgradeButtons.Length : 0;
            return Mathf.Min(4, Mathf.Min(orderCount, buttonCount));
        }

        private RoadUpgrade GetUpgradeAt(int index)
        {
            if (upgradeOrder != null && index >= 0 && index < upgradeOrder.Length)
            {
                return upgradeOrder[index];
            }

            return (RoadUpgrade)Mathf.Clamp(index, 0, 3);
        }

        private static bool ContainsUpgrade(
            IReadOnlyCollection<RoadUpgrade> acquiredUpgrades,
            RoadUpgrade candidate)
        {
            if (acquiredUpgrades == null)
            {
                return false;
            }

            foreach (RoadUpgrade upgrade in acquiredUpgrades)
            {
                if (upgrade == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ComposeResult(string resultText, StatDelta delta)
        {
            StringBuilder builder = new StringBuilder(resultText ?? string.Empty);
            AppendDelta(builder, "Темп", delta.Tempo);
            AppendDelta(builder, "Мотор", delta.Engine);
            AppendDelta(builder, "Видимость", delta.Visibility);
            AppendDelta(builder, "Груз", delta.Load);
            return builder.ToString();
        }

        private static void AppendDelta(StringBuilder builder, string label, int value)
        {
            if (value == 0)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(label);
            builder.Append(' ');
            builder.Append(value > 0 ? "+" : "−");
            builder.Append(Mathf.Abs(value));
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null && panel.activeSelf != active)
            {
                panel.SetActive(active);
            }
        }

        private static void FocusButton(Button button)
        {
            if (EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(
                button != null && button.IsActive() && button.IsInteractable() ? button.gameObject : null);
        }

        private static T GetAt<T>(T[] array, int index) where T : class
        {
            return array != null && index >= 0 && index < array.Length ? array[index] : null;
        }

        private void OnDestroy()
        {
            if (settingsService != null)
            {
                settingsService.Changed -= OnSettingsChanged;
            }
            UnbindButtons();
            UnbindSwipe();
        }
    }
}
