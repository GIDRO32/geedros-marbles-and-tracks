using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace TopDownRace
{
    public class GameControlling : MonoBehaviour
    {
        public static GameControlling m_Current;

        public GameObject carPrefab;
        public Transform spawnPoint;
        public float spawnOffsetX = 2f; // X offset for spawning, adjustable in Inspector
        private float currentOffsetX; // Tracks current offset with sign
        public CameraFollow cameraFollow;
        private bool isPaused = false;

        public float spawnDelay = 1.0f;
        [SerializeField] private Text IntroText; // UI Text for intro sequence
        [SerializeField] private Text CountdownText; // UI Text for countdown
        [SerializeField] private GameObject SpawnPoof; // ParticleSystem prefab for spawn effect
        [SerializeField] private float letterDelay = 0.8f; // Delay between letters in intro text
        [SerializeField] private float countdownDuration = 1f; // Duration per countdown step
        [SerializeField] private float flyAroundRadius = 30f; // Radius for random camera movement
        [SerializeField] private float flyAroundSpeed = 5f; // Speed of camera movement
        [SerializeField] private float cameraSmoothness = 2f; // Controls camera movement smoothness
        [SerializeField] private float cameraWanderSpeed = 1f; // Controls speed of random wandering
        [HideInInspector] public bool isIntroPlaying = false; // Tracks intro sequence
        [HideInInspector] public bool isCountdownPlaying = false; // Tracks countdown
        public GameObject introUI;
        public GameObject raceUI;
        public GameObject countdownUI;
        public AudioSource musicSource;
        public KeyCode toggleKey = KeyCode.F11; // Press F11 to toggle anytime

        private void Awake()
        {
            m_Current = this;
        }

        void Start()
        {
            currentOffsetX = spawnOffsetX;
            isIntroPlaying = true;
            StartCoroutine(PlayIntroSequence());
            introUI.SetActive(true);
            raceUI.SetActive(false);
        }
        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) && !isCountdownPlaying && !isIntroPlaying)
            {
                cameraFollow.maunalCamControl = false;
                StartRace();
            }
            if (Input.GetKeyDown(KeyCode.Escape)) // Or any other key
            {
                TogglePause();
            }
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleFullscreen();
            }
        }
        public void TogglePause()
        {
            isPaused = !isPaused;

            if (isPaused)
            {
                Time.timeScale = 0f; // Pause the game
                musicSource.Pause();
            }
            else
            {
                Time.timeScale = 1f; // Resume the game
                musicSource.Play();
            }
        }
        private IEnumerator PlayIntroSequence()
        {
            if (IntroText == null)
            {
                Debug.LogError("IntroText not assigned!");
                yield break;
            }

            // Load Intro.json
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "Intro.json");
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError("Intro.json not found at " + path);
                yield break;
            }

            string json = System.IO.File.ReadAllText(path);
            var introData = JsonUtility.FromJson<IntroData>(json);
            if (introData == null || string.IsNullOrEmpty(introData.line1) || string.IsNullOrEmpty(introData.line2))
            {
                Debug.LogError("Invalid or empty Intro.json");
                yield break;
            }

            // Start camera in free-fly mode
            CameraFollow cameraFollow = FindObjectOfType<CameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(null); // Free-fly mode
            }
            else
            {
                Debug.LogError("CameraFollow not found!");
                yield break;
            }

            Vector3 trackCenter = spawnPoint.position; // Use spawnPoint as approximate track center
            float time = 0f;
            Vector3 targetPos = trackCenter; // Current target position for smooth movement

            // Display line1 letter by letter
            IntroText.text = "";
            foreach (char c in introData.line1)
            {
                IntroText.text += c;
                yield return new WaitForSeconds(letterDelay);
                time += letterDelay;
            }

            yield return new WaitForSeconds(1f); // Pause between lines

            // Display line2 letter by letter
            IntroText.text = "";
            foreach (char c in introData.line2)
            {
                IntroText.text += c;
                yield return new WaitForSeconds(letterDelay);
                time += letterDelay;
            }

            // 🆕 Smooth random camera movement throughout intro
            float introDuration = introData.line1.Length * letterDelay + 1f + introData.line2.Length * letterDelay + 1f;
            float elapsed = 0f;
            yield return new WaitForSeconds(2f); // Pause between lines
            IntroText.text = "";
            isIntroPlaying = false;
            Debug.Log("Intro sequence completed");
        }

        public void StartRace()
        {
            if (isIntroPlaying || isCountdownPlaying)
            {
                Debug.LogWarning("Cannot start race: Intro or countdown in progress");
                return;
            }
            introUI.SetActive(false);
            raceUI.SetActive(true);
            StartCoroutine(RunCountdownAndSpawn());
        }
        public void ToggleFullscreen()
        {
            bool goFullscreen = !Screen.fullScreen;
            SetFullscreen(goFullscreen);
        }
        public void SetFullscreen(bool fullscreen)
        {
            if (fullscreen)
            {
                // True fullscreen (exclusive mode)
                Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
            }
            else
            {
                // Windowed, but keep the same resolution
                Screen.fullScreenMode = FullScreenMode.Windowed;
            }

            // Optional: keep current resolution when going windowed
            // Remove the lines below if you want Unity to auto-resize
            if (!fullscreen)
            {
                Resolution current = Screen.currentResolution;
                Screen.SetResolution(current.width, current.height, false);
            }

            Debug.Log("Fullscreen: " + fullscreen);
        }
        private IEnumerator SmoothCameraToSpawn()
        {
            CameraFollow cameraFollow = FindObjectOfType<CameraFollow>();
            if (cameraFollow == null)
            {
                Debug.LogError("CameraFollow not found!");
                yield break;
            }

            Vector3 targetPos = new Vector3(spawnPoint.position.x, spawnPoint.position.y, -10f);
            Vector3 startPos = cameraFollow.transform.position;

            float duration = 1.2f;        // How long the camera takes to arrive
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime; // unscaled = smooth even during countdown
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration); // buttery smooth easing

                cameraFollow.transform.position = Vector3.Lerp(startPos, targetPos, t);

                yield return null;
            }

            // Snap to exact position at the end (avoids floating-point drift)
            cameraFollow.transform.position = targetPos;
        }
        private IEnumerator RunCountdownAndSpawn()
        {
            if (CountdownText == null)
            {
                Debug.LogError("CountdownText not assigned!");
                yield break;
            }

            isCountdownPlaying = true;

            // Move camera to spawnPoint
            CameraFollow cameraFollow = FindObjectOfType<CameraFollow>();
            if (cameraFollow == null)
            {
                Debug.LogError("CameraFollow not found!");
                isCountdownPlaying = false;
                yield break;
            }

            StartCoroutine(SmoothCameraToSpawn());
            cameraFollow.SetTarget(null);

            // Countdown sequence
            CountdownText.text = "3";
            yield return new WaitForSeconds(countdownDuration);
            CountdownText.text = "2";
            yield return new WaitForSeconds(countdownDuration);
            CountdownText.text = "1";
            yield return new WaitForSeconds(countdownDuration);
            CountdownText.text = "GO!";
            yield return new WaitForSeconds(countdownDuration);
            CountdownText.text = "";
            // countdownUI.SetActive(false);

            // Start spawning cars with poof effect
            yield return StartCoroutine(SpawnCarsFromJson());

            // Set camera target to first car after all cars have spawned
            var racers = RaceManager.Instance.GetSortedRacers();
            if (racers.Count > 0)
            {
                Rivals firstRacer = racers[0];
                cameraFollow.SetTarget(firstRacer.transform);
                Debug.Log($"Camera set to follow first car after all spawned: {firstRacer.gameObject.name}");
            }
            else
            {
                Debug.LogWarning("No racers found to set as camera target");
            }
            countdownUI.SetActive(false);
            isCountdownPlaying = false;
        }
        private Sprite LoadSpriteFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"Sprite not found at: {path}");
                return null;
            }

            byte[] imageData = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(imageData);

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        IEnumerator SpawnCarsFromJson()
        {
            // Load json from StreamingAssets
            string path = Path.Combine(Application.streamingAssetsPath, "cars.json");
            string json = File.ReadAllText(path);
            CarJsonList carList = JsonUtility.FromJson<CarJsonList>(json);

            // Check for valid car list
            if (carList == null || carList.cars == null)
            {
                yield break;
            }

            foreach (var carDef in carList.cars)
            {
                // Verify offset before calculating spawn position
                if (currentOffsetX == 0)
                {
                    currentOffsetX = spawnOffsetX;
                }

                // Calculate spawn position for each car
                Vector3 spawnPos = spawnPoint.position; // Get base spawn position
                spawnPos.x += currentOffsetX; // Apply current X offset

                // Load sprites
                string carsFolder = Path.Combine(Application.streamingAssetsPath, "Mods/Cars");
                string iconsFolder = Path.Combine(Application.streamingAssetsPath, "Mods/Icons");
                string flagsFolder = Path.Combine(Application.streamingAssetsPath, "Mods/Flags");

                Sprite carSprite = LoadSpriteFromFile(Path.Combine(carsFolder, carDef.image + ".png"));
                Sprite iconSprite = LoadSpriteFromFile(Path.Combine(iconsFolder, carDef.icon + ".png"));
                Sprite countryFlagSprite = LoadSpriteFromFile(Path.Combine(flagsFolder, carDef.countryFlag + ".png"));
                // Check sprite loading
                if (carSprite == null || iconSprite == null)
                {
                    Debug.LogError($"Failed to load sprites for {carDef.id}: carSprite={carSprite}, iconSprite={iconSprite}");
                }
                if (SpawnPoof != null)
                {
                    Instantiate(SpawnPoof, spawnPos, Quaternion.identity);
                }
                // Instantiate prefab
                GameObject car = Instantiate(carPrefab, spawnPos, spawnPoint.rotation);

                // Set child sprite renderer
                SpriteRenderer sr = car.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sprite = carSprite;
                }
                else
                {
                    Debug.LogError($"No SpriteRenderer found for {carDef.id}");
                }

                // Fill CarData
                CarData data = car.GetComponent<CarData>();
                if (data != null)
                {
                    data.id = carDef.id;
                    data.nickname = carDef.nickname;
                    data.shortName = carDef.shortName;
                    data.carImage = carSprite;
                    data.icon = iconSprite;
                    data.countryFlag = countryFlagSprite;
                }
                else
                {
                    Debug.LogError($"No CarData component found for {carDef.id}");
                }

                car.name = carDef.id;

                // Alternate offset for next car
                currentOffsetX *= -1f;
                Debug.Log($"Next offset for {carDef.id}: {currentOffsetX}");

                // Register with RaceManager
                Rivals rival = car.GetComponent<Rivals>();
                if (rival != null)
                {
                    RaceManager.Instance.RegisterRacer(rival);
                }
                else
                {
                    Debug.LogError($"No Rivals component found for {carDef.id}");
                }

                yield return new WaitForSeconds(spawnDelay);
            }
            cameraFollow.FocusOnLeader();
        }
    }
}
[System.Serializable]
public class IntroData
{
    public string line1;
    public string line2;
}