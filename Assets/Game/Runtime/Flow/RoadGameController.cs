using System;
using System.Collections.Generic;
using System.Text;
using JamStarter;
using UnityEngine;
using Zenject;

namespace RoadOfLife
{
    /// <summary>
    /// Connects the authored Canvas, the three-trip session and JamStarter services.
    /// It never creates UI objects at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadGameController : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private TextAsset cardsSource;
        [SerializeField] private RoadGameView gameView;
        [SerializeField] private SandboxController sandboxController;
        [SerializeField] private IntroSequenceView firstLaunchTutorial;

        private InputReader input;
        private SceneLoader scenes;
        private SettingsService settings;
        private RoadGameSession session;
        private ChoiceResolution pendingResolution;
        private readonly List<string> tripJournal = new List<string>(RoadGameSession.TotalTrips);
        private ChoiceSide navigationChoice;
        private bool initialized;
        private bool choiceInProgress;
        private bool sessionIntroductionPending;
        private int sessionIntroductionIndex;

        private static readonly string[] SessionIntroductions =
        {
            "Ледовая трасса\n\nЛенинград окружён, но связь с Большой землёй не прервалась. По льду Ладожского озера идут машины с продовольствием и возвращаются с эвакуированными людьми.",
            "Новая смена\n\nДля города каждый рейс имеет значение. Проведите грузовик по Дороге жизни, доставьте необходимое в Ленинград и помогите вывезти людей из блокады.",
            "Через Ладогу\n\nЗима превратила озеро в опасную дорогу. Лёд, метель и темнота ждут впереди, но именно здесь проходит путь, который поддерживает жизнь осаждённого города.",
            "Цена рейса\n\nЗа каждым ящиком груза и каждым пассажиром стоит чья-то жизнь. Вам предстоит сделать несколько рейсов по ледовой трассе, сохранив машину, груз и людей.",
            "Дорога жизни\n\nЛенинград ждёт продовольствие, медикаменты и помощь. Вы отправляетесь на лёд Ладожского озера, чтобы выполнить свой долг и вернуть людей с линии блокады.",
        };

        public RoadGameSession Session => session;

        /// <summary>Assigns scene-owned references. Intended for the editor scene builder.</summary>
        public void Configure(
            TextAsset source,
            RoadGameView view,
            SandboxController sandbox = null)
        {
            cardsSource = source;
            gameView = view;
            sandboxController = sandbox;
        }

        public void ConfigureFirstLaunchTutorial(IntroSequenceView tutorial)
        {
            firstLaunchTutorial = tutorial;
        }

        [Inject]
        private void Construct(
            InputReader inputReader,
            SceneLoader sceneLoader,
            SettingsService settingsService)
        {
            input = inputReader;
            scenes = sceneLoader;
            settings = settingsService;
        }

        private void Start()
        {
            if (input == null)
            {
                Debug.LogError($"{nameof(RoadGameController)} was not injected by the scene context.", this);
                enabled = false;
                return;
            }

            if (cardsSource == null || gameView == null)
            {
                Debug.LogError(
                    $"{nameof(RoadGameController)} requires a card TextAsset and a {nameof(RoadGameView)}.",
                    this);
                enabled = false;
                return;
            }

            BindEvents();
            StartNewSession();
        }

        public void StartNewSession()
        {
            try
            {
                var database = RoadCardDatabase.FromTsv(cardsSource);
                session = new RoadGameSession(database);
                session.Start();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                ShowTerminalResult(
                    "ОШИБКА КАРТОЧЕК",
                    "Не удалось собрать маршрут. Проверьте Cards.tsv.txt и Console.",
                    false);
                return;
            }

            initialized = true;
            pendingResolution = null;
            tripJournal.Clear();
            navigationChoice = ChoiceSide.None;
            choiceInProgress = false;
            sessionIntroductionPending = true;
            sessionIntroductionIndex = UnityEngine.Random.Range(0, SessionIntroductions.Length);
            input.UseGameplay();
            gameView.SetControlsHint(
                "Мышь / сенсор: потяните карточку   •   Клавиатура / геймпад: ← → и подтвердить");
            gameView.SetStats(session.Stats.Snapshot, false);
            PresentCurrentCard();
            if (firstLaunchTutorial != null && !settings.Current.IntroSeen)
            {
                firstLaunchTutorial.Completed += OnFirstLaunchTutorialCompleted;
                gameView.SetInteractionEnabled(false);
                input.UseUI();
                firstLaunchTutorial.Show();
            }
            else
            {
                ShowSessionIntroduction();
            }
        }

        private void BindEvents()
        {
            gameView.ChoiceCommitted += OnChoiceCommitted;
            gameView.ContinueRequested += OnContinueRequested;
            gameView.UpgradeSelected += OnUpgradeSelected;
            gameView.RestartRequested += OnRestartRequested;
            gameView.MenuRequested += OnMenuRequested;

            input.MoveChanged += OnMoveChanged;
            input.PrimaryPressed += OnPrimaryPressed;
            input.SecondaryPressed += OnSecondaryPressed;
        }

