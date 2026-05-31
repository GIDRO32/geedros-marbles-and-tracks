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
    
    // Track which display positions are taken by finished racers
    HashSet<int> takenPositions = new HashSet<int>();
    
    // First pass: Update finished racers' positions
    foreach (var r in racers)
    {
        if (r.hasFinishedRace && !finishedPositions.ContainsKey(r))
        {
            // Find the lowest available position
            int position = 1;
            while (takenPositions.Contains(position) || IsPositionTakenByFinished(position))
            {
                position++;
            }
            finishedPositions[r] = position;
            Debug.Log($"{r.gameObject.name} finished in position {position}");
        }
        
        if (finishedPositions.ContainsKey(r))
        {
            takenPositions.Add(finishedPositions[r]);
        }
    }
    
    // Second pass: Calculate display positions for active racers
    int currentActivePosition = 1;
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
        
        // Determine display position and visual index
        int displayPosition;
        int visualIndex; // Where the tag appears in the list
        
        if (r.hasFinishedRace)
        {
            // Finished racer - use locked position
            displayPosition = finishedPositions[r];
            visualIndex = finishedPositions[r] - 1; // Convert to 0-based index
        }
        else
        {
            // Active racer - skip positions taken by finished racers
            while (takenPositions.Contains(currentActivePosition))
            {
                currentActivePosition++;
            }
            displayPosition = currentActivePosition;
            visualIndex = currentActivePosition - 1;
            currentActivePosition++;
        }
        
        // Calculate lap difference and interval
        int lapDifference = leaderLaps - r.m_FinishedLaps;
        float intervalSeconds = 0f;

        if (lapDifference == 0 || lapDifference >= 1)
        {
            float gap = leaderProgress - r.GetRaceProgress();
            intervalSeconds = gap * 1.5f;
        }

        // Update tag data
        tag.UpdateTag(displayPosition, data, intervalSeconds, lapDifference, physicsForTag);

        // Calculate target position for smooth movement
        Vector3 targetPos = new Vector3(140, (-visualIndex-1) * 60, 0);

        if (r.hasFinishedRace)
        {
            // Smooth transition to final position, then lock
            float distance = Vector3.Distance(tag.transform.localPosition, targetPos);
            
            if (distance > 0.5f)
            {
                // Still moving to final position
                tag.transform.localPosition = Vector3.Lerp(
                    tag.transform.localPosition,
                    targetPos,
                    Time.deltaTime * 5f // Slower lerp for finished racers
                );
            }
            else
            {
                // Close enough - snap to final position and lock
                tag.transform.localPosition = targetPos;
            }
        }
        else
        {
            // Active racer - normal smooth movement
            tag.transform.localPosition = Vector3.Lerp(
                tag.transform.localPosition,
                targetPos,
                Time.deltaTime * 10f
            );
        }
    }

    // Handle mouse clicks
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

    // Update lap counter
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

// Helper method to check if a position is taken by a finished racer
private bool IsPositionTakenByFinished(int position)
{
    foreach (var finishedPos in finishedPositions.Values)
    {
        if (finishedPos == position)
        {
            return true;
        }
    }
    return false;
}
    }
}