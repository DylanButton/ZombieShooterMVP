using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using System.Linq;

public class SettingsManager : MonoBehaviour
{
    [Header("Graphics Settings")]
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown fullscreenModeDropdown;
    public Toggle fullscreenToggle;
    public Toggle vsyncToggle;
    public Slider fovSlider;
    public TMP_Text fovValueText;
    
    [Header("Audio Settings")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public AudioMixer audioMixer;
    
    [Header("Controls Settings")]
    public Slider mouseSensitivitySlider;
    public TMP_Text sensitivityValueText;
    public Slider controllerSensitivitySlider;
    public TMP_Text controllerSensitivityValueText;
    public Toggle invertMouseToggle;
    public Toggle invertControllerToggle;
    public TMP_Dropdown controllerVibrationDropdown;
    
    [Header("Gameplay Settings")]
    public Toggle showCrosshairToggle;
    public Toggle showDamageNumbersToggle;
    public Toggle autoReloadToggle;
    public Toggle sprintToggleToggle;
    public Toggle crouchToggleToggle;
    public Slider aimAssistSlider;
    public TMP_Text aimAssistValueText;
    
    [Header("Rebinding UI (Optional)")]
    public GameObject rebindPanel;
    public TMP_Text[] keybindTexts;
    public Button[] rebindButtons;
    
    [Header("UI References")]
    public Button applyButton;
    public Button resetButton;
    public Button backButton;
    
    private List<Resolution> resolutions = new List<Resolution>();
    private Resolution currentResolution;
    
    // Input Actions (for rebinding)
    private InputActionAsset inputActions;
    private Dictionary<string, InputAction> actionMap = new Dictionary<string, InputAction>();
    
    // Player Controller reference for sensitivity
    private PlayerController playerController;
    
    private bool isLoading = false;
    
    void Start()
    {
        // Find player controller
        playerController = FindObjectOfType<PlayerController>();
        
        // Initialize all settings
        InitializeGraphicsSettings();
        InitializeAudioSettings();
        InitializeControlsSettings();
        InitializeGameplaySettings();
        
        // Setup UI buttons
        if (applyButton != null)
            applyButton.onClick.AddListener(ApplyAllSettings);
        
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetToDefaults);
        
        if (backButton != null)
            backButton.onClick.AddListener(BackToMenu);
        
        // Load saved settings
        LoadAllSettings();
        
        Debug.Log("Settings Manager initialized");
    }
    
    #region Graphics Settings
    void InitializeGraphicsSettings()
    {
        currentResolution = Screen.currentResolution;
        
        // Resolution dropdown
        if (resolutionDropdown != null)
        {
            InitializeResolutionDropdown();
        }
        
        // Quality dropdown
        if (qualityDropdown != null)
        {
            InitializeQualityDropdown();
        }
        
        // Fullscreen mode dropdown
        if (fullscreenModeDropdown != null)
        {
            InitializeFullscreenModeDropdown();
        }
        
        // Fullscreen toggle
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = Screen.fullScreenMode != FullScreenMode.Windowed;
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
        
        // VSync toggle
        if (vsyncToggle != null)
        {
            vsyncToggle.isOn = QualitySettings.vSyncCount > 0;
            vsyncToggle.onValueChanged.AddListener(SetVSync);
        }
        
        // FOV Slider
        if (fovSlider != null)
        {
            if (playerController != null && playerController.GetComponentInChildren<Camera>() != null)
            {
                Camera playerCam = playerController.GetComponentInChildren<Camera>();
                fovSlider.minValue = 60f;
                fovSlider.maxValue = 120f;
                fovSlider.value = playerCam.fieldOfView;
                fovSlider.onValueChanged.AddListener(SetFOV);
            }
            
            if (fovValueText != null)
            {
                fovValueText.text = $"{fovSlider.value:F0}";
            }
        }
    }
    