        private void UnbindEvents()
        {
            if (gameView != null)
            {
                gameView.ChoiceCommitted -= OnChoiceCommitted;
                gameView.ContinueRequested -= OnContinueRequested;
                gameView.UpgradeSelected -= OnUpgradeSelected;
                gameView.RestartRequested -= OnRestartRequested;
                gameView.MenuRequested -= OnMenuRequested;
            }

            if (input != null)
            {
                input.MoveChanged -= OnMoveChanged;
                input.PrimaryPressed -= OnPrimaryPressed;
                input.SecondaryPressed -= OnSecondaryPressed;
            }
        }

        private void OnFirstLaunchTutorialCompleted()
        {
            settings?.MarkIntroSeen();
            firstLaunchTutorial.Completed -= OnFirstLaunchTutorialCompleted;
            ShowSessionIntroduction();
        }

        private void ShowSessionIntroduction()
        {
            input.UseUI();
            gameView.ShowSessionIntroduction(SessionIntroductions[sessionIntroductionIndex]);
            gameView.FocusContinue();
        }

        private void PresentCurrentCard()
        {
            if (!initialized || session == null || session.Stage != RoadSessionStage.Driving)
            {
                return;
            }

            RoadCard card = session.CurrentCard;
            navigationChoice = ChoiceSide.None;
            choiceInProgress = false;
            pendingResolution = null;

            gameView.SetProgress(
                session.TripNumber,
                RoadGameSession.TotalTrips,
                session.Phase,
                session.CardNumberInLeg,
                RoadGameSession.CardsPerLeg);
            gameView.SetStats(session.Stats.Snapshot, true);
            gameView.ShowRoadCard(
                card.EventText,
                card.LeftChoice.Text,
                card.RightChoice.Text,
                FormatDelta(card.LeftChoice.Delta),
                FormatDelta(card.RightChoice.Delta),
                card);
        }

        private void OnMoveChanged(Vector2 move)
        {
            if (!CanSelectRoadChoice() || Mathf.Abs(move.x) < 0.5f)
            {
                return;
            }

            navigationChoice = move.x < 0f ? ChoiceSide.Left : ChoiceSide.Right;
            gameView.SwipeView?.PreviewChoice(navigationChoice);
        }

        private void OnPrimaryPressed()
        {
            if (pendingResolution != null)
            {
                CompletePendingResolution();
                return;
            }

            if (!CanSelectRoadChoice() || navigationChoice == ChoiceSide.None)
            {
                return;
            }

            choiceInProgress = true;
            gameView.SwipeView?.CommitChoice(navigationChoice);
        }

        private void OnSecondaryPressed()
        {
            if (!CanSelectRoadChoice())
            {
                return;
            }

            navigationChoice = ChoiceSide.None;
            gameView.SwipeView?.PreviewChoice(ChoiceSide.None);
        }

        private bool CanSelectRoadChoice()
        {
            return initialized && session != null &&
                   session.Stage == RoadSessionStage.Driving &&
                   pendingResolution == null && !choiceInProgress;
        }

        private void OnChoiceCommitted(ChoiceSide side)
        {
            if (!initialized || session == null ||
                session.Stage != RoadSessionStage.Driving || pendingResolution != null)
            {
                return;
            }

            choiceInProgress = true;
            navigationChoice = ChoiceSide.None;

            try
            {
                pendingResolution = session.Choose(side);
                RecordCompletedTrip(pendingResolution);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                choiceInProgress = false;
                PresentCurrentCard();
                return;
            }

            string resultText = pendingResolution.Choice.ResultText;
            if (pendingResolution.TriggeredUpgrade.HasValue)
            {
                resultText += $"\n\n{GetUpgradeTriggeredText(pendingResolution.TriggeredUpgrade.Value)}";
            }

            gameView.SetStats(pendingResolution.AfterUpgrades, true);
            gameView.ShowChoiceResult(resultText, pendingResolution.Choice.Delta);
            gameView.FocusContinue();
        }

        private void OnContinueRequested()
        {
            if (sessionIntroductionPending)
            {
                sessionIntroductionPending = false;
                input.UseGameplay();
                PresentCurrentCard();
                return;
            }

            CompletePendingResolution();
        }

        private void CompletePendingResolution()
        {
            if (pendingResolution == null)
            {
                return;
            }

            ChoiceResolution resolution = pendingResolution;
            pendingResolution = null;

            if (resolution.Failure != FailureReason.None || session.Stage == RoadSessionStage.Lost)
            {
                string defeatText = GetFailureText(resolution.Failure);
                gameView.ShowDefeat(defeatText, BuildJourneyJournal());
                ShowTerminalResult(
                    "РЕЙС НЕ ЗАВЕРШЁН",
                    ComposeEndingText(defeatText),
                    false);
                return;
            }

            if (resolution.SessionWon || session.Stage == RoadSessionStage.Won)
            {
                const string victory =
                    "Три рейса через Ладогу завершены. Груз доставлен, люди вывезены на Большую землю.";
                gameView.ShowVictory(victory, BuildJourneyJournal());
                ShowTerminalResult("ДОРОГА ПРОЙДЕНА", ComposeEndingText(victory), true);
                return;
            }

            if (session.Stage == RoadSessionStage.ChoosingUpgrade)
            {
                choiceInProgress = false;
                input.UseUI();
                gameView.ShowUpgradeChoice(session.ActiveUpgrades);
                gameView.FocusFirstAvailableUpgrade();
                return;
            }

            PresentCurrentCard();
        }

