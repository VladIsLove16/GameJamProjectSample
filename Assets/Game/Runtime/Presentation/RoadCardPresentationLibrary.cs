using System;
using UnityEngine;

namespace RoadOfLife
{
    [CreateAssetMenu(fileName = "RoadCardPresentationLibrary", menuName = "Road of Life/Card Presentation Library")]
    public sealed class RoadCardPresentationLibrary : ScriptableObject
    {
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public bool TryGet(string cardId, out Entry entry)
        {
            if (entries != null)
            {
                foreach (Entry candidate in entries)
                {
                    if (candidate != null && string.Equals(candidate.CardId, cardId, StringComparison.OrdinalIgnoreCase))
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string cardId;
            [SerializeField] private Sprite cardSprite;
            [SerializeField] private GameObject eventPrefab;
            [SerializeField] private AudioClip eventSound;

            public string CardId => cardId;
            public Sprite CardSprite => cardSprite;
            public GameObject EventPrefab => eventPrefab;
            public AudioClip EventSound => eventSound;
        }
    }
}
