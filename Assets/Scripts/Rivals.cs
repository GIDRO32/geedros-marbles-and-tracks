using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TopDownRace
{
    public class Rivals : MonoBehaviour
    {
        // ============================================================
        // Serialized fields preserved from the original Rivals script.
        // Renaming or removing any of these will break the CarTemplate
        // prefab's saved overrides.
        // ============================================================

        [HideInInspector] public Transform m_TargetDestination;
        [HideInInspector] public int m_WaypointsCounter;
        [HideInInspector] public int m_FinishedLaps;
        [HideInInspector] public bool m_Control = false;

        [Header("Driver Imperfections")]
        [Range(0f, 1f)] public float missteerChance = 0.15f;
        public float maxMissteerAngle = 15f;

        [Header("References")]
        public CarSensor sensor;
        public CarPhysics carPhysics;

        [Header("Avoidance (Legacy fields — kept for prefab compatibility)")]
        [Tooltip("Legacy. Not used by the new blended-steering AI.")]
        public float steerStrength = 8f;
        [Tooltip("Legacy. Not used by the new blended-steering AI.")]
        public float slowdownFactor = 0.1f;

        [Header("Race State")]
        public int lapGap;
        public int pitstopDuration;
        public bool hasFinishedRace = false;

        // ============================================================
        // New AI tuning. Safe to change in inspector.
        // ============================================================

        [Header("AI Tuning")]
        [Tooltip("How hard wall hits push steering. 1 = subtle, 4 = panic.")]
        public float wallAvoidanceWeight = 2.5f;

        [Tooltip("How hard car hits push steering. Lower than walls so cars don't swerve wildly around each other.")]
        public float carAvoidanceWeight = 1.0f;

        [Tooltip("Steering proportional gain. Higher = sharper turns to checkpoint. Divides delta angle (deg).")]
        public float steerProportionalRange = 60f;

        [Tooltip("Minimum throttle when braking hard for walls.")]
        [Range(0f, 1f)] public float minThrottle = 0.25f;

        [Tooltip("Below this hit-fraction on a forward wall ray, throttle starts ramping down.")]
        [Range(0.1f, 1f)] public float brakeStartFraction = 0.8f;

        [Header("Overtaking & Blocking")]
        [Tooltip("Max distance to another car for overtake/block logic to engage (world units).")]
        public float interactionRange = 8f;

        [Tooltip("Max lateral offset for a car to count as 'on my line' (world units).")]
        public float lateralThreshold = 3.5f;

        [Tooltip("Lateral steer added when committing to a pass.")]
        [Range(0f, 1f)] public float overtakeSteer = 0.55f;

        [Tooltip("Lateral steer added when defending (scaled by defensiveness).")]
        [Range(0f, 1f)] public float blockSteer = 0.45f;

        [Tooltip("Seconds to hold a chosen overtaking side before re-deciding.")]
        public float overtakeCommitTime = 1.5f;

        [Tooltip("Speed ratio (mine / theirs) above which I consider myself faster than the car ahead.")]
        public float overtakeSpeedRatio = 1.02f;

        [Header("Collision Avoidance")]
        [Tooltip("Cars within this distance trigger active separation steering (world units). Keep small — a personal-space bubble, not the dueling range.")]
        public float separationDistance = 3f;

        [Tooltip("How hard to steer away from a car inside the separation bubble.")]
        [Range(0f, 2f)] public float separationStrength = 0.9f;

        [Tooltip("Max throttle reduction (0-1) when a car is right alongside, to avoid driving into it.")]
        [Range(0f, 1f)] public float separationThrottleEase = 0.2f;

        // ============================================================
        // Personality — rolled fresh each race in Start().
        // To make personalities persistent per car, replace the Random
        // calls in Start with values read from CarData / cars.json.
        // ============================================================

        [Header("Personality (rolled in Start)")]
        [Tooltip("Cleanliness of inputs. Low skill = wilder missteer, less smooth.")]
        [Range(0f, 1f)] public float skill = 1f;
        [Tooltip("How late to brake and how close to pass other cars.")]
        [Range(0f, 1f)] public float aggression = 0.7f;
        [Tooltip("Tick-to-tick steering steadiness. Low consistency = visible jitter.")]
        [Range(0f, 1f)] public float consistency = 1f;
        [Tooltip("How hard this car fights to keep its position. Low = lets faster cars by.")]
        [Range(0f, 1f)] public float defensiveness = 0.6f;

        // ============================================================
        // Internal state
        // ============================================================

        private float currentMissteer = 0f;
        private int lapsAtLastSwitch = -1;
        private float originalSpeed;
        private float blockedTime = 0f;
        private const float maxBlockedTime = 1f;

        private float leaderHealthDrainTimer = 0f;
        private const float DRAIN_INTERVAL = 1f;
        private float drainPerSecond = 0f;

        private float noiseSeed;

        // Overtaking commit state
        private int overtakeDir = 0;      // +1 = passing on my left, -1 = on my right, 0 = none
        private float overtakeTimer = 0f;

        // ============================================================
        // Lifecycle
        // ============================================================

        void Start()
        {
            carPhysics = GetComponent<CarPhysics>();
            if (sensor == null) sensor = GetComponentInChildren<CarSensor>();

            m_Control = true;
            m_WaypointsCounter = 1;

            if (RaceManager.Instance != null)
            {
                lapGap = RaceManager.Instance.lapToPitstop;
                pitstopDuration = RaceManager.Instance.pitstopDuration;
                RaceManager.Instance.RegisterRacer(this);
            }

            if (carPhysics != null) originalSpeed = carPhysics.m_SpeedForce;

            // Roll personality within bounded ranges so even the worst-rolled
            // car still has a plausible shot at winning, and the best-rolled
            // car can still lose to bad luck.
            skill       = Random.Range(0.70f, 1.00f);
            aggression  = Random.Range(0.40f, 1.00f);
            consistency = Random.Range(0.50f, 1.00f);
            defensiveness = Random.Range(0.00f, 1.00f);
            noiseSeed   = Random.Range(0f, 1000f);
        }

        void Update()
        {
            if (sensor == null || carPhysics == null) return;
            if (!m_Control) return;
            if (hasFinishedRace) return;

            // --- 1. Resolve current target checkpoint ---
            var checkpoints = (RaceTrackControl.m_Main != null) ? RaceTrackControl.m_Main.m_Checkpoints : null;
            if (checkpoints == null || checkpoints.Length == 0) return;
            int idx = Mathf.Clamp(m_WaypointsCounter, 0, checkpoints.Length - 1);
            m_TargetDestination = checkpoints[idx].transform;

            Vector3 toTarget = m_TargetDestination.position - transform.position;
            toTarget.z = 0f;

            // Signed angle: positive = target is to the car's RIGHT of forward,
            // negative = target is to the car's LEFT.
            // (Vector3.SignedAngle returns the angle from the first arg to the second.)
            // transform.right is car's forward, so we want SignedAngle(forward, toTarget):
            float deltaToTarget = Vector3.SignedAngle(transform.right, toTarget.normalized, Vector3.forward);

            // --- 2. Base steer toward target (proportional) ---
            // Convention: positive m_InputSteer -> CCW angular velocity -> left turn.
            // If target is on left (deltaToTarget > 0 because we measured from forward CCW),
            // we want positive steer.
            float baseSteer = Mathf.Clamp(deltaToTarget / steerProportionalRange, -1f, 1f);

            // --- 3. Wall / car avoidance from per-ray sensor data ---
            float wallSteer = 0f;
            float carSteer = 0f;
            float minWallFrac = 1f;
            float minCarFrac = 1f;

            if (sensor.rayHitFraction != null && sensor.rayHitType != null)
            {
                int last = sensor.rayHitFraction.Length - 1;
                for (int i = 0; i <= last; i++)
                {
                    CarSensor.HitType type = sensor.rayHitType[i];
                    if (type == CarSensor.HitType.None) continue;

                    float frac = sensor.rayHitFraction[i];
                    float urgency = 1f - frac;            // closer = bigger
                    float angle = sensor.RayAngle(i);     // +left, -right

                    // Steer AWAY from the hit. If hit angle is +(left), push right (-steer).
                    // For the exact center ray (angle ~ 0), break the tie by which side
                    // has more clearance overall.
                    float steerSign;
                    if (Mathf.Abs(angle) < 1f)
                    {
                        float leftFreedom = sensor.rayHitFraction[0];
                        float rightFreedom = sensor.rayHitFraction[last];
                        // If left side is freer, pretend hit was on the right (steer left = +).
                        steerSign = (leftFreedom >= rightFreedom) ? 1f : -1f;
                    }
                    else
                    {
                        steerSign = -Mathf.Sign(angle);
                    }

                    float repel = steerSign * urgency;

                    if (type == CarSensor.HitType.Wall)
                    {
                        wallSteer += repel;
                        if (frac < minWallFrac) minWallFrac = frac;
                    }
                    else // Car
                    {
                        carSteer += repel;
                        if (frac < minCarFrac) minCarFrac = frac;
                    }
                }
            }

            // --- 4. Missteer (driver imperfection, scaled by skill) ---
            // skill 1.0 -> missteer chance and angle reduced; skill 0.7 -> close to original.
            float skillMissteerScale = Mathf.Lerp(2.0f, 0.3f, skill);
            float skillAngleScale    = Mathf.Lerp(1.5f, 0.6f, skill);

            if (Random.value < missteerChance * skillMissteerScale * Time.deltaTime)
            {
                currentMissteer = Random.Range(-maxMissteerAngle, maxMissteerAngle) * skillAngleScale;
            }
            // Decay missteer back to zero so it's a momentary mistake, not a permanent bias.
            currentMissteer = Mathf.MoveTowards(currentMissteer, 0f, 30f * Time.deltaTime);
            float missteerContribution = Mathf.Clamp(currentMissteer / steerProportionalRange, -1f, 1f);

            // --- 5. Consistency noise ---
            // Low consistency => small jittery extra steering each frame.
            float noise = (Mathf.PerlinNoise(Time.time * 2f, noiseSeed) - 0.5f) * 2f;
            float noiseContribution = noise * (1f - consistency) * 0.25f;

            // --- 5b. Tactical: overtaking, blocking & collision separation ---
            // Returns a lateral steer contribution, whether we're actively passing
            // (so we don't lift in step 7), and how close the nearest car is (for throttle ease).
            bool isOvertaking;
            float separationUrgency;
            float tacticalSteer = ComputeTacticalSteer(out isOvertaking, out separationUrgency);

            // --- 6. Combine steering ---
            float steer = baseSteer
                        + wallAvoidanceWeight * wallSteer
                        + carAvoidanceWeight  * carSteer
                        + missteerContribution
                        + noiseContribution
                        + tacticalSteer;

            // --- 7. Throttle: full unless walls are too close ahead ---
            float throttle = 1f;

            // Aggressive drivers brake later (smaller effective brakeStart).
            float aggBrakeShift = Mathf.Lerp(0.7f, 1.0f, aggression);
            float effBrakeStart = brakeStartFraction * aggBrakeShift;

            if (minWallFrac < effBrakeStart)
            {
                float t = minWallFrac / Mathf.Max(0.01f, effBrakeStart);
                throttle = Mathf.Lerp(minThrottle, 1f, t);
            }

            // Lift slightly behind a directly-ahead car. More aggressive drivers lift less.
            // Skip entirely while actively overtaking — we want full commitment to the pass.
            if (minCarFrac < 0.6f && !isOvertaking)
            {
                float carUrgency = 1f - minCarFrac;
                float lift = Mathf.Lerp(1f, 0.85f, carUrgency * (1f - aggression));
                throttle *= lift;
            }

            // Ease off slightly when a car is right alongside, so separation steering
            // has room to work instead of us plowing into the gap. Reduced while
            // overtaking so a committed pass still carries speed.
            if (separationUrgency > 0f)
            {
                float ease = separationThrottleEase * separationUrgency;
                if (isOvertaking) ease *= 0.5f;
                throttle *= (1f - ease);
            }

            // --- 8. Unstuck behavior: if fully blocked and stopped, allow a random shove ---
            bool fullyBlocked = sensor.forwardBlocked && !sensor.leftClear && !sensor.rightClear;
            if (fullyBlocked && carPhysics.m_Body != null && carPhysics.m_Body.velocity.magnitude < 1f)
            {
                blockedTime += Time.deltaTime;
                if (blockedTime > maxBlockedTime)
                {
                    steer = (Random.value < 0.5f ? -1f : 1f);
                    throttle = 0.6f;
                }
            }
            else
            {
                blockedTime = 0f;
            }

            // --- 9. Apply ---
            carPhysics.m_InputSteer = Mathf.Clamp(steer, -1f, 1f);
            carPhysics.m_InputAccelerate = Mathf.Clamp01(throttle);

            // --- 10. Leader handicap (preserved from original) ---
            if (RaceManager.Instance != null && RaceManager.Instance.IsLeader(this))
            {
                leaderHealthDrainTimer += Time.deltaTime;
                if (leaderHealthDrainTimer >= DRAIN_INTERVAL && carPhysics.health > 50)
                {
                    leaderHealthDrainTimer = 0f;
                    ApplyLeaderHandicap();
                }
            }
        }

        // ============================================================
        // Tactical layer: overtaking & blocking
        // ============================================================

        /// <summary>
        /// Computes a lateral steer contribution for overtaking the car directly
        /// ahead (when faster), blocking a faster car directly behind, and keeping
        /// a minimum separation from ALL nearby cars to avoid health-draining contact.
        /// </summary>
        private float ComputeTacticalSteer(out bool isOvertaking, out float separationUrgency)
        {
            isOvertaking = false;
            separationUrgency = 0f;
            float tactical = 0f;
            float separationSteer = 0f;

            // Tick down the overtake commit timer regardless.
            if (overtakeTimer > 0f) overtakeTimer -= Time.deltaTime;
            if (overtakeTimer <= 0f) overtakeDir = 0;

            if (RaceManager.Instance == null) return 0f;
            var racers = RaceManager.Instance.GetSortedRacers();
            if (racers == null || racers.Count < 2) return 0f;

            Vector2 fwd = transform.right;   // car forward
            Vector2 left = transform.up;     // car's left in 2D (90deg CCW from forward)

            // Side clearance from the sensor fan — needed for separation tie-breaks and
            // for keeping overtake/block steering out of walls. Computed once up front.
            float leftFreedom, rightFreedom;
            GetSideFreedom(out leftFreedom, out rightFreedom);

            // Find the nearest car ahead and nearest car behind that are roughly on my line.
            Rivals carAhead = null, carBehind = null;
            float aheadDist = interactionRange, behindDist = interactionRange;
            float aheadLeftDot = 0f, behindLeftDot = 0f;

            for (int i = 0; i < racers.Count; i++)
            {
                Rivals other = racers[i];
                if (other == this || other == null) continue;
                if (other.hasFinishedRace) continue;

                Vector2 toOther = (Vector2)(other.transform.position - transform.position);
                float dist = toOther.magnitude;
                if (dist > interactionRange || dist < 0.01f) continue;

                float fwdDot = Vector2.Dot(toOther, fwd);
                float leftDot = Vector2.Dot(toOther, left);

                // --- Separation bubble: applies to cars in ANY direction at close range ---
                if (dist < separationDistance)
                {
                    float closeness = 1f - (dist / separationDistance); // 0..1, bigger when nearer
                    if (closeness > separationUrgency) separationUrgency = closeness;

                    float pushDir;
                    if (Mathf.Abs(leftDot) < 0.5f)
                    {
                        // Nearly head-on/tail-on: break the tie toward the side with more room.
                        pushDir = (leftFreedom >= rightFreedom) ? 1f : -1f;
                    }
                    else
                    {
                        // Steer away from the side the other car is on.
                        pushDir = -Mathf.Sign(leftDot);
                    }
                    separationSteer += pushDir * separationStrength * closeness;
                }

                // --- Ahead/behind selection (for overtake/block) requires being on my line ---
                if (Mathf.Abs(leftDot) > lateralThreshold) continue;

                if (fwdDot > 0f && dist < aheadDist)
                {
                    carAhead = other; aheadDist = dist; aheadLeftDot = leftDot;
                }
                else if (fwdDot < 0f && dist < behindDist)
                {
                    carBehind = other; behindDist = dist; behindLeftDot = leftDot;
                }
            }

            float mySpeed = (carPhysics != null && carPhysics.m_Body != null)
                ? carPhysics.m_Body.velocity.magnitude : 0f;

            // ---- Overtaking ----
            if (carAhead != null)
            {
                float aheadSpeed = SpeedOf(carAhead);
                bool fasterThanAhead = mySpeed > aheadSpeed * overtakeSpeedRatio;

                if (fasterThanAhead)
                {
                    // Choose a side once, then hold it for overtakeCommitTime.
                    if (overtakeDir == 0 || overtakeTimer <= 0f)
                    {
                        float leftScore = leftFreedom;
                        float rightScore = rightFreedom;
                        // Prefer the side the car ahead is NOT sitting on.
                        if (aheadLeftDot > 0f) leftScore *= 0.5f;   // they're left -> favor right
                        else rightScore *= 0.5f;                    // they're right -> favor left

                        overtakeDir = (leftScore >= rightScore) ? +1 : -1;
                        overtakeTimer = overtakeCommitTime;
                    }

                    // Don't drive into a wall on the chosen side; if that side is tight, bail.
                    float chosenFreedom = (overtakeDir > 0) ? leftFreedom : rightFreedom;
                    if (chosenFreedom > 0.25f)
                    {
                        tactical += overtakeDir * overtakeSteer;
                        isOvertaking = true;
                    }
                }
            }

            // ---- Blocking ----
            if (carBehind != null && defensiveness > 0.01f)
            {
                float behindSpeed = SpeedOf(carBehind);
                bool theyAreFaster = behindSpeed > mySpeed; // closing on me

                if (theyAreFaster)
                {
                    // Move toward the side the chaser is using to cover the gap.
                    int blockDir = (behindLeftDot > 0f) ? +1 : -1;

                    // Never block into a wall: if that side is tight, don't.
                    float blockFreedom = (blockDir > 0) ? leftFreedom : rightFreedom;
                    if (blockFreedom > 0.3f)
                    {
                        tactical += blockDir * blockSteer * defensiveness;
                    }
                }
            }

            return tactical + separationSteer;
        }

        private float SpeedOf(Rivals r)
        {
            if (r == null) return 0f;
            CarPhysics p = r.GetComponent<CarPhysics>();
            return (p != null && p.m_Body != null) ? p.m_Body.velocity.magnitude : 0f;
        }

        /// <summary>
        /// Minimum clear fraction across the left half and right half of the ray fan.
        /// 1 = totally clear on that side, 0 = touching. Used to avoid steering into walls
        /// when overtaking or blocking.
        /// </summary>
        private void GetSideFreedom(out float leftFreedom, out float rightFreedom)
        {
            leftFreedom = 1f;
            rightFreedom = 1f;
            if (sensor == null || sensor.rayHitFraction == null) return;

            int count = sensor.rayHitFraction.Length;
            int mid = count / 2;
            for (int i = 0; i < count; i++)
            {
                float f = sensor.rayHitFraction[i];
                if (i < mid) { if (f < leftFreedom) leftFreedom = f; }
                else if (i > mid) { if (f < rightFreedom) rightFreedom = f; }
            }
        }

        // ============================================================
        // Preserved API — called by other scripts. Do not rename.
        // ============================================================

        private void ApplyLeaderHandicap()
        {
            Rivals lastActiveRacer = RaceManager.Instance.GetLastActiveRacer();
            if (lastActiveRacer == null) return;
            float leaderProgress = GetRaceProgress();
            float lastProgress = lastActiveRacer.GetRaceProgress();
            float gap = leaderProgress - lastProgress;
            if (gap <= 0f) return;
            drainPerSecond = gap / 10f;
            carPhysics.health -= drainPerSecond;
            Debug.Log($"{name} (LEADER) loses {drainPerSecond:F3} HP vs {lastActiveRacer.name} (gap={gap:F1}) -> {carPhysics.health:F1}");
        }

        public float GetRaceProgress()
        {
            // Matches the previous implementation: laps * checkpointCount + intra-lap progress.
            if (RaceTrackControl.m_Main == null || RaceTrackControl.m_Main.m_Checkpoints == null) return 0f;
            int totalCheckpoints = RaceTrackControl.m_Main.m_Checkpoints.Length;
            if (totalCheckpoints == 0) return 0f;
            return m_FinishedLaps * totalCheckpoints + GetLapProgress();
        }

        public float GetLapProgress()
        {
            if (RaceTrackControl.m_Main == null || RaceTrackControl.m_Main.m_Checkpoints == null) return 0f;
            int totalCheckpoints = RaceTrackControl.m_Main.m_Checkpoints.Length;
            if (totalCheckpoints == 0) return 0f;

            int prevIndex = (m_WaypointsCounter - 1 + totalCheckpoints) % totalCheckpoints;
            int nextIndex = m_WaypointsCounter % totalCheckpoints;
            Transform prevCP = RaceTrackControl.m_Main.m_Checkpoints[prevIndex].transform;
            Transform nextCP = RaceTrackControl.m_Main.m_Checkpoints[nextIndex].transform;

            float totalDist = Vector2.Distance(prevCP.position, nextCP.position);
            float distToNext = Vector2.Distance(transform.position, nextCP.position);
            float segmentProgress = (totalDist > 0.01f) ? Mathf.Clamp01(1f - (distToNext / totalDist)) : 0f;

            return m_WaypointsCounter + segmentProgress;
        }

        /// <summary>
        /// Called by Checkpoint.OnTriggerEnter2D when a Rival enters the checkpoint trigger.
        /// Preserves the existing pit-stop schedule and lap counting behavior exactly.
        /// </summary>
        public void Checkpointing(int num)
        {
            CarPhysics car = GetComponent<CarPhysics>();
            int globalLap = RaceTrackControl.m_Main.currentLap;

            // Pitstop scheduling — preserved logic.
            if (!car.inPitstop && lapGap > 0 && globalLap % lapGap == 0 && globalLap != lapsAtLastSwitch)
            {
                car.inPitstop = true;
                lapsAtLastSwitch = globalLap;
                CarPhysics.TireType newTireType = (CarPhysics.TireType)Random.Range(0, 3);
                car.ChangeTireType(newTireType);
                Debug.Log($"{name}: Pitstop Time!");
            }
            else if (car.inPitstop && globalLap >= lapsAtLastSwitch + pitstopDuration)
            {
                car.inPitstop = false;
                Debug.Log($"{name}: Pitstop Time Ended!");
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
                    if (m_FinishedLaps >= track.currentLap)
                    {
                        track.currentLap = m_FinishedLaps + 1;
                    }
                }
            }

            if (m_FinishedLaps >= track.totalLaps)
            {
                hasFinishedRace = true;
            }
        }
    }
}