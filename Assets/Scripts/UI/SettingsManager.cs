using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

public class SettingsManager : MonoBehaviour
{
    [Header("Audio")]
    public AudioMixer audioMixer;
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;
    public Toggle muteToggle;
    
    [Header("Graphics")]
    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Toggle vSyncToggle;
    
    [Header("Gameplay")]
    public Slider sensitivitySlider;
    public TMP_Text sensitivityValueText;
    public Toggle invertMouseToggle;
    
    private Resolution[] resolutions;
    
    void Start()
    {
        InitializeSettings();
        LoadSettings();
    }
    
    void InitializeSettings()
    {
        // Setup resolution dropdown
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();
        
        int currentResolutionIndex = 0;
        
        for (int i = 0; i < resolutions.Length; i++)
        {
            // Fixed: Get refresh rate with proper casting
            float refreshRate = GetRefreshRate(resolutions[i]);
            
            string option = resolutions[i].width + " x " + resolutions[i].height + " @ " + refreshRate.ToString("F0") + "Hz";
            resolutionDropdown.options.Add(new TMP_Dropdown.OptionData(option));
            
            // Fixed: Compare with current resolution
            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height &&
                Mathf.Approximately(refreshRate, GetRefreshRate(Screen.currentResolution)))
            {
                currentResolutionIndex = i;
            }
        }
        
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
        
        // Setup quality dropdown
        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
        qualityDropdown.value = QualitySettings.GetQualityLevel();
        
        // Setup listeners
        if (masterSlider) masterSlider.onValueChanged.AddListener(SetMasterVolume);
        if (musicSlider) musicSlider.onValueChanged.AddListener(SetMusicVolume);
        if (sfxSlider) sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        if (muteToggle) muteToggle.onValueChanged.AddListener(ToggleMute);
        if (qualityDropdown) qualityDropdown.onValueChanged.AddListener(SetQuality);
        if (resolutionDropdown) resolutionDropdown.onValueChanged.AddListener(SetResolution);
        if (fullscreenToggle) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        if (vSyncToggle) vSyncToggle.onValueChanged.AddListener(SetVSync);
        if (sensitivitySlider) sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
        if (invertMouseToggle) invertMouseToggle.onValueChanged.AddListener(SetInvertMouse);
    }
    
    // Fixed: Explicitly cast to float to avoid double/float conversion error
    float GetRefreshRate(Resolution res)
    {
        #if UNITY_2022_1_OR_NEWER
            // For Unity 2022.1+ - cast to float explicitly
            return (float)res.refreshRateRatio.value;
        #else
            // For older Unity versions
            return res.refreshRate;  // refreshRate is already a float in older versions
        #endif
    }
    
    void LoadSettings()
    {
        // Audio
        float masterVol = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float musicVol = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 1f);
        int isMuted = PlayerPrefs.GetInt("IsMuted", 0);
        
        if (masterSlider) masterSlider.value = masterVol;
        if (musicSlider) musicSlider.value = musicVol;
        if (sfxSlider) sfxSlider.value = sfxVol;
        if (muteToggle) muteToggle.isOn = isMuted == 1;
        
        SetMasterVolume(masterVol);
        SetMusicVolume(musicVol);
        SetSFXVolume(sfxVol);
        ToggleMute(isMuted == 1);
        
        // Graphics
        int quality = PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel());
        int resolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", 0);
        int fullscreen = PlayerPrefs.GetInt("Fullscreen", 1);
        int vSync = PlayerPrefs.GetInt("VSync", 0);
        
        if (qualityDropdown && quality >= 0 && quality < qualityDropdown.options.Count)
            qualityDropdown.value = quality;
        if (resolutionDropdown && resolutionIndex >= 0 && resolutionIndex < resolutionDropdown.options.Count)
            resolutionDropdown.value = resolutionIndex;
        if (fullscreenToggle) fullscreenToggle.isOn = fullscreen == 1;
        if (vSyncToggle) vSyncToggle.isOn = vSync == 1;
        
        SetQuality(quality);
        SetResolution(resolutionIndex);
        SetFullscreen(fullscreen == 1);
        SetVSync(vSync == 1);
        
        // Gameplay
        float sensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 2f);
        int invertMouse = PlayerPrefs.GetInt("InvertMouse", 0);
        
        if (sensitivitySlider)
        {
            sensitivitySlider.value = sensitivity;
            if (sensitivityValueText) sensitivityValueText.text = sensitivity.ToString("F1");
        }
        if (invertMouseToggle) invertMouseToggle.isOn = invertMouse == 1;
        
        SetSensitivity(sensitivity);
        SetInvertMouse(invertMouse == 1);
    }
    
    public void SetMasterVolume(float volume)
    {
        if (audioMixer)
            audioMixer.SetFloat("MasterVolume", Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20);
        PlayerPrefs.SetFloat("MasterVolume", volume);
    }
    
    public void SetMusicVolume(float volume)
    {
        if (audioMixer)
            audioMixer.SetFloat("MusicVolume", Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20);
        PlayerPrefs.SetFloat("MusicVolume", volume);
    }
    
    public void SetSFXVolume(float volume)
    {
        if (audioMixer)
            audioMixer.SetFloat("SFXVolume", Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20);
        PlayerPrefs.SetFloat("SFXVolume", volume);
    }
    
    public void ToggleMute(bool isMuted)
    {
        if (audioMixer)
            audioMixer.SetFloat("MasterVolume", isMuted ? -80f : Mathf.Log10(Mathf.Max(masterSlider.value, 0.0001f)) * 20);
        PlayerPrefs.SetInt("IsMuted", isMuted ? 1 : 0);
    }
    
    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
        PlayerPrefs.SetInt("QualityLevel", qualityIndex);
    }
    
    public void SetResolution(int resolutionIndex)
    {
        if (resolutionIndex >= 0 && resolutionIndex < resolutions.Length)
        {
            Resolution resolution = resolutions[resolutionIndex];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
            PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
        }
    }
    
    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
    }
    
    public void SetVSync(bool vSyncEnabled)
    {
        QualitySettings.vSyncCount = vSyncEnabled ? 1 : 0;
        PlayerPrefs.SetInt("VSync", vSyncEnabled ? 1 : 0);
    }
    
    public void SetSensitivity(float sensitivity)
    {
        if (sensitivityValueText) sensitivityValueText.text = sensitivity.ToString("F1");
        PlayerPrefs.SetFloat("MouseSensitivity", sensitivity);
    }
    
    public void SetInvertMouse(bool invert)
    {
        PlayerPrefs.SetInt("InvertMouse", invert ? 1 : 0);
    }
    
    public void SaveSettings()
    {
        PlayerPrefs.Save();
        Debug.Log("Settings saved!");
    }
    
    public void ResetToDefault()
    {
        PlayerPrefs.DeleteKey("MasterVolume");
        PlayerPrefs.DeleteKey("MusicVolume");
        PlayerPrefs.DeleteKey("SFXVolume");
        PlayerPrefs.DeleteKey("IsMuted");
        PlayerPrefs.DeleteKey("QualityLevel");
        PlayerPrefs.DeleteKey("ResolutionIndex");
        PlayerPrefs.DeleteKey("Fullscreen");
        PlayerPrefs.DeleteKey("VSync");
        PlayerPrefs.DeleteKey("MouseSensitivity");
        PlayerPrefs.DeleteKey("InvertMouse");
        
        LoadSettings();
    }
}