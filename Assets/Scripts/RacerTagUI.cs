using UnityEngine;
using UnityEngine.UI;

namespace TopDownRace
{
    public class RacerTagUI : MonoBehaviour
    {
        public Text positionText;
        public Text shortNameText;
        public Image iconImage;
        public Image countryFlagImage;
        public Text tireTypeText;
        public Text intervalText;
        [HideInInspector] public float intervalGap;
        private Rivals racer; // Reference to the associated Rivals component
        [HideInInspector] public CarData linkedCarData;
        private float pinnedInterval = 0f; // Stores the interval when car finishes
        private bool isPinned = false; // Tracks if interval is pinned


        public void UpdateTag(int position, CarData data, float interval, int lapDifference, CarPhysics physics)
        {
            positionText.text = position.ToString();
            shortNameText.text = data.shortName;
            iconImage.sprite = data.icon;
            countryFlagImage.sprite = data.countryFlag;
            if (racer != null && racer.hasFinishedRace && !isPinned)
            {
                pinnedInterval = interval; // Pin the interval at finish
                isPinned = true;
                intervalText.text = "FINISH";
                intervalText.color = Color.green;
            }
            else if (isPinned)
            {
                intervalText.text = "FINISH";
                intervalText.color = Color.green;
            }
            else if (physics.health <= 0)
            {
                intervalText.text = "OUT";
                intervalText.color = Color.red; // Optional: Highlight "OUT" in red
            }
            else if (lapDifference > 1)
            {
                intervalText.text = $"-{lapDifference - 1} lap{(lapDifference > 1 ? "s" : "")}";
                intervalGap = 0f; // Reset intervalGap when showing lap difference
            }
            else
            {
                intervalGap = interval;
                intervalText.text = interval <= 0 ? "LEADER" : $"+{interval:F2}s";
            }

            // Tire type display (S / M / H) with colors
            if (tireTypeText != null && physics != null)
            {
                switch (physics.tireType)
                {
                    case CarPhysics.TireType.Soft:
                        tireTypeText.text = "S";
                        tireTypeText.color = Color.red;
                        break;
                    case CarPhysics.TireType.Medium:
                        tireTypeText.text = "M";
                        tireTypeText.color = Color.yellow;
                        break;
                    case CarPhysics.TireType.Hard:
                        tireTypeText.text = "H";
                        tireTypeText.color = Color.white;
                        break;
                    default:
                        tireTypeText.text = "";
                        break;
                }
            }
            else
            {
                tireTypeText.text = "";
            }
        }
    }
}