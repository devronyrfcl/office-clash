using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ShootSystem : MonoBehaviour
{
    [Header("Projectile Prefabs")]
    [SerializeField] private GameObject bigProjectilePrefab;
    [SerializeField] private GameObject mediumProjectilePrefab;
    [SerializeField] private GameObject smallProjectilePrefab;

    [Header("Projectile Inventory Limits")]
    [SerializeField] private int maxBig = 5;
    [SerializeField] private int maxMedium = 10;
    [SerializeField] private int maxSmall = 15;

    [Header("Projectile Inventory Count (Debug)")]
    [SerializeField] private int currentBig;
    [SerializeField] private int currentMedium;
    [SerializeField] private int currentSmall;

    [Header("Projectile Settings")]
    [SerializeField] private Transform shootPoint;
    [SerializeField] private float minShootForce = 5f;
    [SerializeField] private float maxShootForce = 25f;
    [SerializeField] private float chargeSpeed = 2f;
    [SerializeField] private float projectileLifetime = 5f;
    [SerializeField] private float colliderEnableDelay = 0.2f;

    [Header("Trajectory Line Settings")]
    [SerializeField] private LineRenderer trajectoryLine;
    [SerializeField] private int trajectoryPointCount = 30;
    [SerializeField] private float trajectoryTimeStep = 0.1f;
    [SerializeField] private float maxTrajectoryTime = 3f;

    [Header("Trajectory Line Color Transition")]
    [SerializeField] private Color startLineColor = Color.yellow;
    [SerializeField] private Color endLineColor = Color.blue;
    [SerializeField] private float lineColorTransitionDuration = 2f;
    [SerializeField, Range(0f, 1f)] private float trajectoryEndAlpha = 0.3f;

    [Header("Input Settings")]
    [SerializeField] private KeyCode bigShootKey = KeyCode.Return;
    [SerializeField] private KeyCode mediumShootKey = KeyCode.RightBracket; // "]"
    [SerializeField] private KeyCode smallShootKey = KeyCode.LeftBracket;   // "["

    [Header("UI Settings")]
    [SerializeField] private Image forceBarFillImage;
    [SerializeField] private GameObject forceBarContainer;

    [Header("Angle Settings")]
    [SerializeField] private float shootAngle = 45f;

    private bool isCharging = false;
    private Vector3 shootDirection;
    private float currentChargeForce = 0f;
    private float chargeStartTime = 0f;

    private GameObject currentPrefab; // prefab being charged
    private int currentInventory = 0; // inventory of the prefab being charged

    private void Start()
    {
        currentBig = maxBig;
        currentMedium = maxMedium;
        currentSmall = maxSmall;

        if (trajectoryLine == null)
            trajectoryLine = gameObject.AddComponent<LineRenderer>();

        trajectoryLine.enabled = false;
        trajectoryLine.positionCount = trajectoryPointCount;
        trajectoryLine.startWidth = 0.1f;
        trajectoryLine.endWidth = 0.05f;
        trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
        trajectoryLine.startColor = startLineColor;
        trajectoryLine.endColor = new Color(startLineColor.r, startLineColor.g, startLineColor.b, trajectoryEndAlpha);

        if (forceBarContainer != null)
            forceBarContainer.SetActive(false);

        UpdateForceBarUI(0f);
    }

    private void Update()
    {
        HandleInput();

        if (isCharging)
        {
            UpdateChargeForce();
            UpdateTrajectoryLineColor();
        }
    }

    private void HandleInput()
    {
        // Determine which projectile to shoot
        if (Input.GetKeyDown(bigShootKey)) SetupCharge(bigProjectilePrefab, ref currentBig);
        else if (Input.GetKeyDown(mediumShootKey)) SetupCharge(mediumProjectilePrefab, ref currentMedium);
        else if (Input.GetKeyDown(smallShootKey)) SetupCharge(smallProjectilePrefab, ref currentSmall);

        if (!isCharging) return;

        // Cancel throw
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            CancelThrow();
            return;
        }

        // Update trajectory
        if (Input.GetKey(currentPrefab == bigProjectilePrefab ? bigShootKey :
                         currentPrefab == mediumProjectilePrefab ? mediumShootKey :
                         smallShootKey))
        {
            UpdateTrajectory();
        }

        // Shoot
        if (Input.GetKeyUp(currentPrefab == bigProjectilePrefab ? bigShootKey :
                           currentPrefab == mediumProjectilePrefab ? mediumShootKey :
                           smallShootKey))
        {
            Shoot();
        }
    }

    private void SetupCharge(GameObject prefab, ref int inventory)
    {
        if (inventory <= 0) return;

        isCharging = true;
        trajectoryLine.enabled = true;
        chargeStartTime = Time.time;
        currentChargeForce = minShootForce;

        currentPrefab = prefab;
        currentInventory = inventory;

        if (forceBarContainer != null)
            forceBarContainer.SetActive(true);

        UpdateTrajectoryLineColor();
    }

    private void Shoot()
    {
        if (!isCharging || currentPrefab == null) return;

        isCharging = false;
        trajectoryLine.enabled = false;
        if (forceBarContainer != null) forceBarContainer.SetActive(false);

        CalculateShootDirection();

        Vector3 spawnPos = shootPoint != null ? shootPoint.position : transform.position;
        GameObject projectile = Instantiate(currentPrefab, spawnPos, Quaternion.LookRotation(shootDirection));

        Collider projectileCollider = projectile.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
            StartCoroutine(EnableColliderAfterDelay(projectileCollider, colliderEnableDelay));
        }

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb == null) rb = projectile.AddComponent<Rigidbody>();
        rb.linearVelocity = shootDirection * currentChargeForce;

        Destroy(projectile, projectileLifetime);

        // Deduct inventory
        currentInventory--;
        if (currentPrefab == bigProjectilePrefab) currentBig = currentInventory;
        else if (currentPrefab == mediumProjectilePrefab) currentMedium = currentInventory;
        else if (currentPrefab == smallProjectilePrefab) currentSmall = currentInventory;

        currentChargeForce = minShootForce;
    }

    public void AddProjectile(GameObject prefab, int amount)
    {
        if (prefab == bigProjectilePrefab)
            currentBig = Mathf.Min(currentBig + amount, maxBig);
        else if (prefab == mediumProjectilePrefab)
            currentMedium = Mathf.Min(currentMedium + amount, maxMedium);
        else if (prefab == smallProjectilePrefab)
            currentSmall = Mathf.Min(currentSmall + amount, maxSmall);
    }

    private void CancelThrow()
    {
        if (!isCharging) return;
        isCharging = false;
        trajectoryLine.enabled = false;
        if (forceBarContainer != null) forceBarContainer.SetActive(false);
        currentChargeForce = minShootForce;
        currentPrefab = null;
    }

    private void CalculateShootDirection()
    {
        Vector3 forwardDir = transform.forward;
        float angleRad = shootAngle * Mathf.Deg2Rad;
        Vector3 horizontalDir = new Vector3(forwardDir.x, 0f, forwardDir.z).normalized;
        shootDirection = (horizontalDir * Mathf.Cos(angleRad) + Vector3.up * Mathf.Sin(angleRad)).normalized;
    }

    private void UpdateTrajectory()
    {
        if (shootPoint == null || currentPrefab == null) return;

        CalculateShootDirection();
        Vector3 startPos = shootPoint.position;
        Vector3 velocity = shootDirection * currentChargeForce;

        for (int i = 0; i < trajectoryPointCount; i++)
        {
            float t = i * trajectoryTimeStep;
            if (t > maxTrajectoryTime)
            {
                Vector3 lastPos = trajectoryLine.GetPosition(i - 1);
                for (int j = i; j < trajectoryPointCount; j++)
                    trajectoryLine.SetPosition(j, lastPos);
                break;
            }
            Vector3 point = startPos + velocity * t + 0.5f * Physics.gravity * t * t;
            trajectoryLine.SetPosition(i, point);
        }
    }

    private void UpdateChargeForce()
    {
        float chargeTime = Time.time - chargeStartTime;
        currentChargeForce = Mathf.Lerp(minShootForce, maxShootForce, chargeTime * chargeSpeed);
        currentChargeForce = Mathf.Min(currentChargeForce, maxShootForce);

        float fillAmount = (currentChargeForce - minShootForce) / (maxShootForce - minShootForce);
        UpdateForceBarUI(fillAmount);
    }

    private void UpdateForceBarUI(float fillAmount)
    {
        if (forceBarFillImage != null)
            forceBarFillImage.fillAmount = fillAmount;
    }

    private void UpdateTrajectoryLineColor()
    {
        float t = Mathf.Clamp01((Time.time - chargeStartTime) / lineColorTransitionDuration);
        Color current = Color.Lerp(startLineColor, endLineColor, t);
        trajectoryLine.startColor = current;
        trajectoryLine.endColor = new Color(current.r, current.g, current.b, trajectoryEndAlpha);
    }

    private IEnumerator EnableColliderAfterDelay(Collider col, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (col != null) col.enabled = true;
    }
}
