using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using UnityEngine.Networking;

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
        [SerializeField] private Text songnameUI;
        [SerializeField] private Text composerUI;

        private MusicJsonList musicList;
        private SongData currentSong;
        private SongData lastSong;
        private List<SongData> availableSongs;
        private bool isIntenseMode = false;
        private int lapsRemainingThreshold = 10;
        private Coroutine playingCoroutine;
        
        // NEW: Preloading system
        private Dictionary<string, AudioClip> preloadedClips = new Dictionary<string, AudioClip>();
        private bool isPreloading = false;
        private Queue<string> preloadQueue = new Queue<string>();
        private const int MAX_PRELOADED_CLIPS = 3; // Preload next 3 songs

        void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                Debug.LogError("AudioSource component not found on MusicPlayer!");
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            if (songnameUI != null) songnameUI.text = "";
            if (composerUI != null) composerUI.text = "";

            LoadMusicList();
            LoadRaceConfig();
            availableSongs = new List<SongData>();
            ResetAndShuffleSongs();
            
            // NEW: Start preloading
            StartCoroutine(PreloadNextSongs());
            
            playingCoroutine = StartCoroutine(PlayRandomSong());
        }

        void LoadRaceConfig()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "RaceConfig.json");
            if (!File.Exists(path))
            {
                Debug.LogError("RaceConfig.json not found at " + path);
                lapsRemainingThreshold = 10;
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

            if (RaceTrackControl.m_Main != null && !isIntenseMode)
            {
                int lapsRemaining = RaceTrackControl.m_Main.totalLaps - RaceTrackControl.m_Main.currentLap;
                if (lapsRemaining <= lapsRemainingThreshold)
                {
                    isIntenseMode = true;
                    SwitchToIntenseMode();
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

                // Shuffle
                for (int i = availableSongs.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    var temp = availableSongs[i];
                    availableSongs[i] = availableSongs[j];
                    availableSongs[j] = temp;
                }
                Debug.Log($"Song list reset and shuffled ({(isIntenseMode ? "Intense" : "Regular")}): {availableSongs.Count} songs");
            }
        }

        // NEW: Preload next songs in background
        private IEnumerator PreloadNextSongs()
        {
            while (true)
            {
                // Build queue of next songs to preload
                if (availableSongs.Count > 0 && preloadQueue.Count < MAX_PRELOADED_CLIPS)
                {
                    for (int i = 0; i < Mathf.Min(MAX_PRELOADED_CLIPS, availableSongs.Count); i++)
                    {
                        string filename = availableSongs[i].fileName;
                        if (!preloadedClips.ContainsKey(filename) && !preloadQueue.Contains(filename))
                        {
                            preloadQueue.Enqueue(filename);
                        }
                    }
                }

                // Load one clip per frame to avoid stuttering
                if (preloadQueue.Count > 0 && !isPreloading)
                {
                    string filename = preloadQueue.Dequeue();
                    yield return StartCoroutine(LoadAudioClip(filename, true));
                }

                yield return null; // Wait one frame
            }
        }

        // NEW: Unified audio loading method
        private IEnumerator LoadAudioClip(string filename, bool preloadOnly = false)
        {
            // Check if already loaded
            if (preloadedClips.ContainsKey(filename))
            {
                if (!preloadOnly)
                {
                    audioSource.clip = preloadedClips[filename];
                    audioSource.Play();
                }
                yield break;
            }

            isPreloading = true;

            // Try .ogg first, then .mp3
            string musicPath = Path.Combine(Application.streamingAssetsPath, "Mods/Music", filename + ".ogg");
            AudioType audioType = AudioType.OGGVORBIS;
            
            if (!File.Exists(musicPath))
            {
                musicPath = Path.Combine(Application.streamingAssetsPath, "Mods/Music", filename + ".mp3");
                audioType = AudioType.MPEG;
            }

            if (!File.Exists(musicPath))
            {
                Debug.LogError($"Audio file not found: {filename}");
                isPreloading = false;
                yield break;
            }

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + musicPath, audioType))
            {
                // NEW: Stream audio instead of loading entirely
                DownloadHandlerAudioClip handler = (DownloadHandlerAudioClip)www.downloadHandler;
                handler.streamAudio = true; // This prevents freezing!

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Error loading audio: {www.error}");
                    isPreloading = false;
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip == null)
                {
                    Debug.LogError($"Failed to create AudioClip from {filename}");
                    isPreloading = false;
                    yield break;
                }

                // Store in cache
                preloadedClips[filename] = clip;
                
                // Clean up old clips if cache is too large
                if (preloadedClips.Count > MAX_PRELOADED_CLIPS * 2)
                {
                    CleanupOldClips();
                }

                if (!preloadOnly)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                }

                Debug.Log($"Loaded audio clip: {filename} (preload: {preloadOnly})");
            }

            isPreloading = false;
        }

        // NEW: Clean up clips not in current playlist
        private void CleanupOldClips()
        {
            List<string> toRemove = new List<string>();
            
            foreach (var key in preloadedClips.Keys)
            {
                // Keep current song and next few
                bool shouldKeep = false;
                for (int i = 0; i < Mathf.Min(MAX_PRELOADED_CLIPS, availableSongs.Count); i++)
                {
                    if (availableSongs[i].fileName == key || 
                        (currentSong != null && currentSong.fileName == key))
                    {
                        shouldKeep = true;
                        break;
                    }
                }
                
                if (!shouldKeep)
                {
                    toRemove.Add(key);
                }
            }

            foreach (string key in toRemove)
            {
                if (preloadedClips[key] != null)
                {
                    Destroy(preloadedClips[key]);
                }
                preloadedClips.Remove(key);
                Debug.Log($"Cleaned up old clip: {key}");
            }
        }

        private void SwitchToIntenseMode()
        {
            // Clear old playlist clips
            CleanupOldClips();
            
            // Stop current song
            audioSource.Stop();
            if (playingCoroutine != null)
            {
                StopCoroutine(playingCoroutine);
            }
            
            // Reset to intense songs
            ResetAndShuffleSongs();
            
            // Start new playlist
            playingCoroutine = StartCoroutine(PlayRandomSong());
            Debug.Log($"Switched to Intense mode");
        }

        private IEnumerator PlayRandomSong()
        {
            if (availableSongs == null || availableSongs.Count == 0)
            {
                if (musicList == null || musicList.songs.Count == 0)
                {
                    Debug.LogError($"No songs available");
                    yield break;
                }
                ResetAndShuffleSongs();
            }

            // Select random song (not same as last)
            int index = Random.Range(0, availableSongs.Count);
            while (availableSongs[index] == lastSong && availableSongs.Count > 1)
            {
                index = Random.Range(0, availableSongs.Count);
            }
            
            currentSong = availableSongs[index];
            availableSongs.RemoveAt(index);
            lastSong = currentSong;

            // Load and play (will use preloaded clip if available)
            yield return StartCoroutine(LoadAudioClip(currentSong.fileName, false));

            if (audioSource.clip != null)
            {
                // Wait for song to finish
                yield return new WaitForSeconds(audioSource.clip.length);
            }

            // Reset list when empty
            if (availableSongs.Count == 0)
            {
                ResetAndShuffleSongs();
            }

            playingCoroutine = StartCoroutine(PlayRandomSong());
        }

        void OnDestroy()
        {
            // Clean up all clips
            foreach (var clip in preloadedClips.Values)
            {
                if (clip != null) Destroy(clip);
            }
            preloadedClips.Clear();
        }
    }
}