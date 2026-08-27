using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JamStarter;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoadOfLife.Editor
{
    /// <summary>
    /// Authors the prototype UI as normal, serialized Canvas objects in Sandbox.
    /// The runtime never constructs or wires interface objects.
    /// </summary>
    public static class RoadGameSceneBuilder
    {
        private const string MenuRoot = "Tools/Road of Life";
        private const string SandboxScenePath = "Assets/Scenes/Sandbox.unity";
        private const string CardsAssetPath = "Assets/Game/Data/Cards.tsv.txt";
        private const string UiRootName = "RoadGame UI";
        public const string AutomationRequestPath = "Temp/RoadOfLife.BuildPrototypeScene.request";
        public const string PrefabConversionRequestPath = "Temp/RoadOfLife.ConvertUiToPrefabs.request";
        public const string SandboxSettingsPrefabRequestPath = "Temp/RoadOfLife.UseSandboxSettingsPrefab.request";

        private static readonly Color Background = new Color32(5, 14, 25, 255);
        private static readonly Color Sky = new Color32(9, 31, 50, 255);
        private static readonly Color Ice = new Color32(21, 55, 70, 255);
        private static readonly Color Road = new Color32(9, 34, 49, 245);
        private static readonly Color Panel = new Color32(8, 23, 36, 242);
        private static readonly Color PanelRaised = new Color32(15, 42, 57, 248);
        private static readonly Color Frost = new Color32(216, 236, 241, 255);
        private static readonly Color Muted = new Color32(131, 166, 177, 255);
        private static readonly Color Accent = new Color32(84, 180, 190, 255);
        private static readonly Color Warm = new Color32(220, 188, 111, 255);
        private static readonly Color Danger = new Color32(177, 65, 57, 255);

        [MenuItem(MenuRoot + "/Build Prototype Scene", false, 100)]
        public static void BuildPrototypeScene()
        {
            if (!BuildPrototypeSceneInternal(true))
            {
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(SandboxScenePath, OpenSceneMode.Single);
            }
        }

        [MenuItem(MenuRoot + "/Add First-Launch Tutorial To Game Scene", false, 103)]
        public static void AddFirstLaunchTutorialToGameScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play Mode before adding the first-launch tutorial.");
                return;
            }

            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            Scene previousActive = SceneManager.GetActiveScene();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(SandboxScenePath, OpenSceneMode.Single);
                Transform uiRoot = FindDescendant(FindComponentInScene<Canvas>(scene, "Sandbox Canvas")?.transform, UiRootName);
                Transform flow = FindRoot(scene, "Sandbox Flow");
                RoadGameController controller = flow?.GetComponent<RoadGameController>();
                if (uiRoot == null || controller == null)
                {
                    throw new InvalidOperationException("Sandbox must contain RoadGame UI and RoadGameController.");
                }

                DeleteDescendantIfPresent(uiRoot, "First Launch Tutorial");
                IntroSequenceView tutorial = BuildTutorialOverlay(uiRoot);
                controller.ConfigureFirstLaunchTutorial(tutorial);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, SandboxScenePath);
                Debug.Log("Road of Life first-launch tutorial was added to Sandbox.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                RestoreSceneSetup(previousSetup, previousActive, default, false);
            }
        }

        private static IntroSequenceView BuildTutorialOverlay(Transform parent)
        {
            RectTransform root = CreatePanel(
                "First Launch Tutorial",
                parent,
                new Color(Background.r, Background.g, Background.b, 0.99f),
                Vector2.zero,
                Vector2.one,
                true);
            IntroSequenceView view = root.gameObject.AddComponent<IntroSequenceView>();
            GameObject[] panels = new GameObject[5];
            string[] headings = { "ЦЕЛЬ РЕЙСА", "ДОРОГА ЖИЗНИ", "КАК ИГРАТЬ", "ХАРАКТЕРИСТИКИ", "НАЧАЛО ПУТИ" };
            string[] bodies =
            {
                "Доставьте груз в Ленинград, а затем вывезите людей через замёрзшее Ладожское озеро.",
                "Дорога жизни была единственной ледовой связью осаждённого города с Большой землёй.",
                "Потяните карточку влево или вправо, чтобы выбрать решение. Подтвердите последствия кнопкой.",
                "Следите за темпом, двигателем, видимостью и нагрузкой. Край шкалы означает поражение.",
                "Между рейсами выбирайте улучшения трассы. Проведите три рейса и завершите смену.",
            };

            for (int index = 0; index < panels.Length; index++)
            {
                RectTransform panel = CreatePanel(
                    $"Tutorial Panel {index + 1}",
                    root,
                    PanelRaised,
                    new Vector2(0.24f, 0.24f),
                    new Vector2(0.76f, 0.78f),
                    false);
                CreateText($"Tutorial Heading {index + 1}", panel, headings[index], 34f, Warm, TextAlignmentOptions.Center);
                Place(panel.GetChild(0).GetComponent<RectTransform>(), new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.9f));
                CreateText($"Tutorial Body {index + 1}", panel, bodies[index], 24f, Frost, TextAlignmentOptions.Center);
                Place(panel.GetChild(1).GetComponent<RectTransform>(), new Vector2(0.1f, 0.24f), new Vector2(0.9f, 0.65f));
                panels[index] = panel.gameObject;
            }

            TMP_Text progress = CreateText("Tutorial Progress", root, "1/5", 18f, Muted, TextAlignmentOptions.Center);
            Place(progress.rectTransform, new Vector2(0.42f, 0.15f), new Vector2(0.58f, 0.2f));
            Button back = CreateButton("Tutorial Back", root, "НАЗАД", new Vector2(0.18f, 0.06f), new Vector2(0.37f, 0.13f), Panel, 18f, out _);
            Button next = CreateButton("Tutorial Next", root, "ДАЛЕЕ", new Vector2(0.63f, 0.06f), new Vector2(0.82f, 0.13f), Accent, 18f, out _);
            Button skip = CreateButton("Tutorial Skip", root, "ПРОПУСТИТЬ", new Vector2(0.4f, 0.02f), new Vector2(0.6f, 0.07f), Panel, 15f, out _);
            Button start = CreateButton("Tutorial Start", root, "НАЧАТЬ РЕЙС", new Vector2(0.58f, 0.06f), new Vector2(0.82f, 0.13f), Accent, 18f, out _);
            view.Configure(panels, progress, next, back, skip, start);
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>
        /// Automation-safe entry point used by the one-shot request hook. It never
        /// shows a modal dialog and returns whether the scene was built and saved.
        /// </summary>
        public static bool BuildPrototypeSceneNoDialogs()
        {
            return BuildPrototypeSceneInternal(false);
        }

        private static bool BuildPrototypeSceneInternal(bool showDialogs)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Road of Life UI cannot be rebuilt while entering or running Play Mode.");
                return false;
            }

            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            Scene previousActive = SceneManager.GetActiveScene();
            bool sceneWasLoaded = TryGetLoadedSandbox(out Scene sandboxScene);
            bool succeeded = false;

            try
            {
                if (!sceneWasLoaded)
                {
                    sandboxScene = EditorSceneManager.OpenScene(SandboxScenePath, OpenSceneMode.Additive);
                }

                BuildInScene(sandboxScene);
                EditorSceneManager.MarkSceneDirty(sandboxScene);
                if (!EditorSceneManager.SaveScene(sandboxScene, SandboxScenePath))
                {
                    throw new InvalidOperationException($"Unity could not save {SandboxScenePath}.");
                }

                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"Road of Life prototype UI was built as serialized Canvas objects in {SandboxScenePath}.");
                succeeded = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(
                        "Road of Life",
                        "Не удалось собрать интерфейс. Подробности находятся в Console.",
                        "OK");
                }
            }
            finally
            {
                RestoreSceneSetup(previousSetup, previousActive, sandboxScene, sceneWasLoaded);
            }

            return succeeded;
        }

        [MenuItem(MenuRoot + "/Build Prototype Scene", true)]
        private static bool CanBuildPrototypeScene() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(MenuRoot + "/Validate Prototype Scene", false, 101)]
        public static void ValidatePrototypeScene()
        {
            ValidatePrototypeSceneInternal(true);
        }

        /// <summary>Validates the saved hierarchy without opening a modal dialog.</summary>
        public static bool ValidatePrototypeSceneNoDialogs()
        {
            return ValidatePrototypeSceneInternal(false);
        }

        private static bool ValidatePrototypeSceneInternal(bool showDialogs)
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            Scene previousActive = SceneManager.GetActiveScene();
            bool sceneWasLoaded = TryGetLoadedSandbox(out Scene sandboxScene);
            bool valid = false;

            try
            {
                if (!sceneWasLoaded)
                {
                    sandboxScene = EditorSceneManager.OpenScene(SandboxScenePath, OpenSceneMode.Additive);
                }

                List<string> problems = CollectValidationProblems(sandboxScene);
                if (problems.Count == 0)
                {
                    Debug.Log("Road of Life scene validation passed: Canvas hierarchy and serialized references are ready.");
                    if (showDialogs)
                    {
                        EditorUtility.DisplayDialog("Road of Life", "Сцена готова: все обязательные ссылки назначены.", "OK");
                    }

                    valid = true;
                }
                else
                {
                    string report = string.Join("\n", problems.Select(problem => "• " + problem));
                    Debug.LogError("Road of Life scene validation failed:\n" + report);
                    if (showDialogs)
                    {
                        EditorUtility.DisplayDialog(
                            "Road of Life",
                            "Найдены проблемы:\n\n" + report + "\n\nЗапустите Build Prototype Scene.",
                            "OK");
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                RestoreSceneSetup(previousSetup, previousActive, sandboxScene, sceneWasLoaded);
            }

            return valid;
        }

        [InitializeOnLoadMethod]
        private static void RegisterAutomationRequestHook()
        {
            EditorApplication.update -= ConsumeAutomationRequest;
            EditorApplication.update += ConsumeAutomationRequest;
        }

        private static void ConsumeAutomationRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string absoluteRequestPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", AutomationRequestPath));
            string absolutePrefabRequestPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", PrefabConversionRequestPath));
            string absoluteSettingsPrefabRequestPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", SandboxSettingsPrefabRequestPath));
            if (!File.Exists(absoluteRequestPath) && !File.Exists(absolutePrefabRequestPath) &&
                !File.Exists(absoluteSettingsPrefabRequestPath))
            {
                return;
            }

            // Consume before acting, so a failed build never loops every editor frame.
            bool convertPrefabs = File.Exists(absolutePrefabRequestPath);
            bool useSettingsPrefab = File.Exists(absoluteSettingsPrefabRequestPath);
            string requestPath = useSettingsPrefab
                ? absoluteSettingsPrefabRequestPath
                : convertPrefabs ? absolutePrefabRequestPath : absoluteRequestPath;
            File.Delete(requestPath);
            EditorApplication.update -= ConsumeAutomationRequest;

            if (useSettingsPrefab)
            {
                RoadSandboxSettingsPrefabTool.ReplaceSandboxSettingsScreen();
                return;
            }

            if (convertPrefabs)
            {
                RoadUiPrefabConverter.ConvertUiToPrefabs();
                return;
            }

            bool built = BuildPrototypeSceneNoDialogs();
            bool valid = built && ValidatePrototypeSceneNoDialogs();
            Debug.Log(valid
                ? "Road of Life automation request completed successfully."
                : "Road of Life automation request failed; inspect earlier Console messages.");
        }

        private static void BuildInScene(Scene sandboxScene)
        {
            if (!sandboxScene.IsValid() || !sandboxScene.isLoaded)
            {
                throw new InvalidOperationException("Sandbox scene is not loaded.");
            }

            Canvas canvas = FindComponentInScene<Canvas>(sandboxScene, "Sandbox Canvas");
            if (canvas == null)
            {
                throw new InvalidOperationException("Could not find 'Sandbox Canvas'.");
            }

            Transform safeArea = FindDescendant(canvas.transform, "Safe Area");
            Transform hud = safeArea != null ? FindDescendant(safeArea, "HUD") : null;
            if (safeArea == null || hud == null)
            {
                throw new InvalidOperationException("Could not find 'Sandbox Canvas/Safe Area/HUD'.");
            }

            ConfigureCanvasScaler(canvas);
            DeleteDescendantIfPresent(hud, "Info");
            DeleteDescendantIfPresent(hud, "TEST RESULT FLOW");
            DeleteDescendantIfPresent(hud, UiRootName);

            RectTransform roadUiRoot = CreateRect(UiRootName, hud);
            Stretch(roadUiRoot);
            roadUiRoot.SetAsFirstSibling();

            BuildAtmosphere(roadUiRoot);
            DrivingRefs driving = BuildDrivingInterface(roadUiRoot, canvas);
            UpgradeRefs upgrades = BuildUpgradeOverlay(roadUiRoot);
            EndingRefs ending = BuildEndingOverlay(roadUiRoot);

            RoadGameView gameView = roadUiRoot.gameObject.AddComponent<RoadGameView>();
            gameView.Configure(
                driving.Root,
                driving.ChoiceResultRoot,
                upgrades.Root,
                ending.Root,
                driving.Progress,
                driving.Event,
                driving.LeftChoice,
                driving.RightChoice,
                driving.LeftPreview,
                driving.RightPreview,
                driving.LeftGroup,
                driving.RightGroup,
                driving.Swipe,
                driving.Stats[0],
                driving.Stats[1],
                driving.Stats[2],
                driving.Stats[3],
                driving.ChoiceResult,
                driving.ChoiceResultHeader,
                driving.ControlsHint,
                driving.ContinueButton,
                upgrades.Header,
                upgrades.Buttons,
                upgrades.Names,
                upgrades.Descriptions,
                upgrades.AcquiredMarks,
                new[]
                {
                    RoadUpgrade.RoadMarkers,
                    RoadUpgrade.WarmingPoint,
                    RoadUpgrade.PreparedDetour,
                    RoadUpgrade.LoadingPost,
                },
                ending.Title,
                ending.Body,
                ending.RestartButton,
                ending.MenuButton);

            driving.ChoiceResultRoot.SetActive(false);
            upgrades.Root.SetActive(false);
            ending.Root.SetActive(false);

            Transform flow = FindRoot(sandboxScene, "Sandbox Flow");
            if (flow == null)
            {
                throw new InvalidOperationException("Could not find the 'Sandbox Flow' root.");
            }

            RoadGameController controller = flow.GetComponent<RoadGameController>();
            if (controller == null)
            {
                controller = flow.gameObject.AddComponent<RoadGameController>();
            }

            TextAsset cards = AssetDatabase.LoadAssetAtPath<TextAsset>(CardsAssetPath);
            if (cards == null)
            {
                throw new InvalidOperationException($"Could not load card data at {CardsAssetPath}.");
            }

            controller.Configure(cards, gameView, flow.GetComponent<SandboxController>());
            RepositionPauseButton(hud);

            EditorUtility.SetDirty(gameView);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(canvas);
        }

        private static void ConfigureCanvasScaler(Canvas canvas)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private static void BuildAtmosphere(RectTransform root)
        {
            Image background = CreateImage("Polar Night", root, Background, false);
            Stretch(background.rectTransform);

            Image sky = CreateImage("Storm Sky", background.transform, Sky, false);
            Place(sky.rectTransform, new Vector2(0f, 0.43f), Vector2.one);

            Image horizon = CreateImage("Horizon Glow", background.transform,
                new Color(Accent.r, Accent.g, Accent.b, 0.17f), false);
            Place(horizon.rectTransform, new Vector2(0f, 0.44f), new Vector2(1f, 0.55f));

            Image ice = CreateImage("Lake Ice", background.transform, Ice, false);
            Place(ice.rectTransform, Vector2.zero, new Vector2(1f, 0.46f));

            Image road = CreateImage("Ice Road", background.transform, Road, false);
            Place(road.rectTransform, new Vector2(0.37f, 0f), new Vector2(0.63f, 0.49f));

            Image leftEdge = CreateImage("Left Road Edge", background.transform,
                new Color(Frost.r, Frost.g, Frost.b, 0.2f), false);
            Place(leftEdge.rectTransform, new Vector2(0.365f, 0f), new Vector2(0.371f, 0.49f));

            Image rightEdge = CreateImage("Right Road Edge", background.transform,
                new Color(Frost.r, Frost.g, Frost.b, 0.2f), false);
            Place(rightEdge.rectTransform, new Vector2(0.629f, 0f), new Vector2(0.635f, 0.49f));

            for (int index = 0; index < 7; index++)
            {
                float y = 0.025f + index * 0.065f;
                float width = Mathf.Lerp(18f, 5f, index / 6f);
                Image marker = CreateImage($"Route Marker {index + 1}", background.transform,
                    new Color(Warm.r, Warm.g, Warm.b, 0.42f), false);
                AnchorFixed(marker.rectTransform, new Vector2(0.5f, y), new Vector2(width, 38f));
            }

            for (int index = 0; index < 20; index++)
            {
                float x = ((index * 37) % 101) / 100f;
                float y = 0.08f + ((index * 61) % 83) / 100f;
                float width = 24f + index % 5 * 9f;
                Image snow = CreateImage($"Snow Streak {index + 1}", background.transform,
                    new Color(Frost.r, Frost.g, Frost.b, 0.18f + index % 3 * 0.05f), false);
                AnchorFixed(snow.rectTransform, new Vector2(x, y), new Vector2(width, 2f));
                snow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, index % 2 == 0 ? -17f : -24f);
            }

            TMP_Text location = CreateText(
                "Location Stamp",
                background.transform,
                "ЛАДОЖСКОЕ ОЗЕРО  •  ЗИМА 1941",
                19f,
                Muted,
                TextAlignmentOptions.BottomLeft);
            Place(location.rectTransform, new Vector2(0.025f, 0.025f), new Vector2(0.32f, 0.085f));
            location.characterSpacing = 3f;
        }

        private static DrivingRefs BuildDrivingInterface(RectTransform root, Canvas canvas)
        {
            RectTransform driving = CreateRect("Driving Panel", root);
            Stretch(driving);

            TMP_Text progress = CreateText(
                "Route Progress",
                driving,
                "РЕЙС 1/3  •  В ГОРОД  •  1/3",
                25f,
                Frost,
                TextAlignmentOptions.Center);
            Place(progress.rectTransform, new Vector2(0.22f, 0.94f), new Vector2(0.78f, 0.992f));
            progress.fontStyle = FontStyles.Bold;
            progress.characterSpacing = 1.4f;

            RectTransform statsPanel = CreateRect("Bipolar Scales", driving);
            Place(statsPanel, new Vector2(0.045f, 0.79f), new Vector2(0.955f, 0.925f));

            string[] names = { "ТЕМП", "ДВИГАТЕЛЬ", "ВИДИМОСТЬ", "ГРУЗ" };
            BipolarStatView[] stats = new BipolarStatView[4];
            for (int index = 0; index < stats.Length; index++)
            {
                float xMin = index * 0.25f + 0.006f;
                float xMax = (index + 1) * 0.25f - 0.006f;
                stats[index] = BuildStatView(statsPanel, $"{names[index]} Scale", names[index], xMin, xMax);
            }

            ChoiceRefs left = BuildChoicePanel(
                driving,
                "Left Choice",
                new Vector2(0.045f, 0.29f),
                new Vector2(0.305f, 0.665f),
                "← РЕШЕНИЕ",
                "Сбавить ход и проверить лёд",
                TextAlignmentOptions.MidlineLeft);

            ChoiceRefs right = BuildChoicePanel(
                driving,
                "Right Choice",
                new Vector2(0.695f, 0.29f),
                new Vector2(0.955f, 0.665f),
                "РЕШЕНИЕ →",
                "Держать темп и идти дальше",
                TextAlignmentOptions.MidlineRight);

            RectTransform card = CreatePanel(
                "Road Card",
                driving,
                PanelRaised,
                new Vector2(0.345f, 0.19f),
                new Vector2(0.655f, 0.765f),
                true);
            AddFrame(card, new Color(Accent.r, Accent.g, Accent.b, 0.82f), 3f);
            CanvasGroup cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            CardSwipeView swipe = card.gameObject.AddComponent<CardSwipeView>();

            TMP_Text cardKicker = CreateText(
                "Card Kicker",
                card,
                "ДОРОГА ЖИЗНИ",
                18f,
                Warm,
                TextAlignmentOptions.Center);
            Place(cardKicker.rectTransform, new Vector2(0.09f, 0.875f), new Vector2(0.91f, 0.95f));
            cardKicker.characterSpacing = 3.5f;
            cardKicker.fontStyle = FontStyles.Bold;

            Image divider = CreateImage("Card Divider", card, new Color(Accent.r, Accent.g, Accent.b, 0.65f), false);
            Place(divider.rectTransform, new Vector2(0.11f, 0.855f), new Vector2(0.89f, 0.86f));

            TMP_Text eventText = CreateText(
                "Event Text",
                card,
                "Перед машиной лёд потемнел. Колея уходит в сторону, а ветер заметает вешки.",
                32f,
                Frost,
                TextAlignmentOptions.Center);
            Place(eventText.rectTransform, new Vector2(0.095f, 0.2f), new Vector2(0.905f, 0.82f));
            ConfigureAutoSize(eventText, 22f, 34f);

            TMP_Text swipeHint = CreateText(
                "Swipe Hint",
                card,
                "ПОТЯНИТЕ КАРТУ ВЛЕВО ИЛИ ВПРАВО",
                15f,
                Muted,
                TextAlignmentOptions.Center);
            Place(swipeHint.rectTransform, new Vector2(0.08f, 0.055f), new Vector2(0.92f, 0.135f));
            swipeHint.characterSpacing = 1.2f;

            swipe.Configure(card, canvas, cardGroup);

            RectTransform resultPanel = CreatePanel(
                "Choice Result",
                driving,
                new Color(Panel.r, Panel.g, Panel.b, 0.99f),
                new Vector2(0.34f, 0.25f),
                new Vector2(0.66f, 0.71f),
                true);
            AddFrame(resultPanel, new Color(Warm.r, Warm.g, Warm.b, 0.88f), 3f);

            TMP_Text resultHeader = CreateText(
                "Result Header",
                resultPanel,
                "ПОСЛЕДСТВИЯ",
                18f,
                Warm,
                TextAlignmentOptions.Center);
            Place(resultHeader.rectTransform, new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.94f));
            resultHeader.characterSpacing = 3f;
            resultHeader.fontStyle = FontStyles.Bold;

            TMP_Text choiceResult = CreateText(
                "Result Text",
                resultPanel,
                "Решение изменило состояние машины и рейса.",
                27f,
                Frost,
                TextAlignmentOptions.Center);
            Place(choiceResult.rectTransform, new Vector2(0.085f, 0.25f), new Vector2(0.915f, 0.82f));
            ConfigureAutoSize(choiceResult, 18f, 28f);

            Button continueButton = CreateButton(
                "Continue",
                resultPanel,
                "ПРОДОЛЖИТЬ",
                new Vector2(0.19f, 0.055f),
                new Vector2(0.81f, 0.205f),
                Accent,
                21f,
                out _);

            TMP_Text controlsHint = CreateText(
                "Controls Hint",
                driving,
                "Мышь / сенсор: тяните карту   •   Клавиатура / геймпад: ← → и подтвердить",
                18f,
                Muted,
                TextAlignmentOptions.Center);
            Place(controlsHint.rectTransform, new Vector2(0.12f, 0.035f), new Vector2(0.88f, 0.088f));

            return new DrivingRefs
            {
                Root = driving.gameObject,
                ChoiceResultRoot = resultPanel.gameObject,
                Progress = progress,
                Event = eventText,
                LeftChoice = left.Choice,
                RightChoice = right.Choice,
                LeftPreview = left.Preview,
                RightPreview = right.Preview,
                LeftGroup = left.Group,
                RightGroup = right.Group,
                Swipe = swipe,
                Stats = stats,
                ChoiceResultHeader = resultHeader,
                ChoiceResult = choiceResult,
                ControlsHint = controlsHint,
                ContinueButton = continueButton,
            };
        }

        private static BipolarStatView BuildStatView(
            Transform parent,
            string objectName,
            string displayName,
            float xMin,
            float xMax)
        {
            RectTransform root = CreatePanel(
                objectName,
                parent,
                new Color(Panel.r, Panel.g, Panel.b, 0.86f),
                new Vector2(xMin, 0f),
                new Vector2(xMax, 1f),
                false);
            BipolarStatView statView = root.gameObject.AddComponent<BipolarStatView>();

            TMP_Text label = CreateText(
                "Label",
                root,
                displayName,
                19f,
                Frost,
                TextAlignmentOptions.Left);
            Place(label.rectTransform, new Vector2(0.055f, 0.53f), new Vector2(0.77f, 0.91f));
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 1.2f;

            TMP_Text value = CreateText(
                "Value",
                root,
                "50",
                21f,
                Warm,
                TextAlignmentOptions.Right);
            Place(value.rectTransform, new Vector2(0.78f, 0.53f), new Vector2(0.945f, 0.91f));
            value.fontStyle = FontStyles.Bold;

            RectTransform track = CreatePanel(
                "Track",
                root,
                new Color(Frost.r, Frost.g, Frost.b, 0.16f),
                new Vector2(0.055f, 0.2f),
                new Vector2(0.945f, 0.43f),
                false);

            Image fill = CreateImage("Fill", track, new Color(Accent.r, Accent.g, Accent.b, 0.38f), false);
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.5f;

            Image lowDanger = CreateImage("Low Danger", track, new Color(Danger.r, Danger.g, Danger.b, 0.28f), false);
            Place(lowDanger.rectTransform, Vector2.zero, new Vector2(0.15f, 1f));

            Image highDanger = CreateImage("High Danger", track, new Color(Danger.r, Danger.g, Danger.b, 0.28f), false);
            Place(highDanger.rectTransform, new Vector2(0.85f, 0f), Vector2.one);

            Image centerLine = CreateImage("Safe Center", track, new Color(Frost.r, Frost.g, Frost.b, 0.48f), false);
            AnchorFixed(centerLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(2f, 24f));

            Image marker = CreateImage("Value Marker", track, Frost, false);
            RectTransform markerRect = marker.rectTransform;
            markerRect.anchorMin = new Vector2(0.5f, 0f);
            markerRect.anchorMax = new Vector2(0.5f, 1f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(6f, 10f);
            markerRect.anchoredPosition = Vector2.zero;

            statView.Configure(displayName, label, value, markerRect, fill, marker, lowDanger, highDanger);
            statView.SetValue(50, false);
            return statView;
        }

        private static ChoiceRefs BuildChoicePanel(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string heading,
            string initialChoice,
            TextAlignmentOptions alignment)
        {
            RectTransform root = CreatePanel(
                objectName,
                parent,
                new Color(Panel.r, Panel.g, Panel.b, 0.9f),
                anchorMin,
                anchorMax,
                false);
            AddFrame(root, new Color(Frost.r, Frost.g, Frost.b, 0.2f), 2f);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();

            TMP_Text header = CreateText("Heading", root, heading, 17f, Accent, alignment);
            Place(header.rectTransform, new Vector2(0.075f, 0.79f), new Vector2(0.925f, 0.94f));
            header.fontStyle = FontStyles.Bold;
            header.characterSpacing = 1.8f;

            TMP_Text choice = CreateText("Choice Text", root, initialChoice, 27f, Frost, alignment);
            Place(choice.rectTransform, new Vector2(0.075f, 0.35f), new Vector2(0.925f, 0.79f));
            ConfigureAutoSize(choice, 20f, 29f);

            TMP_Text preview = CreateText("Effect Preview", root, "Темп −8  •  Видимость +5", 19f, Warm, alignment);
            Place(preview.rectTransform, new Vector2(0.075f, 0.08f), new Vector2(0.925f, 0.32f));
            ConfigureAutoSize(preview, 15f, 20f);
            preview.gameObject.SetActive(false);

            return new ChoiceRefs
            {
                Group = group,
                Choice = choice,
                Preview = preview,
            };
        }

        private static UpgradeRefs BuildUpgradeOverlay(RectTransform root)
        {
            RectTransform overlay = CreatePanel(
                "Upgrade Overlay",
                root,
                new Color(Background.r, Background.g, Background.b, 0.985f),
                Vector2.zero,
                Vector2.one,
                true);

            RectTransform content = CreatePanel(
                "Upgrade Content",
                overlay,
                Panel,
                new Vector2(0.13f, 0.105f),
                new Vector2(0.87f, 0.895f),
                false);
            AddFrame(content, new Color(Accent.r, Accent.g, Accent.b, 0.75f), 3f);

            TMP_Text header = CreateText(
                "Upgrade Header",
                content,
                "ПОДГОТОВКА К СЛЕДУЮЩЕМУ РЕЙСУ",
                34f,
                Frost,
                TextAlignmentOptions.Center);
            Place(header.rectTransform, new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.95f));
            header.fontStyle = FontStyles.Bold;
            header.characterSpacing = 1.7f;

            TMP_Text subtitle = CreateText(
                "Upgrade Subtitle",
                content,
                "Выберите одно улучшение трассы. Оно останется до конца этой попытки.",
                21f,
                Muted,
                TextAlignmentOptions.Center);
            Place(subtitle.rectTransform, new Vector2(0.08f, 0.765f), new Vector2(0.92f, 0.84f));

            string[] initialNames =
            {
                "ВЕШКИ НА ЛЬДУ",
                "ПУНКТ ОБОГРЕВА",
                "РАЗВЕДАННЫЙ ОБЪЕЗД",
                "ПОГРУЗОЧНЫЙ ПОСТ",
            };
            string[] initialDescriptions =
            {
                "События с видимостью возвращают шкалу на 8 пунктов к безопасной середине.",
                "Снег и неполадки возвращают двигатель на 8 пунктов к безопасной середине.",
                "Опасности льда возвращают темп на 8 пунктов к безопасной середине.",
                "События с грузом возвращают загрузку на 8 пунктов к безопасной середине.",
            };

            Button[] buttons = new Button[4];
            TMP_Text[] names = new TMP_Text[4];
            TMP_Text[] descriptions = new TMP_Text[4];
            GameObject[] acquired = new GameObject[4];

            for (int index = 0; index < 4; index++)
            {
                int row = index / 2;
                int column = index % 2;
                float xMin = column == 0 ? 0.055f : 0.515f;
                float xMax = column == 0 ? 0.485f : 0.945f;
                float yMin = row == 0 ? 0.445f : 0.115f;
                float yMax = row == 0 ? 0.735f : 0.405f;

                RectTransform option = CreatePanel(
                    $"Upgrade Option {index + 1}",
                    content,
                    PanelRaised,
                    new Vector2(xMin, yMin),
                    new Vector2(xMax, yMax),
                    true);
                AddFrame(option, new Color(Accent.r, Accent.g, Accent.b, 0.42f), 2f);
                Button button = option.gameObject.AddComponent<Button>();
                button.targetGraphic = option.GetComponent<Image>();
                button.colors = CreateButtonColors(PanelRaised);

                TMP_Text name = CreateText("Name", option, initialNames[index], 24f, Warm, TextAlignmentOptions.Left);
                Place(name.rectTransform, new Vector2(0.055f, 0.62f), new Vector2(0.945f, 0.9f));
                name.fontStyle = FontStyles.Bold;
                name.characterSpacing = 1f;

                TMP_Text description = CreateText(
                    "Description",
                    option,
                    initialDescriptions[index],
                    19f,
                    Frost,
                    TextAlignmentOptions.TopLeft);
                Place(description.rectTransform, new Vector2(0.055f, 0.12f), new Vector2(0.945f, 0.61f));
                ConfigureAutoSize(description, 15f, 20f);

                TMP_Text acquiredMark = CreateText(
                    "Acquired Mark",
                    option,
                    "УСТАНОВЛЕНО",
                    14f,
                    Accent,
                    TextAlignmentOptions.BottomRight);
                Place(acquiredMark.rectTransform, new Vector2(0.55f, 0.02f), new Vector2(0.94f, 0.16f));
                acquiredMark.fontStyle = FontStyles.Bold;
                acquiredMark.gameObject.SetActive(false);

                buttons[index] = button;
                names[index] = name;
                descriptions[index] = description;
                acquired[index] = acquiredMark.gameObject;
            }

            TMP_Text hint = CreateText(
                "Upgrade Controls Hint",
                content,
                "Мышь / сенсор: нажмите вариант   •   Геймпад / клавиатура: выберите и подтвердите",
                17f,
                Muted,
                TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.08f, 0.03f), new Vector2(0.92f, 0.09f));

            return new UpgradeRefs
            {
                Root = overlay.gameObject,
                Header = header,
                Buttons = buttons,
                Names = names,
                Descriptions = descriptions,
                AcquiredMarks = acquired,
            };
        }

        private static EndingRefs BuildEndingOverlay(RectTransform root)
        {
            RectTransform overlay = CreatePanel(
                "Ending Overlay",
                root,
                new Color(Background.r, Background.g, Background.b, 0.985f),
                Vector2.zero,
                Vector2.one,
                true);

            RectTransform content = CreatePanel(
                "Ending Content",
                overlay,
                Panel,
                new Vector2(0.27f, 0.19f),
                new Vector2(0.73f, 0.81f),
                false);
            AddFrame(content, new Color(Warm.r, Warm.g, Warm.b, 0.82f), 3f);

            TMP_Text eyebrow = CreateText(
                "Ending Eyebrow",
                content,
                "ЛАДОГА  •  ДОРОГА ЖИЗНИ",
                17f,
                Warm,
                TextAlignmentOptions.Center);
            Place(eyebrow.rectTransform, new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.93f));
            eyebrow.characterSpacing = 2.8f;

            TMP_Text title = CreateText(
                "Ending Title",
                content,
                "ДОРОГА ПРОЙДЕНА",
                39f,
                Frost,
                TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.07f, 0.66f), new Vector2(0.93f, 0.84f));
            title.fontStyle = FontStyles.Bold;
            ConfigureAutoSize(title, 28f, 42f);

            TMP_Text body = CreateText(
                "Ending Body",
                content,
                "Три рейса завершены. Груз доставлен через Ладогу.",
                25f,
                Frost,
                TextAlignmentOptions.Center);
            Place(body.rectTransform, new Vector2(0.09f, 0.34f), new Vector2(0.91f, 0.65f));
            ConfigureAutoSize(body, 19f, 27f);

            Button restart = CreateButton(
                "Restart",
                content,
                "ЕЩЁ РАЗ",
                new Vector2(0.09f, 0.11f),
                new Vector2(0.475f, 0.26f),
                Accent,
                21f,
                out _);
            Button menu = CreateButton(
                "Return To Menu",
                content,
                "В МЕНЮ",
                new Vector2(0.525f, 0.11f),
                new Vector2(0.91f, 0.26f),
                new Color32(69, 92, 103, 255),
                21f,
                out _);

            return new EndingRefs
            {
                Root = overlay.gameObject,
                Title = title,
                Body = body,
                RestartButton = restart,
                MenuButton = menu,
            };
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color,
            float fontSize,
            out TMP_Text label)
        {
            RectTransform root = CreatePanel(name, parent, color, anchorMin, anchorMax, true);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();
            button.colors = CreateButtonColors(color);

            label = CreateText("Label", root, text, fontSize, Background, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 12f, 7f);
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 1.5f;
            return button;
        }

        private static ColorBlock CreateButtonColors(Color normal)
        {
            return new ColorBlock
            {
                normalColor = normal,
                highlightedColor = Color.Lerp(normal, Frost, 0.2f),
                pressedColor = Color.Lerp(normal, Background, 0.24f),
                selectedColor = Color.Lerp(normal, Frost, 0.14f),
                disabledColor = new Color(normal.r, normal.g, normal.b, 0.33f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
        }

        private static RectTransform CreatePanel(
            string name,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            bool raycastTarget)
        {
            Image image = CreateImage(name, parent, color, raycastTarget);
            Place(image.rectTransform, anchorMin, anchorMax);
            return image.rectTransform;
        }

        private static Image CreateImage(string name, Transform parent, Color color, bool raycastTarget)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string content,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.margin = new Vector4(2f, 2f, 2f, 2f);
            return text;
        }

        private static void ConfigureAutoSize(TMP_Text text, float minimum, float maximum)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = minimum;
            text.fontSizeMax = maximum;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect, float horizontalInset = 0f, float verticalInset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(horizontalInset, verticalInset);
            rect.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        }

        private static void AnchorFixed(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static void AddFrame(RectTransform parent, Color color, float thickness)
        {
            Image top = CreateImage("Frame Top", parent, color, false);
            top.rectTransform.anchorMin = new Vector2(0f, 1f);
            top.rectTransform.anchorMax = Vector2.one;
            top.rectTransform.pivot = new Vector2(0.5f, 1f);
            top.rectTransform.anchoredPosition = Vector2.zero;
            top.rectTransform.sizeDelta = new Vector2(0f, thickness);

            Image bottom = CreateImage("Frame Bottom", parent, color, false);
            bottom.rectTransform.anchorMin = Vector2.zero;
            bottom.rectTransform.anchorMax = new Vector2(1f, 0f);
            bottom.rectTransform.pivot = new Vector2(0.5f, 0f);
            bottom.rectTransform.anchoredPosition = Vector2.zero;
            bottom.rectTransform.sizeDelta = new Vector2(0f, thickness);

            Image left = CreateImage("Frame Left", parent, color, false);
            left.rectTransform.anchorMin = Vector2.zero;
            left.rectTransform.anchorMax = new Vector2(0f, 1f);
            left.rectTransform.pivot = new Vector2(0f, 0.5f);
            left.rectTransform.anchoredPosition = Vector2.zero;
            left.rectTransform.sizeDelta = new Vector2(thickness, 0f);

            Image right = CreateImage("Frame Right", parent, color, false);
            right.rectTransform.anchorMin = new Vector2(1f, 0f);
            right.rectTransform.anchorMax = Vector2.one;
            right.rectTransform.pivot = new Vector2(1f, 0.5f);
            right.rectTransform.anchoredPosition = Vector2.zero;
            right.rectTransform.sizeDelta = new Vector2(thickness, 0f);
        }

        private static void RepositionPauseButton(Transform hud)
        {
            Transform pause = FindDescendant(hud, "PAUSE");
            if (pause == null || !(pause is RectTransform rect))
            {
                return;
            }

            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-34f, -30f);
            rect.sizeDelta = new Vector2(156f, 52f);
            rect.SetAsLastSibling();

            TMP_Text label = pause.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = "ПАУЗА";
                label.fontSize = 19f;
                label.characterSpacing = 1.2f;
            }
        }

        private static List<string> CollectValidationProblems(Scene scene)
        {
            var problems = new List<string>();
            Canvas canvas = FindComponentInScene<Canvas>(scene, "Sandbox Canvas");
            if (canvas == null)
            {
                problems.Add("Не найден Sandbox Canvas.");
                return problems;
            }

            Transform safeArea = FindDescendant(canvas.transform, "Safe Area");
            Transform hud = safeArea != null ? FindDescendant(safeArea, "HUD") : null;
            Transform root = hud != null ? FindDescendant(hud, UiRootName) : null;
            if (safeArea == null) problems.Add("Не найден Safe Area.");
            if (hud == null) problems.Add("Не найден HUD.");
            if (root == null) problems.Add($"Не найден {UiRootName}.");
            if (canvas.GetComponent<CanvasScaler>() == null) problems.Add("На Canvas отсутствует CanvasScaler.");
            if (canvas.GetComponent<GraphicRaycaster>() == null) problems.Add("На Canvas отсутствует GraphicRaycaster.");

            RoadGameView view = root != null ? root.GetComponent<RoadGameView>() : null;
            if (view == null)
            {
                problems.Add("На RoadGame UI отсутствует RoadGameView.");
            }
            else
            {
                ValidateObjectReferences(
                    new SerializedObject(view),
                    problems,
                    "RoadGameView",
                    "drivingPanel",
                    "choiceResultPanel",
                    "upgradePanel",
                    "endingPanel",
                    "progressLabel",
                    "eventLabel",
                    "leftChoiceLabel",
                    "rightChoiceLabel",
                    "leftPreviewLabel",
                    "rightPreviewLabel",
                    "leftChoiceGroup",
                    "rightChoiceGroup",
                    "cardSwipeView",
                    "tempoView",
                    "engineView",
                    "visibilityView",
                    "loadView",
                    "choiceResultLabel",
                    "controlsHintLabel",
                    "continueButton",
                    "upgradeHeaderLabel",
                    "endingTitleLabel",
                    "endingBodyLabel",
                    "restartButton",
                    "menuButton");
                ValidateArraySize(new SerializedObject(view), problems, "RoadGameView", "upgradeButtons", 4);
                ValidateArraySize(new SerializedObject(view), problems, "RoadGameView", "upgradeNameLabels", 4);
                ValidateArraySize(new SerializedObject(view), problems, "RoadGameView", "upgradeDescriptionLabels", 4);
                ValidateArraySize(new SerializedObject(view), problems, "RoadGameView", "upgradeAcquiredMarks", 4);
                ValidateArraySize(new SerializedObject(view), problems, "RoadGameView", "upgradeOrder", 4);
            }

            Transform flow = FindRoot(scene, "Sandbox Flow");
            RoadGameController controller = flow != null ? flow.GetComponent<RoadGameController>() : null;
            if (controller == null)
            {
                problems.Add("На Sandbox Flow отсутствует RoadGameController.");
            }
            else
            {
                ValidateObjectReferences(
                    new SerializedObject(controller),
                    problems,
                    "RoadGameController",
                    "cardsSource",
                    "gameView");
            }

            if (root != null)
            {
                CardSwipeView swipe = root.GetComponentInChildren<CardSwipeView>(true);
                if (swipe == null)
                {
                    problems.Add("Не найден CardSwipeView.");
                }
                else
                {
                    ValidateObjectReferences(
                        new SerializedObject(swipe),
                        problems,
                        "CardSwipeView",
                        "cardRect",
                        "rootCanvas",
                        "cardCanvasGroup");
                }

                BipolarStatView[] stats = root.GetComponentsInChildren<BipolarStatView>(true);
                if (stats.Length != 4)
                {
                    problems.Add($"Ожидалось 4 bipolar-шкалы, найдено {stats.Length}.");
                }
            }

            return problems;
        }

        private static void ValidateObjectReferences(
            SerializedObject serializedObject,
            ICollection<string> problems,
            string owner,
            params string[] propertyNames)
        {
            serializedObject.UpdateIfRequiredOrScript();
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serializedObject.FindProperty(propertyName);
                if (property == null)
                {
                    problems.Add($"{owner}: не найдено поле {propertyName}.");
                }
                else if (property.propertyType == SerializedPropertyType.ObjectReference &&
                         property.objectReferenceValue == null)
                {
                    problems.Add($"{owner}: не назначена ссылка {propertyName}.");
                }
            }
        }

        private static void ValidateArraySize(
            SerializedObject serializedObject,
            ICollection<string> problems,
            string owner,
            string propertyName,
            int expectedSize)
        {
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                problems.Add($"{owner}: не найден массив {propertyName}.");
                return;
            }

            if (property.arraySize != expectedSize)
            {
                problems.Add($"{owner}: {propertyName} должен содержать {expectedSize} элементов.");
                return;
            }

            for (int index = 0; index < property.arraySize; index++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(index);
                if (element.propertyType == SerializedPropertyType.ObjectReference &&
                    element.objectReferenceValue == null)
                {
                    problems.Add($"{owner}: {propertyName}[{index}] не назначен.");
                }
            }
        }

        private static bool TryGetLoadedSandbox(out Scene scene)
        {
            scene = SceneManager.GetSceneByPath(SandboxScenePath);
            return scene.IsValid() && scene.isLoaded;
        }

        private static void RestoreSceneSetup(
            SceneSetup[] previousSetup,
            Scene previousActive,
            Scene sandboxScene,
            bool sceneWasLoaded)
        {
            if (!sceneWasLoaded && sandboxScene.IsValid() && sandboxScene.isLoaded)
            {
                EditorSceneManager.CloseScene(sandboxScene, true);
            }

            if (previousActive.IsValid() && previousActive.isLoaded)
            {
                EditorSceneManager.SetActiveScene(previousActive);
            }

            // Opening Sandbox additively and closing it above is the least invasive
            // way to preserve unsaved scenes. This comparison guards against future
            // changes accidentally altering the user's scene setup.
            SceneSetup[] currentSetup = EditorSceneManager.GetSceneManagerSetup();
            if (!SceneSetupsMatch(previousSetup, currentSetup))
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }

        private static bool SceneSetupsMatch(SceneSetup[] left, SceneSetup[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index].path != right[index].path ||
                    left[index].isLoaded != right[index].isLoaded ||
                    left[index].isActive != right[index].isActive)
                {
                    return false;
                }
            }

            return true;
        }

        private static T FindComponentInScene<T>(Scene scene, string objectName) where T : Component
        {
            Transform transform = FindRoot(scene, objectName);
            return transform != null ? transform.GetComponent<T>() : null;
        }

        private static Transform FindRoot(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                {
                    return root.transform;
                }

                Transform nested = FindDescendant(root.transform, objectName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static Transform FindDescendant(Transform parent, string objectName)
        {
            if (parent == null)
            {
                return null;
            }

            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == objectName)
                {
                    return child;
                }

                Transform nested = FindDescendant(child, objectName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void DeleteDescendantIfPresent(Transform parent, string objectName)
        {
            Transform existing = FindDescendant(parent, objectName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
        }

        private sealed class DrivingRefs
        {
            public GameObject Root;
            public GameObject ChoiceResultRoot;
            public TMP_Text Progress;
            public TMP_Text Event;
            public TMP_Text LeftChoice;
            public TMP_Text RightChoice;
            public TMP_Text LeftPreview;
            public TMP_Text RightPreview;
            public CanvasGroup LeftGroup;
            public CanvasGroup RightGroup;
            public CardSwipeView Swipe;
            public BipolarStatView[] Stats;
            public TMP_Text ChoiceResult;
            public TMP_Text ChoiceResultHeader;
            public TMP_Text ControlsHint;
            public Button ContinueButton;
        }

        private sealed class ChoiceRefs
        {
            public CanvasGroup Group;
            public TMP_Text Choice;
            public TMP_Text Preview;
        }

        private sealed class UpgradeRefs
        {
            public GameObject Root;
            public TMP_Text Header;
            public Button[] Buttons;
            public TMP_Text[] Names;
            public TMP_Text[] Descriptions;
            public GameObject[] AcquiredMarks;
        }

        private sealed class EndingRefs
        {
            public GameObject Root;
            public TMP_Text Title;
            public TMP_Text Body;
            public Button RestartButton;
            public Button MenuButton;
        }
    }
}
