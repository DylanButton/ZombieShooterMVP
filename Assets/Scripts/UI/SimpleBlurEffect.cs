using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SimpleBlurEffect : MonoBehaviour
{
    [Header("Blur Settings")]
    [Range(0, 10)] public float blurAmount = 5f;
    [Range(0, 1)] public float blurSpeed = 0.5f;
    public Color blurColor = new Color(0, 0, 0, 0.7f);
    
    private Image blurImage;
    private Material blurMaterial;
    private float currentBlur;
    private bool isBlurring;
    
    void Start()
    {
        blurImage = GetComponent<Image>();
        
        // Create a simple blur material (Unity's default UI material with some modifications)
        blurMaterial = new Material(Shader.Find("UI/Default"));
        blurImage.material = blurMaterial;
        
        blurImage.color = new Color(blurColor.r, blurColor.g, blurColor.b, 0);
        gameObject.SetActive(false);
    }
    
    void Update()
    {
        if (isBlurring)
        {
            currentBlur = Mathf.Lerp(currentBlur, blurAmount, blurSpeed * Time.unscaledDeltaTime * 10);
        }
        else
        {
            currentBlur = Mathf.Lerp(currentBlur, 0, blurSpeed * Time.unscaledDeltaTime * 10);
        }
        
        // Simple alpha-based blur effect
        Color currentColor = blurImage.color;
        currentColor.a = Mathf.Clamp01(currentBlur / 10f) * blurColor.a;
        blurImage.color = currentColor;
        
        // You could add a more sophisticated blur shader here
    }
    
    public void EnableBlur()
    {
        gameObject.SetActive(true);
        isBlurring = true;
    }
    
    public void DisableBlur()
    {
        isBlurring = false;
        
        // Wait for fade out then disable
        Invoke("DisableObject", 0.5f);
    }
    
    void DisableObject()
    {
        if (!isBlurring)
            gameObject.SetActive(false);
    }
}