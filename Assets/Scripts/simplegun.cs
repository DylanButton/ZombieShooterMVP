using UnityEngine;

public class SimpleGun : MonoBehaviour
{
    [Header("Shooting")]
    public float fireCooldown = 0.5f;
    public float damage = 25f;
    public float range = 100f;
    
    [Header("Audio")]
    public AudioSource gunAudio;
    public AudioClip shootSound;
    
    [Header("Visual")]
    public Animator gunAnimator;
    public string shootAnimationName = "Shoot";
    
    [Header("Particle Effects")]
    public ParticleSystem muzzleFlash;
    
    private float currentCooldown;
    private Camera playerCamera;
    
    void Start()
    {
        currentCooldown = 0f;
        playerCamera = Camera.main;
        
        if (gunAudio == null)
            gunAudio = GetComponent<AudioSource>();
    }
    
    void Update()
    {
        // Count down cooldown
        if (currentCooldown > 0)
            currentCooldown -= Time.deltaTime;
        
        // Check for shooting input
        if (Input.GetMouseButtonDown(0) && currentCooldown <= 0)
        {
            Shoot();
        }
    }
    
    void Shoot()
    {
        // Reset cooldown
        currentCooldown = fireCooldown;
        
        // Play animation
        if (gunAnimator != null)
            gunAnimator.Play(shootAnimationName);
        
        // Play sound
        if (gunAudio != null && shootSound != null)
            gunAudio.PlayOneShot(shootSound);
        
        // Play muzzle flash
        if (muzzleFlash != null)
            muzzleFlash.Play();
        
        // Fire raycast to detect zombies
        RaycastHit hit;
        Vector3 rayOrigin = playerCamera.transform.position;
        Vector3 rayDirection = playerCamera.transform.forward;
        
        if (Physics.Raycast(rayOrigin, rayDirection, out hit, range))
        {
            // Check if we hit a zombie
            ZombieHealth zombieHealth = hit.collider.GetComponent<ZombieHealth>();
            if (zombieHealth != null)
            {
                // Damage the zombie
                zombieHealth.TakeDamage(damage);
                Debug.Log("Hit zombie!");
            }
        }
    }
}