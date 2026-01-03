using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TopDownRace
{
    public class CameraFollow : MonoBehaviour
    {
        private Vector3 m_Offset = new Vector3(0, 0, -10);
        public float m_SmoothTime = 0.2f;
        private Vector3 m_Velocity = Vector3.zero;
        public Slider cameraZoom;
        public float minZoom = 3f;  // smallest orthographicSize (closest)
        public float maxZoom = 15f; // largest orthographicSize (farthest)

        private Transform m_Target;

        // 🆕 Modes
        private enum FollowMode { Leader, Last, Custom }
        private FollowMode currentMode = FollowMode.Leader;
        private int customIndex = 0;

        // 🆕 UI for showing focused racer
        public Image racerIconUI;
        public Text racerNameUI;
        private string racerID;
        public Image countryFlagUI;
        public Slider health_Bar;
        public Slider fuel_Bar;
        public Slider tires_Bar;
        public Slider speedSlider;
        private List<CarData> allRacers = new List<CarData>();
        private GameControlling gameControlling; // Reference to GameControlling for intro state
        public bool maunalCamControl = true; // if true, disable auto focus
        public float movementSpeed = 5f; // Speed of manual camera movement

        void Start()
        {
            gameControlling = FindObjectOfType<GameControlling>(); // Initialize reference
            if (cameraZoom != null)
            {
                cameraZoom.minValue = 0f;
                cameraZoom.maxValue = 1f;
                cameraZoom.value = 0f; // start in middle
                cameraZoom.onValueChanged.AddListener(OnZoomSliderChanged);
            }
            else
            {
                Debug.LogWarning("CameraFollow: cameraZoom Slider is not assigned!");
            }
            // 🆕 Ensure speedSlider is initialized
            if (speedSlider != null)
            {
                speedSlider.gameObject.SetActive(false); // Hide at start
                speedSlider.value = 0f; // Initialize to 0
            }
        }

        void UpdateSliders()
        {
            // Handle case when no car is targeted
            if (m_Target == null)
            {
                if (speedSlider != null)
                {
                    bool isIntroOrCountdown = gameControlling != null && (gameControlling.isIntroPlaying || gameControlling.isCountdownPlaying);
                    speedSlider.gameObject.SetActive(!isIntroOrCountdown); // Show after intro/countdown
                    if (!isIntroOrCountdown)
                    {
                        speedSlider.value = 0f; // Set to 0 when no target
                        Debug.Log("No camera target, speedSlider set to 0");
                    }
                    else
                    {
                        // Debug.Log("speedSlider hidden during intro/countdown");
                    }
                }
                return;
            }

            CarPhysics car = m_Target.GetComponent<CarPhysics>();
            if (car != null)
            {
                if (health_Bar != null) health_Bar.value = car.health / car.maxHealth;
                if (fuel_Bar != null) fuel_Bar.value = car.fuel / car.maxFuel;
                if (tires_Bar != null) tires_Bar.value = car.tires / car.maxTires;

                if (speedSlider != null)
                {
                    float currentSpeed = car.GetComponent<Rigidbody2D>().velocity.magnitude;
                    speedSlider.value = currentSpeed; // Normalize to maxValue (50)
                    speedSlider.gameObject.SetActive(true); // Ensure visible
                }
            }
            else
            {
                Debug.LogWarning($"Target {m_Target.name} has no CarPhysics component");
            }
        }
        void OnZoomSliderChanged(float value)
        {
            // invert mapping: value=1 → minZoom (close), value=0 → maxZoom (far)
            float targetSize = Mathf.Lerp(maxZoom, minZoom, value);
            Camera.main.orthographicSize = targetSize;
        }
        void FixedUpdate()
        {
            if (m_Target == null)
            {
                if (maunalCamControl)
                {
                    float horizontalInput = Input.GetAxis("Horizontal"); // Left/Right Arrow Keys or A/D
                    float verticalInput = Input.GetAxis("Vertical");   // Up/Down Arrow Keys or W/S

                    // Calculate movement direction
                    Vector3 moveDirection = new Vector3(horizontalInput, verticalInput, 0f).normalized;

                    // Move the camera
                    transform.Translate(moveDirection * movementSpeed * Time.deltaTime);
                }
            }
            else
            {
                Vector3 targetPosition = m_Target.position + m_Offset;
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref m_Velocity, m_SmoothTime);

                UpdateSliders();
            }
        }
        public void SetTarget(Transform newTarget)
        {
            m_Target = newTarget;
            if (speedSlider != null) speedSlider.maxValue = 100f; // reset, will reassign next frame
        }

        public void FocusRacerById(string carId)
        {
            var racers = FindObjectsOfType<Rivals>();
            foreach (var r in racers)
            {
                var data = r.GetComponent<CarData>();
                if (data != null && data.id == carId)
                {
                    m_Target = r.transform;
                    UpdateUI(r);
                    return;
                }
            }
        }

        void UpdateTarget()
        {
            var racers = RaceManager.Instance.GetSortedRacers();
            if (racers.Count == 0) return;

            switch (currentMode)
            {
                case FollowMode.Last:
                    m_Target = racers[racers.Count - 1].transform;
                    UpdateUI(racers[racers.Count - 1]);
                    break;
                case FollowMode.Custom:
                    FocusCustomRacer();
                    break;
            }
        }
        public void FocusOnLeader()
        {
            var racers = RaceManager.Instance.GetSortedRacers();
            if (racers.Count == 0) return;

            m_Target = racers[0].transform;
            UpdateUI(racers[0]);
        }
        void FocusCustomRacer()
        {
            allRacers = new List<CarData>(FindObjectsOfType<CarData>());
            if (allRacers.Count == 0) return;

            // clamp index
            customIndex = Mathf.Clamp(customIndex, 0, allRacers.Count - 1);

            CarData data = allRacers[customIndex];
            if (data == null) return;

            GameObject racerObj = GameObject.Find(data.id);
            if (racerObj != null)
            {
                m_Target = racerObj.transform;

                Rivals rivalsComp = racerObj.GetComponent<Rivals>();
                if (rivalsComp != null)
                {
                    UpdateUI(rivalsComp);
                }
            }
        }

        void UpdateUI(Rivals racer)
        {
            CarData data = racer.GetComponent<CarData>();
            if (data != null)
            {
                racerID = data.id;
                racerIconUI.sprite = data.icon;
                countryFlagUI.sprite = data.countryFlag;
                racerNameUI.text = data.nickname;
            }
        }
    }
}
