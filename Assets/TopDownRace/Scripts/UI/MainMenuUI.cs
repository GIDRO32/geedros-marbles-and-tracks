using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;
using TopDownRace.ScriptableObjects;
namespace TopDownRace
{
    public class MainMenuUI : MonoBehaviour
    {
        [System.Serializable]
        public class CarJsonList
        {
            public List<CarDefinition> cars;
        }
        [SerializeField, Space]
        private DataStorage m_DataStorage;

        [SerializeField, Space]
        private GameplayData m_GameplayData;
        // Start is called before the first frame update
        [SerializeField] private Transform tagsContent; // Content panel for jsonTags
        [SerializeField] private GameObject jsonTagPrefab; // jsonTag prefab
        private List<JsonTag> jsonTags = new List<JsonTag>(); // Tracks spawned JsonTag objects
        [Header("Fullscreen Toggle")]
        public Toggle fullscreenToggle;   // Optional: assign a UI Toggle in Inspector
        public KeyCode toggleKey = KeyCode.F11; // Press F11 to toggle anytime
        void Start()
        {
            SpawnJsonTags();
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = Screen.fullScreen;
                fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleFullscreen();
            }
        }
        public void ToggleFullscreen()
        {
            bool goFullscreen = !Screen.fullScreen;
            SetFullscreen(goFullscreen);

            // Optional: update toggle UI if it exists
            if (fullscreenToggle != null)
                fullscreenToggle.isOn = goFullscreen;
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
        public void SpawnJsonTags()
        {
            // Clear existing tags
            foreach (Transform child in tagsContent)
            {
                Destroy(child.gameObject);
            }

            // Load cars.json
            string path = Path.Combine(Application.streamingAssetsPath, "cars.json");
            if (!File.Exists(path))
            {
                Debug.LogError("cars.json not found at " + path);
                return;
            }

            string json = File.ReadAllText(path);
            CarJsonList carList = JsonUtility.FromJson<CarJsonList>(json);

            if (carList == null || carList.cars == null)
            {
                Debug.LogError("Failed to parse cars.json");
                return;
            }

            // Spawn jsonTags
            foreach (var carDef in carList.cars)
            {
                GameObject tagObj = Instantiate(jsonTagPrefab, tagsContent);
                JsonTag tag = tagObj.GetComponent<JsonTag>();
                if (tag != null)
                {
                    string iconsFolder = Path.Combine(Application.streamingAssetsPath, "Mods/Icons");
                    string flagsFolder = Path.Combine(Application.streamingAssetsPath, "Mods/Flags");
                    tag.nicknameText.text = carDef.nickname;
                    tag.iconImage.sprite = LoadSpriteFromFile(Path.Combine(iconsFolder, carDef.icon + ".png"));
                    tag.carData = carDef;
                    tag.countryFlagImage.sprite = LoadSpriteFromFile(Path.Combine(flagsFolder, carDef.countryFlag + ".png"));
                }
            }

            Debug.Log("jsonTags spawned from cars.json");
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
        public void ConfirmReorder()
        {
            List<CarDefinition> orderedCars = new List<CarDefinition>();

            for (int i = 0; i < tagsContent.childCount; i++)
            {
                JsonTag tag = tagsContent.GetChild(i).GetComponent<JsonTag>();
                if (tag != null && tag.carData != null)
                {
                    orderedCars.Add(tag.carData);
                }
            }

            if (orderedCars.Count == 0)
            {
                Debug.LogError("No jsonTags found for reordering");
                return;
            }

            CarJsonList newList = new CarJsonList { cars = orderedCars };
            string newJson = JsonUtility.ToJson(newList, true);

            string path = Path.Combine(Application.streamingAssetsPath, "cars.json");
            File.WriteAllText(path, newJson);

            Debug.Log("cars.json rearranged and saved based on jsonTags order");
        }
        public void ShuffleCarsJson()
        {
            // Load cars.json
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "cars.json");
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError("cars.json not found at " + path);
                return;
            }

            string json = System.IO.File.ReadAllText(path);
            CarJsonList carList = JsonUtility.FromJson<CarJsonList>(json);

            if (carList == null || carList.cars == null || carList.cars.Count == 0)
            {
                Debug.LogError("Failed to load or parse cars.json, or empty car list");
                return;
            }

            // Shuffle the cars list
            for (int i = carList.cars.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = carList.cars[i];
                carList.cars[i] = carList.cars[j];
                carList.cars[j] = temp;
            }

            // Save shuffled list back to cars.json
            string shuffledJson = JsonUtility.ToJson(carList, true);
            System.IO.File.WriteAllText(path, shuffledJson);
            Debug.Log("cars.json shuffled and saved successfully");
        }
        public void BtnExit()
        {
            Application.Quit();
        }

        public void BtnLevel(int num)
        {
            m_GameplayData.LevelNumber = num;
            switch (num)
            {
                case 0:
                    SceneManager.LoadScene("Forest");
                    break;

                case 1:
                    SceneManager.LoadScene("Desert");
                    break;

                case 2:
                    SceneManager.LoadScene("Snow");
                    break;
                case 3:
                    SceneManager.LoadScene("Linfoxansk");
                    break;
                case 4:
                    SceneManager.LoadScene("HuaHinArena");
                    break;
            }



        }
    }
}