        private void OnUpgradeSelected(RoadUpgrade upgrade)
        {
            if (!initialized || session == null || session.Stage != RoadSessionStage.ChoosingUpgrade)
            {
                return;
            }

            try
            {
                session.ChooseUpgrade(upgrade);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                gameView.ShowUpgradeChoice(session.ActiveUpgrades);
                gameView.FocusFirstAvailableUpgrade();
                return;
            }

            input.UseGameplay();
            gameView.SetStats(session.Stats.Snapshot, false);
            PresentCurrentCard();
        }

        private void ShowTerminalResult(string title, string body, bool won)
        {
            initialized = false;
            choiceInProgress = false;
            pendingResolution = null;

            string message = $"{title}\n\n{body}";
            if (sandboxController != null)
            {
                sandboxController.ShowResult(message);
                return;
            }

            input?.UseUI();
            if (won)
            {
                gameView?.ShowVictory(body);
            }
            else
            {
                gameView?.ShowDefeat(body);
            }
        }

        private void RecordCompletedTrip(ChoiceResolution resolution)
        {
            if (!resolution.TripCompleted || resolution.Failure != FailureReason.None)
            {
                return;
            }

            tripJournal.Add(
                $"Рейс {session.CompletedTrips}: " +
                $"{(resolution.Card.Phase == RoadCardPhase.ToCity ? "груз доставлен" : "люди вывезены")}; " +
                $"темп {resolution.AfterUpgrades.Tempo}, мотор {resolution.AfterUpgrades.Engine}, " +
                $"видимость {resolution.AfterUpgrades.Visibility}, груз {resolution.AfterUpgrades.Load}.");
        }

        private string BuildJourneyJournal()
        {
            if (tripJournal.Count == 0)
            {
                return string.Empty;
            }

            return "ЖУРНАЛ СМЕНЫ\n" + string.Join("\n", tripJournal);
        }

        private string ComposeEndingText(string body)
        {
            string journal = BuildJourneyJournal();
            return string.IsNullOrEmpty(journal) ? body : body + "\n\n" + journal;
        }

        private void OnRestartRequested()
        {
            scenes?.ReloadActiveScene();
        }

        private void OnMenuRequested()
        {
            scenes?.LoadScene(SceneNames.MainMenu);
        }

        private static string FormatDelta(StatDelta delta)
        {
            var parts = new List<string>(4);
            AddDelta(parts, "Темп", delta.Tempo);
            AddDelta(parts, "Мотор", delta.Engine);
            AddDelta(parts, "Видимость", delta.Visibility);
            AddDelta(parts, "Груз", delta.Load);
            return string.Join("  •  ", parts);
        }

        private static void AddDelta(ICollection<string> parts, string label, int value)
        {
            if (value != 0)
            {
                parts.Add($"{label} {(value > 0 ? "↑" : "↓")}");
            }
        }

        private static string GetUpgradeTriggeredText(RoadUpgrade upgrade)
        {
            return upgrade switch
            {
                RoadUpgrade.RoadMarkers => "Вешки вернули видимость на 8 пунктов к безопасной середине.",
                RoadUpgrade.WarmingPoint => "Пункт обогрева вернул нагрев мотора на 8 пунктов к середине.",
                RoadUpgrade.PreparedDetour => "Разведанный объезд вернул темп на 8 пунктов к середине.",
                RoadUpgrade.LoadingPost => "Погрузочный пост вернул загрузку на 8 пунктов к середине.",
                _ => "Улучшение маршрута смягчило последствия.",
            };
        }

        private static string GetFailureText(FailureReason reason)
        {
            return reason switch
            {
                FailureReason.TempoLow => "Темп упал до нуля: машина остановилась на льду.",
                FailureReason.TempoHigh => "Темп стал неуправляемым: грузовик сошёл с безопасной колеи.",
                FailureReason.EngineLow => "Двигатель окончательно замёрз посреди Ладоги.",
                FailureReason.EngineHigh => "Двигатель перегрелся и отказал.",
                FailureReason.VisibilityLow => "Ориентиры потеряны: машина сбилась с Дороги жизни.",
                FailureReason.VisibilityHigh => "Слишком яркий свет демаскировал машину.",
                FailureReason.LoadLow => "В кузове не осталось того, ради чего совершался рейс.",
                FailureReason.LoadHigh => "Перегрузка стала критической: машина и лёд больше не выдерживают.",
                _ => "Машина не смогла продолжить путь.",
            };
        }

        private void OnDestroy()
        {
            if (firstLaunchTutorial != null)
            {
                firstLaunchTutorial.Completed -= OnFirstLaunchTutorialCompleted;
            }
            UnbindEvents();
        }
    }
}
