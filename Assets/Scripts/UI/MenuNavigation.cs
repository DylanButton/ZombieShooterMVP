using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuNavigation : MonoBehaviour
{
    [Header("Navigation")]
    public Selectable firstSelected;
    public bool useControllerNavigation = true;
    
    [Header("Audio")]
    public AudioSource navigationSound;
    public AudioSource confirmSound;
    
    private EventSystem eventSystem;
    
    void Start()
    {
        eventSystem = EventSystem.current;
        
        if (firstSelected != null)
        {
            firstSelected.Select();
        }
    }
    
    void Update()
    {
        if (useControllerNavigation && eventSystem.currentSelectedGameObject == null)
        {
            if (Input.GetAxis("Vertical") != 0 || Input.GetAxis("Horizontal") != 0)
            {
                if (firstSelected != null)
                {
                    firstSelected.Select();
                    PlayNavigationSound();
                }
            }
        }
    }
    
    public void PlayNavigationSound()
    {
        if (navigationSound != null)
            navigationSound.Play();
    }
    
    public void PlayConfirmSound()
    {
        if (confirmSound != null)
            confirmSound.Play();
    }
    
    // Call this when a button is highlighted via mouse
    public void OnButtonHighlighted(Selectable button)
    {
        if (useControllerNavigation)
        {
            button.Select();
            PlayNavigationSound();
        }
    }
}