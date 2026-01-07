using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("=== MOVEMENT SPEEDS ===")]
    [SerializeField] private float walkSpeed = 15f;
    [SerializeField] private float sprintSpeed = 20f;
    [SerializeField] private float crouchSpeed = 10f;
    
    [Header("=== SLIDE SETTINGS ===")]
    [SerializeField] private float slideMinSpeed = 8f;
    [SerializeField] private float slideSpeed = 16f;
    [SerializeField] private float slideSpeedMultiplier = 1.25f;
    [SerializeField] private float slideDecel = 6f;
    [SerializeField] private float slideMaxTime = 1.8f;
    [SerializeField] private float slideEndSpeed = 4f;
    [SerializeField] private float slideCooldown = 0.05f;
    [SerializeField] private float slideHeight = 0.5f;
    [SerializeField] private float slideJumpBoost = 1.5f;
    
    [Header("=== JUMP & GRAVITY ===")]
    [SerializeField] private float jumpHeight = 1.8f;
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float maxFallSpeed = -40f;
    
    [Header("=== ADVANCED MOVEMENT ===")]
    [SerializeField] private float maxBhopSpeed = 30f;
    [SerializeField] private float bhopWindow = 0.25f;
    [SerializeField] private float bhopSpeedGain = 1.2f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float jumpBuffer = 0.15f;
    [SerializeField] private float groundAccel = 100f;
    [SerializeField] private float airAccel = 40f;
    [SerializeField] private float groundFriction = 12f;
    [SerializeField] private float airDrag = 0.97f;
    
    [Header("=== WALL BOUNCE ===")]
    [SerializeField] private bool enableWallBounce = true;
    [SerializeField] private float wallBounceForce = 20f;
    [SerializeField] private LayerMask wallMask = ~0;
    
    [Header("=== INPUT ===")]
    [SerializeField] private bool crouchToggle = false;
    [SerializeField] private float lookSens = 0.1f;
    [SerializeField] private bool showDebug = false;
    
    // Cached components
    private CharacterController cc;
    private PlayerInput input;
    private Camera cam;
    private Transform t;
    
    // Movement state - struct for better cache locality
    private Vector3 vel;
    private Vector2 moveInput;
    private Vector2 lookInput;
    
    // Cached values
    private float normalHeight;
    private float camY;
    private float jumpVelCache;
    
    // Timers
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float bhopTimer;
    private float slideTimer;
    private float slideCooldownTimer;
    private float lastJumpTime;
    
    // Slide state
    private Vector3 slideDir;
    private float slideStartSpeed;
    
    // State flags - packed for better memory
    private bool grounded;
    private bool wasGrounded;
    private bool sliding;
    private bool crouching;
    private bool sprinting;
    private bool bhopping;
    private bool wasSliding;
    private bool hasWallBounced;
    
    // Hop tracking
    private int hopCount;
    
    // Cached input actions
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    
    // Reusable vectors to avoid allocations
    private Vector3 tempVec3;
    private Vector3 hVel;
    
    // Debug strings (cached to avoid allocations)
    private string debugSpeed = "";
    private string debugState = "";
    private float debugUpdateTimer = 0f;
    private const float DEBUG_UPDATE_INTERVAL = 0.1f; // Only update debug UI 10 times per second

    private void Awake()
    {
        // Cache all components once
        cc = GetComponent<CharacterController>();
        input = GetComponent<PlayerInput>();
        t = transform;
        cam = GetComponentInChildren<Camera>();
        if (cam == null) cam = Camera.main;
        
        // Cache original values
        normalHeight = cc.height;
        if (cam != null) camY = cam.transform.localPosition.y;
        
        // Pre-calculate jump velocity
        jumpVelCache = Mathf.Sqrt(jumpHeight * -2f * gravity);
        
        // Cache input actions
        moveAction = input.actions["Move"];
        lookAction = input.actions["Look"];
        jumpAction = input.actions["Jump"];
        sprintAction = input.actions["Sprint"];
        crouchAction = input.actions["Crouch"];
        
        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    void OnPause(InputAction.CallbackContext context)
{
    if (context.performed)
    {
        GameObject pauseMenu = GameObject.Find("PauseMenuPanel");
        GameObject settingsMenu = GameObject.Find("SettingsPanel");
        
        if (pauseMenu != null && !pauseMenu.activeSelf && !settingsMenu.activeSelf)
        {
            Time.timeScale = 0f;
            pauseMenu.SetActive(true);
        }
        else if (pauseMenu != null && pauseMenu.activeSelf)
        {
            Time.timeScale = 1f;
            pauseMenu.SetActive(false);
            if (settingsMenu != null) settingsMenu.SetActive(false);
        }
    }
}
    
    private void Update()
    {
        HandleInput();
        HandleLook();
        UpdateTimers();
    }
    
    private void FixedUpdate()
    {
        HandleMovement();
        ApplyGravity();
        
        // Apply velocity in one operation
        cc.Move(vel * Time.fixedDeltaTime);
    }
    
    private void HandleInput()
    {
        // Read inputs
        moveInput = moveAction.ReadValue<Vector2>();
        lookInput = lookAction.ReadValue<Vector2>();
        
        // Jump input
        if (jumpAction.triggered)
        {
            jumpBufferTimer = jumpBuffer;
            
            float timeSinceJump = Time.time - lastJumpTime;
            if (timeSinceJump < bhopWindow)
            {
                bhopping = true;
                bhopTimer = bhopWindow;
                hopCount++;
            }
            lastJumpTime = Time.time;
        }
        
        // Crouch input
        bool crouchPressed = crouchAction.triggered;
        float crouchValue = crouchAction.ReadValue<float>();
        bool crouchHeld = crouchValue > 0.1f;
        bool sprintHeld = sprintAction.ReadValue<float>() > 0.1f;
        
        if (crouchToggle)
        {
            if (crouchPressed) crouching = !crouching;
        }
        else
        {
            crouching = crouchHeld;
        }
        
        // Slide trigger - only check when grounded and off cooldown
        if (crouchPressed && grounded && slideCooldownTimer <= 0f && moveInput.y > 0.1f)
        {
            // Cache horizontal velocity
            hVel.x = vel.x;
            hVel.y = 0f;
            hVel.z = vel.z;
            float speed = hVel.magnitude;
            
            if (speed >= slideMinSpeed)
            {
                StartSlide();
            }
        }
    }
    
    private void HandleLook()
    {
        // Rotate player body
        t.Rotate(0f, lookInput.x * lookSens, 0f, Space.Self);
        
        // Rotate camera
        if (cam != null)
        {
            tempVec3 = cam.transform.localEulerAngles;
            float x = tempVec3.x > 180f ? tempVec3.x - 360f : tempVec3.x;
            x = Mathf.Clamp(x - lookInput.y * lookSens, -90f, 90f);
            cam.transform.localEulerAngles = new Vector3(x, 0f, 0f);
        }
    }
    
    private void UpdateTimers()
    {
        float dt = Time.deltaTime;
        
        coyoteTimer = grounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - dt);
        jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);
        slideCooldownTimer = Mathf.Max(0f, slideCooldownTimer - dt);
        
        if (bhopping && bhopTimer > 0f)
        {
            bhopTimer -= dt;
            if (bhopTimer <= 0f)
            {
                bhopping = false;
                hopCount = 0;
            }
        }
    }
    
    private void HandleMovement()
    {
        wasGrounded = grounded;
        grounded = cc.isGrounded;
        
        // Landing logic
        if (grounded && !wasGrounded)
        {
            hasWallBounced = false;
            wasSliding = false;
            
            // Cache horizontal velocity
            hVel.x = vel.x;
            hVel.y = 0f;
            hVel.z = vel.z;
            float landingSpeed = hVel.magnitude;
            
            // Landing speed boost
            if (landingSpeed > walkSpeed)
            {
                vel.x *= 1.05f;
                vel.z *= 1.05f;
            }
            
            // Auto-slide on landing
            if (crouching && landingSpeed >= slideMinSpeed && slideCooldownTimer <= 0f)
            {
                StartSlide();
            }
        }
        
        // Ground stick
        if (grounded && vel.y < 0f) vel.y = -2f;
        
        // Jump
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
            PerformJump();
        
        // Calculate move direction once
        float inputMag = moveInput.sqrMagnitude;
        Vector3 moveDir = Vector3.zero;
        if (inputMag > 0.01f)
        {
            moveDir = t.right * moveInput.x + t.forward * moveInput.y;
            if (inputMag > 1f) moveDir.Normalize();
        }
        
        // State-based movement
        if (sliding)
            HandleSlide(moveDir);
        else if (grounded)
            HandleGroundMove(moveDir, inputMag);
        else
            HandleAirMove(moveDir, inputMag);
        
        // Wall bounce
        if (enableWallBounce)
            HandleWallBounce();
        
        // Update crouch
        UpdateCrouch();
    }
    
    private void PerformJump()
    {
        float jumpVel = jumpVelCache;
        
        // Slide jump
        if (sliding)
        {
            jumpVel *= slideJumpBoost;
            
            hVel.x = vel.x;
            hVel.y = 0f;
            hVel.z = vel.z;
            
            if (hVel.magnitude < slideSpeed * 0.7f)
            {
                vel.x = slideDir.x * slideSpeed * 0.7f;
                vel.z = slideDir.z * slideSpeed * 0.7f;
            }
            
            EndSlide();
            bhopping = true;
            bhopTimer = bhopWindow;
            hopCount = 1;
        }
        // Crouch jump with bhop
        else if (crouching && Time.time - lastJumpTime < bhopWindow)
        {
            hVel.x = vel.x;
            hVel.y = 0f;
            hVel.z = vel.z;
            float currentSpeed = hVel.magnitude;
            
            if (currentSpeed > crouchSpeed)
            {
                bhopping = true;
                bhopTimer = bhopWindow;
                hopCount++;
                
                if (hVel.magnitude > 0f)
                {
                    float newSpeed = Mathf.Min(currentSpeed + bhopSpeedGain, maxBhopSpeed);
                    float scale = newSpeed / currentSpeed;
                    vel.x *= scale;
                    vel.z *= scale;
                }
            }
            else
            {
                bhopping = false;
                hopCount = 0;
            }
        }
        // Continue bhop chain
        else if (bhopping && Time.time - lastJumpTime < bhopWindow && wasSliding)
        {
            hopCount++;
            
            hVel.x = vel.x;
            hVel.y = 0f;
            hVel.z = vel.z;
            
            if (hVel.magnitude > 0f)
            {
                float currentSpeed = hVel.magnitude;
                float newSpeed = Mathf.Min(currentSpeed + bhopSpeedGain, maxBhopSpeed);
                float scale = newSpeed / currentSpeed;
                vel.x *= scale;
                vel.z *= scale;
            }
        }
        else
        {
            // Normal jump
            bhopping = false;
            hopCount = 0;
            wasSliding = false;
        }
        
        vel.y = jumpVel;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
    }
    
    private void HandleGroundMove(Vector3 moveDir, float inputMag)
    {
        float targetSpeed = walkSpeed;
        sprinting = false;
        
        if (crouching)
            targetSpeed = crouchSpeed;
        else if (sprintAction.ReadValue<float>() > 0.1f && moveInput.y > 0.1f)
        {
            targetSpeed = sprintSpeed;
            sprinting = true;
        }
        
        hVel.x = vel.x;
        hVel.y = 0f;
        hVel.z = vel.z;
        
        if (inputMag > 0.01f)
        {
            // Accelerate toward target
            tempVec3 = moveDir * targetSpeed;
            float t = groundAccel * Time.fixedDeltaTime;
            hVel = Vector3.Lerp(hVel, tempVec3, t);
        }
        else if (!bhopping)
        {
            // Apply friction
            float t = groundFriction * Time.fixedDeltaTime;
            hVel = Vector3.Lerp(hVel, Vector3.zero, t);
        }
        
        vel.x = hVel.x;
        vel.z = hVel.z;
    }
    
    private void HandleAirMove(Vector3 moveDir, float inputMag)
    {
        if (inputMag > 0.01f)
        {
            hVel.x = vel.x;
            hVel.y = 0f;
            hVel.z = vel.z;
            
            float accel = bhopping ? airAccel * 2f : airAccel;
            float currentSpeed = Vector3.Dot(hVel, moveDir);
            float speedCap = bhopping ? maxBhopSpeed : sprintSpeed;
            float addSpeed = speedCap - currentSpeed;
            
            if (addSpeed > 0f)
            {
                float accelSpeed = Mathf.Min(accel * 3f * Time.fixedDeltaTime, addSpeed);
                vel.x += moveDir.x * accelSpeed;
                vel.z += moveDir.z * accelSpeed;
            }
        }
        
        // Air drag if not bhopping
        if (!bhopping)
        {
            vel.x *= airDrag;
            vel.z *= airDrag;
        }
        
        // Cap speed
        hVel.x = vel.x;
        hVel.y = 0f;
        hVel.z = vel.z;
        float mag = hVel.magnitude;
        
        if (mag > maxBhopSpeed)
        {
            float scale = maxBhopSpeed / mag;
            vel.x *= scale;
            vel.z *= scale;
        }
    }
    
    private void StartSlide()
    {
        sliding = true;
        slideTimer = 0f;
        
        hVel.x = vel.x;
        hVel.y = 0f;
        hVel.z = vel.z;
        
        float currentSpeed = hVel.magnitude;
        slideStartSpeed = currentSpeed;
        
        // Determine slide direction
        if (hVel.magnitude > 0.1f)
        {
            slideDir = hVel.normalized;
        }
        else
        {
            slideDir = t.forward;
        }
        
        // Apply boost
        float boostedSpeed = currentSpeed * slideSpeedMultiplier;
        float finalSpeed = Mathf.Min(boostedSpeed, slideSpeed);
        
        vel.x = slideDir.x * finalSpeed;
        vel.z = slideDir.z * finalSpeed;
        
        slideCooldownTimer = slideCooldown;
        crouching = false;
    }
    
    private void HandleSlide(Vector3 moveDir)
    {
        slideTimer += Time.fixedDeltaTime;
        
        hVel.x = vel.x;
        hVel.y = 0f;
        hVel.z = vel.z;
        
        float currentSpeed = hVel.magnitude;
        float targetSpeed = Mathf.Max(slideEndSpeed, slideStartSpeed - (slideDecel * slideTimer));
        
        // Steering
        tempVec3 = Vector3.Lerp(slideDir, moveDir, 0.3f);
        if (tempVec3.sqrMagnitude > 0.01f) tempVec3.Normalize();
        
        tempVec3 *= targetSpeed;
        hVel = Vector3.Lerp(hVel, tempVec3, 5f * Time.fixedDeltaTime);
        
        vel.x = hVel.x;
        vel.z = hVel.z;
        
        // End conditions
        bool expired = slideTimer >= slideMaxTime;
        bool tooSlow = currentSpeed < slideEndSpeed;
        bool released = !crouchToggle && crouchAction.ReadValue<float>() < 0.1f;
        
        if (expired || tooSlow || released)
        {
            EndSlide();
            if (crouchAction.ReadValue<float>() > 0.1f)
                crouching = true;
        }
    }
    
    private void EndSlide()
    {
        sliding = false;
        wasSliding = true;
    }
    
    private void UpdateCrouch()
    {
        bool shouldCrouch = sliding || crouching;
        float targetHeight = shouldCrouch ? slideHeight : normalHeight;
        cc.height = Mathf.Lerp(cc.height, targetHeight, 15f * Time.fixedDeltaTime);
        
        if (cam != null)
        {
            float targetY = shouldCrouch ? slideHeight * 0.5f : camY;
            tempVec3 = cam.transform.localPosition;
            tempVec3.y = Mathf.Lerp(tempVec3.y, targetY, 15f * Time.fixedDeltaTime);
            cam.transform.localPosition = tempVec3;
        }
    }
    
    private void ApplyGravity()
    {
        if (!grounded)
        {
            float mult = vel.y < 0f ? 2.5f : 1.0f;
            vel.y += gravity * mult * Time.fixedDeltaTime;
            if (vel.y < maxFallSpeed) vel.y = maxFallSpeed;
        }
    }
    
    private void HandleWallBounce()
    {
        if (jumpBufferTimer <= 0f || hasWallBounced || grounded)
            return;
        
        tempVec3 = t.position;
        tempVec3.y += 0.5f;
        
        Vector3 dir = vel.normalized;
        if (dir.sqrMagnitude < 0.01f) dir = t.forward;
        
        RaycastHit hit;
        if (Physics.SphereCast(tempVec3, 0.3f, dir, out hit, 1f, wallMask) && 
            Vector3.Angle(hit.normal, Vector3.up) > 60f)
        {
            Vector3 bounceDir = Vector3.Reflect(dir, hit.normal);
            bounceDir.y = Mathf.Clamp(bounceDir.y + 0.5f, 0.5f, 0.8f);
            bounceDir.Normalize();
            
            vel = bounceDir * wallBounceForce;
            vel.y = Mathf.Max(vel.y, jumpVelCache);
            
            hasWallBounced = true;
            jumpBufferTimer = 0f;
        }
    }
    
    private void OnGUI()
    {
        if (!showDebug) return;
        
        // Only update debug strings periodically to reduce GC
        debugUpdateTimer += Time.deltaTime;
        if (debugUpdateTimer >= DEBUG_UPDATE_INTERVAL)
        {
            debugUpdateTimer = 0f;
            
            hVel.x = vel.x;
            hVel.y = 0f;
            hVel.z = vel.z;
            float speed = hVel.magnitude;
            
            debugSpeed = $"Speed: {speed:F1}";
            debugState = $"State: {GetState()}";
        }
        
        GUI.color = Color.cyan;
        GUI.Label(new Rect(10, 10, 200, 20), debugSpeed);
        GUI.Label(new Rect(10, 30, 200, 20), debugState);
        
        if (bhopping)
        {
            GUI.color = Color.green;
            GUI.Label(new Rect(10, 50, 200, 20), $"Bhop Chain: {hopCount}");
        }
    }
    
    private string GetState()
    {
        if (sliding) return "SLIDING";
        if (sprinting) return "SPRINTING";
        if (crouching) return "CROUCHING";
        return grounded ? "GROUNDED" : "AIRBORNE";
    }
}