    void InitializeResolutionDropdown()
    {
        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();
        int currentResolutionIndex = 0;
        
        Resolution[] availableResolutions = Screen.resolutions;
        resolutions.Clear();
        
        var uniqueResolutions = new Dictionary<string, Resolution>();
        
        for (int i = 0; i < availableResolutions.Length; i++)
        {
            Resolution res = availableResolutions[i];
            int refreshRate = (int)res.refreshRateRatio.value;
            string key = $"{res.width}x{res.height}@{refreshRate}";
            
            if (!uniqueResolutions.ContainsKey(key))
            {
                uniqueResolutions.Add(key, res);
                resolutions.Add(res);
                
                string optionText = $"{res.width} x {res.height} @ {refreshRate} Hz";
                options.Add(optionText);
                
                if (res.width == currentResolution.width && 
                    res.height == currentResolution.height &&
                    (int)res.refreshRateRatio.value == (int)currentResolution.refreshRateRatio.value)
                {
                    currentResolutionIndex = resolutions.Count - 1;
                }
            }
        }
        
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }
    
    void InitializeQualityDropdown()
    {
        qualityDropdown.ClearOptions();
        List<string> options = new List<string>();
        
        string[] qualityLevels = QualitySettings.names;
        
        foreach (string level in qualityLevels)
        {
            options.Add(level);
        }
        
        qualityDropdown.AddOptions(options);
        qualityDropdown.value = QualitySettings.GetQualityLevel();
        qualityDropdown.RefreshShownValue();
        qualityDropdown.onValueChanged.AddListener(SetQuality);
    }
    
    void InitializeFullscreenModeDropdown()
    {
        fullscreenModeDropdown.ClearOptions();
        List<string> options = new List<string>
        {
            "Exclusive Fullscreen",
            "Fullscreen Window",
            "Maximized Window",
            "Windowed"
        };
        
        fullscreenModeDropdown.AddOptions(options);
        fullscreenModeDropdown.value = (int)Screen.fullScreenMode;
        fullscreenModeDropdown.RefreshShownValue();
        fullscreenModeDropdown.onValueChanged.AddListener(SetFullscreenMode);
    }
    
    public void SetResolution(int resolutionIndex)
    {
        if (resolutionIndex < 0 || resolutionIndex >= resolutions.Count) return;
        
        Resolution selectedResolution = resolutions[resolutionIndex];
        FullScreenMode currentMode = Screen.fullScreenMode;
        
        Screen.SetResolution(
            selectedResolution.width,
            selectedResolution.height,
            currentMode,
            selectedResolution.refreshRateRatio
        );
        
        currentResolution = selectedResolution;
        SaveGraphicsSettings();
    }
    
    public void SetFullscreen(bool isFullscreen)
    {
        isLoading = true;
        
        if (isFullscreen)
        {
            FullScreenMode mode = (FullScreenMode)fullscreenModeDropdown.value;
            Screen.SetResolution(currentResolution.width, currentResolution.height, mode, currentResolution.refreshRateRatio);
        }
        else
        {
            Screen.SetResolution(currentResolution.width, currentResolution.height, FullScreenMode.Windowed, currentResolution.refreshRateRatio);
        }
        
        isLoading = false;
        SaveGraphicsSettings();
    }
    
    public void SetFullscreenMode(int modeIndex)
    {
        FullScreenMode mode = (FullScreenMode)modeIndex;
        Screen.SetResolution(currentResolution.width, currentResolution.height, mode, currentResolution.refreshRateRatio);
        
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = mode != FullScreenMode.Windowed;
        }
        
