using UnityEngine;

public class TopDownController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float rotationSpeed = 720f;

    [Header("Hover Settings")]
    [SerializeField] private float rideHeight = 1.5f; // Desired hover height (NEUTRAL HEIGHT FROM GROUND)
    [SerializeField] private float rideSpringStrength = 50f; // Spring stiffness
    [SerializeField] private float rideSpringDamper = 5f; // Spring damping
    [SerializeField] private float raycastDistance = 3f; // How far to check for ground
    [SerializeField] private LayerMask groundLayer = -1; // What counts as ground

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 10f; // Upward force when jumping
    [SerializeField] private float jumpCooldown = 0.5f; // Time between jumps
    [SerializeField] private bool canJumpInAir = false; // Allow jumping while airborne

    [Header("Smoothing")]
    [SerializeField] private float accelerationTime = 0.1f;
    [SerializeField] private float decelerationTime = 0.1f;

    private Rigidbody rb;
    private Vector3 moveDirection;
    private float currentSpeed;
    private bool isSprinting;

    // Hover system variables
    private RaycastHit rayHit;
    private bool rayDidHit;
    private Vector3 downDir = Vector3.down;

    // Jump variables
    private float lastJumpTime = -999f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.freezeRotation = true;
            // Use interpolation for smoother visuals
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    void Update()
    {
        HandleInput();
    }

    void FixedUpdate()
    {
        ApplyHoverForce();
        HandleMovement();
        HandleRotation();
    }

    void HandleInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        moveDirection = new Vector3(horizontal, 0f, vertical).normalized;
        isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // Jump input (Space bar)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryJump();
        }
    }

    void ApplyHoverForce()
    {
        if (rb == null) return;

        // Cast ray downward from character position
        Vector3 rayOrigin = transform.position;
        Vector3 rayDir = transform.TransformDirection(downDir);

        rayDidHit = Physics.Raycast(rayOrigin, rayDir, out rayHit, raycastDistance, groundLayer);

        // Debug visualization
        Debug.DrawRay(rayOrigin, rayDir * raycastDistance, rayDidHit ? Color.green : Color.red);

        if (rayDidHit)
        {
            // Get current velocity
            Vector3 vel = rb.linearVelocity;
            Vector3 otherVel = Vector3.zero;

            // Get velocity of the object we hit (for moving platforms)
            Rigidbody hitBody = rayHit.rigidbody;
            if (hitBody != null)
            {
                otherVel = hitBody.linearVelocity;
            }

            // Calculate relative velocity along the ray direction
            float rayDirVel = Vector3.Dot(rayDir, vel);
            float otherDirVel = Vector3.Dot(rayDir, otherVel);
            float relVel = rayDirVel - otherDirVel;

            // Calculate spring force using Hooke's law with damping
            float x = rayHit.distance - rideHeight;
            float springForce = (x * rideSpringStrength) - (relVel * rideSpringDamper);

            // Apply hover force to character
            rb.AddForce(rayDir * springForce);

            // Apply equal and opposite force to hit object (for physics interaction)
            if (hitBody != null)
            {
                hitBody.AddForceAtPosition(rayDir * -springForce, rayHit.point);
            }

            // Debug visualization of spring force
            Debug.DrawLine(transform.position, transform.position + (rayDir * springForce * 0.1f), Color.yellow);
        }
    }

    void HandleMovement()
    {
        if (rb == null) return;

        // Determine target speed
        float targetSpeed = isSprinting ? sprintSpeed : moveSpeed;

        if (moveDirection.magnitude == 0)
        {
            targetSpeed = 0f;
        }

        // Smooth speed interpolation
        float smoothTime = targetSpeed > currentSpeed ? accelerationTime : decelerationTime;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.fixedDeltaTime / smoothTime);

        // Calculate target velocity (only in XZ plane)
        Vector3 targetVelocity = moveDirection * currentSpeed;

        // Apply force instead of directly setting velocity for more natural physics
        Vector3 currentXZVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 velocityDiff = targetVelocity - currentXZVelocity;

        // Apply acceleration force
        rb.AddForce(velocityDiff * 10f, ForceMode.Force);
    }

    void HandleRotation()
    {
        if (moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            );
        }
    }

    void TryJump()
    {
        // Check if enough time has passed since last jump
        if (Time.time - lastJumpTime < jumpCooldown)
            return;

        // Check if we can jump (grounded or air jump allowed)
        if (!rayDidHit && !canJumpInAir)
            return;

        // Apply jump force
        if (rb != null)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            lastJumpTime = Time.time;
        }
    }

    // Public getters
    public bool IsGrounded() => rayDidHit;
    public float GetGroundDistance() => rayDidHit ? rayHit.distance : raycastDistance;
    public bool IsMoving() => currentSpeed > 0.1f;
    public bool IsSprinting() => isSprinting && IsMoving();
}