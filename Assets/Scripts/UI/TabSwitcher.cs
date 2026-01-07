using UnityEngine;
using UnityEngine.UI;

public class TabSwitcher : MonoBehaviour
{
    [Header("Tab Buttons")]
    public Button audioTabButton;
    public Button graphicsTabButton;
    public Button controlsTabButton;
    
    [Header("Tab Panels")]
    public GameObject audioTabPanel;
    public GameObject graphicsTabPanel;
    public GameObject controlsTabPanel;
    
    [Header("Colors")]
    public Color activeButtonColor = Color.white;
    public Color inactiveButtonColor = Color.gray;
    
    void Start()
    {
        // Set up button click events
        if (audioTabButton != null)
            audioTabButton.onClick.AddListener(() => SwitchTab(TabType.Audio));
            
        if (graphicsTabButton != null)
            graphicsTabButton.onClick.AddListener(() => SwitchTab(TabType.Graphics));
            
        if (controlsTabButton != null)
            controlsTabButton.onClick.AddListener(() => SwitchTab(TabType.Controls));
        
        // Show first tab by default
        SwitchTab(TabType.Audio);
    }
    
    public void SwitchTab(TabType tab)
    {
        // Hide all panels
        if (audioTabPanel != null) audioTabPanel.SetActive(false);
        if (graphicsTabPanel != null) graphicsTabPanel.SetActive(false);
        if (controlsTabPanel != null) controlsTabPanel.SetActive(false);
        
        // Show selected panel
        switch (tab)
        {
            case TabType.Audio:
                if (audioTabPanel != null) audioTabPanel.SetActive(true);
                UpdateButtonColors(audioTabButton, graphicsTabButton, controlsTabButton);
                break;
                
            case TabType.Graphics:
                if (graphicsTabPanel != null) graphicsTabPanel.SetActive(true);
                UpdateButtonColors(graphicsTabButton, audioTabButton, controlsTabButton);
                break;
                
            case TabType.Controls:
                if (controlsTabPanel != null) controlsTabPanel.SetActive(true);
                UpdateButtonColors(controlsTabButton, audioTabButton, graphicsTabButton);
                break;
        }
    }
    
    void UpdateButtonColors(Button activeButton, Button inactiveButton1, Button inactiveButton2)
    {
        // Update active button color
        if (activeButton != null)
        {
            ColorBlock activeColors = activeButton.colors;
            activeColors.normalColor = activeButtonColor;
            activeButton.colors = activeColors;
        }
        
        // Update inactive buttons colors
        if (inactiveButton1 != null)
        {
            ColorBlock inactiveColors1 = inactiveButton1.colors;
            inactiveColors1.normalColor = inactiveButtonColor;
            inactiveButton1.colors = inactiveColors1;
        }
        
        if (inactiveButton2 != null)
        {
            ColorBlock inactiveColors2 = inactiveButton2.colors;
            inactiveColors2.normalColor = inactiveButtonColor;
            inactiveButton2.colors = inactiveColors2;
        }
    }
    
    public enum TabType
    {
        Audio,
        Graphics,
        Controls
    }
}