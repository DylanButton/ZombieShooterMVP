using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class WeaponManager : MonoBehaviour
{
    [Header("=== WEAPON SLOTS ===")]
    [SerializeField] private List<WeaponSlot> weaponSlots = new List<WeaponSlot>();
    
    [Header("=== SWITCHING ===")]
    [SerializeField] private float switchCooldown = 0.3f;
    [SerializeField] private bool cycleWeapons = true;
    
    [Header("=== INPUT ===")]
    [SerializeField] private InputActionReference switchWeaponAction;
    [SerializeField] private InputActionReference weapon1Action;
    [SerializeField] private InputActionReference weapon2Action;
    [SerializeField] private InputActionReference weapon3Action;
    [SerializeField] private InputActionReference dropWeaponAction;
    
    [Header("=== VISUALS ===")]
    [SerializeField] private Transform weaponParent; // Where weapons are attached
    [SerializeField] private AudioSource switchAudio;
    [SerializeField] private AudioClip switchSound;
    
    // Current state
    private int currentWeaponIndex = -1;
    private GameObject currentWeaponObject;
    private float lastSwitchTime;
    private bool canSwitch = true;
    
    [System.Serializable]
    public class WeaponSlot
    {
        public string weaponName;
        public GameObject weaponPrefab; // Complete arm+gun prefab
        public bool isUnlocked = true;
        public bool hasAmmo = true;
        public int ammoCount = 30;
        public int maxAmmo = 30;
        
        // Runtime reference
        [HideInInspector] public GameObject spawnedWeapon;
        [HideInInspector] public Gun gunScript; // Reference to Gun component if exists
        [HideInInspector] public SimpleGun simpleGunScript; // Reference to SimpleGun if exists
        [HideInInspector] public ReloadSystem reloadSystem; // Reference to ReloadSystem if exists
    }
    
    private void Awake()
    {
        // Create weapon parent if not assigned
        if (weaponParent == null)
        {
            GameObject parent = new GameObject("WeaponParent");
            parent.transform.SetParent(transform);
            parent.transform.localPosition = Vector3.zero;
            parent.transform.localRotation = Quaternion.identity;
            weaponParent = parent.transform;
        }
        
        // Initialize input actions
        InitializeInput();
    }
    
    private void Start()
    {
        // Spawn all weapons initially (disabled)
        InitializeWeapons();
        
        // Equip first available weapon
        if (weaponSlots.Count > 0)
        {
            EquipWeapon(0);
        }
        
        lastSwitchTime = -switchCooldown; // Allow immediate first switch
    }
    
    private void Update()
    {
        // Update switch cooldown
        if (!canSwitch && Time.time >= lastSwitchTime + switchCooldown)
        {
            canSwitch = true;
        }
        
        // Handle weapon cycling
        HandleWeaponInput();
    }
    
    private void InitializeInput()
    {
        if (switchWeaponAction != null)
        {
            switchWeaponAction.action.Enable();
            switchWeaponAction.action.performed += OnSwitchWeapon;
        }
        
        if (weapon1Action != null)
        {
            weapon1Action.action.Enable();
            weapon1Action.action.performed += ctx => QuickSwitch(0);
        }
        
        if (weapon2Action != null)
        {
            weapon2Action.action.Enable();
            weapon2Action.action.performed += ctx => QuickSwitch(1);
        }
        
        if (weapon3Action != null)
        {
            weapon3Action.action.Enable();
            weapon3Action.action.performed += ctx => QuickSwitch(2);
        }
        
        if (dropWeaponAction != null)
        {
            dropWeaponAction.action.Enable();
            dropWeaponAction.action.performed += OnDropWeapon;
        }
    }
    
    private void InitializeWeapons()
    {
        for (int i = 0; i < weaponSlots.Count; i++)
        {
            if (weaponSlots[i].weaponPrefab != null)
            {
                // Instantiate weapon
                GameObject weaponObj = Instantiate(
                    weaponSlots[i].weaponPrefab, 
                    weaponParent.position, 
                    weaponParent.rotation, 
                    weaponParent
                );
                
                // Store reference
                weaponSlots[i].spawnedWeapon = weaponObj;
                
                // Get weapon scripts
                weaponSlots[i].gunScript = weaponObj.GetComponent<Gun>();
                weaponSlots[i].simpleGunScript = weaponObj.GetComponent<SimpleGun>();
                weaponSlots[i].reloadSystem = weaponObj.GetComponent<ReloadSystem>();
                
                // Initially disable
                weaponObj.SetActive(false);
                
                // Disable all weapon scripts initially
                if (weaponSlots[i].gunScript != null)
                    weaponSlots[i].gunScript.enabled = false;
                if (weaponSlots[i].simpleGunScript != null)
                    weaponSlots[i].simpleGunScript.enabled = false;
                if (weaponSlots[i].reloadSystem != null)
                    weaponSlots[i].reloadSystem.enabled = false;
                
                Debug.Log($"Initialized weapon: {weaponSlots[i].weaponName}");
            }
            else
            {
                Debug.LogWarning($"Weapon slot {i} has no prefab assigned!");
            }
        }
    }
    
    private void HandleWeaponInput()
    {
        // Mouse wheel switching
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0 && canSwitch && cycleWeapons)
        {
            if (scroll > 0)
            {
                SwitchToNextWeapon();
            }
            else if (scroll < 0)
            {
                SwitchToPreviousWeapon();
            }
        }
        
        // Number key switching (1, 2, 3, etc.)
        for (int i = 0; i < Mathf.Min(9, weaponSlots.Count); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) && canSwitch)
            {
                QuickSwitch(i);
            }
        }
    }
    
    private void OnSwitchWeapon(InputAction.CallbackContext context)
    {
        if (!canSwitch || weaponSlots.Count <= 1) return;
        
        // Cycle to next weapon
        SwitchToNextWeapon();
    }
    
    private void OnDropWeapon(InputAction.CallbackContext context)
    {
        if (currentWeaponIndex == -1) return;
        
        // Drop current weapon (just unequip for now, could spawn pickup)
        UnequipCurrentWeapon();
        
        // Try to equip next available weapon
        if (weaponSlots.Count > 0)
        {
            int nextIndex = (currentWeaponIndex + 1) % weaponSlots.Count;
            if (nextIndex == currentWeaponIndex) // Only one weapon
            {
                currentWeaponIndex = -1;
                currentWeaponObject = null;
            }
            else
            {
                EquipWeapon(nextIndex);
            }
        }
    }
    
    public void EquipWeapon(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Count)
        {
            Debug.LogWarning($"Invalid weapon slot index: {slotIndex}");
            return;
        }
        
        if (!weaponSlots[slotIndex].isUnlocked)
        {
            Debug.Log($"Weapon {weaponSlots[slotIndex].weaponName} is locked!");
            return;
        }
        
        if (!CanUseWeapon(slotIndex))
        {
            Debug.Log($"Weapon {weaponSlots[slotIndex].weaponName} cannot be used!");
            return;
        }
        
        if (!canSwitch) return;
        
        // Unequip current weapon first
        UnequipCurrentWeapon();
        
        // Enable new weapon
        weaponSlots[slotIndex].spawnedWeapon.SetActive(true);
        currentWeaponObject = weaponSlots[slotIndex].spawnedWeapon;
        currentWeaponIndex = slotIndex;
        
        // Enable weapon scripts
        if (weaponSlots[slotIndex].gunScript != null)
        {
            weaponSlots[slotIndex].gunScript.enabled = true;
        }
        if (weaponSlots[slotIndex].simpleGunScript != null)
        {
            weaponSlots[slotIndex].simpleGunScript.enabled = true;
        }
        
        // Enable and initialize ReloadSystem if present
        if (weaponSlots[slotIndex].reloadSystem != null)
        {
            weaponSlots[slotIndex].reloadSystem.enabled = true;
            weaponSlots[slotIndex].reloadSystem.OnWeaponEquipped();
            
            // Update ammo count from reload system
            weaponSlots[slotIndex].ammoCount = weaponSlots[slotIndex].reloadSystem.CurrentAmmoInMagazine;
            weaponSlots[slotIndex].hasAmmo = weaponSlots[slotIndex].ammoCount > 0;
        }
        
        // Play switch sound
        if (switchAudio != null && switchSound != null)
        {
            switchAudio.PlayOneShot(switchSound);
        }
        
        // Set cooldown
        lastSwitchTime = Time.time;
        canSwitch = false;
        
        Debug.Log($"Equipped: {weaponSlots[slotIndex].weaponName}");
        
        // Notify other systems
        OnWeaponSwitched?.Invoke(slotIndex);
    }
    
    private void UnequipCurrentWeapon()
    {
        if (currentWeaponIndex == -1 || currentWeaponObject == null) return;
        
        // Disable ReloadSystem if present
        if (weaponSlots[currentWeaponIndex].reloadSystem != null)
        {
            weaponSlots[currentWeaponIndex].reloadSystem.OnWeaponUnequipped();
            weaponSlots[currentWeaponIndex].reloadSystem.enabled = false;
        }
        
        // Disable current weapon
        currentWeaponObject.SetActive(false);
        
        // Disable weapon scripts
        if (weaponSlots[currentWeaponIndex].gunScript != null)
        {
            weaponSlots[currentWeaponIndex].gunScript.enabled = false;
        }
        if (weaponSlots[currentWeaponIndex].simpleGunScript != null)
        {
            weaponSlots[currentWeaponIndex].simpleGunScript.enabled = false;
        }
    }
    
    private bool CanUseWeapon(int slotIndex)
    {
        // Check if weapon has ReloadSystem
        if (weaponSlots[slotIndex].reloadSystem != null)
        {
            // With ReloadSystem, we can always equip (even with 0 ammo)
            return true;
        }
        else
        {
            // Without ReloadSystem, check ammo
            return weaponSlots[slotIndex].hasAmmo;
        }
    }
    
    private void SwitchToNextWeapon()
    {
        if (weaponSlots.Count <= 1) return;
        
        int startIndex = currentWeaponIndex;
        int attempts = 0;
        
        do
        {
            currentWeaponIndex = (currentWeaponIndex + 1) % weaponSlots.Count;
            attempts++;
            
            if (attempts > weaponSlots.Count)
            {
                // No available weapons
                currentWeaponIndex = startIndex;
                return;
            }
            
        } while (!IsWeaponAvailable(currentWeaponIndex));
        
        EquipWeapon(currentWeaponIndex);
    }
    
    private void SwitchToPreviousWeapon()
    {
        if (weaponSlots.Count <= 1) return;
        
        int startIndex = currentWeaponIndex;
        int attempts = 0;
        
        do
        {
            currentWeaponIndex--;
            if (currentWeaponIndex < 0) currentWeaponIndex = weaponSlots.Count - 1;
            attempts++;
            
            if (attempts > weaponSlots.Count)
            {
                // No available weapons
                currentWeaponIndex = startIndex;
                return;
            }
            
        } while (!IsWeaponAvailable(currentWeaponIndex));
        
        EquipWeapon(currentWeaponIndex);
    }
    
    private void QuickSwitch(int slotIndex)
    {
        if (slotIndex == currentWeaponIndex) return; // Already equipped
        if (slotIndex >= weaponSlots.Count) return; // Invalid slot
        
        EquipWeapon(slotIndex);
    }
    
    private bool IsWeaponAvailable(int index)
    {
        if (index < 0 || index >= weaponSlots.Count) return false;
        if (!weaponSlots[index].isUnlocked) return false;
        
        // With ReloadSystem, weapon is always available to equip
        if (weaponSlots[index].reloadSystem != null)
        {
            return true;
        }
        
        // Without ReloadSystem, check ammo
        return weaponSlots[index].hasAmmo;
    }
    
    private bool HasInfiniteAmmo(int index)
    {
        // Check if this weapon type has infinite ammo (like melee)
        // Check ReloadSystem first
        if (weaponSlots[index].reloadSystem != null)
        {
            return weaponSlots[index].reloadSystem.HasInfiniteAmmo();
        }
        
        return false; // Default to limited ammo
    }
    
    // Public methods for other systems
    public void AddAmmo(int slotIndex, int amount)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Count) return;
        
        // Check if weapon has ReloadSystem
        if (weaponSlots[slotIndex].reloadSystem != null)
        {
            weaponSlots[slotIndex].reloadSystem.AddReserveAmmo(amount);
            weaponSlots[slotIndex].ammoCount = weaponSlots[slotIndex].reloadSystem.CurrentAmmoInMagazine;
        }
        else
        {
            weaponSlots[slotIndex].ammoCount = Mathf.Min(
                weaponSlots[slotIndex].ammoCount + amount, 
                weaponSlots[slotIndex].maxAmmo
            );
            
            if (weaponSlots[slotIndex].ammoCount > 0)
            {
                weaponSlots[slotIndex].hasAmmo = true;
            }
        }
        
        // Update ammo display
        OnAmmoUpdated?.Invoke(slotIndex, weaponSlots[slotIndex].ammoCount, weaponSlots[slotIndex].maxAmmo);
    }
    
    public bool ConsumeAmmo(int slotIndex, int amount = 1)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Count) return false;
        
        // Check if weapon has ReloadSystem
        if (weaponSlots[slotIndex].reloadSystem != null)
        {
            bool consumed = weaponSlots[slotIndex].reloadSystem.ConsumeAmmo(amount);
            if (!consumed)
            {
                // Auto-reload if out of ammo
                weaponSlots[slotIndex].reloadSystem.StartReload();
            }
            else
            {
                // Update ammo count after consumption
                weaponSlots[slotIndex].ammoCount = weaponSlots[slotIndex].reloadSystem.CurrentAmmoInMagazine;
            }
            return consumed;
        }
        else
        {
            // Fallback to original ammo system
            weaponSlots[slotIndex].ammoCount = Mathf.Max(0, weaponSlots[slotIndex].ammoCount - amount);
            
            if (weaponSlots[slotIndex].ammoCount <= 0)
            {
                weaponSlots[slotIndex].hasAmmo = false;
                // Auto-switch to next available weapon if current is out of ammo
                if (slotIndex == currentWeaponIndex)
                {
                    SwitchToNextWeapon();
                }
            }
            return weaponSlots[slotIndex].ammoCount > 0;
        }
    }
    
    // New method to update weapon ammo from external sources (like ReloadSystem)
    public void UpdateWeaponAmmo(int slotIndex, int currentAmmo)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Count) return;
        
        weaponSlots[slotIndex].ammoCount = currentAmmo;
        weaponSlots[slotIndex].hasAmmo = currentAmmo > 0;
        
        // Trigger ammo update event
        OnAmmoUpdated?.Invoke(slotIndex, currentAmmo, weaponSlots[slotIndex].maxAmmo);
    }
    
    public void UnlockWeapon(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Count) return;
        
        weaponSlots[slotIndex].isUnlocked = true;
        Debug.Log($"Unlocked weapon: {weaponSlots[slotIndex].weaponName}");
    }
    
    public void StartReloadCurrentWeapon()
    {
        if (currentWeaponIndex == -1) return;
        
        if (weaponSlots[currentWeaponIndex].reloadSystem != null)
        {
            weaponSlots[currentWeaponIndex].reloadSystem.StartReload();
        }
    }
    
    public WeaponSlot GetCurrentWeapon()
    {
        if (currentWeaponIndex == -1) return null;
        return weaponSlots[currentWeaponIndex];
    }
    
    public int GetCurrentWeaponIndex() => currentWeaponIndex;
    public string GetCurrentWeaponName() => currentWeaponIndex != -1 ? weaponSlots[currentWeaponIndex].weaponName : "None";
    public int GetWeaponCount() => weaponSlots.Count;
    
    // Get ammo info for UI
    public string GetCurrentAmmoInfo()
    {
        if (currentWeaponIndex == -1) return "No Weapon";
        
        if (weaponSlots[currentWeaponIndex].reloadSystem != null)
        {
            return weaponSlots[currentWeaponIndex].reloadSystem.GetAmmoStatus();
        }
        
        return $"{weaponSlots[currentWeaponIndex].ammoCount}/{weaponSlots[currentWeaponIndex].maxAmmo}";
    }
    
    // Events for UI/other systems
    public delegate void WeaponSwitchEvent(int newWeaponIndex);
    public event WeaponSwitchEvent OnWeaponSwitched;
    
    public delegate void AmmoUpdateEvent(int weaponIndex, int currentAmmo, int maxAmmo);
    public event AmmoUpdateEvent OnAmmoUpdated;
    
    private void OnDestroy()
    {
        // Clean up input events
        if (switchWeaponAction != null)
            switchWeaponAction.action.performed -= OnSwitchWeapon;
        
        if (weapon1Action != null)
            weapon1Action.action.performed -= ctx => QuickSwitch(0);
            
        if (weapon2Action != null)
            weapon2Action.action.performed -= ctx => QuickSwitch(1);
            
        if (weapon3Action != null)
            weapon3Action.action.performed -= ctx => QuickSwitch(2);
            
        if (dropWeaponAction != null)
            dropWeaponAction.action.performed -= OnDropWeapon;
    }
    
    // Debug GUI
    private void OnGUI()
    {
        if (currentWeaponIndex == -1) return;
        
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 16;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.UpperLeft;
        
        string info = $"Weapon: {weaponSlots[currentWeaponIndex].weaponName}\n";
        
        if (weaponSlots[currentWeaponIndex].reloadSystem != null)
        {
            info += $"Ammo: {GetCurrentAmmoInfo()}\n";
            if (weaponSlots[currentWeaponIndex].reloadSystem.IsReloading)
            {
                info += $"Reloading: {(weaponSlots[currentWeaponIndex].reloadSystem.ReloadProgress * 100):F0}%\n";
            }
        }
        else
        {
            info += $"Ammo: {weaponSlots[currentWeaponIndex].ammoCount}/{weaponSlots[currentWeaponIndex].maxAmmo}\n";
        }
        
        if (!canSwitch)
        {
            float remaining = Mathf.Max(0, lastSwitchTime + switchCooldown - Time.time);
            info += $"Switching: {remaining:F1}s";
        }
        
        GUI.Box(new Rect(10, 350, 250, 100), info, style);
    }
}
