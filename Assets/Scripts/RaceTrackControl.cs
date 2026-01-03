using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
namespace TopDownRace
{
    public class RaceConfigLaps
    {
        public int totalLaps;
    }
    public class RaceTrackControl : MonoBehaviour
    {
        public static RaceTrackControl m_Main;

        public Checkpoint[] m_Checkpoints;
        public Transform[] m_StartPositions;
        [Header("Alternate Route Settings")]
        public bool AltEnabled = false;
        public Checkpoint[] m_AltCheckpoints;

        private Checkpoint[] baseCheckpoints; // backup of original list

        [Header("Lap System")]
        public int currentLap = 1;          // UI lap count
        public int totalLaps;          // configurable in inspector



        // track how many cars are still in pit
        public int activePitstoppers = 0;

        private void Awake()
        {
            m_Main = this;
            baseCheckpoints = m_Checkpoints; // save base layout
            LoadRaceConfig();
        }
        // Start is called before the first frame update
        private void LoadRaceConfig()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "RaceConfig.json");
            if (!File.Exists(path))
            {
                Debug.LogError("RaceConfig.json not found at " + path);
                totalLaps = 50; // Default value
                return;
            }

            string json = File.ReadAllText(path);
            RaceConfigLaps config = JsonUtility.FromJson<RaceConfigLaps>(json);

            if (config == null)
            {
                Debug.LogError("Invalid RaceConfig.json");
                totalLaps = 50;
                return;
            }

            totalLaps = config.totalLaps;
        }
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        private void RestoreBaseCheckpoints()
        {
            m_Checkpoints = baseCheckpoints;
        }
    }
}