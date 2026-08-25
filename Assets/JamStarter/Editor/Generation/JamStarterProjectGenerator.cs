using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JamStarter;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JamStarter.Editor
{
    /// <summary>
    /// Deterministically creates the starter's Unity objects and wires serialized
    /// references. Runtime scripts contain no project-specific asset lookups.
    /// </summary>
    public static class JamStarterProjectGenerator
    {
        private const string SettingsFolder = JamStarterPaths.Root + "/Settings";
        private const string MaterialsFolder = JamStarterPaths.Root + "/Art/Materials";
        private const string InputActionsPath = SettingsFolder + "/JamInputActions.inputactions";
        private const string LegacyInputActionsPath = SettingsFolder + "/JamInputActions.asset";
        private const string InputActionsReferencePath = SettingsFolder + "/JamInputActionsReference.asset";
        private const string InputConfigurationPath = SettingsFolder + "/JamInputConfiguration.asset";
        private const string AudioMixerPath = SettingsFolder + "/JamAudioMixer.mixer";
        private const string GroundMaterialPath = MaterialsFolder + "/Ground.mat";
        private const string AccentMaterialPath = MaterialsFolder + "/Accent.mat";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string ExitAfterGenerationArgument = "-jamStarterExit";

        private static readonly Color Background = new(0.035f, 0.055f, 0.09f, 1f);
        private static readonly Color Panel = new(0.075f, 0.11f, 0.18f, 0.98f);
        private static readonly Color PanelLight = new(0.12f, 0.17f, 0.27f, 1f);
        private static readonly Color Accent = new(0.22f, 0.88f, 0.72f, 1f);
        private static readonly Color AccentBlue = new(0.28f, 0.58f, 1f, 1f);
        private static readonly Color TextPrimary = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color TextSecondary = new(0.62f, 0.69f, 0.79f, 1f);

        private sealed class MixerParts
        {
            public AudioMixer Mixer;
            public AudioMixerGroup Music;
            public AudioMixerGroup Sfx;
            public AudioMixerGroup Ui;
        }

        private sealed class SettingsUi
        {
            public UIScreen Screen;
            public SettingsPanel Panel;
            public Button Back;
        }

        [MenuItem("Jam Starter/Generate or Rebuild Starter", priority = 0)]
        private static void GenerateFromMenu()
        {
            bool scenesExist = AssetDatabase.LoadAssetAtPath<SceneAsset>(JamStarterPaths.BootstrapScene) != null;
            if (scenesExist && !EditorUtility.DisplayDialog(
                    "Rebuild Jam Starter?",
                    "Generated Jam Starter scenes and settings assets will be replaced. Runtime scripts are kept.",
                    "Rebuild",
                    "Cancel"))
            {
                return;
            }

            Generate();
        }

        public static void Generate()
        {
            EnsureFolders();
            if (!EnsureTmpEssentials())
            {
                Debug.Log("TMP Essential Resources import started. Run generation again after the import completes.");
                return;
            }

            InputActionAsset inputActions = CreateInputActions();
            if (inputActions == null || !EditorUtility.IsPersistent(inputActions))
            {
                throw new InvalidOperationException(
                    $"Input Actions must be a persistent asset. Loaded path: '{AssetDatabase.GetAssetPath(inputActions)}'.");
            }
            InputActionReference inputActionsReference = CreateInputActionsReference(inputActions);
            InputConfiguration inputConfiguration = CreateInputConfiguration(inputActionsReference);
            MixerParts mixer = CreateAudioMixer();
            Material ground = CreateOrUpdateMaterial(GroundMaterialPath, new Color(0.08f, 0.12f, 0.18f));
            Material accent = CreateOrUpdateMaterial(AccentMaterialPath, Accent);

            // AudioMixer creation imports sub-assets and can invalidate previously loaded
            // ScriptableObject handles. Reload before serializing the scene reference.
            inputConfiguration = AssetDatabase.LoadAssetAtPath<InputConfiguration>(InputConfigurationPath)
                ?? throw new InvalidOperationException("The Input Configuration was lost during asset import.");

            CreateBootstrapScene(inputConfiguration, mixer);
            CreateMainMenuScene();
            CreateSandboxScene(ground, accent);

            JamStarterWorkflow.ConfigureBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            List<string> errors = JamStarterWorkflow.ValidateProject(false);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException("Jam Starter generation completed with validation errors.");
            }

            Debug.Log("Jam Starter generated successfully. Open MainMenu or use Jam Starter/Play From Bootstrap.");

            if (HasCommandLineArgument(ExitAfterGenerationArgument))
            {
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "JamStarter");
            EnsureFolder(JamStarterPaths.Root, "Scenes");
            EnsureFolder(JamStarterPaths.Root, "Settings");
            EnsureFolder(JamStarterPaths.Root, "Art");
            EnsureFolder(JamStarterPaths.Root + "/Art", "Materials");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static bool EnsureTmpEssentials()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null)
            {
                return true;
            }

            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            if (package == null)
            {
                throw new InvalidOperationException("Could not locate the uGUI package for TMP resources.");
            }

            string packagePath = Path.Combine(
                package.resolvedPath,
                "Package Resources",
                "TMP Essential Resources.unitypackage");

            if (!File.Exists(packagePath))
            {
                throw new FileNotFoundException("TMP Essential Resources package was not found.", packagePath);
            }

            AssetDatabase.importPackageCompleted -= OnTmpPackageImported;
            AssetDatabase.importPackageCompleted += OnTmpPackageImported;
            AssetDatabase.ImportPackage(packagePath, false);
            return AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null;
        }

        private static void OnTmpPackageImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnTmpPackageImported;
            EditorApplication.delayCall += Generate;
        }

        private static bool HasCommandLineArgument(string argument)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static InputActionAsset CreateInputActions()
        {
            InputActionAsset existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (existing != null)
            {
                return existing;
            }

            AssetDatabase.DeleteAsset(LegacyInputActionsPath);

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "Jam Input Actions";

            InputActionMap gameplay = asset.AddActionMap("Gameplay");
            InputAction move = gameplay.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddBinding("<Gamepad>/leftStick");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            InputAction look = gameplay.AddAction("Look", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick");
            look.AddBinding("<Touchscreen>/primaryTouch/delta");

            InputAction primary = gameplay.AddAction("Primary", InputActionType.Button, expectedControlLayout: "Button");
            primary.AddBinding("<Mouse>/leftButton");
            primary.AddBinding("<Gamepad>/buttonSouth");
            primary.AddBinding("<Touchscreen>/primaryTouch/press");

            InputAction secondary = gameplay.AddAction("Secondary", InputActionType.Button, expectedControlLayout: "Button");
            secondary.AddBinding("<Mouse>/rightButton");
            secondary.AddBinding("<Gamepad>/buttonEast");

            InputAction interact = gameplay.AddAction("Interact", InputActionType.Button, expectedControlLayout: "Button");
            interact.AddBinding("<Keyboard>/e");
            interact.AddBinding("<Gamepad>/buttonWest");

            InputAction pause = gameplay.AddAction("Pause", InputActionType.Button, expectedControlLayout: "Button");
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");

            InputActionMap ui = asset.AddActionMap("UI");
            InputAction navigate = ui.AddAction("Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            navigate.AddBinding("<Gamepad>/leftStick");
            navigate.AddBinding("<Gamepad>/dpad");
            navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            InputAction submit = ui.AddAction("Submit", InputActionType.Button, expectedControlLayout: "Button");
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/space");
            submit.AddBinding("<Gamepad>/buttonSouth");

            InputAction cancel = ui.AddAction("Cancel", InputActionType.Button, expectedControlLayout: "Button");
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");

            InputAction point = ui.AddAction("Point", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            point.AddBinding("<Mouse>/position");
            point.AddBinding("<Pen>/position");
            point.AddBinding("<Touchscreen>/primaryTouch/position");

            InputAction click = ui.AddAction("Click", InputActionType.PassThrough, expectedControlLayout: "Button");
            click.AddBinding("<Mouse>/leftButton");
            click.AddBinding("<Pen>/tip");
            click.AddBinding("<Touchscreen>/primaryTouch/press");

            InputAction scroll = ui.AddAction("ScrollWheel", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            scroll.AddBinding("<Mouse>/scroll");

            string json = asset.ToJson();
            File.WriteAllText(Path.GetFullPath(InputActionsPath), json);
            Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath)
                ?? throw new InvalidOperationException("The generated Input Actions asset could not be imported.");
        }

        private static MixerParts CreateAudioMixer()
        {
            AssetDatabase.DeleteAsset(AudioMixerPath);

            Assembly editorAssembly = typeof(AudioImporter).Assembly;
            Type controllerType = editorAssembly.GetType("UnityEditor.Audio.AudioMixerController", true);
            MethodInfo createMethod = controllerType.GetMethod(
                "CreateMixerControllerAtPath",
                BindingFlags.Public | BindingFlags.Static);
            object controller = createMethod?.Invoke(null, new object[] { AudioMixerPath })
                ?? throw new InvalidOperationException("Unity did not create the AudioMixer controller.");

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            PropertyInfo masterProperty = controllerType.GetProperty("masterGroup", flags);
            MethodInfo createGroupMethod = controllerType.GetMethod("CreateNewGroup", flags);
            MethodInfo addChildMethod = controllerType.GetMethod("AddChildToParent", flags);
            object master = masterProperty?.GetValue(controller)
                ?? throw new InvalidOperationException("AudioMixer master group is unavailable.");

            object music = createGroupMethod?.Invoke(controller, new object[] { "Music", false });
            object sfx = createGroupMethod?.Invoke(controller, new object[] { "SFX", false });
            object ui = createGroupMethod?.Invoke(controller, new object[] { "UI", false });
            addChildMethod?.Invoke(controller, new[] { music, master });
            addChildMethod?.Invoke(controller, new[] { sfx, master });
            addChildMethod?.Invoke(controller, new[] { ui, master });

            ExposeGroupVolume(editorAssembly, controller, master, "MasterVolume");
            ExposeGroupVolume(editorAssembly, controller, music, "MusicVolume");
            ExposeGroupVolume(editorAssembly, controller, sfx, "SfxVolume");
            ExposeGroupVolume(editorAssembly, controller, ui, "UiVolume");

            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioMixerPath);
            EditorUtility.SetDirty(mixer);
            AssetDatabase.SaveAssets();

            return new MixerParts
            {
                Mixer = mixer,
                Music = (AudioMixerGroup)music,
                Sfx = (AudioMixerGroup)sfx,
                Ui = (AudioMixerGroup)ui,
            };
        }

        private static InputActionReference CreateInputActionsReference(InputActionAsset inputActions)
        {
            InputActionReference existing =
                AssetDatabase.LoadAssetAtPath<InputActionReference>(InputActionsReferencePath);
            if (existing != null && existing.action != null && existing.action.actionMap?.asset == inputActions)
            {
                return existing;
            }

            AssetDatabase.DeleteAsset(InputActionsReferencePath);
            InputAction move = inputActions.FindAction("Gameplay/Move", true);
            InputActionReference reference = InputActionReference.Create(move);
            reference.name = "Jam Input Actions Reference";
            AssetDatabase.CreateAsset(reference, InputActionsReferencePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(InputActionsReferencePath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionReference>(InputActionsReferencePath)
                ?? throw new InvalidOperationException("The Input Actions reference asset could not be created.");
        }

        private static InputConfiguration CreateInputConfiguration(InputActionReference reference)
        {
            InputConfiguration configuration =
                AssetDatabase.LoadAssetAtPath<InputConfiguration>(InputConfigurationPath);
            if (configuration == null)
            {
                configuration = ScriptableObject.CreateInstance<InputConfiguration>();
                configuration.name = "Jam Input Configuration";
                AssetDatabase.CreateAsset(configuration, InputConfigurationPath);
            }

            SetObject(configuration, "assetAnchor", reference);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(InputConfigurationPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputConfiguration>(InputConfigurationPath);
        }

        private static void ExposeGroupVolume(
            Assembly editorAssembly,
            object controller,
            object group,
            string parameterName)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            object guid = group.GetType().GetMethod("GetGUIDForVolume", flags)?.Invoke(group, null)
                ?? throw new InvalidOperationException("Could not read the AudioMixer group volume GUID.");

            Type pathType = editorAssembly.GetType("UnityEditor.Audio.AudioGroupParameterPath", true);
            object path = Activator.CreateInstance(
                pathType,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { group, guid },
                null);

            MethodInfo addMethod = controller.GetType().GetMethod("AddExposedParameter", flags);
            addMethod?.Invoke(controller, new[] { path });
            RenameExposedParameter(controller, guid, parameterName);
        }

        private static void RenameExposedParameter(object controller, object targetGuid, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            PropertyInfo property = controller.GetType().GetProperty("exposedParameters", flags);
            Array parameters = property?.GetValue(controller) as Array
                ?? throw new InvalidOperationException("Could not read AudioMixer exposed parameters.");

            for (int index = 0; index < parameters.Length; index++)
            {
                object parameter = parameters.GetValue(index);
                Type parameterType = parameter.GetType();
                FieldInfo guidField = parameterType.GetField("guid", flags);
                FieldInfo nameField = parameterType.GetField("name", flags);
                if (guidField != null && Equals(guidField.GetValue(parameter), targetGuid))
                {
                    nameField?.SetValue(parameter, name);
                    parameters.SetValue(parameter, index);
                    property.SetValue(controller, parameters);
                    return;
                }
            }

            throw new InvalidOperationException($"Could not rename exposed AudioMixer parameter to '{name}'.");
        }

        private static Material CreateOrUpdateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.SetFloat("_Smoothness", 0.45f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateBootstrapScene(InputConfiguration inputConfiguration, MixerParts mixer)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("AppRoot");
            AppBootstrap bootstrap = root.AddComponent<AppBootstrap>();
            GamePauseService pause = root.AddComponent<GamePauseService>();
            InputReader input = root.AddComponent<InputReader>();
            AudioService audio = root.AddComponent<AudioService>();
            SettingsService settings = root.AddComponent<SettingsService>();

            var musicObject = new GameObject("Music Source");
            musicObject.transform.SetParent(root.transform, false);
            AudioSource musicSource = musicObject.AddComponent<AudioSource>();
            musicSource.outputAudioMixerGroup = mixer.Music;
            musicSource.playOnAwake = false;

            var oneShotRoot = new GameObject("One Shot Voices");
            oneShotRoot.transform.SetParent(root.transform, false);

            Canvas transitionCanvas = CreateCanvas("Transition Canvas", root.transform, 1000);
            GameObject fadeObject = CreateUiObject(
                "Fade Overlay",
                transitionCanvas.transform,
                typeof(Image),
                typeof(CanvasGroup));
            Stretch(fadeObject.GetComponent<RectTransform>());
            Image fadeImage = fadeObject.GetComponent<Image>();
            fadeImage.color = Color.black;
            fadeImage.raycastTarget = true;
            CanvasGroup fadeGroup = fadeObject.GetComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.interactable = false;
            fadeGroup.blocksRaycasts = false;

            SceneLoader sceneLoader = root.AddComponent<SceneLoader>();
            SetObject(sceneLoader, "loadingOverlay", fadeGroup);
            SetObject(sceneLoader, "pauseService", pause);

            ConfigureEventSystem(root.transform);

            SetObject(input, "configuration", inputConfiguration);
            SetObject(audio, "audioMixer", mixer.Mixer);
            SetObject(audio, "musicGroup", mixer.Music);
            SetObject(audio, "sfxGroup", mixer.Sfx);
            SetObject(audio, "uiGroup", mixer.Ui);
            SetObject(audio, "musicSource", musicSource);
            SetObject(audio, "oneShotRoot", oneShotRoot.transform);

            SetObject(settings, "audioMixer", mixer.Mixer);

            SetObject(bootstrap, "input", input);
            SetObject(bootstrap, "pause", pause);
            SetObject(bootstrap, "scenes", sceneLoader);
            SetObject(bootstrap, "audioService", audio);
            SetObject(bootstrap, "settings", settings);

            EditorSceneManager.SaveScene(scene, JamStarterPaths.BootstrapScene);
        }

        private static void ConfigureEventSystem(Transform parent)
        {
            var eventObject = new GameObject("EventSystem");
            eventObject.transform.SetParent(parent, false);
            eventObject.AddComponent<EventSystem>();
            InputSystemUIInputModule module = eventObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            module.deselectOnBackgroundClick = false;
        }

        private static void CreateMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(Background);

            var controllerObject = new GameObject("Main Menu Flow");
            MainMenuController controller = controllerObject.AddComponent<MainMenuController>();

            Canvas canvas = CreateCanvas("Main Menu Canvas", null, 0);
            CreateBackground(canvas.transform, Background);
            RectTransform safeArea = CreateSafeArea(canvas.transform);

            UIScreen mainScreen = CreateScreen("Main Screen", safeArea, out GameObject mainRoot);
            GameObject panel = CreatePanel(mainRoot.transform, new Vector2(620f, 720f));
            CreateText(panel.transform, "JAM STARTER", 66f, FontStyles.Bold, TextPrimary, 110f);
            CreateText(
                panel.transform,
                "A neutral Unity 6 foundation for rapid prototypes",
                24f,
                FontStyles.Normal,
                TextSecondary,
                80f);

            Button start = CreateButton(panel.transform, "START SANDBOX", Accent);
            Button settingsButton = CreateButton(panel.transform, "SETTINGS", AccentBlue);
            Button quit = CreateButton(panel.transform, "QUIT", PanelLight);
            UnityEventTools.AddPersistentListener(start.onClick, controller.StartGame);
            UnityEventTools.AddPersistentListener(settingsButton.onClick, controller.OpenSettings);
            UnityEventTools.AddPersistentListener(quit.onClick, controller.Quit);
            ConfigureScreen(mainScreen, mainRoot.GetComponent<CanvasGroup>(), start);

            SettingsUi settingsUi = CreateSettingsScreen(safeArea);
            UnityEventTools.AddPersistentListener(settingsUi.Back.onClick, controller.CloseSettings);

            SetObject(controller, "mainScreen", mainScreen);
            SetObject(controller, "settingsScreen", settingsUi.Screen);
            SetObject(controller, "settingsPanel", settingsUi.Panel);

            EditorSceneManager.SaveScene(scene, JamStarterPaths.MainMenuScene);
        }

        private static void CreateSandboxScene(Material groundMaterial, Material accentMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = CreateCamera(Background);
            camera.transform.position = new Vector3(0f, 7.5f, -10f);
            camera.transform.rotation = Quaternion.Euler(28f, 0f, 0f);

            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(0.83f, 0.9f, 1f);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Neutral Ground";
            ground.transform.position = new Vector3(0f, -0.25f, 0f);
            ground.transform.localScale = new Vector3(9f, 0.5f, 9f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            GameObject showcase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            showcase.name = "Input Preview";
            showcase.transform.position = new Vector3(0f, 0.75f, 0f);
            showcase.transform.localScale = Vector3.one * 1.5f;
            showcase.GetComponent<Renderer>().sharedMaterial = accentMaterial;

            var flowObject = new GameObject("Sandbox Flow");
            SandboxController controller = flowObject.AddComponent<SandboxController>();
            SandboxShowcase preview = flowObject.AddComponent<SandboxShowcase>();
            SetObject(preview, "target", showcase.transform);

            Canvas canvas = CreateCanvas("Sandbox Canvas", null, 0);
            RectTransform safeArea = CreateSafeArea(canvas.transform);

            UIScreen hud = CreateScreen("HUD", safeArea, out GameObject hudRoot);
            CreateHud(hudRoot.transform, controller, out Button pauseButton, out Button completeButton);
            ConfigureScreen(hud, hudRoot.GetComponent<CanvasGroup>(), completeButton);

            UIScreen pause = CreateOverlayScreen("Pause Screen", safeArea, out GameObject pauseRoot, out GameObject pausePanel);
            CreateText(pausePanel.transform, "PAUSED", 58f, FontStyles.Bold, TextPrimary, 100f);
            Button resume = CreateButton(pausePanel.transform, "RESUME", Accent);
            Button openSettings = CreateButton(pausePanel.transform, "SETTINGS", AccentBlue);
            Button restart = CreateButton(pausePanel.transform, "RESTART SCENE", PanelLight);
            Button mainMenu = CreateButton(pausePanel.transform, "MAIN MENU", PanelLight);
            UnityEventTools.AddPersistentListener(resume.onClick, controller.Resume);
            UnityEventTools.AddPersistentListener(openSettings.onClick, controller.OpenSettings);
            UnityEventTools.AddPersistentListener(restart.onClick, controller.Restart);
            UnityEventTools.AddPersistentListener(mainMenu.onClick, controller.ReturnToMainMenu);
            ConfigureScreen(pause, pauseRoot.GetComponent<CanvasGroup>(), resume);

            SettingsUi settingsUi = CreateSettingsScreen(safeArea);
            settingsUi.Screen.gameObject.name = "Sandbox Settings Screen";
            UnityEventTools.AddPersistentListener(settingsUi.Back.onClick, controller.CloseSettings);

            UIScreen result = CreateOverlayScreen("Result Screen", safeArea, out GameObject resultRoot, out GameObject resultPanel);
            TMP_Text resultText = CreateText(
                resultPanel.transform,
                "Flow complete",
                42f,
                FontStyles.Bold,
                TextPrimary,
                180f);
            Button resultRestart = CreateButton(resultPanel.transform, "RESTART", Accent);
            Button resultMenu = CreateButton(resultPanel.transform, "MAIN MENU", AccentBlue);
            UnityEventTools.AddPersistentListener(resultRestart.onClick, controller.Restart);
            UnityEventTools.AddPersistentListener(resultMenu.onClick, controller.ReturnToMainMenu);
            ConfigureScreen(result, resultRoot.GetComponent<CanvasGroup>(), resultRestart);

            SetObject(controller, "hudScreen", hud);
            SetObject(controller, "pauseScreen", pause);
            SetObject(controller, "settingsScreen", settingsUi.Screen);
            SetObject(controller, "resultScreen", result);
            SetObject(controller, "settingsPanel", settingsUi.Panel);
            SetObject(controller, "resultMessage", resultText);

            EditorSceneManager.SaveScene(scene, JamStarterPaths.SandboxScene);
        }

        private static void CreateHud(
            Transform parent,
            SandboxController controller,
            out Button pauseButton,
            out Button completeButton)
        {
            GameObject info = CreateUiObject("Info", parent, typeof(Image));
            RectTransform infoRect = info.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0f, 1f);
            infoRect.anchorMax = new Vector2(0f, 1f);
            infoRect.pivot = new Vector2(0f, 1f);
            infoRect.anchoredPosition = new Vector2(32f, -32f);
            infoRect.sizeDelta = new Vector2(620f, 150f);
            info.GetComponent<Image>().color = Panel;

            TMP_Text title = CreateText(info.transform, "SANDBOX / REPLACE ME", 30f, FontStyles.Bold, TextPrimary, 52f);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(24f, -68f);
            titleRect.offsetMax = new Vector2(-24f, -16f);

            TMP_Text help = CreateText(
                info.transform,
                "WASD / stick moves the preview · Esc / Start pauses",
                21f,
                FontStyles.Normal,
                TextSecondary,
                58f);
            RectTransform helpRect = help.rectTransform;
            helpRect.anchorMin = new Vector2(0f, 0f);
            helpRect.anchorMax = new Vector2(1f, 0f);
            helpRect.pivot = new Vector2(0.5f, 0f);
            helpRect.offsetMin = new Vector2(24f, 20f);
            helpRect.offsetMax = new Vector2(-24f, 78f);

            pauseButton = CreateAnchoredButton(parent, "PAUSE", new Vector2(-32f, -32f), new Vector2(230f, 68f));
            completeButton = CreateAnchoredButton(parent, "TEST RESULT FLOW", new Vector2(-32f, -118f), new Vector2(300f, 68f));
            UnityEventTools.AddPersistentListener(pauseButton.onClick, controller.Pause);
            UnityEventTools.AddPersistentListener(completeButton.onClick, controller.CompleteSandboxFlow);
        }

        private static SettingsUi CreateSettingsScreen(Transform parent)
        {
            UIScreen screen = CreateOverlayScreen("Settings Screen", parent, out GameObject root, out GameObject panel);
            SettingsPanel settingsPanel = root.AddComponent<SettingsPanel>();

            CreateText(panel.transform, "SETTINGS", 52f, FontStyles.Bold, TextPrimary, 80f);
            Slider master = CreateSliderRow(panel.transform, "Master");
            Slider music = CreateSliderRow(panel.transform, "Music");
            Slider sfx = CreateSliderRow(panel.transform, "SFX");
            Slider ui = CreateSliderRow(panel.transform, "UI");
            Toggle fullscreen = CreateToggleRow(panel.transform, "Fullscreen");
            TMP_Dropdown quality = CreateDropdownRow(panel.transform, "Quality");
            Button reset = CreateButton(panel.transform, "RESET DEFAULTS", PanelLight);
            Button back = CreateButton(panel.transform, "BACK", AccentBlue);
            UnityEventTools.AddPersistentListener(reset.onClick, settingsPanel.ResetToDefaults);

            SetObject(settingsPanel, "masterVolume", master);
            SetObject(settingsPanel, "musicVolume", music);
            SetObject(settingsPanel, "sfxVolume", sfx);
            SetObject(settingsPanel, "uiVolume", ui);
            SetObject(settingsPanel, "fullscreen", fullscreen);
            SetObject(settingsPanel, "quality", quality);
            ConfigureScreen(screen, root.GetComponent<CanvasGroup>(), back);

            return new SettingsUi
            {
                Screen = screen,
                Panel = settingsPanel,
                Back = back,
            };
        }

        private static Camera CreateCamera(Color color)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = color;
            return camera;
        }

        private static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            GameObject canvasObject = CreateUiObject(
                name,
                parent,
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateBackground(Transform parent, Color color)
        {
            GameObject background = CreateUiObject("Background", parent, typeof(Image));
            Stretch(background.GetComponent<RectTransform>());
            Image image = background.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static RectTransform CreateSafeArea(Transform parent)
        {
            GameObject safe = CreateUiObject("Safe Area", parent);
            RectTransform rect = safe.GetComponent<RectTransform>();
            Stretch(rect);
            SafeAreaFitter fitter = safe.AddComponent<SafeAreaFitter>();
            SetObject(fitter, "target", rect);
            return rect;
        }

        private static UIScreen CreateScreen(string name, Transform parent, out GameObject root)
        {
            root = CreateUiObject(name, parent, typeof(CanvasGroup));
            Stretch(root.GetComponent<RectTransform>());
            return root.AddComponent<UIScreen>();
        }

        private static UIScreen CreateOverlayScreen(
            string name,
            Transform parent,
            out GameObject root,
            out GameObject panel)
        {
            UIScreen screen = CreateScreen(name, parent, out root);
            Image dim = root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f);
            dim.raycastTarget = true;
            panel = CreatePanel(root.transform, new Vector2(680f, 850f));
            return screen;
        }

        private static GameObject CreatePanel(Transform parent, Vector2 size)
        {
            GameObject panelObject = CreateUiObject(
                "Panel",
                parent,
                typeof(Image),
                typeof(VerticalLayoutGroup));
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            panelObject.GetComponent<Image>().color = Panel;

            VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(58, 58, 48, 48);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return panelObject;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string content,
            float fontSize,
            FontStyles style,
            Color color,
            float preferredHeight)
        {
            GameObject textObject = CreateUiObject("Text", parent, typeof(TextMeshProUGUI), typeof(LayoutElement));
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            textObject.GetComponent<LayoutElement>().preferredHeight = preferredHeight;
            return text;
        }

        private static Button CreateButton(Transform parent, string label, Color color)
        {
            GameObject buttonObject = CreateUiObject(
                label,
                parent,
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            Image image = buttonObject.GetComponent<Image>();
            image.color = color;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.78f, 0.82f, 0.86f, 1f);
            colors.selectedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            button.colors = colors;

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 460f;
            layout.preferredHeight = 70f;

            GameObject textObject = CreateUiObject("Label", buttonObject.transform, typeof(TextMeshProUGUI));
            Stretch(textObject.GetComponent<RectTransform>(), 18f, 8f);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 24f;
            text.fontStyle = FontStyles.Bold;
            text.color = Background;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return button;
        }

        private static Button CreateAnchoredButton(
            Transform parent,
            string label,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            Button button = CreateButton(parent, label, AccentBlue);
            LayoutElement layout = button.GetComponent<LayoutElement>();
            Object.DestroyImmediate(layout);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return button;
        }

        private static Slider CreateSliderRow(Transform parent, string label)
        {
            GameObject row = CreateRow(parent, label, out Transform controlParent);
            GameObject sliderObject = CreateUiObject(
                "Slider",
                controlParent,
                typeof(Slider),
                typeof(LayoutElement));
            sliderObject.GetComponent<LayoutElement>().preferredWidth = 300f;
            sliderObject.GetComponent<LayoutElement>().preferredHeight = 48f;

            GameObject background = CreateUiObject("Background", sliderObject.transform, typeof(Image));
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(0f, 10f);
            background.GetComponent<Image>().color = PanelLight;

            GameObject fillArea = CreateUiObject("Fill Area", sliderObject.transform);
            Stretch(fillArea.GetComponent<RectTransform>(), 8f, 0f);
            GameObject fill = CreateUiObject("Fill", fillArea.transform, typeof(Image));
            Stretch(fill.GetComponent<RectTransform>());
            fill.GetComponent<Image>().color = Accent;

            GameObject handleArea = CreateUiObject("Handle Slide Area", sliderObject.transform);
            Stretch(handleArea.GetComponent<RectTransform>(), 12f, 0f);
            GameObject handle = CreateUiObject("Handle", handleArea.transform, typeof(Image));
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(26f, 36f);
            handle.GetComponent<Image>().color = TextPrimary;

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static Toggle CreateToggleRow(Transform parent, string label)
        {
            CreateRow(parent, label, out Transform controlParent);
            GameObject toggleObject = CreateUiObject(
                "Toggle",
                controlParent,
                typeof(Toggle),
                typeof(LayoutElement));
            LayoutElement layout = toggleObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 56f;
            layout.preferredHeight = 48f;

            Image background = toggleObject.AddComponent<Image>();
            background.color = PanelLight;
            GameObject checkObject = CreateUiObject("Checkmark", toggleObject.transform, typeof(Image));
            Stretch(checkObject.GetComponent<RectTransform>(), 9f, 9f);
            Image check = checkObject.GetComponent<Image>();
            check.color = Accent;

            Toggle toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            toggle.isOn = true;
            return toggle;
        }

        private static TMP_Dropdown CreateDropdownRow(Transform parent, string label)
        {
            CreateRow(parent, label, out Transform controlParent);
            GameObject dropdownObject = TMP_DefaultControls.CreateDropdown(default);
            dropdownObject.name = "Quality Dropdown";
            dropdownObject.transform.SetParent(controlParent, false);
            dropdownObject.layer = 5;
            AddOrGet<LayoutElement>(dropdownObject).preferredWidth = 300f;
            AddOrGet<LayoutElement>(dropdownObject).preferredHeight = 54f;

            TMP_Dropdown dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            foreach (Image image in dropdownObject.GetComponentsInChildren<Image>(true))
            {
                image.color = image.gameObject == dropdownObject ? PanelLight : TextPrimary;
            }

            foreach (TMP_Text text in dropdownObject.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = TMP_Settings.defaultFontAsset;
                text.color = text.gameObject.name.Contains("Item") ? Background : TextPrimary;
                text.fontSize = 20f;
            }

            return dropdown;
        }

        private static GameObject CreateRow(Transform parent, string label, out Transform controlParent)
        {
            GameObject row = CreateUiObject(
                label + " Row",
                parent,
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            row.GetComponent<LayoutElement>().preferredHeight = 62f;
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            TMP_Text rowLabel = CreateText(row.transform, label, 23f, FontStyles.Normal, TextPrimary, 58f);
            LayoutElement labelLayout = rowLabel.GetComponent<LayoutElement>();
            labelLayout.preferredWidth = 230f;
            labelLayout.flexibleWidth = 1f;
            rowLabel.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject control = CreateUiObject("Control", row.transform, typeof(LayoutElement));
            LayoutElement controlLayout = control.GetComponent<LayoutElement>();
            controlLayout.preferredWidth = 310f;
            controlLayout.preferredHeight = 58f;
            controlParent = control.transform;
            return row;
        }

        private static void ConfigureScreen(UIScreen screen, CanvasGroup group, Selectable selection)
        {
            SetObject(screen, "canvasGroup", group);
            SetObject(screen, "defaultSelection", selection);
        }

        private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
        {
            var types = new List<Type> { typeof(RectTransform) };
            types.AddRange(components);
            var gameObject = new GameObject(name, types.ToArray());
            gameObject.layer = 5;
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }

        private static T AddOrGet<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void Stretch(RectTransform rect, float horizontalInset = 0f, float verticalInset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalInset, verticalInset);
            rect.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        }

        private static void SetObject(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new MissingFieldException(target.GetType().Name, propertyName);
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            FieldInfo field = target.GetType().GetField(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            field?.SetValue(target, value);
            serialized.Update();
            if (value != null && property.objectReferenceValue != value)
            {
                throw new InvalidOperationException(
                    $"Could not assign '{AssetDatabase.GetAssetPath(value)}' to {target.GetType().Name}.{propertyName}.");
            }
            EditorUtility.SetDirty(target);
        }
    }
}
