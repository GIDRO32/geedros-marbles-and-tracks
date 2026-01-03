using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TopDownRace
{
    public class CarSensor : MonoBehaviour
    {
        [Header("Raycast Settings")]
        public float rayLength = 4f; // Reduced to avoid over-detection
        public float sideAngle = 45f; // Reduced for narrower rays
        public LayerMask obstacleMask;

        [Header("Detection States (Read-Only)")]
        public bool forwardBlocked;
        public bool leftClear;
        public bool rightClear;
        public bool hasObstacleInBubble;
        public float closestObstacleDistance;

        private HashSet<Collider2D> obstaclesInBubble = new HashSet<Collider2D>();
        private Transform carTransform;
        private Rivals rivalScript;
        private Collider2D[] carColliders; // To ignore car's own colliders

        void Awake()
        {
            carTransform = transform.parent; // Parent is the car
            rivalScript = carTransform.GetComponent<Rivals>();
            carColliders = carTransform.GetComponentsInChildren<Collider2D>(); // Get car's colliders
            closestObstacleDistance = rayLength;

            // Check CapsuleCollider2D size
            CapsuleCollider2D collider = GetComponent<CapsuleCollider2D>();

        }

        void Update()
        {
            RunRaycasts();
        }

        private void RunRaycasts()
        {
            forwardBlocked = false;
            leftClear = true;
            rightClear = true;
            closestObstacleDistance = rayLength;

            Vector2 dir = carTransform.right;
            Vector2 startPos = carTransform.position;
            RaycastHit2D hit = Physics2D.Raycast(startPos, dir, rayLength, obstacleMask);
            if (hit.collider != null && IsValidObstacle(hit.collider))
            {
                forwardBlocked = true;
                closestObstacleDistance = Mathf.Min(closestObstacleDistance, hit.distance);
            }

            Vector2 leftDir = Quaternion.Euler(0, 0, sideAngle) * dir;
            hit = Physics2D.Raycast(startPos, leftDir, rayLength, obstacleMask);
            if (hit.collider != null && IsValidObstacle(hit.collider))
            {
                leftClear = false;
                closestObstacleDistance = Mathf.Min(closestObstacleDistance, hit.distance);
            }

            Vector2 rightDir = Quaternion.Euler(0, 0, -sideAngle) * dir;
            hit = Physics2D.Raycast(startPos, rightDir, rayLength, obstacleMask);
            if (hit.collider != null && IsValidObstacle(hit.collider))
            {
                rightClear = false;
                closestObstacleDistance = Mathf.Min(closestObstacleDistance, hit.distance);
            }

            Debug.DrawRay(startPos, dir * rayLength, forwardBlocked ? Color.red : Color.green);
            Debug.DrawRay(startPos, leftDir * rayLength, leftClear ? Color.green : Color.red);
            Debug.DrawRay(startPos, rightDir * rayLength, rightClear ? Color.green : Color.red);
        }

        private bool IsValidObstacle(Collider2D col)
        {
            // Ignore the car's own colliders
            if (carColliders != null && System.Array.Exists(carColliders, c => c == col))
            {
                return false;
            }

            bool isValid = col.CompareTag("Rival") || col.CompareTag("Wall");
            return isValid;
        }

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
            CapsuleCollider2D collider = GetComponent<CapsuleCollider2D>();
            if (collider != null)
            {
                Gizmos.DrawWireSphere(transform.position, collider.size.magnitude / 2f);
            }
        }
    }
}