using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoadOfLife.Editor
{
    public sealed class RoadCardAssetIndexer : AssetPostprocessor
    {
        private const string CardsPath = "Assets/Game/Data/Cards.tsv.txt";
        private const string LibraryPath = "Assets/Game/Data/RoadCardPresentationLibrary.asset";
        private const string SearchRoot = "Assets/Game/Art/Cards";

        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".psd", ".tga", ".tif", ".tiff" };
        private static readonly string[] AudioExtensions = { ".wav", ".mp3", ".ogg", ".aiff", ".aif" };
        private static readonly string[] IdSuffixes = { "_image", "_sprite", "_card", "_event", "_model", "_prefab", "_sound", "_sfx", "_audio" };

        [MenuItem("Tools/Road of Life/Index Card Assets", false, 102)]
        public static void IndexCardAssets()
        {
            if (Index(out string report))
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogWarning(report);
            }
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets.Any(IsCardAssetPath) || movedAssets.Any(IsCardAssetPath))
            {
                Index(out _);
            }
        }

        private static bool Index(out string report)
        {
            TextAsset cardsAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(CardsPath);
            if (cardsAsset == null)
            {
                report = $"Card asset indexing skipped: missing {CardsPath}.";
                return false;
            }

            IReadOnlyList<RoadCard> cards;
            try
            {
                cards = CardTsvParser.Parse(cardsAsset);
            }
            catch (Exception exception)
            {
                report = $"Card asset indexing skipped: {exception.Message}";
                return false;
            }

            RoadCardPresentationLibrary library = AssetDatabase.LoadAssetAtPath<RoadCardPresentationLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<RoadCardPresentationLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            SerializedObject serializedLibrary = new SerializedObject(library);
            SerializedProperty entriesProperty = serializedLibrary.FindProperty("entries");
            Dictionary<string, SerializedProperty> existing = ReadExistingEntries(entriesProperty);
            Dictionary<string, AssetMatch> matches = FindMatches();
            int matchedCards = 0;

            entriesProperty.arraySize = cards.Count;
            for (int index = 0; index < cards.Count; index++)
            {
                RoadCard card = cards[index];
                SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(index);
                string key = NormalizeId(card.Id);
                entry.FindPropertyRelative("cardId").stringValue = card.Id;

                if (matches.TryGetValue(key, out AssetMatch match))
                {
                    SetObject(entry, "cardSprite", match.Sprite);
                    SetObject(entry, "eventPrefab", match.Prefab);
                    SetObject(entry, "eventSound", match.Sound);
                    matchedCards++;
                }
                else if (existing.TryGetValue(key, out SerializedProperty oldEntry))
                {
                    CopyObjectReference(oldEntry, entry, "cardSprite");
                    CopyObjectReference(oldEntry, entry, "eventPrefab");
                    CopyObjectReference(oldEntry, entry, "eventSound");
                }
            }

            serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            report = $"Indexed {matchedCards}/{cards.Count} card presentations into {LibraryPath}.";
            return true;
        }

        private static Dictionary<string, SerializedProperty> ReadExistingEntries(SerializedProperty entries)
        {
            var result = new Dictionary<string, SerializedProperty>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                string id = entry.FindPropertyRelative("cardId").stringValue;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    result[NormalizeId(id)] = entry.Copy();
                }
            }

            return result;
        }

        private static Dictionary<string, AssetMatch> FindMatches()
        {
            var result = new Dictionary<string, AssetMatch>(StringComparer.OrdinalIgnoreCase);
            if (!AssetDatabase.IsValidFolder(SearchRoot))
            {
                return result;
            }

            foreach (string guid in AssetDatabase.FindAssets("", new[] { SearchRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string extension = Path.GetExtension(path).ToLowerInvariant();
                string key = NormalizeId(Path.GetFileNameWithoutExtension(path));
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (!result.TryGetValue(key, out AssetMatch match))
                {
                    match = new AssetMatch();
                    result[key] = match;
                }

                if (ImageExtensions.Contains(extension))
                {
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.SaveAndReimport();
                    }

                    match.Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                else if (AudioExtensions.Contains(extension))
                {
                    match.Sound = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
                else if (extension == ".prefab")
                {
                    match.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }

            return result;
        }

        private static bool IsCardAssetPath(string path)
        {
            return path.StartsWith(SearchRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeId(string value)
        {
            string key = Path.GetFileNameWithoutExtension(value).Trim().ToLowerInvariant();
            bool changed;
            do
            {
                changed = false;
                foreach (string suffix in IdSuffixes)
                {
                    if (!key.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    key = key.Substring(0, key.Length - suffix.Length);
                    changed = true;
                    break;
                }
            }
            while (changed);

            return key;
        }

        private static void SetObject(SerializedProperty entry, string propertyName, UnityEngine.Object value)
        {
            entry.FindPropertyRelative(propertyName).objectReferenceValue = value;
        }

        private static void CopyObjectReference(
            SerializedProperty source,
            SerializedProperty destination,
            string propertyName)
        {
            destination.FindPropertyRelative(propertyName).objectReferenceValue =
                source.FindPropertyRelative(propertyName).objectReferenceValue;
        }

        private sealed class AssetMatch
        {
            public Sprite Sprite;
            public GameObject Prefab;
            public AudioClip Sound;
        }
    }
}
