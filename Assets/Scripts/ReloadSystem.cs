using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class ReloadSystem : MonoBehaviour
{
    [Header("=== RELOAD SETTINGS ===")]
    [SerializeField] private float reloadTime = 2f;
    [SerializeField] private int magazineSize = 30;
    [SerializeField] private int maxReserveAmmo = 90;
    [SerializeField] private bool infiniteReserveAmmo = false;
    
    [Header("=== AUTO-RELOAD ===")]
    [SerializeField] private bool autoReloadOnEmpty = true;
    
    [Header("=== INPUT ===")]
    [SerializeField] private InputActionReference reloadAction;
    
    [Header("=== AUDIO ===")]
    [SerializeField] private AudioClip reloadStartSound;
    [SerializeField] private AudioClip reloadFinishSound;
    [SerializeField] private AudioClip emptyMagazineSound;
    
    [Header("=== ANIMATION ===")]
    [SerializeField] private Animator animator;
    [SerializeField] private string reloadAnimationTrigger = "Reload";
    
    [Header("=== EVENTS ===")]
    public UnityEvent onReloadStart;
    public UnityEvent onReloadComplete;
    public UnityEvent onReloadFailed;
    
    // Current ammo state
    private int currentAmmoInMagazine;
    private int currentReserveAmmo;
    private bool isReloading = false;
    private bool isMagazineEmpty = false;
    
    // Components
    private AudioSource audioSource;
    private WeaponManager weaponManager;
    private Gun gunScript;
    private SimpleGun simpleGunScript;
    private int weaponSlotIndex = -1;
    
    // Properties
    public int CurrentAmmoInMagazine => currentAmmoInMagazine;
    public int CurrentReserveAmmo => currentReserveAmmo;
    public bool IsReloading => isReloading;
    public bool IsMagazineEmpty => isMagazineEmpty;
    public float ReloadProgress { get; private set; }
    
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        weaponManager = GetComponentInParent<WeaponManager>();
        
        // Get gun scripts
        gunScript = GetComponent<Gun>();
        simpleGunScript = GetComponent<SimpleGun>();
        
        // Initialize ammo
        currentAmmoInMagazine = magazineSize;
        currentReserveAmmo = maxReserveAmmo;
        
        // Setup input
        if (reloadAction != null)
        {
            reloadAction.action.Enable();
            reloadAction.action.performed += OnReloadInput;
        }
    }
    
    private void Start()
    {
        // Find weapon slot index
        FindWeaponSlotIndex();
        
        // Update ammo display
        UpdateAmmoDisplay();
    }
    
    private void Update()
    {
        // Update reload progress
        if (isReloading)
        {
            ReloadProgress += Time.deltaTime / reloadTime;
        }
        
        // Check for empty magazine
        if (currentAmmoInMagazine == 0 && !isMagazineEmpty)
        {
            isMagazineEmpty = true;
            if (emptyMagazineSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(emptyMagazineSound);
            }
            
            // Auto-reload when magazine is empty (if enabled)
            if (autoReloadOnEmpty && currentReserveAmmo > 0 && !isReloading)
            {
                StartReload();
            }
        }
        else if (currentAmmoInMagazine > 0 && isMagazineEmpty)
        {
            isMagazineEmpty = false;
        }
    }
    
    private void FindWeaponSlotIndex()
    {
        if (weaponManager == null) return;
        
        // Find which weapon slot this gun belongs to
        for (int i = 0; i < weaponManager.GetWeaponCount(); i++)
        {
            var weapon = weaponManager.GetCurrentWeapon();
            if (weapon != null && weapon.spawnedWeapon == gameObject)
            {
                weaponSlotIndex = i;
                break;
            }
        }
    }
    
    private void OnReloadInput(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            StartReload();
        }
    }
    
    public bool CanShoot()
    {
        // Can't shoot while reloading
        if (isReloading) return false;
        
        // Check if we have ammo
        return currentAmmoInMagazine > 0 || HasInfiniteAmmo();
    }
    
    public bool HasInfiniteAmmo()
    {
        return infiniteReserveAmmo;
    }
    
    public void StartReload()
    {
        // Can't reload if already reloading
        if (isReloading) return;
        
        // Don't reload if magazine is already full
        if (currentAmmoInMagazine == magazineSize)
        {
            onReloadFailed?.Invoke();
            return;
        }
        
        // Check if we have reserve ammo
        if (currentReserveAmmo <= 0 && !infiniteReserveAmmo)
        {
            onReloadFailed?.Invoke();
            Debug.Log("No reserve ammo!");
            return;
        }
        
        // Start reload process
        StartCoroutine(ReloadCoroutine());
    }
    
    private IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        ReloadProgress = 0f;
        
        // Notify start
        onReloadStart?.Invoke();
        
        // Play start sound
        if (reloadStartSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(reloadStartSound);
        }
        
        // Play animation
        if (animator != null)
        {
            animator.SetTrigger(reloadAnimationTrigger);
        }
        
        // Don't disable the Gun script - it will check CanShoot() which returns false during reload
        
        // Wait for reload time
        yield return new WaitForSeconds(reloadTime);
        
        // Calculate ammo to reload
        int ammoNeeded = magazineSize - currentAmmoInMagazine;
        int ammoToAdd;
        
        if (infiniteReserveAmmo)
        {
            // Infinite ammo - fill magazine completely
            ammoToAdd = ammoNeeded;
            currentAmmoInMagazine = magazineSize;
        }
        else
        {
            // Limited ammo
            ammoToAdd = Mathf.Min(ammoNeeded, currentReserveAmmo);
            currentAmmoInMagazine += ammoToAdd;
            currentReserveAmmo -= ammoToAdd;
        }
        
        // Update WeaponManager ammo count
        if (weaponManager != null && weaponSlotIndex != -1)
        {
            weaponManager.UpdateWeaponAmmo(weaponSlotIndex, currentAmmoInMagazine);
        }
        
        // Play finish sound
        if (reloadFinishSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(reloadFinishSound);
        }
        
        // Don't need to re-enable shooting scripts - they were never disabled
        
        // Notify completion
        onReloadComplete?.Invoke();
        
        // Update display
        UpdateAmmoDisplay();
        
        isReloading = false;
        ReloadProgress = 1f;
        
        Debug.Log($"Reload complete! Ammo: {currentAmmoInMagazine}/{magazineSize}, Reserve: {currentReserveAmmo}");
    }
    
    public bool ConsumeAmmo(int amount = 1)
    {
        // Check if we can consume ammo
        if (currentAmmoInMagazine < amount)
        {
            // Not enough ammo - auto reload if enabled
            if (autoReloadOnEmpty && (currentReserveAmmo > 0 || infiniteReserveAmmo) && !isReloading)
            {
                StartReload();
            }
            return false;
        }
        
        currentAmmoInMagazine -= amount;
        isMagazineEmpty = currentAmmoInMagazine == 0;
        
        // Update WeaponManager
        if (weaponManager != null && weaponSlotIndex != -1)
        {
            weaponManager.UpdateWeaponAmmo(weaponSlotIndex, currentAmmoInMagazine);
        }
        
        UpdateAmmoDisplay();
        return true;
    }
    
    public void AddReserveAmmo(int amount)
    {
        currentReserveAmmo = Mathf.Min(currentReserveAmmo + amount, maxReserveAmmo);
        UpdateAmmoDisplay();
        
        Debug.Log($"Added {amount} ammo. Total reserve: {currentReserveAmmo}");
    }
    
    public void SetInfiniteAmmo(bool infinite)
    {
        infiniteReserveAmmo = infinite;
    }
    
    public void SetAutoReload(bool autoReload)
    {
        autoReloadOnEmpty = autoReload;
    }
    
    public bool GetAutoReload()
    {
        return autoReloadOnEmpty;
    }
    
    public void SetAmmo(int magazine, int reserve)
    {
        currentAmmoInMagazine = Mathf.Clamp(magazine, 0, magazineSize);
        currentReserveAmmo = Mathf.Clamp(reserve, 0, maxReserveAmmo);
        UpdateAmmoDisplay();
    }
    
    private void UpdateAmmoDisplay()
    {
        // This can be called by UI components
        // You can hook this up to a UI script
        
        // Debug display
        if (weaponManager != null)
        {
            // WeaponManager has its own debug GUI
        }
    }
    
    // Called when weapon is equipped
    public void OnWeaponEquipped()
    {
        // Reset reload state
        isReloading = false;
        
        // Update display
        UpdateAmmoDisplay();
    }
    
    // Called when weapon is unequipped
    public void OnWeaponUnequipped()
    {
        // Stop any reload in progress
        StopAllCoroutines();
        isReloading = false;
    }
    
    // Quick method to check ammo status
    public string GetAmmoStatus()
    {
        if (infiniteReserveAmmo)
        {
            return $"{currentAmmoInMagazine}/{magazineSize} (∞)";
        }
        return $"{currentAmmoInMagazine}/{magazineSize} | {currentReserveAmmo}";
    }
    
    private void OnGUI()
    {
        // Debug GUI
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 12;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.UpperLeft;
        
        string info = $"Ammo: {currentAmmoInMagazine}/{magazineSize}\n";
        info += $"Reserve: {currentReserveAmmo}\n";
        
        if (autoReloadOnEmpty)
        {
            info += "Auto-Reload: ON\n";
        }
        else
        {
            info += "Auto-Reload: OFF\n";
        }
        
        if (isReloading)
        {
            info += $"Reloading: {(ReloadProgress * 100):F0}%\n";
        }
        
        if (isMagazineEmpty)
        {
            info += "EMPTY\n";
        }
        
        GUI.Box(new Rect(10, 450, 150, 100), info, style);
    }
    
    private void OnDestroy()
    {
        // Clean up input
        if (reloadAction != null)
            reloadAction.action.performed -= OnReloadInput;
    }
}