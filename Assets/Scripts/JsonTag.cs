// JsonTag.cs (New Script)
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace TopDownRace
{
    [System.Serializable]
    public class CarDefinition
    {
        public string id;
        public string nickname;
        public string shortName;
        public string image;
        public string icon;
        public string countryFlag;
    }

    public class JsonTag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Text nicknameText;
        public Image iconImage;
        public Image countryFlagImage;
        public CarDefinition carData;

        private CanvasGroup canvasGroup;
        private Transform originalParent;
        private RectTransform rectTransform; // Reference to this object's RectTransform
        private Canvas canvas; // Reference to the Canvas

        void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>(); // Initialize RectTransform
            canvas = GetComponentInParent<Canvas>(); // Find the parent Canvas
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            originalParent = transform.parent;
            transform.SetParent(originalParent.parent);
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (canvas != null)
            {
                // Convert screen position to local position in canvas
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.transform as RectTransform,
                    eventData.position,
                    canvas.worldCamera,
                    out localPos
                );
                rectTransform.anchoredPosition = localPos;
            }
            else
            {
                // Fallback if canvas not found
                rectTransform.position = eventData.position;
            }
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            transform.SetParent(originalParent);
            canvasGroup.blocksRaycasts = true;

            // Calculate new sibling index based on y position (assuming vertical layout, higher y = top)
            int newIndex = originalParent.childCount - 1;
            for (int i = 0; i < originalParent.childCount; i++)
            {
                if (transform.position.y > originalParent.GetChild(i).position.y)
                {
                    newIndex = i;
                    if (transform.GetSiblingIndex() < newIndex) newIndex--;
                    break;
                }
            }
            transform.SetSiblingIndex(newIndex);
        }
    }
}