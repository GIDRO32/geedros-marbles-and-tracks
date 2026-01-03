using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace TopDownRace
{
    public class RaceConfig
    {
        public int lapToPitstop;
        public int pitstopDuration;
    }
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance;

        private List<Rivals> racers = new List<Rivals>();
        public int lapToPitstop;
        public int pitstopDuration;

        private void Awake()
        {
            Instance = this;
            LoadRaceConfig();
        }
        private void LoadRaceConfig()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "RaceConfig.json");
            if (!File.Exists(path))
            {
                Debug.LogError("RaceConfig.json not found at " + path);
                lapToPitstop = 5; // Default value
                pitstopDuration = 1; // Default value
                return;
            }

            string json = File.ReadAllText(path);
            RaceConfig config = JsonUtility.FromJson<RaceConfig>(json);

            if (config == null)
            {
                Debug.LogError("Invalid RaceConfig.json");
                lapToPitstop = 5;
                pitstopDuration = 1;
                return;
            }

            lapToPitstop = config.lapToPitstop;
            pitstopDuration = config.pitstopDuration;
            Debug.Log($"RaceConfig loaded: lapToPitstop={lapToPitstop}, pitstopDuration={pitstopDuration}");
        }
        public void RegisterRacer(Rivals racer)
        {
            if (!racers.Contains(racer))
            {
                racers.Add(racer);
            }
        }

        public void UnregisterRacer(Rivals racer)
        {
            racers.Remove(racer);
        }

        void Update()
        {
            racers = racers.OrderByDescending(r => r.m_FinishedLaps)
                           .ThenByDescending(r => r.GetLapProgress())
                           .ToList();
        }


        float DistanceToNextCheckpoint(Rivals r)
        {
            if (r.m_TargetDestination == null) return Mathf.Infinity;
            return Vector2.Distance(r.transform.position, r.m_TargetDestination.position);
        }

        public List<Rivals> GetSortedRacers()
        {
            return racers;
        }
        public Rivals GetLastActiveRacer()
        {
            var sorted = GetSortedRacers();
            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                CarPhysics car = sorted[i].GetComponent<CarPhysics>();
                if (car != null && car.health > 0)
                {
                    return sorted[i]; // 🆕 Return FIRST healthy racer from the end
                }
            }
            return null; // No active racers
        }

        public bool IsLeader(Rivals racer)
        {
            return racers.Count > 0 && racers[0] == racer;
        }
        public Rivals GetLastRacer()
        {
            return racers.Count > 0 ? racers[racers.Count - 1] : null;
        }
        public Rivals GetLeader()
        {
            return racers.Count > 0 ? racers[0] : null;
        }

        public int GetPosition(Rivals racer)
        {
            return racers.IndexOf(racer) + 1;
        }
    }
}

