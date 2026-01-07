using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    [Header("Menu References")]
    public GameObject pauseMenu;
    public GameObject settingsMenu;
    public Image backgroundBlur;
    
    [Header("Input - Link to Existing PlayerInput")]
    public PlayerInput playerInput; // Drag your Player's PlayerInput component here
    [Range(0, 1)] public float blurOpacity = 0.7f;
    
    private bool isInGameScene;
    
    void Start()
    {
        isInGameScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu";
        
        if (pauseMenu) pauseMenu.SetActive(false);
        if (settingsMenu) settingsMenu.SetActive(false);
        
        if (backgroundBlur)
        {
            backgroundBlur.gameObject.SetActive(false);
            Color blurColor = backgroundBlur.color;
            blurColor.a = blurOpacity;
            backgroundBlur.color = blurColor;
        }
        
        // If PlayerInput is not assigned, try to find it
        if (playerInput == null && isInGameScene)
        {
            // Look for PlayerInput on any GameObject
            playerInput = FindObjectOfType<PlayerInput>();
            
            // If still not found, check if there's a "Player" GameObject
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerInput = player.GetComponent<PlayerInput>();
            }
        }
        
        if (playerInput != null)
        {
            Debug.Log($"UIManager connected to PlayerInput on: {playerInput.gameObject.name}");
        }
        else
        {
            Debug.LogWarning("No PlayerInput found. Pause menu won't work with ESC.");
        }
    }
    
    void Update()
    {
        // Backup input check if PlayerInput isn't working
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePauseMenu();
        }
    }
    
    public void TogglePauseMenu()
    {
        if (!isInGameScene) return;
        
        if (settingsMenu.activeSelf)
        {
            CloseSettings();
            return;
        }
        
        if (pauseMenu.activeSelf)
        {
            ResumeGame();
        }
        else
        {
            OpenPauseMenu();
        }
    }
    
    public void OpenPauseMenu()
    {
        GameManager.Instance.PauseGame();
        if (pauseMenu) pauseMenu.SetActive(true);
        if (backgroundBlur) backgroundBlur.gameObject.SetActive(true);
        
        // Disable player input while paused
        if (playerInput != null)
        {
            playerInput.enabled = false;
        }
    }
    
    public void ResumeGame()
    {
        GameManager.Instance.ResumeGame();
        if (pauseMenu) pauseMenu.SetActive(false);
        if (settingsMenu) settingsMenu.SetActive(false);
        if (backgroundBlur) backgroundBlur.gameObject.SetActive(false);
        
        // Re-enable player input
        if (playerInput != null)
        {
            playerInput.enabled = true;
        }
    }
    
    public void OpenSettings()
    {
        if (pauseMenu) pauseMenu.SetActive(false);
        if (settingsMenu) settingsMenu.SetActive(true);
    }
    
    public void CloseSettings()
    {
        if (settingsMenu) settingsMenu.SetActive(false);
        
        if (GameManager.Instance.IsGamePaused && isInGameScene)
        {
            if (pauseMenu) pauseMenu.SetActive(true);
        }
    }
    
    public void BackToMainMenu()
    {
        GameManager.Instance.LoadMainMenu();
    }
    
    public void QuitGame()
    {
        GameManager.Instance.QuitGame();
    }
}