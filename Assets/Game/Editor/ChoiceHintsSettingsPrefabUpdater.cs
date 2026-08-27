using JamStarter;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RoadOfLife.Editor
{
    [InitializeOnLoad]
    internal static class ChoiceHintsSettingsPrefabUpdater
    {
        private const string PrefabPath = "Assets/Prefabs/UI/Road Of Life/MainMenu/Settings Screen.prefab";

        static ChoiceHintsSettingsPrefabUpdater()
        {
            EditorApplication.delayCall += EnsureToggleExists;
        }

        [MenuItem("Tools/Road of Life/Update Choice Hints Setting", false, 108)]
        private static void EnsureToggleExists()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                return;
            }

            try
            {
                SettingsPanel panel = root.GetComponentInChildren<SettingsPanel>(true);
                SerializedObject serializedPanel = new SerializedObject(panel);
                SerializedProperty hintsProperty = serializedPanel.FindProperty("showChoiceHints");
                if (hintsProperty.objectReferenceValue != null)
                {
                    return;
                }

                Toggle exactToggle = (Toggle)serializedPanel.FindProperty("showExactStats").objectReferenceValue;
                Transform sourceRow = exactToggle.transform.parent.parent;
                GameObject hintsRow = Object.Instantiate(sourceRow.gameObject, sourceRow.parent);
                hintsRow.name = "Choice Hints Row";
                hintsRow.transform.SetSiblingIndex(sourceRow.GetSiblingIndex() + 1);

                foreach (TMP_Text label in hintsRow.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.text.Contains("цифр"))
                    {
                        label.text = "Показывать стрелки на карточках";
                    }
                }

                Toggle hintsToggle = hintsRow.GetComponentInChildren<Toggle>(true);
                hintsToggle.SetIsOnWithoutNotify(false);
                hintsProperty.objectReferenceValue = hintsToggle;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Added the choice hints toggle to the Settings Screen prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
