using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TopDownRace
{
    public class CarSensor : MonoBehaviour
    {
        public enum HitType { None, Wall, Car }

        // ===== Configuration =====
        [Header("Raycast Settings")]
        [Tooltip("Maximum distance each ray will probe.")]
        public float rayLength = 10f;

        [Tooltip("Number of rays in the forward fan (odd number recommended so there is a center ray).")]
        [Range(3, 15)] public int rayCount = 7;

        [Tooltip("Total angular spread of the ray fan in degrees (centered on car forward).")]
        [Range(20f, 240f)] public float arcAngle = 120f;

        [Tooltip("Legacy field kept for inspector/prefab compatibility. Not used by the new fan logic.")]
        public float sideAngle = 45f;

        public LayerMask obstacleMask;

        // ===== Legacy outputs (kept so existing scripts still compile and behave the same) =====
        [Header("Detection — Legacy (read-only)")]
        public bool forwardBlocked;
        public bool leftClear;
        public bool rightClear;
        public bool hasObstacleInBubble;
        public float closestObstacleDistance;

        // ===== New outputs =====
        [Header("Detection — Per-ray (read-only)")]
        [Tooltip("Hit fraction per ray. 1 = clear, 0 = touching. Index 0 is leftmost ray.")]
        public float[] rayHitFraction;
        [Tooltip("What each ray hit. Aligned with rayHitFraction.")]
        public HitType[] rayHitType;

        [Header("Detection — Closest hits (read-only)")]
        public float closestWallDistance;
        public float closestCarDistance;
        public int closestWallRayIndex = -1;
        public int closestCarRayIndex = -1;

        // ===== Internals =====
        private readonly HashSet<Collider2D> obstaclesInBubble = new HashSet<Collider2D>();
        private Transform carTransform;
        private Collider2D[] carColliders;

        void Awake()
        {
            carTransform = transform.parent;
            if (carTransform != null)
                carColliders = carTransform.GetComponentsInChildren<Collider2D>();
            EnsureArrays();
            closestObstacleDistance = rayLength;
        }

        void EnsureArrays()
        {
            if (rayCount < 3) rayCount = 3;
            if (rayHitFraction == null || rayHitFraction.Length != rayCount)
                rayHitFraction = new float[rayCount];
            if (rayHitType == null || rayHitType.Length != rayCount)
                rayHitType = new HitType[rayCount];
        }

        /// <summary>Signed world angle of ray i in degrees. Positive = car's left.</summary>
        public float RayAngle(int i)
        {
            float halfArc = arcAngle * 0.5f;
            float step = (rayCount > 1) ? arcAngle / (rayCount - 1) : 0f;
            return halfArc - step * i;
        }

        /// <summary>World-space direction for ray i.</summary>
        public Vector2 RayDirection(int i)
        {
            if (carTransform == null) return Vector2.right;
            return (Vector2)(Quaternion.Euler(0f, 0f, RayAngle(i)) * carTransform.right);
        }

        void Update()
        {
            EnsureArrays();
            RunRaycasts();
        }

        private void RunRaycasts()
        {
            // Reset
            forwardBlocked = false;
            leftClear = true;
            rightClear = true;
            closestObstacleDistance = rayLength;
            closestWallDistance = rayLength;
            closestCarDistance = rayLength;
            closestWallRayIndex = -1;
            closestCarRayIndex = -1;

            if (carTransform == null) return;

            Vector2 startPos = carTransform.position;
            int midIndex = rayCount / 2;

            for (int i = 0; i < rayCount; i++)
            {
                Vector2 dir = RayDirection(i);
                RaycastHit2D hit = Physics2D.Raycast(startPos, dir, rayLength, obstacleMask);

                HitType type = HitType.None;
                float frac = 1f;

                if (hit.collider != null && IsValidObstacle(hit.collider))
                {
                    frac = Mathf.Clamp01(hit.distance / rayLength);
                    if (hit.collider.CompareTag("Wall"))
                    {
                        type = HitType.Wall;
                        if (hit.distance < closestWallDistance)
                        {
                            closestWallDistance = hit.distance;
                            closestWallRayIndex = i;
                        }
                    }
                    else if (hit.collider.CompareTag("Rival") || hit.collider.CompareTag("Player"))
                    {
                        type = HitType.Car;
                        if (hit.distance < closestCarDistance)
                        {
                            closestCarDistance = hit.distance;
                            closestCarRayIndex = i;
                        }
                    }

                    if (hit.distance < closestObstacleDistance)
                        closestObstacleDistance = hit.distance;
                }

                rayHitFraction[i] = frac;
                rayHitType[i] = type;

                // Debug draw: green=clear, red=wall, yellow=car
                Color c = (type == HitType.None) ? Color.green
                        : (type == HitType.Wall ? Color.red : Color.yellow);
                Debug.DrawRay(startPos, dir * rayLength * frac, c);
            }

            // Populate legacy outputs from new data so old callers behave identically
            forwardBlocked = (rayHitType[midIndex] != HitType.None);
            for (int i = 0; i < midIndex; i++)
            {
                if (rayHitType[i] != HitType.None) { leftClear = false; break; }
            }
            for (int i = midIndex + 1; i < rayCount; i++)
            {
                if (rayHitType[i] != HitType.None) { rightClear = false; break; }
            }
        }

        private bool IsValidObstacle(Collider2D col)
        {
            if (carColliders != null && System.Array.Exists(carColliders, c => c == col))
                return false;
            return col.CompareTag("Rival") || col.CompareTag("Wall") || col.CompareTag("Player");
        }

        // ===== Bubble (trigger volume) — preserved =====
        void OnTriggerEnter2D(Collider2D other)
        {
            if (IsValidObstacle(other) && ((1 << other.gameObject.layer) & obstacleMask) != 0)
            {
                obstaclesInBubble.Add(other);
                hasObstacleInBubble = true;
            }
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (obstaclesInBubble.Contains(other))
            {
                obstaclesInBubble.Remove(other);
                hasObstacleInBubble = obstaclesInBubble.Count > 0;
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = hasObstacleInBubble ? Color.red : Color.green;
            CapsuleCollider2D col = GetComponent<CapsuleCollider2D>();
            if (col != null)
                Gizmos.DrawWireSphere(transform.position, col.size.magnitude / 2f);
        }
    }
}