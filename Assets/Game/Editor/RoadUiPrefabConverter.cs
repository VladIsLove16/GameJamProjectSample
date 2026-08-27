using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoadOfLife.Editor
{
    public static class RoadUiPrefabConverter
    {
        private const string PrefabRoot = "Assets/Prefabs/UI/Road Of Life";
        private const string MainMenuPath = "Assets/JamStarter/Scenes/MainMenu.unity";
        private const string SandboxPath = "Assets/JamStarter/Scenes/Sandbox.unity";

        [MenuItem("Tools/Road of Life/Convert UI To Prefabs", false, 104)]
        public static void ConvertUiToPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play Mode before converting UI objects to prefabs.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder(PrefabRoot);
            int converted = 0;
            converted += ConvertScene(MainMenuPath, "MainMenu", new[] { "Main Screen", "Settings Screen", "Intro Screen" });
            converted += ConvertScene(SandboxPath, "Sandbox", new[]
            {
                "Driving Panel", "Road Card", "Left Choice", "Right Choice", "Bipolar Scales",
                "Choice Result", "Upgrade Overlay", "Ending Overlay"
            });

            EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Road of Life UI prefab conversion complete: {converted} objects processed.");
        }

        private static int ConvertScene(string scenePath, string folderName, IReadOnlyCollection<string> objectNames)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"Could not open UI scene {scenePath}.");
                return 0;
            }

            string folder = $"{PrefabRoot}/{folderName}";
            EnsureFolder(folder);
            int converted = 0;
            foreach (string objectName in objectNames)
            {
                GameObject target = FindSceneObject(scene, objectName);
                if (target == null)
                {
                    continue;
                }

                if (PrefabUtility.IsPartOfPrefabInstance(target))
                {
                    Debug.Log($"Skipped {scenePath}/{objectName}: already belongs to a prefab.");
                    continue;
                }

                string prefabPath = $"{folder}/{SanitizeFileName(objectName)}.prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(target, prefabPath, InteractionMode.UserAction);
                converted++;
            }

            foreach (BipolarStatView stat in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<BipolarStatView>(true)))
            {
                GameObject target = stat.gameObject;
                if (PrefabUtility.IsPartOfPrefabInstance(target))
                {
                    continue;
                }

                string prefabPath = $"{folder}/Characteristic - {SanitizeFileName(target.name)}.prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(target, prefabPath, InteractionMode.UserAction);
                converted++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            return converted;
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                Transform match = transforms.FirstOrDefault(transform => transform.name == objectName);
                if (match != null)
                {
                    return match.gameObject;
                }
            }

            return null;
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

        private static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        }
    }
}
