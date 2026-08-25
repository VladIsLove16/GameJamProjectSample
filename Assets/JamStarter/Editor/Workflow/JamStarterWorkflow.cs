using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JamStarter;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamStarter.Editor
{
    public static class JamStarterPaths
    {
        public const string Root = "Assets/JamStarter";
        public const string BootstrapScene = Root + "/Scenes/Bootstrap.unity";
        public const string MainMenuScene = Root + "/Scenes/MainMenu.unity";
        public const string SandboxScene = Root + "/Scenes/Sandbox.unity";

        public static readonly string[] BuildScenes =
        {
            BootstrapScene,
            MainMenuScene,
            SandboxScene,
        };
    }

    public static class JamStarterWorkflow
    {
        [MenuItem("Jam Starter/Play From Bootstrap", priority = 10)]
        public static void PlayFromBootstrap()
        {
            SceneAsset bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(JamStarterPaths.BootstrapScene);
            if (bootstrap == null)
            {
                EditorUtility.DisplayDialog("Jam Starter", "Bootstrap scene is missing. Generate the starter project first.", "OK");
                return;
            }

            EditorSceneManager.playModeStartScene = bootstrap;
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Jam Starter/Stop Using Bootstrap", priority = 11)]
        public static void StopUsingBootstrap()
        {
            EditorSceneManager.playModeStartScene = null;
        }

        [MenuItem("Jam Starter/Configure Build Scenes", priority = 30)]
        public static void ConfigureBuildScenes()
        {
            EditorBuildSettings.scenes = Array.ConvertAll(
                JamStarterPaths.BuildScenes,
                path => new EditorBuildSettingsScene(path, true));
            Debug.Log("Jam Starter build scene order configured: Bootstrap, MainMenu, Sandbox.");
        }

        [MenuItem("Jam Starter/Validate Project", priority = 31)]
        public static void ValidateProjectMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            List<string> errors = ValidateProject(true);
            if (errors.Count == 0)
            {
                EditorUtility.DisplayDialog("Jam Starter", "Validation passed.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Jam Starter",
                    $"Validation found {errors.Count} issue(s). See Console for details.",
                    "OK");
            }
        }

        /// <summary>CI and command-line entry point. Throws when validation fails.</summary>
        public static void ValidateForCommandLine()
        {
            List<string> errors = ValidateProject(true);
            if (errors.Count > 0)
            {
                throw new BuildFailedException(string.Join(Environment.NewLine, errors));
            }

            Debug.Log("Jam Starter command-line validation passed.");
        }

        [MenuItem("Jam Starter/Clear Saved Settings", priority = 50)]
        public static void ClearSavedSettings()
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear saved settings?",
                    "This removes only the Jam Starter settings key from PlayerPrefs.",
                    "Clear",
                    "Cancel"))
            {
                return;
            }

            PlayerPrefs.DeleteKey(SettingsService.PlayerPrefsKey);
            PlayerPrefs.Save();
            Debug.Log("Jam Starter saved settings were cleared.");
        }

        [MenuItem("Jam Starter/Quick Build Active Target", priority = 60)]
        public static void QuickBuildActiveTarget()
        {
            List<string> errors = ValidateBuildConfiguration();
            if (errors.Count > 0)
            {
                LogErrors(errors);
                EditorUtility.DisplayDialog("Jam Starter", "Build configuration is invalid. See Console.", "OK");
                return;
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string outputPath = GetBuildOutputPath(target);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? outputPath);

            var options = new BuildPlayerOptions
            {
                scenes = GetEnabledBuildScenes(),
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build completed: {outputPath} ({report.summary.totalSize / 1048576f:F1} MB)");
            }
            else
            {
                Debug.LogError($"Build failed: {report.summary.result}");
            }
        }

        public static List<string> ValidateProject(bool inspectScenes)
        {
            List<string> errors = ValidateBuildConfiguration();
            if (!inspectScenes || errors.Count > 0)
            {
                LogErrors(errors);
                return errors;
            }

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for (int index = 0; index < JamStarterPaths.BuildScenes.Length; index++)
                {
                    Scene scene = EditorSceneManager.OpenScene(JamStarterPaths.BuildScenes[index], OpenSceneMode.Single);
                    ValidateScene(scene, errors);
                }
            }
            finally
            {
                if (originalSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }

            LogErrors(errors);
            return errors;
        }

        public static List<string> ValidateBuildConfiguration()
        {
            var errors = new List<string>();

            for (int index = 0; index < JamStarterPaths.BuildScenes.Length; index++)
            {
                string expectedPath = JamStarterPaths.BuildScenes[index];
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(expectedPath) == null)
                {
                    errors.Add($"Missing scene: {expectedPath}");
                }

                if (EditorBuildSettings.scenes.Length <= index ||
                    !EditorBuildSettings.scenes[index].enabled ||
                    EditorBuildSettings.scenes[index].path != expectedPath)
                {
                    errors.Add($"Build scene {index} must be enabled and point to {expectedPath}.");
                }
            }

            return errors;
        }

        private static void ValidateScene(Scene scene, List<string> errors)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    GameObject gameObject = transforms[transformIndex].gameObject;
                    int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                    if (missingScripts > 0)
                    {
                        errors.Add($"{scene.name}/{GetHierarchyPath(gameObject)} has {missingScripts} missing script(s).");
                    }

                    MonoBehaviour[] behaviours = gameObject.GetComponents<MonoBehaviour>();
                    for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
                    {
                        MonoBehaviour behaviour = behaviours[behaviourIndex];
                        if (behaviour != null && behaviour.GetType().Assembly.GetName().Name == "JamStarter.Runtime")
                        {
                            ValidateDirectObjectReferences(scene, behaviour, errors);
                        }
                    }
                }
            }
        }

        private static void ValidateDirectObjectReferences(
            Scene scene,
            MonoBehaviour behaviour,
            List<string> errors)
        {
            var serialized = new SerializedObject(behaviour);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.depth != 0 || property.name == "m_Script" ||
                    property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                FieldInfo field = FindField(behaviour.GetType(), property.name);
                if (field == null || field.GetCustomAttribute<SerializeField>() == null)
                {
                    continue;
                }

                if (property.objectReferenceValue == null)
                {
                    errors.Add(
                        $"{scene.name}/{GetHierarchyPath(behaviour.gameObject)}: " +
                        $"{behaviour.GetType().Name}.{property.name} is not assigned.");
                }
            }
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            string path = gameObject.name;
            Transform parent = gameObject.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private static void LogErrors(List<string> errors)
        {
            for (int index = 0; index < errors.Count; index++)
            {
                Debug.LogError("Jam Starter validation: " + errors[index]);
            }
        }

        private static string[] GetEnabledBuildScenes()
        {
            var paths = new List<string>();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int index = 0; index < scenes.Length; index++)
            {
                if (scenes[index].enabled)
                {
                    paths.Add(scenes[index].path);
                }
            }

            return paths.ToArray();
        }

        private static string GetBuildOutputPath(BuildTarget target)
        {
            string productName = SanitizeFileName(PlayerSettings.productName);
            string root = Path.Combine("Builds", target.ToString());

            return target switch
            {
                BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 => Path.Combine(root, productName + ".exe"),
                BuildTarget.StandaloneOSX => Path.Combine(root, productName + ".app"),
                BuildTarget.WebGL => Path.Combine(root, productName),
                BuildTarget.Android => Path.Combine(root, productName + ".apk"),
                _ => Path.Combine(root, productName),
            };
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "JamGame" : value;
        }
    }

    public sealed class JamStarterBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            List<string> errors = JamStarterWorkflow.ValidateBuildConfiguration();
            if (errors.Count > 0)
            {
                throw new BuildFailedException(string.Join(Environment.NewLine, errors));
            }
        }
    }
}
