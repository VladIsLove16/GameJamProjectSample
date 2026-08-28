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
        private const string ImportedGameAssetRoot = "Assets/Sprites/Game";

        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".psd", ".tga", ".tif", ".tiff" };
        private static readonly string[] AudioExtensions = { ".wav", ".mp3", ".ogg", ".aiff", ".aif" };
        private static readonly string[] IdSuffixes = { "_image", "_sprite", "_card", "_event", "_model", "_prefab", "_sound", "_sfx", "_audio" };

        private static readonly Dictionary<string, string> ImportedAssetIds = new(StringComparer.OrdinalIgnoreCase)
        {
            { NormalizeId("1 - потерян путь"), "road_snow_markers" },
            { NormalizeId("2 - двигатель теряет тягу"), "engine_power_loss" },
            { NormalizeId("3 ляденой гребень"), "ice_ridge" },
            { NormalizeId("4 отражение снега"), "headlight_whiteout" },
            { NormalizeId("5 - трещина"), "fresh_ice_crack" },
            { NormalizeId("6 - воздушная тревога"), "air_alarm" },
            { NormalizeId("7 - подъезжаем к лагерю"), "warming_point" },
            { NormalizeId("8 - посветите пж (("), "road_crew_lighting" },
        };

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
            foreach (string root in GetSearchRoots())
            {
                foreach (string guid in AssetDatabase.FindAssets("", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string extension = Path.GetExtension(path).ToLowerInvariant();
                    string key = GetAssetKey(path);
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
                            importer.mipmapEnabled = false;
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
            }

            ApplyTagFallbacks(result);
            return result;
        }

        private static IEnumerable<string> GetSearchRoots()
        {
            if (AssetDatabase.IsValidFolder(SearchRoot))
            {
                yield return SearchRoot;
            }

            if (AssetDatabase.IsValidFolder(ImportedGameAssetRoot))
            {
                yield return ImportedGameAssetRoot;
            }
        }

        private static string GetAssetKey(string path)
        {
            string key = NormalizeId(Path.GetFileNameWithoutExtension(path));
            if (path.StartsWith(ImportedGameAssetRoot + "/", StringComparison.OrdinalIgnoreCase) &&
                ImportedAssetIds.TryGetValue(key, out string mappedId))
            {
                return NormalizeId(mappedId);
            }

            return key;
        }

        private static void ApplyTagFallbacks(Dictionary<string, AssetMatch> result)
        {
            CopyFallback(result, "road_snow_markers", "deep_snow", "torn_tarpaulin", "passenger_cold", "snow_on_body", "false_track_in_snow");
            CopyFallback(result, "engine_power_loss", "radiator_cover_loose", "wiper_linkage_broken", "tow_stuck_truck", "frostbitten_passenger", "medical_help", "warming_post_fuel");
            CopyFallback(result, "ice_ridge", "ice_survey_pause", "snowed_shore_ramp", "loose_passenger_bench");
            CopyFallback(result, "headlight_whiteout", "windshield_ice", "shifted_route_sign", "hooded_signal_lamp", "loose_blackout_covers", "light_leak_in_body");
            CopyFallback(result, "fresh_ice_crack", "road_repair_timber");
            CopyFallback(result, "air_alarm");
            CopyFallback(result, "warming_point", "snowed_shore_ramp");
            CopyFallback(result, "road_crew_lighting");
        }

        private static void CopyFallback(Dictionary<string, AssetMatch> result, string sourceId, params string[] targetIds)
        {
            if (!result.TryGetValue(NormalizeId(sourceId), out AssetMatch source))
            {
                return;
            }

            foreach (string targetId in targetIds)
            {
                string key = NormalizeId(targetId);
                if (!result.TryGetValue(key, out AssetMatch target))
                {
                    target = new AssetMatch();
                    result[key] = target;
                }

                target.Sprite ??= source.Sprite;
                target.Prefab ??= source.Prefab;
                target.Sound ??= source.Sound;
            }
        }

        private static bool IsCardAssetPath(string path)
        {
            return path.StartsWith(SearchRoot + "/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(ImportedGameAssetRoot + "/", StringComparison.OrdinalIgnoreCase);
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
