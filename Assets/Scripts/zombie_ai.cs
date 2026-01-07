using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAI : MonoBehaviour
{
    [Header("=== ZOMBIE TYPE ===")]
    [SerializeField] private bool isTankZombie = false;
    
    [Header("=== MOVEMENT ===")]
    [SerializeField] private float normalSpeed = 3.5f;
    [SerializeField] private float tankSpeed = 2f;
    [SerializeField] private float chaseSpeedMultiplier = 1.5f;
    [SerializeField] private float rotationSpeed = 120f;
    
    [Header("=== DETECTION ===")]
    [SerializeField] private float detectionRange = 20f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private LayerMask playerLayer = ~0;
    
    [Header("=== COMBAT ===")]
    [SerializeField] private float normalDamage = 10f;
    [SerializeField] private float tankDamage = 25f;
    [SerializeField] private float attackCooldown = 1f;
    
    [Header("=== DEBUG ===")]
    [SerializeField] private bool showDebugGizmos = false;
    
    // Components
    private NavMeshAgent agent;
    private Transform target;
    private Animator animator;
    private ZombieHealth zombieHealth;
    
    // State
    private enum ZombieState { Idle, Chasing, Attacking }
    private ZombieState currentState = ZombieState.Idle;
    
    // Combat
    private float lastAttackTime;
    private float damage;
    
    // Target locking
    private bool hasLockedTarget = false;
    
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        zombieHealth = GetComponent<ZombieHealth>();
        
        // Set stats based on type
        agent.speed = isTankZombie ? tankSpeed : normalSpeed;
        damage = isTankZombie ? tankDamage : normalDamage;
        
        // Configure NavMeshAgent
        agent.acceleration = isTankZombie ? 4f : 8f;
        agent.angularSpeed = rotationSpeed;
        agent.stoppingDistance = attackRange * 0.8f;
        
        // FIXED: Ensure NavMeshAgent is properly initialized
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }
    
    private void Update()
    {
        // FIXED: Check if zombie is dead
        if (zombieHealth != null && zombieHealth.GetHealthPercent() <= 0)
            return;
            
        // Find or update target
        if (!hasLockedTarget || target == null)
        {
            FindClosestPlayer();
            if (target != null)
            {
                hasLockedTarget = true;
            }
        }
        
        // Update state machine
        switch (currentState)
        {
            case ZombieState.Idle:
                UpdateIdle();
                break;
            case ZombieState.Chasing:
                UpdateChasing();
                break;
            case ZombieState.Attacking:
                UpdateAttacking();
                break;
        }
        
        // Update animator if present
        UpdateAnimator();
    }
    
    private void FindClosestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        
        if (players.Length == 0) return;
        
        float closestDistance = Mathf.Infinity;
        Transform closestPlayer = null;
        
        foreach (GameObject player in players)
        {
            // Check if player is alive
            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null && playerHealth.IsDead())
                continue;
                
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPlayer = player.transform;
            }
        }
        
        if (closestDistance <= detectionRange)
        {
            target = closestPlayer;
        }
    }
    
    private void UpdateIdle()
    {
        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance <= detectionRange)
            {
                currentState = ZombieState.Chasing;
            }
        }
    }
    
    private void UpdateChasing()
    {
        if (target == null)
        {
            currentState = ZombieState.Idle;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            return;
        }
        
        // FIXED: Check if player is dead
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null && playerHealth.IsDead())
        {
            OnTargetDied();
            return;
        }
        
        float distance = Vector3.Distance(transform.position, target.position);
        
        // Check if in attack range
        if (distance <= attackRange)
        {
            currentState = ZombieState.Attacking;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            return;
        }
        
        // FIXED: Check if NavMeshAgent is valid
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
            
            // Speed up when chasing
            agent.speed = (isTankZombie ? tankSpeed : normalSpeed) * chaseSpeedMultiplier;
        }
        
        // Look at target
        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0f;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed * 0.1f);
        }
    }
    
    private void UpdateAttacking()
    {
        if (target == null)
        {
            currentState = ZombieState.Idle;
            return;
        }
        
        // FIXED: Check if player is dead
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null && playerHealth.IsDead())
        {
            OnTargetDied();
            return;
        }
        
        float distance = Vector3.Distance(transform.position, target.position);
        
        // If target moved away, chase again
        if (distance > attackRange * 1.2f)
        {
            currentState = ZombieState.Chasing;
            return;
        }
        
        // Face target
        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0f;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed * 0.1f);
        }
        
        // Attack on cooldown
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            PerformAttack();
            lastAttackTime = Time.time;
        }
    }
    
    private void PerformAttack()
    {
        if (target == null) return;
        
        // Check if still in range
        float distance = Vector3.Distance(transform.position, target.position);
        if (distance > attackRange) return;
        
        // Try to damage player
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            
            // Trigger attack animation
            if (animator != null)
            {
                animator.SetTrigger("Attack");
            }
        }
    }
    
    private void UpdateAnimator()
    {
        if (animator == null) return;
        
        // Set movement speed
        float speed = agent != null ? agent.velocity.magnitude : 0f;
        animator.SetFloat("Speed", speed);
        
        // Set state
        animator.SetBool("IsChasing", currentState == ZombieState.Chasing);
        animator.SetBool("IsAttacking", currentState == ZombieState.Attacking);
    }
    
    // Called when player dies
    public void OnTargetDied()
    {
        target = null;
        hasLockedTarget = false;
        currentState = ZombieState.Idle;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
    }
    
    // Public method to check if this is a tank
    public bool IsTankZombie()
    {
        return isTankZombie;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // Attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // Line to target
        if (target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
}