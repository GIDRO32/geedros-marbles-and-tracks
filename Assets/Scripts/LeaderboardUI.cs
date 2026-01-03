using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace TopDownRace
{
    public class LeaderboardUI : MonoBehaviour
    {
        public RectTransform contentRoot;
        public GameObject racerTagPrefab;

        private Dictionary<Rivals, RacerTagUI> tagMap = new Dictionary<Rivals, RacerTagUI>();
        public Text lapText;
        private int lastAltOpenLap = -1;
        public int altOpenDuration = 2; // laps pitlane stays open
        private Dictionary<Rivals, int> finishedPositions = new Dictionary<Rivals, int>(); // Tracks fixed positions for finished cars

        void Start()
        {
            var racers = RaceManager.Instance.GetSortedRacers();
            foreach (var racer in racers)
            {
                var carData = racer.GetComponent<CarData>();
                GameObject tagObj = Instantiate(racerTagPrefab, contentRoot);
                RacerTagUI tagUI = tagObj.GetComponent<RacerTagUI>();

                CarPhysics physics = racer.GetComponent<CarPhysics>();
                tagUI.UpdateTag(0, carData, 0f, 0, physics); // Initialize with 0 interval and 0 lap difference

                tagMap[racer] = tagUI;
            }
        }
        // public void CheckTagClick()
        // {

        // }

        void Update()
        {
            var racers = RaceManager.Instance.GetSortedRacers();
            if (racers == null || racers.Count == 0) return;

            // Use leader's lap count and progress as reference
            int leaderLaps = racers[0].m_FinishedLaps;
            float leaderProgress = racers[0].GetRaceProgress();
            int currentPosition = 1;
            for (int i = 0; i < racers.Count; i++)
            {
                Rivals r = racers[i];

                if (!tagMap.ContainsKey(r))
                {
                    var carData = r.GetComponent<CarData>();
                    GameObject tagObj = Instantiate(racerTagPrefab, contentRoot);
                    RacerTagUI tagUI = tagObj.GetComponent<RacerTagUI>();
                    CarPhysics physics = r.GetComponent<CarPhysics>();
                    tagUI.UpdateTag(0, carData, 0f, 0, physics);
                    tagMap[r] = tagUI;
                    tagUI.linkedCarData = carData;
                }

                CarData data = r.GetComponent<CarData>();
                RacerTagUI tag = tagMap[r];
                CarPhysics physicsForTag = r.GetComponent<CarPhysics>();
                int displayPosition;
                if (r.hasFinishedRace && !finishedPositions.ContainsKey(r))
                {
                    // Assign final position when car finishes
                    finishedPositions[r] = currentPosition;
                    Debug.Log($"{r.gameObject.name} finished in position {currentPosition}");
                }
                displayPosition = finishedPositions.ContainsKey(r) ? finishedPositions[r] : currentPosition;
                currentPosition++; // Increment for next active racer
                // Calculate lap difference
                int lapDifference = leaderLaps - r.m_FinishedLaps;
                float intervalSeconds = 0f;

                // Only calculate time interval if on the same lap
                if (lapDifference == 0 || lapDifference >= 1)
                {
                    float gap = leaderProgress - r.GetRaceProgress();
                    intervalSeconds = gap * 1.5f; // Adjust scale to taste
                }



                // Smoothly move tag to its slot
                Vector3 targetPos = new Vector3(120, -i * 32, 0); // row spacing

                if (r.hasFinishedRace)
                {
                    tag.transform.localPosition = targetPos; // Pin to fixed position
                }
                else
                {
                    tag.transform.localPosition = Vector3.Lerp(
                        tag.transform.localPosition,
                        targetPos,
                        Time.deltaTime * 10f
                    );

                    // Update tag with position, data, interval (or lap difference), and physics
                    tag.UpdateTag(i + 1, data, intervalSeconds, lapDifference, physicsForTag);
                    tag.transform.localPosition = Vector3.Lerp(
    tag.transform.localPosition,
    targetPos,
    Time.deltaTime * 10f
);
                }
            }
            if (Input.GetMouseButtonDown(0))
            {
                PointerEventData pointerData = new PointerEventData(EventSystem.current);
                pointerData.position = Input.mousePosition;

                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                foreach (var result in results)
                {
                    RacerTagUI tag = result.gameObject.GetComponentInParent<RacerTagUI>();
                    if (tag != null)
                    {
                        CarData data = tag.linkedCarData;
                        if (data != null)
                        {
                            CameraFollow cam = FindObjectOfType<CameraFollow>();
                            if (cam != null)
                            {
                                cam.FocusRacerById(data.id);
                            }
                        }
                        break;
                    }
                }
            }

            if (RaceTrackControl.m_Main != null)
            {
                int lap = RaceTrackControl.m_Main.currentLap;
                int total = RaceTrackControl.m_Main.totalLaps;
                lapText.text = "Lap: " + lap + "/" + total;
                if (lap > total)
                {
                     lapText.text = "THE END!";
                }
            }

        }
    }
}