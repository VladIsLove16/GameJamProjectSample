using UnityEditor;
using UnityEngine;

namespace RoadOfLife.Editor
{
    public static class RoadImportedAssetsConfigurator
    {
        private const string DrivingPanelPath = "Assets/Prefabs/UI/Road Of Life/Sandbox/Driving Panel.prefab";

        [MenuItem("Tools/Road of Life/Configure Imported Game Assets", false, 103)]
        public static void Configure()
        {
            RoadCardAssetIndexer.IndexCardAssets();
            ConfigureAudioView();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigureAudioView()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DrivingPanelPath);
            if (prefab == null)
            {
                Debug.LogWarning($"Imported asset audio skipped: missing {DrivingPanelPath}.");
                return;
            }

            RoadAudioView audioView = prefab.GetComponentInChildren<RoadAudioView>(true);
            if (audioView == null)
            {
                Debug.LogWarning($"Imported asset audio skipped: {DrivingPanelPath} has no {nameof(RoadAudioView)}.");
                return;
            }

            var serialized = new SerializedObject(audioView);
            SetClip(serialized, "cardChoiceSound", "Assets/Sprites/Game/click.wav");
            SetClip(serialized, "consequenceSound", "Assets/Sprites/Game/smena-volna.ogg");
            SetClip(serialized, "upgradeSound", "Assets/Sprites/Game/radio-rech3.ogg");
            SetClip(serialized, "failureSound", "Assets/Sprites/Game/smena-volna.ogg");
            SetClip(serialized, "victorySound", "Assets/Sprites/Game/music-radio-3.ogg");
            SetClip(serialized, "startEngineSound", "Assets/Sprites/Game/zapuck-engine.ogg");
            SetClip(serialized, "drivingAmbience", "Assets/Sprites/Game/engine-edet.ogg");
            SetClip(serialized, "radioMusic", "Assets/Sprites/Game/music-radio-2.ogg");
            serialized.FindProperty("playAudioOnStart").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(audioView);
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log("Configured imported audio clips on the Driving Panel prefab.");
        }

        private static void SetClip(SerializedObject serialized, string propertyName, string path)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
        }
    }
}