        SaveGraphicsSettings();
    }
    
    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
        SaveGraphicsSettings();
    }
    
    public void SetVSync(bool useVSync)
    {
        QualitySettings.vSyncCount = useVSync ? 1 : 0;
        SaveGraphicsSettings();
    }
    
    public void SetFOV(float fovValue)
    {
        if (playerController != null && playerController.GetComponentInChildren<Camera>() != null)
        {
            Camera playerCam = playerController.GetComponentInChildren<Camera>();
            playerCam.fieldOfView = fovValue;
            
            if (fovValueText != null)
            {
                fovValueText.text = $"{fovValue:F0}";
            }
            
            SaveGameplaySettings();
        }
    }
    #endregion
    
    #region Audio Settings
    void InitializeAudioSettings()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        }
        
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
            musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
        }
        
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
            sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
        }
    }
    
    public void SetMasterVolume(float volume)
    {
        if (audioMixer != null)
        {
            // Convert 0-1 slider to -80 to 0 dB
            float dB = volume > 0.01f ? 20f * Mathf.Log10(volume) : -80f;
            audioMixer.SetFloat("MasterVolume", dB);
        }
        SaveAudioSettings();
    }
    
    public void SetMusicVolume(float volume)
    {
        if (audioMixer != null)
        {
            float dB = volume > 0.01f ? 20f * Mathf.Log10(volume) : -80f;
            audioMixer.SetFloat("MusicVolume", dB);
        }
        SaveAudioSettings();
    }
    
    public void SetSFXVolume(float volume)
    {
        if (audioMixer != null)
        {
            float dB = volume > 0.01f ? 20f * Mathf.Log10(volume) : -80f;
            audioMixer.SetFloat("SFXVolume", dB);
        }
        SaveAudioSettings();
    }
    #endregion
    
    #region Controls Settings
    void InitializeControlsSettings()
    {
        // Mouse Sensitivity
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.minValue = 0.1f;
            mouseSensitivitySlider.maxValue = 10f;
            mouseSensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 0.1f);
            mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);
            
            if (sensitivityValueText != null)
            {
                sensitivityValueText.text = $"{mouseSensitivitySlider.value:F1}";
            }
        }
        
        // Controller Sensitivity
        if (controllerSensitivitySlider != null)
        {
            controllerSensitivitySlider.minValue = 0.1f;
            controllerSensitivitySlider.maxValue = 10f;
            controllerSensitivitySlider.value = PlayerPrefs.GetFloat("ControllerSensitivity", 1f);
            controllerSensitivitySlider.onValueChanged.AddListener(SetControllerSensitivity);
            
            if (controllerSensitivityValueText != null)
            {
                controllerSensitivityValueText.text = $"{controllerSensitivitySlider.value:F1}";
            }
        }
        
        // Invert Mouse
        if (invertMouseToggle != null)
        {
            invertMouseToggle.isOn = PlayerPrefs.GetInt("InvertMouse", 0) == 1;
            invertMouseToggle.onValueChanged.AddListener(SetInvertMouse);
        }
        
        // Invert Controller
        if (invertControllerToggle != null)
        {
            invertControllerToggle.isOn = PlayerPrefs.GetInt("InvertController", 0) == 1;
            invertControllerToggle.onValueChanged.AddListener(SetInvertController);
        }
        
        // Controller Vibration
        if (controllerVibrationDropdown != null)
        {
            controllerVibrationDropdown.value = PlayerPrefs.GetInt("ControllerVibration", 0);
            controllerVibrationDropdown.onValueChanged.AddListener(SetControllerVibration);
        }
    }
    
    public void SetMouseSensitivity(float sensitivity)
    {
        if (playerController != null)
        {
            // Assuming PlayerController has a public sensitivity variable
            // You'll need to adjust this based on your actual PlayerController script
            // playerController.mouseSensitivity = sensitivity;
            
            // For now, we'll store it and you can modify your PlayerController to read it
            PlayerPrefs.SetFloat("MouseSensitivity", sensitivity);
            
            if (sensitivityValueText != null)
            {
                sensitivityValueText.text = $"{sensitivity:F1}";
            }
        }
        SaveControlsSettings();
    }
    
    public void SetControllerSensitivity(float sensitivity)
    {
        PlayerPrefs.SetFloat("ControllerSensitivity", sensitivity);
        
        if (controllerSensitivityValueText != null)
        {
            controllerSensitivityValueText.text = $"{sensitivity:F1}";
        }
        SaveControlsSettings();
    }
    
    public void SetInvertMouse(bool invert)
    {
        PlayerPrefs.SetInt("InvertMouse", invert ? 1 : 0);
        SaveControlsSettings();
    }
    
    public void SetInvertController(bool invert)
    {
        PlayerPrefs.SetInt("InvertController", invert ? 1 : 0);
        SaveControlsSettings();
    }
    
    public void SetControllerVibration(int vibrationLevel)
    {
        PlayerPrefs.SetInt("ControllerVibration", vibrationLevel);
        SaveControlsSettings();
    }
    #endregion
    
    #region Gameplay Settings
    void InitializeGameplaySettings()
    {
        // Show Crosshair
        if (showCrosshairToggle != null)
        {
            showCrosshairToggle.isOn = PlayerPrefs.GetInt("ShowCrosshair", 1) == 1;
            showCrosshairToggle.onValueChanged.AddListener(SetShowCrosshair);
        }
        
        // Show Damage Numbers
        if (showDamageNumbersToggle != null)
        {
            showDamageNumbersToggle.isOn = PlayerPrefs.GetInt("ShowDamageNumbers", 1) == 1;
            showDamageNumbersToggle.onValueChanged.AddListener(SetShowDamageNumbers);
        }
        
        // Auto Reload
        if (autoReloadToggle != null)
        {
            autoReloadToggle.isOn = PlayerPrefs.GetInt("AutoReload", 1) == 1;
            autoReloadToggle.onValueChanged.AddListener(SetAutoReload);
        }
        
        // Sprint Toggle
        if (sprintToggleToggle != null)
        {
            sprintToggleToggle.isOn = PlayerPrefs.GetInt("SprintToggle", 0) == 1;
            sprintToggleToggle.onValueChanged.AddListener(SetSprintToggle);
        }
        
        // Crouch Toggle
        if (crouchToggleToggle != null)
        {
            crouchToggleToggle.isOn = PlayerPrefs.GetInt("CrouchToggle", 0) == 1;
            crouchToggleToggle.onValueChanged.AddListener(SetCrouchToggle);
        }
        
        // Aim Assist
        if (aimAssistSlider != null)
        {
            aimAssistSlider.minValue = 0f;
            aimAssistSlider.maxValue = 100f;
            aimAssistSlider.value = PlayerPrefs.GetFloat("AimAssist", 50f);
            aimAssistSlider.onValueChanged.AddListener(SetAimAssist);
            
            if (aimAssistValueText != null)
            {
                aimAssistValueText.text = $"{aimAssistSlider.value:F0}%";
            }
        }
    }
    
    public void SetShowCrosshair(bool show)
    {
        PlayerPrefs.SetInt("ShowCrosshair", show ? 1 : 0);
        SaveGameplaySettings();
    }
    
    public void SetShowDamageNumbers(bool show)
    {
        PlayerPrefs.SetInt("ShowDamageNumbers", show ? 1 : 0);
        SaveGameplaySettings();
    }
    
    public void SetAutoReload(bool autoReload)
    {
        PlayerPrefs.SetInt("AutoReload", autoReload ? 1 : 0);
        SaveGameplaySettings();
    }
    
    public void SetSprintToggle(bool toggle)
    {
        PlayerPrefs.SetInt("SprintToggle", toggle ? 1 : 0);
        SaveGameplaySettings();
    }
    
    public void SetCrouchToggle(bool toggle)
    {
        PlayerPrefs.SetInt("CrouchToggle", toggle ? 1 : 0);
        SaveGameplaySettings();
    }
    
    public void SetAimAssist(float value)
    {
        PlayerPrefs.SetFloat("AimAssist", value);
        
        if (aimAssistValueText != null)
        {
            aimAssistValueText.text = $"{value:F0}%";
        }
        SaveGameplaySettings();
    }
    #endregion
    
    #region Save/Load Methods
    void SaveGraphicsSettings()
    {
        if (isLoading) return;
        
        PlayerPrefs.SetInt("ResolutionWidth", currentResolution.width);
        PlayerPrefs.SetInt("ResolutionHeight", currentResolution.height);
        PlayerPrefs.SetInt("FullscreenMode", (int)Screen.fullScreenMode);
        PlayerPrefs.SetInt("QualityLevel", QualitySettings.GetQualityLevel());
        PlayerPrefs.SetInt("VSync", QualitySettings.vSyncCount);
        
        if (fovSlider != null)
        {
            PlayerPrefs.SetFloat("FOV", fovSlider.value);
        }
        
        PlayerPrefs.Save();
        Debug.Log("Graphics settings saved");
    }
    
    void SaveAudioSettings()
    {
        if (isLoading) return;
        
        if (masterVolumeSlider != null)
            PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider.value);
        
        if (musicVolumeSlider != null)
            PlayerPrefs.SetFloat("MusicVolume", musicVolumeSlider.value);
        
        if (sfxVolumeSlider != null)
            PlayerPrefs.SetFloat("SFXVolume", sfxVolumeSlider.value);
        
        PlayerPrefs.Save();
        Debug.Log("Audio settings saved");
    }
    
    void SaveControlsSettings()
    {
        if (isLoading) return;
        
        PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivitySlider.value);
        PlayerPrefs.SetFloat("ControllerSensitivity", controllerSensitivitySlider.value);
        PlayerPrefs.SetInt("InvertMouse", invertMouseToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("InvertController", invertControllerToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("ControllerVibration", controllerVibrationDropdown.value);
        
        PlayerPrefs.Save();
        Debug.Log("Controls settings saved");
    }
    
    void SaveGameplaySettings()
    {
        if (isLoading) return;
        
        PlayerPrefs.SetInt("ShowCrosshair", showCrosshairToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("ShowDamageNumbers", showDamageNumbersToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("AutoReload", autoReloadToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("SprintToggle", sprintToggleToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("CrouchToggle", crouchToggleToggle.isOn ? 1 : 0);
        PlayerPrefs.SetFloat("AimAssist", aimAssistSlider.value);
        
        if (fovSlider != null)
        {
            PlayerPrefs.SetFloat("FOV", fovSlider.value);
        }
        
        PlayerPrefs.Save();
        Debug.Log("Gameplay settings saved");
    }
    
    void LoadAllSettings()
    {
        isLoading = true;
        
        // Load Graphics
        if (PlayerPrefs.HasKey("ResolutionWidth"))
        {
            int width = PlayerPrefs.GetInt("ResolutionWidth");
            int height = PlayerPrefs.GetInt("ResolutionHeight");
            
            for (int i = 0; i < resolutions.Count; i++)
            {
                if (resolutions[i].width == width && resolutions[i].height == height)
                {
                    if (resolutionDropdown != null)
                    {
                        resolutionDropdown.value = i;
                        SetResolution(i);
                    }
                    break;
                }
            }
        }
        
        if (fullscreenModeDropdown != null && PlayerPrefs.HasKey("FullscreenMode"))
        {
            fullscreenModeDropdown.value = PlayerPrefs.GetInt("FullscreenMode");
        }
        
        if (qualityDropdown != null && PlayerPrefs.HasKey("QualityLevel"))
        {
            qualityDropdown.value = PlayerPrefs.GetInt("QualityLevel");
        }
        
        if (vsyncToggle != null && PlayerPrefs.HasKey("VSync"))
        {
            vsyncToggle.isOn = PlayerPrefs.GetInt("VSync") > 0;
        }
        
        if (fovSlider != null && PlayerPrefs.HasKey("FOV"))
        {
            fovSlider.value = PlayerPrefs.GetFloat("FOV");
        }
        
        // Load Controls
        if (mouseSensitivitySlider != null && PlayerPrefs.HasKey("MouseSensitivity"))
        {
            mouseSensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity");
        }
        
        if (invertMouseToggle != null && PlayerPrefs.HasKey("InvertMouse"))
        {
            invertMouseToggle.isOn = PlayerPrefs.GetInt("InvertMouse") == 1;
        }
        
        // Load Gameplay
        if (showCrosshairToggle != null && PlayerPrefs.HasKey("ShowCrosshair"))
        {
            showCrosshairToggle.isOn = PlayerPrefs.GetInt("ShowCrosshair") == 1;
        }
        
        if (autoReloadToggle != null && PlayerPrefs.HasKey("AutoReload"))
        {
            autoReloadToggle.isOn = PlayerPrefs.GetInt("AutoReload") == 1;
        }
        
        isLoading = false;
        Debug.Log("All settings loaded");
    }
    #endregion
    
    #region UI Methods
    public void ApplyAllSettings()
    {
        // Apply graphics
        FullScreenMode mode = (FullScreenMode)fullscreenModeDropdown.value;
        Screen.SetResolution(currentResolution.width, currentResolution.height, mode, currentResolution.refreshRateRatio);
        
        // Apply quality
        QualitySettings.SetQualityLevel(qualityDropdown.value);
        
        // Apply vsync
        QualitySettings.vSyncCount = vsyncToggle.isOn ? 1 : 0;
        
        // Apply FOV
        if (playerController != null && playerController.GetComponentInChildren<Camera>() != null)
        {
            Camera playerCam = playerController.GetComponentInChildren<Camera>();
            playerCam.fieldOfView = fovSlider.value;
        }
        
        // Apply audio
        SetMasterVolume(masterVolumeSlider.value);
        SetMusicVolume(musicVolumeSlider.value);
        SetSFXVolume(sfxVolumeSlider.value);
        
        // Apply controls to player controller
        if (playerController != null)
        {
            // You'll need to add a public method or variable in PlayerController for sensitivity
            // For example: playerController.SetSensitivity(mouseSensitivitySlider.value);
        }
        
        // Save all settings
        SaveGraphicsSettings();
        SaveAudioSettings();
        SaveControlsSettings();
        SaveGameplaySettings();
        
        Debug.Log("All settings applied and saved");
    }
    
    public void ResetToDefaults()
    {
        // Graphics defaults
        if (resolutionDropdown != null && resolutions.Count > 0)
        {
            resolutionDropdown.value = 0;
            SetResolution(0);
        }
        
        if (qualityDropdown != null)
        {
            qualityDropdown.value = 2; // Medium
            SetQuality(2);
        }
        
        if (fullscreenModeDropdown != null)
        {
            fullscreenModeDropdown.value = 1; // Fullscreen Window
            SetFullscreenMode(1);
        }
        
        if (vsyncToggle != null)
        {
            vsyncToggle.isOn = false;
            SetVSync(false);
        }
        
        if (fovSlider != null)
        {
            fovSlider.value = 90f;
            SetFOV(90f);
        }
        
        // Audio defaults
        if (masterVolumeSlider != null) masterVolumeSlider.value = 1f;
        if (musicVolumeSlider != null) musicVolumeSlider.value = 0.8f;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = 1f;
        
        // Controls defaults
        if (mouseSensitivitySlider != null) 
        {
            mouseSensitivitySlider.value = 0.1f;
            SetMouseSensitivity(0.1f);
        }
        
        if (invertMouseToggle != null)
        {
            invertMouseToggle.isOn = false;
            SetInvertMouse(false);
        }
        
        // Gameplay defaults
        if (showCrosshairToggle != null)
        {
            showCrosshairToggle.isOn = true;
            SetShowCrosshair(true);
        }
        
        if (autoReloadToggle != null)
        {
            autoReloadToggle.isOn = true;
            SetAutoReload(true);
        }
        
        Debug.Log("Settings reset to defaults");
    }
    
    public void BackToMenu()
    {
        // Save before going back
        ApplyAllSettings();
        
        // Return to previous menu
        // This will be handled by your menu navigation script
    }
    #endregion
    
    void OnDestroy()
    {
        // Clean up event listeners
        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.RemoveListener(SetResolution);
        
        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.RemoveListener(SetQuality);
        
        if (fullscreenModeDropdown != null)
            fullscreenModeDropdown.onValueChanged.RemoveListener(SetFullscreenMode);
        
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);
        
        if (vsyncToggle != null)
            vsyncToggle.onValueChanged.RemoveListener(SetVSync);
        
        if (fovSlider != null)
            fovSlider.onValueChanged.RemoveListener(SetFOV);
        
        if (applyButton != null)
            applyButton.onClick.RemoveListener(ApplyAllSettings);
        
        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetToDefaults);
        
        if (backButton != null)
            backButton.onClick.RemoveListener(BackToMenu);
    }
}