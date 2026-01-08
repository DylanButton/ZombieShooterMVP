using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections;

public class Gun : MonoBehaviour
{
    [Header("Weapon Manager Reference")]
    [SerializeField] private WeaponManager weaponManager;
    
    [Header("Reload System")]
    private ReloadSystem reloadSystem;
    
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
    [SerializeField] private AudioClip emptyClickSound;
    
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
    [SerializeField] private bool showDebugLine = true;
    
    // Cached references
    private Camera playerCamera;
    private CharacterController characterController;
    
    // State
    private Coroutine currentShotRoutine;
    private bool isInShootingSequence = false;
    private Vector2 mouseLook;
    
    // Input
    private bool shootPressed = false;
    private bool shootTriggered = false;
    
    // Track if we're enabled (for reload handling)
    private bool isEnabled = true;
    
    // Dry fire tracking
    private float lastDryFireTime;
    private float dryFireCooldown = 0.3f; // Prevent spam

    void Start()
    {
        currentCooldown = fireCooldown;
        playerCamera = Camera.main;
        
        if (gunAudioSource == null)
            gunAudioSource = GetComponent<AudioSource>();
        
        initialSwayPosition = transform.localPosition;
        initialSwayRotation = transform.localRotation;
        characterController = GetComponentInParent<CharacterController>();
        
        // Get ReloadSystem
        reloadSystem = GetComponent<ReloadSystem>();
        
        // Get weapon slot index from parent
        if (weaponManager == null)
        {
            weaponManager = GetComponentInParent<WeaponManager>();
            if (weaponManager == null)
            {
                weaponManager = FindAnyObjectByType<WeaponManager>();
            }
        }
        
        // Setup input actions
        if (shootAction != null)
        {
            shootAction.action.Enable();
        }
        
        Debug.Log($"Gun initialized. Player Camera: {playerCamera != null}, ReloadSystem: {reloadSystem != null}");
    }

    void Update()
    {
        if (!isEnabled) return;
        
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
                TryShoot();
                shootTriggered = false; // Reset after processing
            }
        }
        else
        {
            // Automatic fire - while button is held
            if (shootPressed && currentCooldown <= 0)
            {
                TryShoot();
            }
        }
    }
    
    bool CanShoot()
    {
        if (!isEnabled) return false;
        
        // Check cooldown
        if (currentCooldown > 0) return false;
        
        // Check if in shooting sequence
        if (isInShootingSequence) return false;
        
        // Check ReloadSystem if available
        if (reloadSystem != null)
        {
            return reloadSystem.CanShoot();
        }
        
        // Fallback - always can shoot if no reload system
        return true;
    }
    
    bool IsGunEmpty()
    {
        // Check if gun is completely empty (no ammo in magazine or reserve)
        if (reloadSystem != null)
        {
            // Check if magazine is empty and there's no reserve ammo
            bool magazineEmpty = reloadSystem.CurrentAmmoInMagazine <= 0;
            bool noReserveAmmo = reloadSystem.CurrentReserveAmmo <= 0 && !reloadSystem.HasInfiniteAmmo();
            bool notReloading = !reloadSystem.IsReloading;
            
            return magazineEmpty && noReserveAmmo && notReloading;
        }
        
        return false; // If no reload system, gun is never "empty"
    }
    
    void PlayDryFireSound()
    {
        // Prevent spamming dry fire sound
        if (Time.time - lastDryFireTime < dryFireCooldown) return;
        
        lastDryFireTime = Time.time;
        
        if (emptyClickSound != null && gunAudioSource != null)
        {
            gunAudioSource.PlayOneShot(emptyClickSound);
            Debug.Log("Dry fire - gun is empty!");
        }
    }
    
    void TryShoot()
    {
        if (!isEnabled || Time.timeScale == 0) return;
        
        // Check if gun is completely empty (no ammo to shoot or reload)
        if (IsGunEmpty())
        {
            PlayDryFireSound();
            return;
        }
        
        // Check if we can shoot (cooldown, not reloading, etc.)
        if (!CanShoot()) return;
            
        // Check ammo via ReloadSystem
        if (reloadSystem != null)
        {
            if (!reloadSystem.ConsumeAmmo(1))
            {
                // Out of ammo but might have reserve - play empty click
                PlayDryFireSound();
                return;
            }
        }
            
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
    
    void HandleInput()
    {
        // Get look input (mouse delta) - always read even when disabled
        if (lookAction != null)
        {
            mouseLook = lookAction.action.ReadValue<Vector2>();
        }
        
        // Get shoot input - only process if enabled
        if (shootAction != null && isEnabled)
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
        if (!isEnabled) return;
        
        float swayX = -mouseLook.x * swayAmount;
        float swayY = -mouseLook.y * swayAmount;
        
        Vector3 targetPosition = initialSwayPosition + new Vector3(swayX, swayY, 0);
        Quaternion targetRotation = initialSwayRotation * 
            Quaternion.Euler(-swayY * 2, swayX * 2, swayX * 3);
        
        float smooth = deltaTime * swaySmoothness;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, smooth);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, smooth);
    }
    
    public void FireBullet()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning("Player camera is null!");
            playerCamera = Camera.main;
            if (playerCamera == null)
                return;
        }
        
        // Shoot from camera center
        Ray gunRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        // Show debug line if enabled
        if (showDebugLine)
        {
            Debug.DrawRay(gunRay.origin, gunRay.direction * bulletRange, Color.red, 2f);
        }
        
        // Use simple Raycast
        RaycastHit hit;
        if (Physics.Raycast(gunRay, out hit, bulletRange, targetLayers))
        {
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
                zombieHealth = hit.collider.GetComponentInParent<ZombieHealth>();
            }
            
            if (zombieHealth != null)
            {
                zombieHealth.TakeDamage(damage);
            }
            else
            {
                // Check for PlayerHealth (friendly fire)
                PlayerHealth playerHealth = hit.collider.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage);
                }
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
        isEnabled = true;
        
        // Enable shoot action when script is enabled
        if (shootAction != null)
            shootAction.action.Enable();
    }
    
    void OnDisable()
    {
        isEnabled = false;
        
        // Only disable shoot action, NOT look action
        if (shootAction != null)
            shootAction.action.Disable();
        
        // Stop any running coroutines
        if (currentShotRoutine != null)
            StopCoroutine(currentShotRoutine);
        
        isInShootingSequence = false;
    }
}