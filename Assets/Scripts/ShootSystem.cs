using UnityEngine;
using UnityEngine.UI;

public class ShootSystem : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private float minShootForce = 5f; // Minimum shoot force
    [SerializeField] private float maxShootForce = 25f; // Maximum shoot force
    [SerializeField] private float chargeSpeed = 2f; // How fast the force charges up
    [SerializeField] private float projectileLifetime = 5f;
    [SerializeField] private float colliderEnableDelay = 0.2f; // Delay before enabling collider
    
    [Header("Trajectory Line Settings")]
    [SerializeField] private LineRenderer trajectoryLine;
    [SerializeField] private int trajectoryPointCount = 30;
    [SerializeField] private float trajectoryTimeStep = 0.1f;
    [SerializeField] private float maxTrajectoryTime = 3f;
    
    [Header("Input Settings")]
    [SerializeField] private KeyCode shootKey = KeyCode.Return;
    
    [Header("UI Settings")]
    [SerializeField] private Image forceBarFillImage; // UI Image to show charge level
    [SerializeField] private GameObject forceBarContainer; // Container to show/hide the force bar
    
    [Header("Angle Settings")]
    [SerializeField] private float shootAngle = 45f; // Angle in degrees (upward angle)
    
    private bool isCharging = false;
    private Vector3 shootDirection;
    private TopDownController controller;
    private float currentChargeForce = 0f; // Current charged force
    private float chargeStartTime = 0f;

    private void Start()
    {
        // Get reference to the character controller
        controller = GetComponent<TopDownController>();
        
        // Setup trajectory line
        if (trajectoryLine == null)
        {
            trajectoryLine = gameObject.AddComponent<LineRenderer>();
        }
        
        trajectoryLine.enabled = false;
        trajectoryLine.positionCount = trajectoryPointCount;
        trajectoryLine.startWidth = 0.1f;
        trajectoryLine.endWidth = 0.05f;
        
        // Optional: Set line material/color
        trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
        trajectoryLine.startColor = Color.yellow;
        trajectoryLine.endColor = new Color(1f, 1f, 0f, 0.3f);
        
        // Hide force bar initially
        if (forceBarContainer != null)
        {
            forceBarContainer.SetActive(false);
        }
        
        // Initialize force bar
        UpdateForceBarUI(0f);
    }

    private void Update()
    {
        HandleInput();
        
        // Update charge force while charging
        if (isCharging)
        {
            UpdateChargeForce();
        }
    }

    private void HandleInput()
    {
        // When key is pressed down, start charging
        if (Input.GetKeyDown(shootKey))
        {
            StartCharging();
        }
        
        // While key is held, show trajectory
        if (Input.GetKey(shootKey))
        {
            UpdateTrajectory();
        }
        
        // When key is released, shoot
        if (Input.GetKeyUp(shootKey))
        {
            Shoot();
        }
    }

    private void StartCharging()
    {
        isCharging = true;
        trajectoryLine.enabled = true;
        chargeStartTime = Time.time;
        currentChargeForce = minShootForce;
        
        // Show force bar
        if (forceBarContainer != null)
        {
            forceBarContainer.SetActive(true);
        }
    }
    
    private void UpdateChargeForce()
    {
        // Calculate charge based on time held
        float chargeTime = Time.time - chargeStartTime;
        float chargeProgress = chargeTime * chargeSpeed;
        
        // Lerp between min and max force
        currentChargeForce = Mathf.Lerp(minShootForce, maxShootForce, chargeProgress);
        
        // Clamp to max force
        currentChargeForce = Mathf.Min(currentChargeForce, maxShootForce);
        
        // Update UI
        float fillAmount = (currentChargeForce - minShootForce) / (maxShootForce - minShootForce);
        UpdateForceBarUI(fillAmount);
    }
    
    private void UpdateForceBarUI(float fillAmount)
    {
        if (forceBarFillImage != null)
        {
            forceBarFillImage.fillAmount = fillAmount;
            
            // Optional: Change color based on charge level
            //forceBarFillImage.color = Color.Lerp(Color.yellow, Color.red, fillAmount);
        }
    }

    private void CalculateShootDirection()
    {
        // Get the character's forward direction (where they're facing)
        Vector3 forwardDir = transform.forward;
        
        // Calculate the shoot direction with the upward angle
        // Rotate the forward direction upward by shootAngle degrees
        float angleInRadians = shootAngle * Mathf.Deg2Rad;
        
        // Create a direction that combines forward movement with upward angle
        // Using the character's forward as the horizontal component
        Vector3 horizontalDir = new Vector3(forwardDir.x, 0f, forwardDir.z).normalized;
        
        // Combine horizontal direction with upward component based on angle
        shootDirection = (horizontalDir * Mathf.Cos(angleInRadians) + Vector3.up * Mathf.Sin(angleInRadians)).normalized;
    }

    private void UpdateTrajectory()
    {
        // Recalculate shoot direction every frame to follow character rotation
        CalculateShootDirection();
        
        Vector3 startPos = shootPoint != null ? shootPoint.position : transform.position;
        // Use current charge force for trajectory
        Vector3 velocity = shootDirection * currentChargeForce;
        
        for (int i = 0; i < trajectoryPointCount; i++)
        {
            float t = i * trajectoryTimeStep;
            
            // Stop calculating if time exceeds max
            if (t > maxTrajectoryTime)
            {
                // Set remaining points to the last valid position
                Vector3 lastPos = trajectoryLine.GetPosition(i - 1);
                for (int j = i; j < trajectoryPointCount; j++)
                {
                    trajectoryLine.SetPosition(j, lastPos);
                }
                break;
            }
            
            // Calculate position using physics formula: p = p0 + v*t + 0.5*g*t^2
            Vector3 point = startPos + velocity * t + 0.5f * Physics.gravity * t * t;
            trajectoryLine.SetPosition(i, point);
        }
    }

    private void Shoot()
    {
        if (!isCharging) return;
        
        isCharging = false;
        trajectoryLine.enabled = false;
        
        // Hide force bar
        if (forceBarContainer != null)
        {
            forceBarContainer.SetActive(false);
        }
        
        if (projectilePrefab == null)
        {
            Debug.LogWarning("Projectile prefab is not assigned!");
            return;
        }
        
        // Recalculate direction one final time before shooting
        CalculateShootDirection();
        
        // Spawn projectile
        Vector3 spawnPos = shootPoint != null ? shootPoint.position : transform.position;
        GameObject projectile = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(shootDirection));
        
        // Disable collider initially
        Collider projectileCollider = projectile.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
            // Enable collider after delay
            StartCoroutine(EnableColliderAfterDelay(projectileCollider, colliderEnableDelay));
        }
        
        // Add Rigidbody if it doesn't have one
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = projectile.AddComponent<Rigidbody>();
        }
        
        // Apply velocity using the charged force
        rb.linearVelocity = shootDirection * currentChargeForce;
        
        // Destroy projectile after lifetime
        Destroy(projectile, projectileLifetime);
        
        // Reset charge force
        currentChargeForce = minShootForce;
    }
    
    private System.Collections.IEnumerator EnableColliderAfterDelay(Collider collider, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Check if collider still exists (projectile might have been destroyed)
        if (collider != null)
        {
            collider.enabled = true;
        }
    }

    // Public methods to adjust angle at runtime
    public void SetShootAngle(float angle)
    {
        shootAngle = Mathf.Clamp(angle, 0f, 90f);
    }

    public void SetMaxShootForce(float force)
    {
        maxShootForce = Mathf.Max(minShootForce, force);
    }
    
    public float GetCurrentChargeForce()
    {
        return currentChargeForce;
    }

    private void OnDrawGizmos()
    {
        // Draw shoot direction in editor
        if (Application.isPlaying && shootPoint != null)
        {
            CalculateShootDirection();
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(shootPoint.position, shootDirection * 3f);
            
            // Draw character forward direction for reference
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
    }
}