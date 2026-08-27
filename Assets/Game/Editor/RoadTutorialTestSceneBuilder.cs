using System;
using System.Collections.Generic;
using JamStarter;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoadOfLife.Editor
{
    public static class RoadTutorialTestSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/TutorialTest.unity";
        private static readonly Color Background = new Color32(5, 14, 25, 255);
        private static readonly Color Panel = new Color32(15, 42, 57, 255);
        private static readonly Color Frost = new Color32(216, 236, 241, 255);
        private static readonly Color Muted = new Color32(131, 166, 177, 255);
        private static readonly Color Accent = new Color32(84, 180, 190, 255);
        private static readonly Color Warm = new Color32(220, 188, 111, 255);

        [MenuItem("Tools/Road of Life/Create Tutorial Test Scene", false, 105)]
        public static void CreateTutorialTestScene()
        {
            EnsureFolder("Assets/Game/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();

            Canvas canvas = CreateCanvas();
            RectTransform root = CreateRect("Tutorial Test UI", canvas.transform);
            Stretch(root);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = Background;
            background.raycastTarget = true;

            RoadTutorialTestController controller = root.gameObject.AddComponent<RoadTutorialTestController>();
            IntroSequenceView tutorial = BuildTutorial(root);
            SetObject(controller, "tutorial", tutorial);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"Tutorial test scene created at {ScenePath}.");
        }

        private static IntroSequenceView BuildTutorial(RectTransform parent)
        {
            RectTransform root = CreateRect("Tutorial Overlay", parent);
            Stretch(root);
            IntroSequenceView view = root.gameObject.AddComponent<IntroSequenceView>();
            GameObject[] panels = new GameObject[5];
            string[] headings = { "ЦЕЛЬ РЕЙСА", "ДОРОГА ЖИЗНИ", "КАК ИГРАТЬ", "ХАРАКТЕРИСТИКИ", "НАЧАЛО ПУТИ" };
            string[] bodies =
            {
                "Доставьте груз в Ленинград, а затем вывезите людей через замёрзшее Ладожское озеро.",
                "Дорога жизни была ледовой связью осаждённого города с Большой землёй.",
                "Потяните карточку влево или вправо, чтобы выбрать решение. Подтвердите последствия.",
                "Следите за темпом, двигателем, видимостью и нагрузкой. Край шкалы означает поражение.",
                "Между рейсами выбирайте улучшения трассы. Проведите три рейса и завершите смену.",
            };

            for (int index = 0; index < panels.Length; index++)
            {
                RectTransform panel = CreatePanel($"Tutorial Panel {index + 1}", root, Panel, new Vector2(0.2f, 0.25f), new Vector2(0.8f, 0.8f));
                TMP_Text heading = CreateText($"Heading {index + 1}", panel, headings[index], 38f, Warm, TextAlignmentOptions.Center);
                Place(heading.rectTransform, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.9f));
                TMP_Text body = CreateText($"Body {index + 1}", panel, bodies[index], 26f, Frost, TextAlignmentOptions.Center);
                Place(body.rectTransform, new Vector2(0.1f, 0.24f), new Vector2(0.9f, 0.65f));
                panels[index] = panel.gameObject;
            }

            TMP_Text progress = CreateText("Progress", root, "1/5", 20f, Muted, TextAlignmentOptions.Center);
            Place(progress.rectTransform, new Vector2(0.43f, 0.16f), new Vector2(0.57f, 0.21f));
            Button back = CreateButton("Back", root, "НАЗАД", new Vector2(0.18f, 0.07f), new Vector2(0.36f, 0.14f), Panel);
            Button next = CreateButton("Next", root, "ДАЛЕЕ", new Vector2(0.64f, 0.07f), new Vector2(0.82f, 0.14f), Accent);
            Button skip = CreateButton("Skip", root, "ПРОПУСТИТЬ", new Vector2(0.39f, 0.02f), new Vector2(0.61f, 0.075f), Panel);
            Button start = CreateButton("Start", root, "НАЧАТЬ РЕЙС", new Vector2(0.58f, 0.07f), new Vector2(0.82f, 0.14f), Accent);
            view.Configure(panels, progress, next, back, skip, start);
            return view;
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject("Tutorial Test Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = 5;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
        }

        private static void CreateEventSystem()
        {
            GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem));
            Type moduleType = FindInputSystemUiModuleType();
            if (moduleType == null || !typeof(BaseInputModule).IsAssignableFrom(moduleType))
            {
                throw new InvalidOperationException("InputSystemUIInputModule is unavailable. Install the Input System UI module.");
            }

            BaseInputModule module = (BaseInputModule)eventObject.AddComponent(moduleType);
            moduleType.GetMethod("AssignDefaultActions")?.Invoke(module, null);
        }

        private static Type FindInputSystemUiModuleType()
        {
            const string typeName = "UnityEngine.InputSystem.UI.InputSystemUIInputModule";
            foreach (string assemblyName in new[] { "Unity.InputSystem", "Unity.InputSystem.ForUI" })
            {
                try
                {
                    Type loadedType = System.Reflection.Assembly.Load(assemblyName).GetType(typeName, false);
                    if (loadedType != null)
                    {
                        return loadedType;
                    }
                }
                catch (System.IO.FileNotFoundException)
                {
                }
            }

            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return Type.GetType(typeName + ", Unity.InputSystem.ForUI") ??
                   Type.GetType(typeName + ", Unity.InputSystem");
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject objectToCreate = new GameObject(name, typeof(RectTransform));
            objectToCreate.layer = 5;
            objectToCreate.transform.SetParent(parent, false);
            return objectToCreate.GetComponent<RectTransform>();
        }

        private static RectTransform CreatePanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rect = CreateRect(name, parent);
            Place(rect, anchorMin, anchorMax);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Place(rect, anchorMin, anchorMax);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            TMP_Text text = CreateText("Label", rect, label, 20f, Frost, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 10f, 4f);
            return button;
        }

        private static void Place(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect, float horizontal = 0f, float vertical = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        private static void SetObject(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

    }
}
