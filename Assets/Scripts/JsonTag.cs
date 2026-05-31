// JsonTag.cs
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
        private RectTransform rectTransform;
        private Canvas canvas;

        void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();

            // Use the ROOT canvas. If this tag lives inside a nested canvas, dragging
            // relative to the inner one can still produce small offsets; the root canvas
            // is what we want as the temporary parent during drag.
            Canvas c = GetComponentInParent<Canvas>();
            canvas = (c != null) ? c.rootCanvas : null;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            originalParent = transform.parent;

            // Re-parent to the root canvas so the tag can move freely across the screen
            // AND so its anchoredPosition shares a coordinate space with screen-to-local
            // conversions. Pass worldPositionStays=true so the tag does NOT snap on pickup
            // (this is what was causing the big upward jump previously).
            if (canvas != null)
                transform.SetParent(canvas.transform, true);

            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (canvas == null) return;

            // Delta-based movement: works correctly for Screen Space - Overlay AND
            // Screen Space - Camera canvases regardless of pivot/anchor configuration.
            // Dividing by scaleFactor handles Canvas Scaler resolution scaling.
            rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;

            if (originalParent == null)
            {
                // Safety fallback — shouldn't happen in normal flow.
                return;
            }

            // Compute the new sibling index BEFORE re-parenting. Right now `transform`
            // is still a child of the canvas, so originalParent's children are all the
            // OTHER tags — no need to skip ourselves in the loop.
            //
            // Vertical layout convention: higher world Y == top of the list == lower
            // sibling index. We find the first existing child whose Y is below the
            // dragged tag's Y, and insert just before it.
            int newIndex = originalParent.childCount; // default: drop at the bottom
            for (int i = 0; i < originalParent.childCount; i++)
            {
                if (transform.position.y > originalParent.GetChild(i).position.y)
                {
                    newIndex = i;
                    break;
                }
            }

            // Re-parent back to the list container and apply the computed index.
            // worldPositionStays=true so the tag doesn't snap visually on drop;
            // a LayoutGroup on originalParent will animate/snap it next frame.
            transform.SetParent(originalParent, true);
            transform.SetSiblingIndex(newIndex);
        }
    }
}