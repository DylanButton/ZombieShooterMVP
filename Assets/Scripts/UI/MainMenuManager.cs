using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;
    
    [Header("Buttons")]
    public Button playButton;
    public Button settingsButton;
    public Button creditsButton;
    public Button quitButton;
    public Button backButton;
    
    void Start()
    {
        // Ensure time is running normally
        Time.timeScale = 1f;
        
        // Setup button listeners
        if (playButton) playButton.onClick.AddListener(PlayGame);
        if (settingsButton) settingsButton.onClick.AddListener(OpenSettings);
        if (creditsButton) creditsButton.onClick.AddListener(OpenCredits);
        if (quitButton) quitButton.onClick.AddListener(QuitGame);
        if (backButton) backButton.onClick.AddListener(BackToMainMenu);
        
        // Show main menu, hide others
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (creditsPanel) creditsPanel.SetActive(false);
    }
    
    public void PlayGame()
    {
        // Load your first level scene
        SceneManager.LoadScene("GameScene");
    }
    
    public void OpenSettings()
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);
    }
    
    public void OpenCredits()
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (creditsPanel) creditsPanel.SetActive(true);
    }
    
    public void BackToMainMenu()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
        if (creditsPanel) creditsPanel.SetActive(false);
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
    }
    
    public void QuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}