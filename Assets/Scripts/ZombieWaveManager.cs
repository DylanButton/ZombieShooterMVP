using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class ZombieWaveManager : MonoBehaviour
{
    [Header("=== WAVE SETTINGS ===")]
    [Tooltip("Time between waves in seconds")]
    [SerializeField] private float timeBetweenWaves = 30f;
    [Tooltip("Base zombies per wave (before increases)")]
    [SerializeField] private int baseZombiesPerWave = 8;
    [Tooltip("Additional zombies spawned per wave")]
    [SerializeField] private int zombiesPerWaveIncrease = 2;
    [Tooltip("Starting wave number")]
    [SerializeField] private int startingWave = 1;
    [Tooltip("Auto-start waves on game start")]
    [SerializeField] private bool autoStartWaves = true;
    
    [Header("=== ZOMBIE PREFABS ===")]
    [Tooltip("Normal zombie prefab")]
    [SerializeField] private GameObject normalZombiePrefab;
    [Tooltip("Tank zombie prefab (mini boss)")]
    [SerializeField] private GameObject tankZombiePrefab;
    
    [Header("=== TANK SPAWN CHANCES ===")]
    [Tooltip("Wave number when tanks can start spawning")]
    [SerializeField] private int tankStartWave = 3;
    [Tooltip("Base chance for tank spawn (0-100%)")]
    [SerializeField] private float baseTankChance = 5f;
    [Tooltip("Tank chance increase per wave (0-100%)")]
    [SerializeField] private float tankChancePerWave = 5f;
    [Tooltip("Maximum tank chance (0-100%)")]
    [SerializeField] private float maxTankChance = 40f;
    [Tooltip("Max tanks per wave")]
    [SerializeField] private int maxTanksPerWave = 3;
    
    [Header("=== SPAWN POINTS ===")]
    [Tooltip("All spawn points in the level")]
    [SerializeField] private List<ZombieSpawnPoint> spawnPoints = new List<ZombieSpawnPoint>();
    
    [Header("=== SPAWN RETRY SETTINGS ===")]
    [Tooltip("Maximum spawn attempts per zombie")]
    [SerializeField] private int maxSpawnAttempts = 3;
    [Tooltip("Delay between spawn attempts (seconds)")]
    [SerializeField] private float spawnRetryDelay = 0.5f;
    [Tooltip("Minimum distance from player to spawn")]
    [SerializeField] private float minSpawnDistanceFromPlayer = 10f;
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool showDebugInfo = true;
    
    // Current wave state
    private int currentWave = 0;
    private int zombiesAlive = 0;
    private int zombiesSpawnedThisWave = 0;
    private int zombiesToSpawnThisWave = 0;
    private float waveTimer = 0f;
    private bool waveActive = false;
    private bool canStartNextWave = false;
    private bool isSpawning = false; // Track if we're currently spawning
    
    // Pooling lists
    private List<GameObject> activeZombies = new List<GameObject>();
    
    // Coroutine reference
    private Coroutine spawnCoroutine;
    
    private void Start()
    {
        currentWave = startingWave - 1; // Will increment to startingWave on first wave
        
        // Auto-find spawn points if none assigned
        if (spawnPoints.Count == 0)
        {
            // FIXED: Replaced deprecated FindObjectsOfType with FindObjectsByType
            spawnPoints.AddRange(FindObjectsByType<ZombieSpawnPoint>(FindObjectsSortMode.None));
            if (spawnPoints.Count > 0)
            {
                Debug.Log($"Auto-found {spawnPoints.Count} spawn points");
            }
        }
        
        if (spawnPoints.Count == 0)
        {
            Debug.LogError("No spawn points assigned or found! Please add ZombieSpawnPoint components to the scene.");
        }
        
        if (autoStartWaves)
        {
            StartNextWave();
        }
    }
    
    private void Update()
    {
        if (spawnPoints.Count == 0) return;
        
        // Check if wave is complete
        if (waveActive && zombiesAlive == 0 && !isSpawning && zombiesSpawnedThisWave > 0)
        {
            // All zombies are dead and we're not currently spawning more
            EndWave();
        }
        
        // Timer between waves
        if (canStartNextWave)
        {
            waveTimer -= Time.deltaTime;
            if (waveTimer <= 0f)
            {
                StartNextWave();
            }
        }
    }
    
    public void StartNextWave()
    {
        currentWave++;
        waveActive = true;
        canStartNextWave = false;
        isSpawning = false;
        zombiesSpawnedThisWave = 0;
        
        // Calculate zombies to spawn this wave (always increasing)
        zombiesToSpawnThisWave = CalculateTotalZombiesForWave();
        
        if (showDebugInfo)
        {
            Debug.Log($"=== WAVE {currentWave} STARTED ===");
            Debug.Log($"Target zombies to spawn: {zombiesToSpawnThisWave}");
            Debug.Log($"Tank spawn chance: {GetTankSpawnChance():F1}%");
        }
        
        // Start spawning zombies
        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);
        
        spawnCoroutine = StartCoroutine(SpawnWaveZombiesCoroutine());
    }
    
    private IEnumerator SpawnWaveZombiesCoroutine()
    {
        isSpawning = true;
        int zombiesLeftToSpawn = zombiesToSpawnThisWave;
        int tanksSpawned = 0;
        int spawnAttempts = 0;
        
        Debug.Log($"Starting to spawn wave {currentWave}: {zombiesLeftToSpawn} zombies");
        
        // Keep trying to spawn until we reach our target
        while (zombiesLeftToSpawn > 0 && spawnAttempts < maxSpawnAttempts * zombiesToSpawnThisWave)
        {
            // Determine zombie type for this spawn attempt
            bool spawnTank = false;
            if (currentWave >= tankStartWave && tanksSpawned < maxTanksPerWave)
            {
                float tankChance = GetTankSpawnChance();
                float roll = Random.Range(0f, 100f);
                
                if (roll < tankChance)
                {
                    spawnTank = true;
                    tanksSpawned++;
                }
            }
            
            // Try to spawn a zombie
            bool spawned = TrySpawnZombie(spawnTank);
            
            if (spawned)
            {
                zombiesSpawnedThisWave++;
                zombiesLeftToSpawn--;
                
                if (showDebugInfo)
                {
                    Debug.Log($"Successfully spawned zombie. Remaining: {zombiesLeftToSpawn}");
                }
                
                // Small delay between successful spawns to prevent lag
                yield return new WaitForSeconds(0.1f);
            }
            else
            {
                spawnAttempts++;
                
                if (showDebugInfo && spawnAttempts % 10 == 0) // Log every 10 attempts
                {
                    Debug.Log($"Failed spawn attempt {spawnAttempts}. Still need to spawn: {zombiesLeftToSpawn}");
                }
                
                // Wait before retrying
                yield return new WaitForSeconds(spawnRetryDelay);
            }
            
            // If we're stuck, try a different approach
            if (spawnAttempts > zombiesToSpawnThisWave * 2 && zombiesLeftToSpawn > 0)
            {
                Debug.LogWarning($"Stuck spawning. Skipping {zombiesLeftToSpawn} zombies for wave {currentWave}");
                zombiesLeftToSpawn = 0;
            }
        }
        
        // Finished spawning (either success or gave up)
        isSpawning = false;
        
        if (showDebugInfo)
        {
            Debug.Log($"Wave {currentWave} spawning complete:");
            Debug.Log($"- Successfully spawned: {zombiesSpawnedThisWave}/{zombiesToSpawnThisWave}");
            Debug.Log($"- Zombies currently alive: {zombiesAlive}");
            
            if (zombiesSpawnedThisWave == 0)
            {
                Debug.LogError($"WAVE {currentWave} FAILED: No zombies were spawned!");
                // Force end wave if no zombies were spawned
                EndWave();
            }
        }
    }
    
    private bool TrySpawnZombie(bool isTank)
    {
        if (spawnPoints.Count == 0)
            return false;
        
        GameObject prefabToSpawn = isTank ? tankZombiePrefab : normalZombiePrefab;
        
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"Missing prefab for {(isTank ? "Tank" : "Normal")} zombie!");
            return false;
        }
        
        // Try up to 3 random spawn points
        for (int i = 0; i < 3; i++)
        {
            ZombieSpawnPoint spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
            
            if (spawnPoint == null)
                continue;
                
            Vector3 spawnPos = spawnPoint.GetRandomSpawnPosition();
            
            // Validate spawn position
            if (spawnPos == Vector3.zero)
                continue;
            
            // Check if spawn position is valid (not too close to player)
            if (!IsValidSpawnPosition(spawnPos))
                continue;
            
            // Attempt to spawn
            GameObject zombie = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            
            if (zombie == null)
                continue;
            
            // Get ZombieHealth component
            ZombieHealth health = zombie.GetComponent<ZombieHealth>();
            if (health == null)
            {
                health = zombie.GetComponentInChildren<ZombieHealth>();
            }
            
            if (health != null)
            {
                // Use a proper event handler
                health.OnDeath += () => OnZombieDeath(zombie);
                
                // Track zombie
                activeZombies.Add(zombie);
                zombiesAlive++;
                
                if (showDebugInfo)
                {
                    Debug.Log($"Spawned {(isTank ? "TANK" : "Normal")} zombie at {spawnPoint.name} (Position: {spawnPos})");
                }
                
                return true;
            }
            else
            {
                Destroy(zombie);
            }
        }
        
        return false; // Failed to spawn after trying multiple points
    }
    
    private bool IsValidSpawnPosition(Vector3 position)
    {
        // Check if too close to player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(position, player.transform.position);
            if (distanceToPlayer < minSpawnDistanceFromPlayer)
            {
                return false; // Too close to player
            }
        }
        
        // Optional: Add more checks here (like checking for obstacles, etc.)
        
        return true;
    }
    
    private void EndWave()
    {
        if (!waveActive) return; // Don't end if wave is already ended
        
        waveActive = false;
        canStartNextWave = true;
        waveTimer = timeBetweenWaves;
        
        if (showDebugInfo)
        {
            Debug.Log($"=== WAVE {currentWave} COMPLETE ===");
            Debug.Log($"Zombies spawned: {zombiesSpawnedThisWave}/{zombiesToSpawnThisWave}");
            Debug.Log($"Next wave in {timeBetweenWaves} seconds");
        }
    }
    
    private int CalculateTotalZombiesForWave()
    {
        // Base zombies increase with each wave
        int totalZombies = baseZombiesPerWave + (currentWave * zombiesPerWaveIncrease);
        
        // Make sure we always spawn at least a few zombies
        return Mathf.Max(3, totalZombies);
    }
    
    private void OnZombieDeath(GameObject zombie)
    {
        if (zombie == null) return;
        
        if (activeZombies.Contains(zombie))
        {
            activeZombies.Remove(zombie);
            zombiesAlive = Mathf.Max(0, zombiesAlive - 1);
            
            if (showDebugInfo)
            {
                Debug.Log($"Zombie killed! Remaining: {zombiesAlive}/{zombiesSpawnedThisWave}");
            }
        }
    }
    
    private float GetTankSpawnChance()
    {
        if (currentWave < tankStartWave) return 0f;
        
        float chance = baseTankChance + ((currentWave - tankStartWave) * tankChancePerWave);
        return Mathf.Min(chance, maxTankChance);
    }
    
    // Public methods for external control
    public void ForceStartWave()
    {
        if (!waveActive)
        {
            StartNextWave();
        }
    }
    
    public void SkipToNextWave()
    {
        if (waveActive)
        {
            // Kill all remaining zombies
            foreach (GameObject zombie in new List<GameObject>(activeZombies))
            {
                if (zombie != null)
                {
                    ZombieHealth health = zombie.GetComponent<ZombieHealth>();
                    if (health != null)
                    {
                        health.TakeDamage(10000f); // Instant kill
                    }
                }
            }
            EndWave();
        }
    }
    
    public void ForceSpawnMoreZombies()
    {
        if (waveActive && !isSpawning)
        {
            // Force spawn remaining zombies
            int remaining = zombiesToSpawnThisWave - zombiesSpawnedThisWave;
            if (remaining > 0)
            {
                Debug.Log($"Force spawning {remaining} remaining zombies...");
                StartCoroutine(ForceSpawnRemainingZombies(remaining));
            }
        }
    }
    
    private IEnumerator ForceSpawnRemainingZombies(int count)
    {
        isSpawning = true;
        
        for (int i = 0; i < count; i++)
        {
            bool spawned = TrySpawnZombie(false); // Spawn normal zombies
            if (spawned)
            {
                zombiesSpawnedThisWave++;
                yield return new WaitForSeconds(0.1f);
            }
            else
            {
                yield return new WaitForSeconds(spawnRetryDelay);
            }
        }
        
        isSpawning = false;
    }
    
    public int GetCurrentWave() => currentWave;
    public int GetZombiesAlive() => zombiesAlive;
    public int GetZombiesSpawnedThisWave() => zombiesSpawnedThisWave;
    public int GetZombiesToSpawnThisWave() => zombiesToSpawnThisWave;
    public bool IsWaveActive() => waveActive;
    public float GetTimeUntilNextWave() => waveTimer;
    
    private void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 16;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.UpperLeft;
        
        string info = $"WAVE: {currentWave}\n";
        info += $"Zombies Alive: {zombiesAlive}/{zombiesSpawnedThisWave}\n";
        info += $"Target: {zombiesToSpawnThisWave}\n";
        
        if (isSpawning)
        {
            info += $"Spawning...\n";
        }
        
        if (canStartNextWave)
        {
            info += $"Next Wave: {waveTimer:F1}s\n";
        }
        else if (waveActive)
        {
            info += "Wave Active!\n";
        }
        
        info += $"Tank Chance: {GetTankSpawnChance():F1}%\n";
        info += $"Spawn Points: {spawnPoints.Count}";
        
        GUI.Box(new Rect(10, 130, 250, 180), info, style);
    }
}