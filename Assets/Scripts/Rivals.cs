using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TopDownRace
{
    public class Rivals : MonoBehaviour
    {
        [HideInInspector]
        public Transform m_TargetDestination;
        [HideInInspector]
        public int m_WaypointsCounter;
        [HideInInspector]
        public int m_FinishedLaps;
        [HideInInspector]
        public bool m_Control = false;

        [Header("Driver Imperfections")]
        [Range(0f, 1f)] public float missteerChance = 0.15f;
        public float maxMissteerAngle = 15f;
        private float currentMissteer = 0f;
        private int lapsAtLastSwitch = -1;
        public CarSensor sensor;
        public float steerStrength = 8f; // Increased for stronger dodging
        public float slowdownFactor = 0.1f; // Strong slowdown
        private float originalSpeed;
        public CarPhysics carPhysics;
        private float blockedTime = 0f;
        public int lapGap;
        public int pitstopDuration;
        private const float maxBlockedTime = 1f; // Increased to avoid premature stopping
        private float leaderHealthDrainTimer = 0f;
        private const float DRAIN_INTERVAL = 1f; // every second
        private float drainPerSecond = 0f;
        public bool hasFinishedRace = false; // Tracks if car has finished the race
        void Start()
        {
            carPhysics = GetComponent<CarPhysics>();
            m_Control = true;
            m_WaypointsCounter = 1;
            lapGap = RaceManager.Instance.lapToPitstop; // Get from RaceManager
            pitstopDuration = RaceManager.Instance.pitstopDuration; // Get from RaceManager
            RaceManager.Instance.RegisterRacer(this);
            sensor = GetComponentInChildren<CarSensor>();
            originalSpeed = carPhysics.m_SpeedForce;

        }

        void Update()
        {
            if (sensor == null || carPhysics == null) return;

            bool obstacleDetected = sensor.forwardBlocked || sensor.hasObstacleInBubble;
            HandleAvoidance(obstacleDetected);

            // Only steer toward checkpoint if no obstacle is detected
            if (!obstacleDetected)
            {
                m_TargetDestination = RaceTrackControl.m_Main.m_Checkpoints[m_WaypointsCounter].transform;
                float distance = Vector2.Distance(m_TargetDestination.position, transform.position);

                Vector3 movementDirection = m_TargetDestination.position - transform.position;
                movementDirection.z = 0;
                movementDirection.Normalize();

                carPhysics.m_InputAccelerate = 1;

                float delta = Vector3.SignedAngle(movementDirection, transform.right, Vector3.forward);
                if (carPhysics.m_InputAccelerate < 0)
                {
                    carPhysics.m_InputSteer = Mathf.Sign(delta);
                }
                else
                {
                    carPhysics.m_InputSteer = -Mathf.Sign(delta);
                }

                if (m_Control)
                {
                    carPhysics.m_InputAccelerate = 1;
                    if (Random.value < missteerChance * Time.deltaTime)
                    {
                        currentMissteer = Random.Range(-maxMissteerAngle, maxMissteerAngle);
                    }
                    delta += currentMissteer;

                    if (carPhysics.m_InputAccelerate < 0)
                    {
                        carPhysics.m_InputSteer = Mathf.Sign(delta);
                    }
                    else
                    {
                        carPhysics.m_InputSteer = -Mathf.Sign(delta);
                    }
                }
            }
            else
            {
                // Only stop if fully blocked and no progress is made
                if (!sensor.leftClear && !sensor.rightClear && sensor.forwardBlocked)
                {
                    blockedTime += Time.deltaTime;
                    if (blockedTime >= maxBlockedTime && carPhysics.m_Body.velocity.magnitude < 1f)
                    {
                        carPhysics.m_InputAccelerate = 0;
                        carPhysics.m_InputSteer = 0;
                    }
                }
                else
                {
                    blockedTime = 0f;
                    carPhysics.m_InputAccelerate = 1; // Keep moving while dodging
                }
            }
            if (RaceManager.Instance.IsLeader(this))
            {
                leaderHealthDrainTimer += Time.deltaTime;
                if (leaderHealthDrainTimer >= DRAIN_INTERVAL && carPhysics.health > 50)
                {
                    leaderHealthDrainTimer = 0f;
                    Debug.Log("Applying leader health drain: " + drainPerSecond);
                    ApplyLeaderHandicap();
                }
            }
        }
        private void ApplyLeaderHandicap()
        {
            carPhysics = GetComponent<CarPhysics>();

            // 🆕 Get LAST ACTIVE racer (skip OUT cars)
            Rivals lastActiveRacer = RaceManager.Instance.GetLastActiveRacer();
            if (lastActiveRacer == null) return;

            // 🆕 Use race progress gap (more reliable than UI)
            float leaderProgress = GetRaceProgress();
            float lastProgress = lastActiveRacer.GetRaceProgress();
            float gap = leaderProgress - lastProgress;

            if (gap <= 0) return;

            drainPerSecond = gap / 10f;
            carPhysics.health -= drainPerSecond;

            Debug.Log($"{name} (LEADER) loses {drainPerSecond:F3} HP vs {lastActiveRacer.name} (gap={gap:F1}) → {carPhysics.health:F1}");
        }
        private void HandleAvoidance(bool obstacleDetected)
        {
            carPhysics.m_SpeedForce = originalSpeed;

            if (!obstacleDetected) return;

            float proximityFactor = Mathf.Clamp01(sensor.closestObstacleDistance / sensor.rayLength);
            float adjustedSteerStrength = steerStrength * (2f - proximityFactor); // Stronger steering for close obstacles

            if (sensor.leftClear)
            {
                carPhysics.m_InputSteer = adjustedSteerStrength;
            }
            else if (sensor.rightClear)
            {
                carPhysics.m_InputSteer = -adjustedSteerStrength;
            }
            else
            {
                carPhysics.m_SpeedForce = originalSpeed * slowdownFactor;
                // Fallback: Try steering randomly to avoid getting stuck
                carPhysics.m_InputSteer = Random.value < 0.5f ? adjustedSteerStrength : -adjustedSteerStrength;
            }
        }

        public float GetRaceProgress()
        {
            int totalCheckpoints = RaceTrackControl.m_Main.m_Checkpoints.Length;
            float progress = m_FinishedLaps * totalCheckpoints + m_WaypointsCounter;
            int prevIndex = (m_WaypointsCounter - 1 + totalCheckpoints) % totalCheckpoints;
            Transform prevCP = RaceTrackControl.m_Main.m_Checkpoints[prevIndex].transform;
            Transform nextCP = RaceTrackControl.m_Main.m_Checkpoints[m_WaypointsCounter].transform;

            float totalDist = Vector2.Distance(prevCP.position, nextCP.position);
            float distToNext = Vector2.Distance(transform.position, nextCP.position);

            float segmentProgress = Mathf.Clamp01(1f - (distToNext / totalDist));

            return m_FinishedLaps * RaceTrackControl.m_Main.m_Checkpoints.Length + GetLapProgress();
        }

        public float GetLapProgress()
        {
            int totalCheckpoints = RaceTrackControl.m_Main.m_Checkpoints.Length;
            float progress = m_WaypointsCounter;
            int prevIndex = (m_WaypointsCounter - 1 + totalCheckpoints) % totalCheckpoints;
            Transform prevCP = RaceTrackControl.m_Main.m_Checkpoints[prevIndex].transform;
            Transform nextCP = RaceTrackControl.m_Main.m_Checkpoints[m_WaypointsCounter].transform;

            float totalDist = Vector2.Distance(prevCP.position, nextCP.position);
            float distToNext = Vector2.Distance(transform.position, nextCP.position);

            float segmentProgress = Mathf.Clamp01(1f - (distToNext / totalDist));

            return progress + segmentProgress;
        }

        public void Checkpointing(int num)
        {
            Checkpoint cp = RaceTrackControl.m_Main.m_Checkpoints[m_WaypointsCounter];
            int globalLap = RaceTrackControl.m_Main.currentLap;
            CarPhysics car = GetComponent<CarPhysics>();

            if (!car.inPitstop && globalLap % lapGap == 0 && globalLap != lapsAtLastSwitch)
            {
                car.inPitstop = true;
                lapsAtLastSwitch = globalLap;
                CarPhysics.TireType newTireType = (CarPhysics.TireType)Random.Range(0, 3);
                car.ChangeTireType(newTireType);
                Debug.Log("Pitstop Time!");
            }
            else if (car.inPitstop && globalLap >= lapsAtLastSwitch + pitstopDuration)
            {
                car.inPitstop = false;
                Debug.Log("Pitstop Time Ended!");
            }

            var track = RaceTrackControl.m_Main;
            if (track == null || track.m_Checkpoints == null || track.m_Checkpoints.Length == 0) return;

            if (m_WaypointsCounter < 0) m_WaypointsCounter = 0;
            if (m_WaypointsCounter >= track.m_Checkpoints.Length) m_WaypointsCounter = 0;

            Checkpoint expected = track.m_Checkpoints[m_WaypointsCounter];

            if (expected.m_ID == num)
            {
                m_WaypointsCounter++;

                if (m_WaypointsCounter >= track.m_Checkpoints.Length)
                {
                    m_WaypointsCounter = 0;
                    m_FinishedLaps++;

                    if (m_FinishedLaps >= RaceTrackControl.m_Main.currentLap)
                    {
                        RaceTrackControl.m_Main.currentLap = m_FinishedLaps + 1;
                    }
                }
            }
            // 🆕 Check if race is finished
            if (m_FinishedLaps >= track.totalLaps)
            {
                hasFinishedRace = true;
            }
        }
    }
}