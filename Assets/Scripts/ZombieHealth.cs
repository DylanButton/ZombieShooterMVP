using UnityEngine;
using System;

public class ZombieHealth : MonoBehaviour
{
    [Header("=== HEALTH SETTINGS ===")]
    [SerializeField] private float maxHealth = 100f;
    
    [Header("=== VISUAL FEEDBACK ===")]
    [SerializeField] private bool showDamageNumbers = false;
    
    private float currentHealth;
    private bool isDead = false;
    
    // Event for when zombie dies
    public event Action OnDeath;
    
    private void Awake()
    {
        // Check if this is a tank zombie from ZombieAI
        ZombieAI ai = GetComponent<ZombieAI>();
        if (ai != null && ai.IsTankZombie())
        {
            maxHealth *= 3f; // Tanks have 3x health
        }
        
        currentHealth = maxHealth;
    }
    
    public void TakeDamage(float damage)
    {
        if (isDead) return;
        
        currentHealth -= damage;
        
        if (showDamageNumbers)
        {
            Debug.Log($"{gameObject.name} took {damage} damage! ({currentHealth}/{maxHealth})");
        }
        
        if (currentHealth <= 0f && gameObject != null)
        {
            Die();
        }
    }
    
    private void Die()
    {
        if (isDead) return;
        
        isDead = true;
        
        // FIXED: Invoke death event before destroying
        OnDeath?.Invoke();
        
        // Destroy zombie
        Destroy(gameObject);
    }
    
    // FIXED: Add OnDestroy to handle cases where zombie is destroyed without Die() being called
    private void OnDestroy()
    {
        if (!isDead)
        {
            // If the zombie is destroyed without Die() being called, still invoke the event
            OnDeath?.Invoke();
        }
    }
    
    public float GetHealthPercent()
    {
        return currentHealth / maxHealth;
    }
}