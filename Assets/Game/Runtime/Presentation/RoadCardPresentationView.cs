using JamStarter;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace RoadOfLife
{
    [DisallowMultipleComponent]
    public sealed class RoadCardPresentationView : MonoBehaviour
    {
        [SerializeField] private RoadCardPresentationLibrary library;
        [SerializeField] private Image cardImage;
        [SerializeField] private GameObject cardImageRoot;
        [SerializeField] private Transform eventModelAnchor;
        [SerializeField] private GameObject eventModelRoot;
        private AudioService audioService;
        private GameObject activeModel;

        [Inject]
        private void Construct(AudioService audioService)
        {
            this.audioService = audioService;
        }

        public void Show(RoadCard card)
        {
            if (card == null)
            {
                Clear();
                return;
            }

            if (library == null || !library.TryGet(card.Id, out RoadCardPresentationLibrary.Entry entry))
            {
                ClearVisuals();
                return;
            }

            if (cardImage != null)
            {
                cardImage.sprite = entry.CardSprite;
                cardImage.preserveAspect = true;
            }

            SetOptionalRoot(cardImageRoot, entry.CardSprite != null);
            ReplaceModel(entry.EventPrefab);
            if (entry.EventSound != null)
            {
                audioService?.PlaySfx(entry.EventSound);
            }
        }

        public void Clear()
        {
            ClearVisuals();
        }

        private void ClearVisuals()
        {
            if (cardImage != null)
            {
                cardImage.sprite = null;
            }

            SetOptionalRoot(cardImageRoot, false);
            ReplaceModel(null);
        }

        private void ReplaceModel(GameObject prefab)
        {
            if (activeModel != null)
            {
                Destroy(activeModel);
                activeModel = null;
            }

            SetOptionalRoot(eventModelRoot, prefab != null);
            if (prefab == null || eventModelAnchor == null)
            {
                return;
            }

            activeModel = Instantiate(prefab, eventModelAnchor, false);
        }

        private static void SetOptionalRoot(GameObject root, bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
            }
        }

        private void OnDestroy()
        {
            if (activeModel != null)
            {
                Destroy(activeModel);
            }
        }
    }
}
