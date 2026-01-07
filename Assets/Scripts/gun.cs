using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections;

public class Gun : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference shootAction;
    [SerializeField] private InputActionReference lookAction;
    
    [Header("Shooting Settings")]
    public UnityEvent onGunShoot;
    public float fireCooldown = 0.5f;
    public bool singleFire = true;
    public float currentCooldown;
    
    [Header("Audio")]
    [SerializeField] private AudioSource gunAudioSource;
    [SerializeField] private AudioClip hammerCockSound;
    [SerializeField] private AudioClip shootSound;
    
    [Header("Timing")]
    [SerializeField] private float shootSoundDelay = 0.2f;
    [SerializeField] private float damageDelay = 0.2f;
    
    [Header("Visual")]
    public Animator anim1;
    [SerializeField] private ParticleSystem muzzleFlash;
    
    [Header("Damage")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float bulletRange = 100f;
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private GameObject hitEffectPrefab;
    
    [Header("Camera Effects")]
    [SerializeField] private float cameraShakeDuration = 0.1f;
    [SerializeField] private float cameraShakeIntensity = 0.05f;
    
    [Header("Weapon Sway")]
    [SerializeField] private bool useWeaponSway = true;
    [SerializeField] private float swayAmount = 0.02f;
    [SerializeField] private float swaySmoothness = 6f;
    private Vector3 initialSwayPosition;
    private Quaternion initialSwayRotation;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLine = true; // NEW: Toggle for debug line
    
    // Cached references
    private Camera playerCamera;
    private CharacterController characterController;
    private RaycastHit[] raycastHits = new RaycastHit[1];
    
    // State
    private Coroutine currentShotRoutine;
    private bool isInShootingSequence = false;
    private Vector2 mouseLook;
    
    // Input
    private bool shootPressed = false;
    private bool shootTriggered = false;

    void Start()
    {
        currentCooldown = fireCooldown;
        playerCamera = Camera.main;
        
        if (gunAudioSource == null)
            gunAudioSource = GetComponent<AudioSource>();
        
        initialSwayPosition = transform.localPosition;
        initialSwayRotation = transform.localRotation;
        characterController = GetComponentInParent<CharacterController>();
        
        // Setup input actions
        if (shootAction != null)
        {
            shootAction.action.Enable();
        }
        
        if (lookAction != null)
        {
            lookAction.action.Enable();
        }
        
        Debug.Log($"Gun initialized. Player Camera: {playerCamera != null}, Target Layers: {targetLayers.value}");
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;
        
        if (currentCooldown > 0)
            currentCooldown -= deltaTime;
        
        // Handle input
        HandleInput();
        
        // Update weapon sway
        if (useWeaponSway)
            ApplyWeaponSway(deltaTime);
        
        // Handle shooting based on input mode
        if (singleFire)
        {
            // Single fire - trigger on button press
            if (shootTriggered)
            {
                StartShootingProcess();
                shootTriggered = false; // Reset after processing
            }
        }
        else
        {
            // Automatic fire - while button is held
            if (shootPressed && currentCooldown <= 0)
            {
                StartShootingProcess();
            }
        }
    }
    
    void HandleInput()
    {
        // Get look input (mouse delta)
        if (lookAction != null)
        {
            mouseLook = lookAction.action.ReadValue<Vector2>();
        }
        
        // Get shoot input
        if (shootAction != null)
        {
            // Check for button press (for single fire)
            if (shootAction.action.triggered && !shootTriggered)
            {
                shootTriggered = true;
            }
            
            // Check for button hold (for automatic fire)
            shootPressed = shootAction.action.IsPressed();
        }
    }
    
    void ApplyWeaponSway(float deltaTime)
    {
        float swayX = -mouseLook.x * swayAmount;
        float swayY = -mouseLook.y * swayAmount;
        
        Vector3 targetPosition = initialSwayPosition + new Vector3(swayX, swayY, 0);
        Quaternion targetRotation = initialSwayRotation * 
            Quaternion.Euler(-swayY * 2, swayX * 2, swayX * 3);
        
        float smooth = deltaTime * swaySmoothness;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, smooth);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, smooth);
    }
    
    void StartShootingProcess()
    {
        if (isInShootingSequence || currentCooldown > 0 || Time.timeScale == 0)
            return;
            
        currentCooldown = fireCooldown;
        isInShootingSequence = true;
        
        if (currentShotRoutine != null)
            StopCoroutine(currentShotRoutine);
        
        currentShotRoutine = StartCoroutine(ShootingSequence());
    }
    
    IEnumerator ShootingSequence()
    {
        // Hammer cock
        if (hammerCockSound != null && gunAudioSource != null)
            gunAudioSource.PlayOneShot(hammerCockSound);
        
        if (anim1 != null)
            anim1.SetTrigger("Shoot");
        
        // Wait for shoot sound
        yield return new WaitForSeconds(shootSoundDelay);
        
        // Shoot sound
        if (shootSound != null && gunAudioSource != null)
            gunAudioSource.PlayOneShot(shootSound);
        
        if (muzzleFlash != null)
            muzzleFlash.Play();
        
        // Wait for damage delay if needed
        if (damageDelay > shootSoundDelay)
            yield return new WaitForSeconds(damageDelay - shootSoundDelay);
        
        // Apply damage
        FireBullet();
        
        // Camera shake
        if (cameraShakeDuration > 0 && playerCamera != null)
            StartCoroutine(ShakeCamera());
        
        onGunShoot?.Invoke();
        
        isInShootingSequence = false;
    }
    
    public void FireBullet()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning("Player camera is null!");
            playerCamera = Camera.main; // Try to get it again
            if (playerCamera == null)
                return;
        }
        
        // Shoot from camera center (simplest FPS hit-scan)
        Ray gunRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        // Show debug line if enabled
        if (showDebugLine)
        {
            Debug.DrawRay(gunRay.origin, gunRay.direction * bulletRange, Color.red, 2f);
            Debug.Log($"Shooting from: {gunRay.origin}, Direction: {gunRay.direction}");
        }
        
        // Use simple Raycast (easier to debug)
        RaycastHit hit;
        if (Physics.Raycast(gunRay, out hit, bulletRange, targetLayers))
        {
            GameObject hitObject = hit.collider.gameObject;
            Debug.Log($"Hit: {hitObject.name} at distance: {hit.distance:F2}m");
            
            // Hit effect
            if (hitEffectPrefab != null)
            {
                GameObject effect = Instantiate(hitEffectPrefab, hit.point, 
                    Quaternion.LookRotation(hit.normal));
                Destroy(effect, 2f);
            }
            
            // Check for ZombieHealth component
            ZombieHealth zombieHealth = hit.collider.GetComponent<ZombieHealth>();
            if (zombieHealth == null)
            {
                // Try parent
                zombieHealth = hit.collider.GetComponentInParent<ZombieHealth>();
            }
            
            if (zombieHealth != null)
            {
                zombieHealth.TakeDamage(damage);
                Debug.Log($"SUCCESS: Hit zombie '{hitObject.name}' for {damage} damage!");
            }
            else
            {
                Debug.Log($"Hit object '{hitObject.name}' but no ZombieHealth component found");
                
                // Check for PlayerHealth (friendly fire)
                PlayerHealth playerHealth = hit.collider.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage);
                    Debug.Log($"Hit player for {damage} damage!");
                }
            }
        }
        else
        {
            Debug.Log("No target hit - raycast didn't hit anything");
            
            // Draw debug line in scene view to see where it went
            if (showDebugLine)
            {
                Debug.DrawLine(gunRay.origin, gunRay.origin + gunRay.direction * bulletRange, Color.yellow, 2f);
            }
        }
    }
    
    IEnumerator ShakeCamera()
    {
        float elapsedTime = 0f;
        Vector3 originalPos = playerCamera.transform.localPosition;
        
        while (elapsedTime < cameraShakeDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / cameraShakeDuration;
            float strength = cameraShakeIntensity * (1f - progress);
            
            playerCamera.transform.localPosition = originalPos + 
                Random.insideUnitSphere * strength;
            
            yield return null;
        }
        
        playerCamera.transform.localPosition = originalPos;
    }
    
    void OnEnable()
    {
        // Enable input actions when script is enabled
        if (shootAction != null)
            shootAction.action.Enable();
        
        if (lookAction != null)
            lookAction.action.Enable();
    }
    
    void OnDisable()
    {
        // Disable input actions when script is disabled
        if (shootAction != null)
            shootAction.action.Disable();
        
        if (lookAction != null)
            lookAction.action.Disable();
        
        // Stop any running coroutines
        if (currentShotRoutine != null)
            StopCoroutine(currentShotRoutine);
        
        isInShootingSequence = false;
    }
}