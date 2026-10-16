using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class HandGrabHandler : MonoBehaviour
{
    [Header("Grab")]
    [SerializeField] float grabRadius = 0.9f;
    [SerializeField] float maxGrabMass = 10f;
    [SerializeField] float breakDistance = 2.5f;

    [Header("Magnet")]
    //How fast the object closes the distance to the hand, and how quickly its velocity follows that wish
    [SerializeField] float pullGain = 10f;
    [SerializeField] float pullResponse = 20f;
    [SerializeField] float maxPullSpeed = 12f;
    [SerializeField] float maxAcceleration = 150f;
    [SerializeField] float angularDamping = 8f;

    [Header("Release")]
    //After letting go the object ignores our own body until it is this far away or this much time has passed
    [SerializeField] float ignoreSelfTime = 0.4f;
    [SerializeField] float ignoreSelfDistance = 1.5f;
    [SerializeField] float ignoreSelfMaxTime = 2f;

    // Objects that are currently held by any hand
    static readonly HashSet<Rigidbody> heldBodies = new HashSet<Rigidbody>();

    // Used when predicting a throw
    static readonly RaycastHit[] trajectoryHits = new RaycastHit[8];

    // Our own rigidbody
    Rigidbody rigidbody3D;

    // The object we are holding
    Rigidbody heldBody;

    Collider[] heldColliders;

    // Used to blend the held object towards a fixed point while throwing
    Vector3 anchorPosition;
    float anchorBlend = 0f;

    // Objects that were let go of and still ignore our body
    struct ReleasedBody
    {
        public Rigidbody body;
        public Collider[] colliders;
        public float releaseTime;
    }

    List<ReleasedBody> releasedBodies = new List<ReleasedBody>();

    Collider[] overlapBuffer = new Collider[16];
    Collider[] playerColliders;

    // References
    NetworkPlayer networkPlayer;

    public SyncPhysicsObject Arm { get; private set; }
    public Rigidbody HeldBody => heldBody;
    public bool IsHolding => heldBody != null;

    void Awake()
    {
        // Get references
        networkPlayer = GetComponentInParent<NetworkPlayer>();
        rigidbody3D = GetComponent<Rigidbody>();
        Arm = GetComponentInParent<SyncPhysicsObject>();

        playerColliders = networkPlayer.GetComponentsInChildren<Collider>(true);
    }

    void OnDestroy()
    {
        if(heldBody != null)
            heldBodies.Remove(heldBody);

        // Do not leave collisions disabled on objects that outlive us
        for (int i = 0; i < releasedBodies.Count; i++)
            SetIgnoreSelf(releasedBodies[i].colliders, false);

        if(heldColliders != null)
            SetIgnoreSelf(heldColliders, false);
    }

    // Finds the closest object in reach, returns the distance from the hand to it
    public float FindGrabbable(out Rigidbody closestBody)
    {
        closestBody = null;

        float closestDistance = float.MaxValue;

        int numberOfColliders = Physics.OverlapSphereNonAlloc(transform.position, grabRadius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < numberOfColliders; i++)
        {
            Rigidbody otherBody = overlapBuffer[i].attachedRigidbody;

            if(!IsGrabbable(otherBody))
                continue;

            float distance = Vector3.Distance(GetClosestPoint(overlapBuffer[i]), transform.position);

            if(distance < closestDistance)
            {
                closestDistance = distance;
                closestBody = otherBody;
            }
        }

        return closestDistance;
    }

    bool IsGrabbable(Rigidbody otherBody)
    {
        if(otherBody == null || otherBody.isKinematic)
            return false;

        if(otherBody.mass > maxGrabMass)
            return false;

        if(heldBodies.Contains(otherBody))
            return false;

        // Never pick up a player
        if(otherBody.GetComponentInParent<NetworkPlayer>() != null)
            return false;

        // Only the host can move networked objects
        NetworkObject otherObject = otherBody.GetComponentInParent<NetworkObject>();

        return otherObject != null && otherObject.HasStateAuthority;
    }

    Vector3 GetClosestPoint(Collider otherCollider)
    {
        // ClosestPoint does not work on concave mesh colliders
        if(otherCollider is MeshCollider meshCollider && !meshCollider.convex)
            return otherCollider.bounds.ClosestPoint(transform.position);

        return otherCollider.ClosestPoint(transform.position);
    }

    public void Grab(Rigidbody bodyToGrab)
    {
        if(IsHolding)
            return;

        heldBody = bodyToGrab;
        heldColliders = heldBody.GetComponentsInChildren<Collider>();

        heldBodies.Add(heldBody);

        // The object must never push or shake our own body while we carry it
        SetIgnoreSelf(heldColliders, true);

        anchorBlend = 0f;

        heldBody.WakeUp();
    }

    // Lets go of the object, it keeps its current velocity
    public void Release()
    {
        if(!IsHolding)
            return;

        heldBodies.Remove(heldBody);

        // Collisions with our body come back once the object is clear of us
        ReleasedBody releasedBody = new ReleasedBody();
        releasedBody.body = heldBody;
        releasedBody.colliders = heldColliders;
        releasedBody.releaseTime = Time.time;

        releasedBodies.Add(releasedBody);

        heldBody = null;
        heldColliders = null;
        anchorBlend = 0f;
    }

    // Lets go of the object and launches it
    public void Throw(Vector3 velocity, Vector3 angularVelocity)
    {
        if(!IsHolding)
            return;

        Rigidbody thrownBody = heldBody;

        Release();

        thrownBody.WakeUp();
        thrownBody.linearVelocity = velocity;
        thrownBody.angularVelocity = angularVelocity;
    }

    // Pulls the held object towards a fixed point instead of the hand, blend 0 is the hand and 1 is the point
    public void SetAnchor(Vector3 position, float blend)
    {
        anchorPosition = position;
        anchorBlend = blend;
    }

    // Called every network tick on the host
    public void Tick(float deltaTime)
    {
        UpdateReleasedBodies();

        if(!IsHolding)
            return;

        Vector3 handPosition = transform.position;

        // The hand got dragged away from the object, for example stuck behind a wall
        if(Vector3.Distance(handPosition, heldBody.worldCenterOfMass) > breakDistance)
        {
            Release();
            return;
        }

        Vector3 targetPosition = Vector3.Lerp(handPosition, anchorPosition, anchorBlend);

        // Magnet, the object wants to move towards the hand with a capped speed and follows the speed of the hand
        // Forces are only applied to the object, so our body is never pushed back
        Vector3 desiredVelocity = Vector3.ClampMagnitude((targetPosition - heldBody.worldCenterOfMass) * pullGain, maxPullSpeed) + rigidbody3D.linearVelocity;

        Vector3 acceleration = (desiredVelocity - heldBody.linearVelocity) * pullResponse;

        // Cancel gravity so the object does not sag, gravity stays enabled on the rigidbody
        acceleration -= Physics.gravity;

        acceleration = Vector3.ClampMagnitude(acceleration, maxAcceleration);

        heldBody.AddForce(acceleration, ForceMode.Acceleration);

        // Calm down any spinning
        heldBody.AddTorque(-heldBody.angularVelocity * angularDamping, ForceMode.Acceleration);
    }

    void UpdateReleasedBodies()
    {
        for (int i = releasedBodies.Count - 1; i >= 0; i--)
        {
            ReleasedBody releasedBody = releasedBodies[i];

            // Destroyed or picked up again, nothing to restore
            if(releasedBody.body == null || heldBodies.Contains(releasedBody.body))
            {
                releasedBodies.RemoveAt(i);
                continue;
            }

            float timeSinceRelease = Time.time - releasedBody.releaseTime;

            bool isClear = timeSinceRelease > ignoreSelfTime && Vector3.Distance(releasedBody.body.worldCenterOfMass, networkPlayer.transform.position) > ignoreSelfDistance;

            if(isClear || timeSinceRelease > ignoreSelfMaxTime)
            {
                SetIgnoreSelf(releasedBody.colliders, false);
                releasedBodies.RemoveAt(i);
            }
        }
    }

    void SetIgnoreSelf(Collider[] otherColliders, bool ignore)
    {
        for (int i = 0; i < otherColliders.Length; i++)
        {
            if(otherColliders[i] == null)
                continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                if(playerColliders[j] != null)
                    Physics.IgnoreCollision(otherColliders[i], playerColliders[j], ignore);
            }
        }
    }

    // Simulates a throw with the same steps as the physics engine, returns the number of points written
    public static int PredictTrajectory(Vector3 origin, Vector3 velocity, float drag, Vector3[] points, Transform ignoreRoot, Rigidbody ignoreBody)
    {
        float timeStep = Time.fixedDeltaTime;

        // One point every few physics steps keeps the line smooth and cheap
        const int stepsPerPoint = 2;
        const int maxSteps = 150;

        int numberOfPoints = 0;

        points[numberOfPoints++] = origin;

        Vector3 position = origin;

        for (int step = 1; step <= maxSteps; step++)
        {
            velocity += Physics.gravity * timeStep;
            velocity *= 1f / (1f + drag * timeStep);

            Vector3 nextPosition = position + velocity * timeStep;

            Vector3 movement = nextPosition - position;

            int numberOfHits = Physics.RaycastNonAlloc(position, movement.normalized, trajectoryHits, movement.magnitude, ~0, QueryTriggerInteraction.Ignore);

            float closestHit = float.MaxValue;

            for (int i = 0; i < numberOfHits; i++)
            {
                // Ignore ourselves and the thing we throw
                if(ignoreRoot != null && trajectoryHits[i].collider.transform.IsChildOf(ignoreRoot))
                    continue;

                if(ignoreBody != null && trajectoryHits[i].rigidbody == ignoreBody)
                    continue;

                closestHit = Mathf.Min(closestHit, trajectoryHits[i].distance);
            }

            if(closestHit < float.MaxValue)
            {
                points[numberOfPoints++] = position + movement.normalized * closestHit;
                break;
            }

            position = nextPosition;

            if(step % stepsPerPoint == 0 || numberOfPoints == points.Length - 1)
                points[numberOfPoints++] = position;

            if(numberOfPoints >= points.Length)
                break;
        }

        return numberOfPoints;
    }
}
