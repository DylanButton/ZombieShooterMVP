using UnityEngine;

public class ZombieSpawnPoint : MonoBehaviour
{
    [Header("=== SPAWN SETTINGS ===")]
    [Tooltip("Radius around this point where zombies can spawn")]
    [SerializeField] private float spawnRadius = 5f;
    [Tooltip("Height offset above ground to prevent clipping")]
    [SerializeField] private float spawnHeightOffset = 0.5f;
    [Tooltip("Minimum zombies to spawn per wave")]
    [SerializeField] private int minZombiesPerWave = 1;
    [Tooltip("Maximum zombies to spawn per wave")]
    [SerializeField] private int maxZombiesPerWave = 5;
    
    [Header("=== VISUALIZATION ===")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.3f);
    [SerializeField] private Color gizmoOutlineColor = Color.red;
    [SerializeField] private bool showSpawnPreview = true;
    
    // Public properties for wave manager to access
    public float SpawnRadius => spawnRadius;
    public int MinZombies => minZombiesPerWave;
    public int MaxZombies => maxZombiesPerWave;
    
    // Get random position within spawn radius
    public Vector3 GetRandomSpawnPosition()
    {
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
        
        // Start raycast from above the spawn point
        Vector3 rayStart = new Vector3(spawnPos.x, transform.position.y + 50f, spawnPos.z);
        
        // Raycast down to find ground
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f))
        {
            // Spawn above ground with offset
            return hit.point + Vector3.up * spawnHeightOffset;
        }
        
        // Fallback: spawn at spawn point height with offset
        return new Vector3(spawnPos.x, transform.position.y + spawnHeightOffset, spawnPos.z);
    }
    
    // Get random zombie count for this spawn point
    public int GetRandomZombieCount()
    {
        return Random.Range(minZombiesPerWave, maxZombiesPerWave + 1);
    }
    
    private void OnDrawGizmos()
    {
        if (!showSpawnPreview) return;
        
        // Draw wire circle outline
        Gizmos.color = gizmoOutlineColor;
        DrawWireCircle(transform.position, spawnRadius, 32);
        
        // Draw filled disc using lines (no mesh needed)
        Gizmos.color = gizmoColor;
        for (int i = 0; i < 16; i++)
        {
            float angle = (360f / 16f) * i * Mathf.Deg2Rad;
            float nextAngle = (360f / 16f) * (i + 1) * Mathf.Deg2Rad;
            
            Vector3 point1 = transform.position + new Vector3(Mathf.Cos(angle) * spawnRadius, 0f, Mathf.Sin(angle) * spawnRadius);
            Vector3 point2 = transform.position + new Vector3(Mathf.Cos(nextAngle) * spawnRadius, 0f, Mathf.Sin(nextAngle) * spawnRadius);
            
            Gizmos.DrawLine(transform.position, point1);
            Gizmos.DrawLine(point1, point2);
        }
        
        // Draw direction indicator
        Gizmos.color = Color.yellow;
        Vector3 forward = transform.forward * (spawnRadius * 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + forward);
        Gizmos.DrawSphere(transform.position + forward, 0.2f);
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw additional info when selected
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        // Draw height indicator
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
    }
    
    private void DrawWireCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}