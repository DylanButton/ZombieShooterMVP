using UnityEngine;
using System;

public class PlayerHealth : MonoBehaviour
{
    [Header("=== HEALTH SETTINGS ===")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float healthRegenRate = 5f;
    [SerializeField] private float regenDelay = 5f;
    
    [Header("=== DEATH SETTINGS ===")]
    [SerializeField] private bool respawnOnDeath = true;
    [SerializeField] private float respawnDelay = 3f;
    [SerializeField] private Transform respawnPoint;
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool showHealthUI = true;
    
    private float currentHealth;
    private float lastDamageTime;
    private bool isDead = false;
    
    // Events
    public event Action OnDeath;
    public event Action<float> OnHealthChanged;
    
    private void Awake()
    {
        currentHealth = maxHealth;
    }
    
    private void Update()
    {
        if (isDead) return;
        
        // Health regeneration after delay
        if (currentHealth < maxHealth && Time.time >= lastDamageTime + regenDelay)
        {
            currentHealth += healthRegenRate * Time.deltaTime;
            currentHealth = Mathf.Min(currentHealth, maxHealth);
            OnHealthChanged?.Invoke(currentHealth / maxHealth);
        }
    }
    
    public void TakeDamage(float damage)
    {
        if (isDead) return;
        
        currentHealth -= damage;
        lastDamageTime = Time.time;
        
        // Invoke health changed event
        OnHealthChanged?.Invoke(currentHealth / maxHealth);
        
        if (currentHealth <= 0f)
        {
            Die();
        }
    }
    
    private void Die()
    {
        if (isDead) return;
        
        isDead = true;
        currentHealth = 0f;
        
        Debug.Log($"{gameObject.name} has died!");
        
        // Invoke death event
        OnDeath?.Invoke();
        
        // FIXED: Replaced deprecated FindObjectsOfType with FindObjectsByType
        ZombieAI[] zombies = FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
        foreach (ZombieAI zombie in zombies)
        {
            zombie.OnTargetDied();
        }
        
        // Handle respawn or game over
        if (respawnOnDeath)
        {
            Invoke(nameof(Respawn), respawnDelay);
        }
        else
        {
            // Disable player (or show game over screen)
            gameObject.SetActive(false);
        }
    }
    
    private void Respawn()
    {
        isDead = false;
        currentHealth = maxHealth;
        
        // Move to respawn point
        if (respawnPoint != null)
        {
            transform.position = respawnPoint.position;
            transform.rotation = respawnPoint.rotation;
        }
        
        // Reset velocity if has character controller
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            cc.enabled = true;
        }
        
        OnHealthChanged?.Invoke(1f);
        
        Debug.Log($"{gameObject.name} respawned!");
    }
    
    public void Heal(float amount)
    {
        if (isDead) return;
        
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        OnHealthChanged?.Invoke(currentHealth / maxHealth);
    }
    
    public float GetHealthPercent()
    {
        return currentHealth / maxHealth;
    }
    
    public bool IsDead()
    {
        return isDead;
    }
    
    private void OnGUI()
    {
        if (!showHealthUI) return;
        
        // Health bar
        float barWidth = 200f;
        float barHeight = 30f;
        float barX = 10f;
        float barY = 10f;
        
        // Background
        GUI.color = Color.black;
        GUI.Box(new Rect(barX, barY, barWidth, barHeight), "");
        
        // Health fill
        float healthPercent = currentHealth / maxHealth;
        GUI.color = Color.Lerp(Color.red, Color.green, healthPercent);
        GUI.Box(new Rect(barX + 2, barY + 2, (barWidth - 4) * healthPercent, barHeight - 4), "");
        
        // Health text
        GUI.color = Color.white;
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 16;
        style.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(barX, barY, barWidth, barHeight), $"HP: {currentHealth:F0}/{maxHealth:F0}", style);
        
        // Death message
        if (isDead)
        {
            GUI.color = Color.red;
            style.fontSize = 32;
            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 50, 300, 100), "YOU DIED", style);
            
            if (respawnOnDeath)
            {
                style.fontSize = 20;
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2, 300, 50), "Respawning...", style);
            }
        }
    }
}