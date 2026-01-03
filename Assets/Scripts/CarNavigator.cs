using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TopDownRace
{
    public class CarNavigator : MonoBehaviour
    {
        public Transform car;              // the car following this navigator
        public Transform circle;           // child "Circle" with trigger collider
        public float followDistance = 2f;  // how far ahead the circle should stay
        public float turnSpeed = 3f;       // how fast navigator rotates
        private Transform checkpoint;      // current checkpoint target
        private Vector2 desiredDir;
        private Vector2 safeDir;

        void Start()
        {
            checkpoint = RaceTrackControl.m_Main.m_Checkpoints[
                car.GetComponent<Rivals>().m_WaypointsCounter
            ].transform;
        }

        void Update()
        {
            // Desired direction = from navigator to checkpoint
            desiredDir = (checkpoint.position - transform.position).normalized;

            // Get car’s current facing angle
            float carAngle = car.eulerAngles.z;

            // Compute desired absolute rotation angle toward checkpoint
            float targetAngle = Mathf.Atan2(desiredDir.y, desiredDir.x) * Mathf.Rad2Deg;

            // --- Clamp navigator rotation relative to car --- //
            float deltaAngle = Mathf.DeltaAngle(carAngle, targetAngle);
            deltaAngle = Mathf.Clamp(deltaAngle, -120f, 120f);
            float clampedAngle = carAngle + deltaAngle;

            // Smooth rotation
            float smoothAngle = Mathf.LerpAngle(transform.eulerAngles.z, clampedAngle, Time.deltaTime * turnSpeed * 0.5f);
            transform.rotation = Quaternion.Euler(0, 0, smoothAngle);

            // Smooth circle motion
            Vector3 targetPos = Vector3.right * followDistance;
            circle.localPosition = Vector3.Lerp(circle.localPosition, targetPos, Time.deltaTime * 5f);
            // Always fetch current checkpoint of this specific car
            int total = RaceTrackControl.m_Main.m_Checkpoints.Length;
            int index = car.GetComponent<Rivals>().m_WaypointsCounter % total;
            if (RaceTrackControl.m_Main.m_Checkpoints != null &&
                index >= 0 && index < RaceTrackControl.m_Main.m_Checkpoints.Length)
            {
                checkpoint = RaceTrackControl.m_Main.m_Checkpoints[index].transform;
            }

        }

        public void AvoidObstacle(Collider2D obstacle)
        {
            // Compute direction that still aims to checkpoint but avoids the obstacle
            Vector2 away = (transform.position - obstacle.transform.position).normalized;
            Vector2 toCheckpoint = (checkpoint.position - transform.position).normalized;
            safeDir = Vector2.Lerp(toCheckpoint, away, 0.7f).normalized;

            float safeAngle = Mathf.Atan2(safeDir.y, safeDir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.Euler(0, 0, safeAngle),
                Time.deltaTime * turnSpeed * 2f
            );
        }
    }
}
