using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SetVolume : MonoBehaviour
{
    public AudioMixer Music_Mixer;
    public Slider Music_Slider;
    void Start()
    {
        Music_Slider.value = PlayerPrefs.GetFloat("Music", 0.75f);
    }
    public void setMusicVol(float sliderValue)
    {
        Music_Mixer.SetFloat("MusicVol", Mathf.Log10(sliderValue)*20);
        PlayerPrefs.SetFloat("Music", sliderValue);
    }
}