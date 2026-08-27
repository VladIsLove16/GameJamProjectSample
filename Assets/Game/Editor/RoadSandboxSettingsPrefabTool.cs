using System;
using JamStarter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoadOfLife.Editor
{
    public static class RoadSandboxSettingsPrefabTool
    {
        private const string ScenePath = "Assets/JamStarter/Scenes/Sandbox.unity";
        private const string PrefabPath = "Assets/Prefabs/UI/Settings Screen.prefab";
        private const string SettingsName = "Sandbox Settings Screen";

        [MenuItem("Tools/Road of Life/Use Settings Screen Prefab In Sandbox", false, 107)]
        public static void ReplaceSandboxSettingsScreen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play Mode before replacing the Sandbox settings screen.");
                return;
            }

            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            Scene previousActive = SceneManager.GetActiveScene();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                GameObject oldSettings = FindSceneObject(scene, SettingsName);
                GameObject sandboxControllerObject = FindSceneObject(scene, "Sandbox Flow");
                if (prefab == null || oldSettings == null || sandboxControllerObject == null)
                {
                    throw new InvalidOperationException("Sandbox, Settings Screen prefab, or Sandbox Flow is missing.");
                }

                Transform parent = oldSettings.transform.parent;
                int siblingIndex = oldSettings.transform.GetSiblingIndex();
                UnityEngine.Object.DestroyImmediate(oldSettings);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.name = SettingsName;
                instance.transform.SetSiblingIndex(siblingIndex);

                SandboxController controller = sandboxControllerObject.GetComponent<SandboxController>();
                UIScreen screen = instance.GetComponent<UIScreen>();
                SettingsPanel panel = instance.GetComponentInChildren<SettingsPanel>(true);
                if (screen == null || panel == null)
                {
                    throw new InvalidOperationException("Settings Screen prefab must contain UIScreen and SettingsPanel.");
                }

                SetObject(controller, "settingsScreen", screen);
                SetObject(controller, "settingsPanel", panel);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("Sandbox Settings Screen was replaced with Assets/Prefabs/UI/Settings Screen.prefab.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                if (previousActive.IsValid())
                {
                    SceneManager.SetActiveScene(previousActive);
                }
            }
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                {
                    return root;
                }

                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == objectName)
                    {
                        return child.gameObject;
                    }
                }
            }

            return null;
        }

        private static void SetObject(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new MissingFieldException(target.GetType().Name, propertyName);
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
