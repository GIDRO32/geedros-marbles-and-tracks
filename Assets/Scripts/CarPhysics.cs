using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TopDownRace
{
    public class CarPhysics : MonoBehaviour
    {
        [HideInInspector]
        public Rigidbody2D m_Body;

        [HideInInspector]
        public float m_InputAccelerate = 0;
        [HideInInspector]
        public float m_InputSteer = 0;

        public float m_SpeedForce = 18f;

        public GameObject m_TireTracks;
        public Transform m_TireMarkPoint;

        [Header("Car Stats")]
        public float maxHealth = 100f;
        public float maxFuel = 100f;
        public float maxTires = 100f;
        public float health = 100f;
        public float fuel = 100f;
        public float tires = 100f;
        [HideInInspector] public float maxSpeedForce; // Inspector-assigned value

        public enum TireType { Soft, Medium, Hard }
        public TireType tireType;
        public float baseSpeedForce = 18f;
        public float effectiveSpeedForce;
        public float baseMaxTires = 200f;
        public float tireWearRate = 0.002f; // Base tire wear per skid
        public bool inPitstop = false;
        public float pitRegenRate = 5f; // HP, Fuel, and Tires per second
        public Rivals rivalScript; // Reference to Rivals component for lap count

        [Header("Handling")]
        public float steerTorque = 30f; // Reduced for smoother steering
        private bool isOut = false; // Flag to track if car is out
        public float finishDecelerationRate = 5f; // Rate at which speed and angular velocity reduce when finishing, adjustable in Inspector
        void Start()
        {
            m_Body = GetComponent<Rigidbody2D>();
            baseSpeedForce = m_SpeedForce;
            maxSpeedForce = m_SpeedForce; // Save the original inspector value
            tireType = (TireType)Random.Range(0, 3); // Randomly select tire type
            ConfigureTireType();
            tires = maxTires; // Initialize tires to max
            rivalScript = GetComponent<Rivals>(); // Initialize Rivals reference
        }

        private void ConfigureTireType()
        {
            switch (tireType)
            {
                case TireType.Soft:
                    effectiveSpeedForce = baseSpeedForce + 1f; // +2 speed
                    maxTires = baseMaxTires - 30f; // Reduced endurance
                    tireWearRate = 0.0003f; // Faster wear
                    steerTorque = 40f;
                    rivalScript.maxMissteerAngle = 15f;
                    break;
                case TireType.Medium:
                    effectiveSpeedForce = baseSpeedForce; // Default speed
                    maxTires = baseMaxTires; // Default endurance
                    tireWearRate = 0.00025f; // Default wear
                    steerTorque = 30f;
                    rivalScript.maxMissteerAngle = 8f;
                    break;
                case TireType.Hard:
                    effectiveSpeedForce = baseSpeedForce - 1f; // -2 speed
                    maxTires = baseMaxTires + 50f; // Increased endurance
                    tireWearRate = 0.0002f; // Slower wear
                    steerTorque = 30f;
                    rivalScript.maxMissteerAngle = 5f;
                    break;
            }
            m_SpeedForce = effectiveSpeedForce;
        }

        public void ChangeTireType(TireType newType)
        {
            tireType = newType;
            ConfigureTireType();
            // Note: No instant tire refill; regeneration happens in FixedUpdate
        }
        void Update()
        {
            Vector2 velocity = m_Body.velocity;
            Vector2 forward = Helper.ToVector2(transform.right);
            float delta = Vector2.SignedAngle(forward, velocity);
            if (velocity.magnitude > 10 && Mathf.Abs(delta) > 20)
            {
                GameObject obj = Instantiate(m_TireTracks);
                tires = Mathf.Max(0f, tires - tireWearRate); // Apply tire-type-specific wear
                obj.transform.position = m_TireMarkPoint.position;
                obj.transform.rotation = m_TireMarkPoint.rotation;
                Destroy(obj, 2);
            }

            if (health <= 0f && !isOut)
            {
                isOut = true;
                GetComponent<Collider2D>().enabled = false;
                GetComponentInChildren<CarSensor>().GetComponent<Collider2D>().enabled = false;
                m_InputAccelerate = 0f; // Stop acceleration
                m_InputSteer = 0f; // Stop steering
            }
        }
        void FixedUpdate()
        {
            Rivals rival = GetComponent<Rivals>();
            if (rival != null && rival.hasFinishedRace)
            {
                m_Body.velocity = Vector2.Lerp(m_Body.velocity, Vector2.zero, Time.fixedDeltaTime * finishDecelerationRate);
                m_Body.angularVelocity = Mathf.Lerp(m_Body.angularVelocity, 0f, Time.fixedDeltaTime * finishDecelerationRate);
                m_InputAccelerate = 0;
                m_InputSteer = 0;
                GetComponent<Collider2D>().enabled = false;
                GetComponentInChildren<CarSensor>().GetComponent<Collider2D>().enabled = false;
                return; // Skip further updates
            }
            m_SpeedForce = CalculateEffectiveSpeed();
            Vector3 forward = Quaternion.Euler(0, 0, m_Body.rotation) * Vector3.right;

            m_Body.AddForce(m_InputAccelerate * m_SpeedForce * Helper.ToVector2(forward), ForceMode2D.Impulse);

            Vector3 right = Quaternion.Euler(0, 0, 90) * forward;
            Vector3 project1 = Vector3.Project(Helper.ToVector3(m_Body.velocity), right);

            m_Body.velocity -= .02f * Helper.ToVector2(project1);

            // Clamp steer input to prevent over-steering
            m_InputSteer = Mathf.Clamp(m_InputSteer, -1f, 1f);

            m_Body.angularVelocity += steerTorque * m_InputSteer;

            float speed = m_Body.velocity.magnitude;
            fuel = Mathf.Max(0f, fuel - (speed * 0.0002f));
            tires = Mathf.Max(0f, tires - (speed * tireWearRate));

            m_InputAccelerate = 0;
            m_InputSteer = 0;

            // Pitstop regeneration (health, fuel, and tires)
            if (inPitstop)
            {
                health = Mathf.Min(maxHealth, health + pitRegenRate * Time.fixedDeltaTime);
                fuel = Mathf.Min(maxFuel, fuel + pitRegenRate * Time.fixedDeltaTime);
                tires = Mathf.Min(maxTires, tires + pitRegenRate * Time.fixedDeltaTime);
            }
        }

        public float CalculateEffectiveSpeed()
        {
            float effective = effectiveSpeedForce; // Use tire-type-specific speed

            // 1. Health → 30% influence
            float healthFactor = (health / maxHealth) * 0.3f;
            effective -= effectiveSpeedForce * (0.3f - healthFactor);
            if (health <= 0f) return 0f; // Car stops completely

            // 2. Tires → 40% influence
            float tireFactor = (tires / maxTires) * 0.40f;
            effective -= effectiveSpeedForce * (0.40f - tireFactor);

            // 3. Fuel → only when empty
            if (fuel <= 0f)
            {
                if (tires > 0)
                {
                    effective *= 0.90f; // 90% speed
                }
                else
                {
                    effective *= 0.95f; // 95% speed if also no tires
                }
            }

            return effective;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if ((collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Rival")) && !inPitstop)
            {
                // Check if car is on the same lap as the leader
                var racers = RaceManager.Instance.GetSortedRacers();
                if (racers != null && racers.Count > 0)
                {
                    int leaderLaps = racers[0].m_FinishedLaps;
                    int myLaps = rivalScript != null ? rivalScript.m_FinishedLaps : 0;
                    if (myLaps >= leaderLaps) // Only reduce health if not lapped
                    {
                        health = Mathf.Max(0f, health - 0.4f);
                    }
                }
            }
        }
    }
}