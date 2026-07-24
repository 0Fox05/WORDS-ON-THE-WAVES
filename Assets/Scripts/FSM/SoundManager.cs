using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Default Music")]
    public AudioClip defaultMusic;
    [Header("Default SFX")]
    public AudioClip buttonClickSFX;
    public AudioClip BookPlaced;

    [Header("Location Audio Clips")]
    public List<AudioClip> locationClips;

    [Header("Audio Sources")]
    public AudioSource musicSource;        // Default background music
    public AudioSource sfxSource;          // Sound effects
    public AudioSource locationMusicSource; // 🎵 New source for location themes
    [Header("Combo SFX")]
    public AudioClip comboSFX;

    [Header("UI Sliders")]
    public Slider musicSlider;
    public Slider sfxSlider;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persist across scenes
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Set starting volume lower
        musicSource.volume = 0.3f;
        sfxSource.volume = 0.5f;

        if (defaultMusic != null)
        {
            PlayMusic(defaultMusic);
        }

        // Initialize sliders if assigned
        if (musicSlider != null)
        {
            musicSlider.value = musicSource.volume;
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = sfxSource.volume;
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }

        // Attach click sound to ALL buttons in the scene
        AttachClickSoundToAllButtons();
    }

    private void AttachClickSoundToAllButtons()
    {
        Button[] buttons = FindObjectsOfType<Button>();
        foreach (Button btn in buttons)
        {
            btn.onClick.AddListener(PlayButtonClick);
        }
    }

    // 🎶 Play background music
    public void PlayMusic(AudioClip clip)
    {
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    // 🔊 Play sound effect
    public void PlaySFX(AudioClip clip)
    {
        sfxSource.PlayOneShot(clip);
    }

    // 🎚️ Volume controls
    // 🎚️ Volume controls
    public void SetMusicVolume(float value)
    {
        musicSource.volume = value;
        locationMusicSource.volume = value; // ✅ keep location music in sync
    }
    public void SetSFXVolume(float value) => sfxSource.volume = value;

    public void CrossfadeMusic(AudioClip newClip, float duration = 1f)
    {
        StartCoroutine(FadeMusic(newClip, duration));
    }

    private IEnumerator FadeMusic(AudioClip newClip, float duration)
    {
        float startVolume = musicSource.volume;

        // Fade out
        while (musicSource.volume > 0)
        {
            musicSource.volume -= startVolume * Time.deltaTime / duration;
            yield return null;
        }

        musicSource.Stop();
        musicSource.clip = newClip;
        musicSource.Play();

        // Fade in
        while (musicSource.volume < startVolume)
        {
            musicSource.volume += startVolume * Time.deltaTime / duration;
            yield return null;
        }
    }

    public void PlayLocationMusic(int index)
    {
        if (index >= 0 && index < locationClips.Count)
        {
            locationMusicSource.clip = locationClips[index];
            locationMusicSource.loop = true;
            locationMusicSource.volume = musicSource.volume; // ✅ start at same slider value
            locationMusicSource.Play();
            Debug.Log($"Playing location background music: {index}");
        }
        else
        {
            Debug.LogWarning("Invalid location music index!");
        }
    }


    // 🔇 Mute toggle
    public void ToggleMute()
    {
        AudioListener.pause = !AudioListener.pause;
    }

    public void MuteLocationMusic(bool mute)
    {
        if (locationMusicSource != null)
        {
            locationMusicSource.mute = mute;
        }
    }


    public void PlayButtonClick()
    {
        if (buttonClickSFX != null)
        {
            PlaySFX(buttonClickSFX);
        }
    }
    public void PlayBookPlaced()
    {
        if (BookPlaced != null)
        {
            PlaySFX(BookPlaced);
        }
    }
    public void PlayLocationAudio(int index)
    {
        if (index >= 0 && index < locationClips.Count)
        {
            PlaySFX(locationClips[index]);
            Debug.Log($"Playing location audio: {index}");
        }
        else
        {
            Debug.LogWarning("Invalid location audio index!");
        }
    }
    public void PlayCombo()
    {
        if (comboSFX != null)
        {
            PlaySFX(comboSFX);
            Debug.Log("Combo sound played!");
        }
    }

}
