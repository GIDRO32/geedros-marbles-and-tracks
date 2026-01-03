using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

namespace TopDownRace
{
    [System.Serializable]
    public class SongData
    {
        public string displayName;
        public string fileName;
        public string composer;
        public string type;
    }
    public class RaceConfigMusic
    {
        public int lapsThreshold;
    }
    [System.Serializable]
    public class MusicJsonList
    {
        public List<SongData> songs;
    }

    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Text songnameUI; // 🆕 UI Text for song name
        [SerializeField] private Text composerUI; // 🆕 UI Text for composer

        private MusicJsonList musicList;
        private SongData currentSong;
        private SongData lastSong;
        private List<SongData> availableSongs;
        private bool isIntenseMode = false;
        private int lapsRemainingThreshold = 10;
        private Coroutine playingCoroutine; // 🆕 Tracks current coroutine

        void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                Debug.LogError("AudioSource component not found on MusicPlayer!");
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Initialize UI
            if (songnameUI != null) songnameUI.text = "";
            if (composerUI != null) composerUI.text = "";

            LoadMusicList();
            LoadRaceConfig();
            availableSongs = new List<SongData>();
            ResetAndShuffleSongs();
            playingCoroutine = StartCoroutine(PlayRandomSong());
        }
        void LoadRaceConfig()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "RaceConfig.json");
            if (!File.Exists(path))
            {
                Debug.LogError("RaceConfig.json not found at " + path);
                lapsRemainingThreshold = 10; // Default value
                return;
            }
            string json = File.ReadAllText(path);
            RaceConfigMusic config = JsonUtility.FromJson<RaceConfigMusic>(json);

            if (config == null)
            {
                Debug.LogError("Invalid RaceConfig.json");
                lapsRemainingThreshold = 10;
                return;
            }

            lapsRemainingThreshold = config.lapsThreshold;
        }
        void Update()
        {
            // Update UI
            if (currentSong != null)
            {
                if (songnameUI != null) songnameUI.text = "Now playing: " + currentSong.displayName;
                if (composerUI != null) composerUI.text = "By: " + currentSong.composer;
            }
            else
            {
                if (songnameUI != null) songnameUI.text = "Now playing:\nNone";
                if (composerUI != null) composerUI.text = "By: None";
            }

            // 🆕 Check lap count to switch to Intense songs
            if (RaceTrackControl.m_Main != null && !isIntenseMode)
            {
                int lapsRemaining = RaceTrackControl.m_Main.totalLaps - RaceTrackControl.m_Main.currentLap;
                if (lapsRemaining <= lapsRemainingThreshold)
                {
                    isIntenseMode = true;
                    audioSource.Stop(); // Interrupt current song
                    if (playingCoroutine != null)
                    {
                        StopCoroutine(playingCoroutine); // Stop current coroutine
                    }
                    ResetAndShuffleSongs(); // Switch to Intense songs
                    playingCoroutine = StartCoroutine(PlayRandomSong()); // Start new song
                    Debug.Log($"Switched to Intense songs with {lapsRemaining} laps remaining");
                }
            }
        }

        private void LoadMusicList()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "music.json");
            if (!File.Exists(path))
            {
                Debug.LogError("music.json not found at " + path);
                return;
            }

            string json = File.ReadAllText(path);
            musicList = JsonUtility.FromJson<MusicJsonList>(json);

            if (musicList == null || musicList.songs == null || musicList.songs.Count == 0)
            {
                Debug.LogError("Invalid or empty music.json");
            }
        }

        private void ResetAndShuffleSongs()
        {
            availableSongs.Clear();
            if (musicList != null && musicList.songs != null)
            {
                availableSongs.AddRange(musicList.songs.FindAll(song =>
                    song.type == (isIntenseMode ? "Intense" : "Regular")));

                // Shuffle the filtered list
                for (int i = availableSongs.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    var temp = availableSongs[i];
                    availableSongs[i] = availableSongs[j];
                    availableSongs[j] = temp;
                }
                Debug.Log($"Song list reset and shuffled ({(isIntenseMode ? "Intense" : "Regular")}): {string.Join(", ", availableSongs.ConvertAll(s => s.displayName))}");
            }
        }

        private IEnumerator PlayRandomSong()
        {
            if (availableSongs == null || availableSongs.Count == 0)
            {
                if (musicList == null || musicList.songs.Count == 0 ||
                    musicList.songs.FindAll(song => song.type == (isIntenseMode ? "Intense" : "Regular")).Count == 0)
                {
                    Debug.LogError($"No {(isIntenseMode ? "Intense" : "Regular")} songs available in music.json");
                    if (songnameUI != null) songnameUI.text = "Now playing: None";
                    if (composerUI != null) composerUI.text = "By: None";
                    yield break;
                }
                ResetAndShuffleSongs();
            }

            // Select a random song (not the same as lastSong)
            int index = Random.Range(0, availableSongs.Count);
            while (availableSongs[index] == lastSong && availableSongs.Count > 1)
            {
                index = Random.Range(0, availableSongs.Count);
            }
            currentSong = availableSongs[index];
            availableSongs.RemoveAt(index);
            lastSong = currentSong;

            // Load from StreamingAssets instead of Resources
            string musicPath = Path.Combine(Application.streamingAssetsPath, "Mods/Music", currentSong.fileName + ".ogg");
            if (!File.Exists(musicPath))
            {
                musicPath = Path.Combine(Application.streamingAssetsPath, "Mods/Music", currentSong.fileName + ".mp3");
            }
            if (!File.Exists(musicPath))
            {
                Debug.LogError($"Audio file not found in Mods/Music: {currentSong.fileName}");
                yield break;
            }

            // Load audio clip from file
            using (var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip("file://" + musicPath, AudioType.UNKNOWN))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Error loading audio clip: {www.error}");
                    yield break;
                }

                AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                if (clip == null)
                {
                    Debug.LogError($"Failed to create AudioClip from {currentSong.fileName}");
                    yield break;
                }

                audioSource.clip = clip;
                audioSource.Play();

                if (songnameUI != null)
                    songnameUI.text = $"Now playing: {currentSong.displayName}";

                if (composerUI != null)
                    composerUI.text = $"By: {currentSong.composer}";

                Debug.Log($"Playing song: {currentSong.displayName} by {currentSong.composer} ({currentSong.type})");

                yield return new WaitForSeconds(clip.length);
            }

            // Reset list when empty
            if (availableSongs.Count == 0)
            {
                ResetAndShuffleSongs();
            }

            playingCoroutine = StartCoroutine(PlayRandomSong());
        }

    }
}