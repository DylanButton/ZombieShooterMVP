using UnityEngine;
public class FrameRateLimiter : MonoBehaviour
{
    void Start()
    {
        // Limits the game to 60 FPS. Change to your target.
        Application.targetFrameRate = 120;
    }
}