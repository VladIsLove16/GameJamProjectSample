using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RoadOfLife.Editor
{
    [InitializeOnLoad]
    public static class RoadStatPrefabConfigurator
    {
        private const string HudPath = "Assets/Prefabs/UI/Road Of Life/Sandbox/HUD.prefab";
        private const string StatPrefabPath = "Assets/Prefabs/UI/Road Of Life/Sandbox/Stat View.prefab";
        private const string IndicatorSpritePath = "Assets/Sprites/Game/newnewnew_ind.png";
        private const string AutoRunKey = "RoadOfLife.StatPrefabConfigurator.v2";

        static RoadStatPrefabConfigurator()
        {
            EditorApplication.delayCall += AutoConfigureOnce;
        }

        [MenuItem("Tools/Road of Life/Configure Stat Prefab", false, 104)]
        public static void Configure()
        {
            GameObject statPrefab = EnsureStatPrefab();
            ApplyToHud(statPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Road stat HUD configured from Stat View prefab.");
        }

        private static void AutoConfigureOnce()
        {
            if (EditorPrefs.GetBool(AutoRunKey, false))
            {
                return;
            }

            EditorPrefs.SetBool(AutoRunKey, true);
            Configure();
        }

        private static GameObject EnsureStatPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject("Stat View", typeof(RectTransform), typeof(Image), typeof(BipolarStatView));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(430f, 140f);

            Image background = root.GetComponent<Image>();
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IndicatorSpritePath);
            background.color = Color.white;
            background.raycastTarget = false;

            TMP_Text icon = CreateText("Icon", rootRect, "⏱", 28f, TextAlignmentOptions.Center);
            Place(icon.rectTransform, new Vector2(0.045f, 0.49f), new Vector2(0.165f, 0.9f));

            TMP_Text label = CreateText("Label", rootRect, "ТЕМП", 19f, TextAlignmentOptions.Left);
            Place(label.rectTransform, new Vector2(0.19f, 0.53f), new Vector2(0.74f, 0.91f));
            label.fontStyle = FontStyles.Bold;

            TMP_Text value = CreateText("Value", rootRect, "50", 21f, TextAlignmentOptions.Right);
            Place(value.rectTransform, new Vector2(0.76f, 0.53f), new Vector2(0.94f, 0.91f));
            value.fontStyle = FontStyles.Bold;

            Image track = CreateImage("Track", rootRect, new Color(0.85f, 0.93f, 0.95f, 0.16f));
            Place(track.rectTransform, new Vector2(0.055f, 0.2f), new Vector2(0.945f, 0.43f));

            Image lowDanger = CreateImage("Low Danger", track.rectTransform, new Color(0.48f, 0.12f, 0.09f, 0.28f));
            Place(lowDanger.rectTransform, new Vector2(0f, 0f), new Vector2(0.15f, 1f));

            Image highDanger = CreateImage("High Danger", track.rectTransform, new Color(0.48f, 0.12f, 0.09f, 0.28f));
            Place(highDanger.rectTransform, new Vector2(0.85f, 0f), new Vector2(1f, 1f));

            Image fill = CreateImage("Fill", track.rectTransform, new Color(0.33f, 0.71f, 0.75f, 0.38f));
            Place(fill.rectTransform, Vector2.zero, Vector2.one);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0.5f;

            Image marker = CreateImage("Value Marker", track.rectTransform, new Color(0.82f, 0.87f, 0.82f, 1f));
            RectTransform markerRect = marker.rectTransform;
            markerRect.anchorMin = new Vector2(0.5f, 0f);
            markerRect.anchorMax = new Vector2(0.5f, 1f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.anchoredPosition = Vector2.zero;
            markerRect.sizeDelta = new Vector2(6f, 12f);

            root.GetComponent<BipolarStatView>().Configure(
                "ТЕМП",
                label,
                value,
                markerRect,
                fill,
                marker,
                lowDanger,
                highDanger);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, StatPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ApplyToHud(GameObject statPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                Transform statsPanel = Find(root.transform, "Bipolar Scales");
                RoadGameView view = root.GetComponentInChildren<RoadGameView>(true);
                if (statsPanel == null || view == null)
                {
                    return;
                }

                while (statsPanel.childCount > 0)
                {
                    Object.DestroyImmediate(statsPanel.GetChild(0).gameObject);
                }

                string[] names = { "ТЕМП", "ДВИГАТЕЛЬ", "ВИДИМОСТЬ", "ГРУЗ" };
                string[] icons = { "⏱", "⚙", "◉", "▣" };
                string[] properties = { "tempoView", "engineView", "visibilityView", "loadView" };

                var serializedView = new SerializedObject(view);
                for (int index = 0; index < names.Length; index++)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(statPrefab, statsPanel);
                    instance.name = $"{names[index]} Scale";
                    RectTransform rect = instance.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(index * 0.25f + 0.006f, 0f);
                    rect.anchorMax = new Vector2((index + 1) * 0.25f - 0.006f, 1f);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    rect.anchoredPosition = Vector2.zero;
                    rect.localScale = Vector3.one;

                    SetText(instance.transform, "Icon", icons[index]);
                    SetText(instance.transform, "Label", names[index]);

                    BipolarStatView statView = instance.GetComponent<BipolarStatView>();
                    statView.SetDisplayName(names[index]);
                    serializedView.FindProperty(properties[index]).objectReferenceValue = statView;
                }

                serializedView.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, float size, TextAlignmentOptions alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            gameObject.layer = LayerMask.NameToLayer("UI");
            gameObject.transform.SetParent(parent, false);

            TMP_Text text = gameObject.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = size;
            text.color = new Color(0.85f, 0.93f, 0.95f, 1f);
            text.alignment = alignment;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.layer = LayerMask.NameToLayer("UI");
            gameObject.transform.SetParent(parent, false);

            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Place(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void SetText(Transform root, string name, string value)
        {
            Transform child = root.Find(name);
            TMP_Text text = child != null ? child.GetComponent<TMP_Text>() : null;
            if (text != null)
            {
                text.text = value;
            }
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform found = Find(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
