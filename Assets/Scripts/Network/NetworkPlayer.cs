using UnityEngine;
using System.Collections;
using Fusion;
using Cinemachine;
using Fusion.Addons.Physics;

public enum GrabState : byte
{
    None,
    Holding,
    Charging,
    Swinging
}

public class NetworkPlayer : NetworkBehaviour, IPlayerLeft
{

    public static NetworkPlayer Local { get; set; }
    
    [SerializeField] Rigidbody rigidbody3D;

    [SerializeField] NetworkRigidbody3D networkRigidbody3D;

    [SerializeField] ConfigurableJoint mainJoint;

    Vector2 moveInputVector = Vector2.zero;

    public float maxSpeed = 2f;

    bool isGrounded = false;
    bool isActiveRagdoll = true;

    public bool IsActiveRagdoll  => isActiveRagdoll;
    bool isJumpButtonPressed = false;
    bool isReviveButtonPressed = false;
    bool isGrabButtonPressed = false;
    bool isThrowButtonHeld = false;

    RaycastHit[] raycastHits = new RaycastHit[10];

    [SerializeField] private SyncPhysicsObject[] syncPhysicsObjects;

    [SerializeField] private Animator animator;

    [Header("Grab and throw")]
    [SerializeField] private HandGrabHandler[] handGrabHandlers;

    [SerializeField] float minThrowSpeed = 4f;
    [SerializeField] float maxThrowSpeed = 16f;
    [SerializeField] float throwChargeTime = 1.2f;
    [SerializeField] float throwPitch = 30f;

    //Arm swing, angles in degrees and times in seconds
    [SerializeField] float windupAngle = 100f;
    [SerializeField] float followThroughAngle = 80f;
    [SerializeField] float windupTime = 0.2f;
    [SerializeField] float swingTime = 0.14f;
    [SerializeField] float releasePoint = 0.6f;
    [SerializeField] float followThroughTime = 0.15f;
    [SerializeField] float swingSpring = 400f;
    [SerializeField] float swingDamper = 40f;
    [SerializeField] float swingMaxAngularVelocity = 40f;

    //Cinemachine
    CinemachineVirtualCamera cinemachineVirtualCamera;

    CinemachineBrain cinemachineBrain;

    //Networked array to store the rotations of the sync physics objects
    [Networked, Capacity(10)] public NetworkArray<Quaternion> networkPhysicsSyncedRotations { get; }

    [Networked] public float networkedMovementSpeed { get; set; }

    //Grab and throw state, decided by the host
    [Networked] public GrabState grabState { get; set; }
    [Networked] public float throwCharge { get; set; }
    [Networked] public byte heldHandIndex { get; set; }
    [Networked] public NetworkId heldObjectId { get; set; }

    //Only used by the host
    bool wasThrowHeld = false;
    bool hasReleasedThrow = false;
    float swingTimer = 0f;
    float windupSign = 1f;
    Vector3 swingAnchorLocal = Vector3.zero;
    Vector3 swingThrowVelocity = Vector3.zero;

    float startSlerpPositionSpring = 0.0f;
    float lastTimeBecameRagdoll = 0.0f;

    
    
    void Start()
    {
        startSlerpPositionSpring = mainJoint.slerpDrive.positionSpring;
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        //seach for SyncPhysicsObjects in children and add them to the list
        if(syncPhysicsObjects.Length == 0)
            syncPhysicsObjects = GetComponentsInChildren<SyncPhysicsObject>();

        if(handGrabHandlers.Length == 0)
            handGrabHandlers = GetComponentsInChildren<HandGrabHandler>();


    }

    // Update is called once per frame
    void Update()
    {
        //move input
        moveInputVector.x = Input.GetAxis("Horizontal");
        moveInputVector.y = Input.GetAxis("Vertical");

        if(Input.GetKeyDown(KeyCode.Space))
                isJumpButtonPressed = true;

        if(Input.GetKeyDown(KeyCode.R))
                isReviveButtonPressed = true;

        if(Input.GetKeyDown(KeyCode.G))
                isGrabButtonPressed = true;

        isThrowButtonHeld = Input.GetKey(KeyCode.F);

    }


    public override void FixedUpdateNetwork()
    {
        
        Vector3 localVelocityForward = Vector3.zero;
        float localForwardVelocity = 0f;
        
        if(Object.HasInputAuthority || Object.HasStateAuthority)
        {
            //Assume that we are not grounded
            isGrounded = false;

            //Check if we are grounded
            int numberOfHits = Physics.SphereCastNonAlloc(transform.position, 0.1f, transform.up * -1f, raycastHits, 0.5f);

            for (int i = 0; i < numberOfHits; i++)
            {
                //ignore self hits
                if (raycastHits[i].collider.gameObject == gameObject)
                    continue;

                isGrounded = true;
                break;
            }

            //apply extra gravity to character to make it less floaty
            if(!isGrounded)
                rigidbody3D.AddForce(Vector3.down * 10f);

            
            localVelocityForward = transform.forward * Vector3.Dot(transform.forward, rigidbody3D.linearVelocity);
            localForwardVelocity = localVelocityForward.magnitude;

            
        }
        
        bool hasInput = GetInput(out NetworkInputData networkInputData);

        if(hasInput)
        {
            float inputMagnitude = networkInputData.movementInput.magnitude;

            if(isActiveRagdoll)
            {
                if (inputMagnitude != 0f)
                {
                    Quaternion desireDirection = Quaternion.LookRotation(new Vector3(networkInputData.movementInput.x, 0f, networkInputData.movementInput.y * -1f), transform.up);

                    mainJoint.targetRotation = Quaternion.RotateTowards(mainJoint.targetRotation, desireDirection, Runner.DeltaTime * 300f);

                    

                    if (localForwardVelocity < maxSpeed)
                    {
                        rigidbody3D.AddForce(transform.forward * inputMagnitude * 30f);
                    }

                    
                }
                if (networkInputData.isJumpPressed && isGrounded)
                {
                    rigidbody3D.AddForce(transform.up * 20f, ForceMode.Impulse);
                    isJumpButtonPressed = false;
                }
            }
            else
            {
                if(networkInputData.isRevivePressed && Runner.SimulationTime - lastTimeBecameRagdoll > 3f)
                {
                    MakeActiveRagdoll();
                    isReviveButtonPressed = false;
                }
                
            }

            

            
        }

        if(Object.HasStateAuthority)
        {
            networkedMovementSpeed = localForwardVelocity * 0.4f;

            UpdateGrab(hasInput, networkInputData);

            //update the joint rotation based on the animation
            for (int i = 0; i < syncPhysicsObjects.Length; i++)
            {
                syncPhysicsObjects[i].UpdateJointAnimation();
                networkPhysicsSyncedRotations.Set(i, syncPhysicsObjects[i].transform.localRotation);
            }

            if(transform.position.y < -10f)
            {
                networkRigidbody3D.Teleport(Vector3.zero, Quaternion.identity);
                MakeActiveRagdoll();
            }
                
        }
    }

    public override void Render()
    {
        animator.SetFloat("movementSpeed", networkedMovementSpeed);

        bool isCarrying = grabState == GrabState.Holding || grabState == GrabState.Charging;

        animator.SetBool("isGrabbing", isCarrying);
        animator.SetBool("isCarrying", isCarrying);

        if(!Object.HasStateAuthority)
        {
            var interpolated = new NetworkBehaviourBufferInterpolator(this);

            //Get the networked physics object from the host and updae the clients
            for(int i = 0; i < syncPhysicsObjects.Length; i++)
            {
                syncPhysicsObjects[i].transform.localRotation = Quaternion.Slerp(syncPhysicsObjects[i].transform.localRotation, networkPhysicsSyncedRotations.Get(i), interpolated.Alpha);
            }
        }

        if (Object.HasInputAuthority)
        {
            cinemachineBrain.ManualUpdate();
            cinemachineVirtualCamera.UpdateCameraState(Vector3.up, Runner.LocalAlpha);
        }
    }

    public NetworkInputData GetNetworkInput()
    {
        NetworkInputData networkInputData = new NetworkInputData();

        networkInputData.movementInput = moveInputVector;

        if(isJumpButtonPressed)
            networkInputData.isJumpPressed = true;

        if(isReviveButtonPressed)
            networkInputData.isRevivePressed = true;

        if(isGrabButtonPressed)
            networkInputData.isGrabPressed = true;

        networkInputData.isThrowHeld = isThrowButtonHeld;

        isJumpButtonPressed = false;
        isReviveButtonPressed = false;
        isGrabButtonPressed = false;

        return networkInputData;
    }

    void MakeRegdoll()
    {
        if(!Object.HasStateAuthority)
            return;

        DropHeldObject();
           
        //Update the joints rotation and send them to clients
        JointDrive jointDrive = mainJoint.slerpDrive;
        jointDrive.positionSpring = 0f;
        mainJoint.slerpDrive = jointDrive;

        for (int i = 0; i < syncPhysicsObjects.Length; i++)
        {
            syncPhysicsObjects[i].MakeRagdoll();
            
        }

        lastTimeBecameRagdoll = Runner.SimulationTime;
        
        isActiveRagdoll = false; 
    }

    public void OnPlayerBodyPartHit()
    {
        if(!IsActiveRagdoll)
            return;

        MakeRegdoll();
    }

    void MakeActiveRagdoll()
    {
        if(!Object.HasStateAuthority)
            return;

        DropHeldObject();

        
        //Update Main Joint Drive to the original value
        JointDrive jointDrive = mainJoint.slerpDrive;
        jointDrive.positionSpring = startSlerpPositionSpring;
        mainJoint.slerpDrive = jointDrive; 

        //Update the joints rotation and send them to clients
        for (int i = 0; i < syncPhysicsObjects.Length; i++)
        {
            syncPhysicsObjects[i].MakeActiveRagdoll();
            
        }

        isActiveRagdoll = true; 
    }

    /*void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("CauseDamage"))
            MakeRegdoll();

        
    }*/

    HandGrabHandler HeldHand => handGrabHandlers[heldHandIndex];

    public bool IsChargingThrow => grabState == GrabState.Charging;

    void UpdateGrab(bool hasInput, NetworkInputData networkInputData)
    {
        //Without input the grab button is not pressed and the throw button stays as it was
        bool isGrabPressed = hasInput && networkInputData.isGrabPressed;
        bool isThrowHeld = hasInput ? (bool)networkInputData.isThrowHeld : wasThrowHeld;

        bool isThrowStarted = isThrowHeld && !wasThrowHeld;

        wasThrowHeld = isThrowHeld;

        switch (grabState)
        {
            case GrabState.None:
                if(isGrabPressed && isActiveRagdoll)
                    TryGrabObject();
                break;

            case GrabState.Holding:
                //The object got lost, for example the hand was pulled away from it
                if(!HeldHand.IsHolding || isGrabPressed)
                    DropHeldObject();
                else if(isThrowStarted)
                {
                    grabState = GrabState.Charging;
                    throwCharge = 0f;
                }
                break;

            case GrabState.Charging:
                if(!HeldHand.IsHolding || isGrabPressed)
                {
                    DropHeldObject();
                    break;
                }

                throwCharge = Mathf.Min(1f, throwCharge + Runner.DeltaTime / throwChargeTime);

                if(!isThrowHeld)
                    StartThrowSwing();
                break;

            case GrabState.Swinging:
                UpdateThrowSwing(Runner.DeltaTime);
                break;
        }

        for (int i = 0; i < handGrabHandlers.Length; i++)
        {
            handGrabHandlers[i].Tick(Runner.DeltaTime);
        }
    }

    void TryGrabObject()
    {
        //Use the hand that is closest to an object
        int closestHandIndex = -1;
        float closestDistance = float.MaxValue;
        Rigidbody closestBody = null;

        for (int i = 0; i < handGrabHandlers.Length; i++)
        {
            float distance = handGrabHandlers[i].FindGrabbable(out Rigidbody body);

            if(body != null && distance < closestDistance)
            {
                closestHandIndex = i;
                closestDistance = distance;
                closestBody = body;
            }
        }

        if(closestBody == null)
            return;

        handGrabHandlers[closestHandIndex].Grab(closestBody);

        heldHandIndex = (byte)closestHandIndex;
        heldObjectId = closestBody.GetComponentInParent<NetworkObject>().Id;
        throwCharge = 0f;
        grabState = GrabState.Holding;
    }

    //Lets go of whatever we hold and gives the arm back to the ragdoll
    void DropHeldObject()
    {
        if(grabState == GrabState.None)
            return;

        HandGrabHandler hand = HeldHand;

        hand.Release();
        hand.Arm.EndSwing();

        ResetGrabState();
    }

    void ResetGrabState()
    {
        grabState = GrabState.None;
        throwCharge = 0f;
        heldObjectId = default;
    }

    Vector3 GetFlatForward()
    {
        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;

        return flatForward.sqrMagnitude > 0.001f ? flatForward.normalized : Vector3.forward;
    }

    Vector3 GetThrowVelocity(float charge)
    {
        Vector3 flatForward = GetFlatForward();
        Vector3 right = Vector3.Cross(Vector3.up, flatForward);

        //Tilt the direction up
        Vector3 direction = Quaternion.AngleAxis(-throwPitch, right) * flatForward;

        return direction * Mathf.Lerp(minThrowSpeed, maxThrowSpeed, charge);
    }

    //Used for the line the player sees while charging
    public int GetThrowPreview(Vector3[] points)
    {
        if(grabState != GrabState.Charging)
            return 0;

        Rigidbody heldBody = null;
        float drag = 0f;

        if(Runner.TryFindObject(heldObjectId, out NetworkObject heldObject) && heldObject.TryGetComponent(out heldBody))
            drag = heldBody.linearDamping;

        return HandGrabHandler.PredictTrajectory(HeldHand.transform.position, GetThrowVelocity(throwCharge), drag, points, transform, heldBody);
    }

    void StartThrowSwing()
    {
        HandGrabHandler hand = HeldHand;

        grabState = GrabState.Swinging;

        swingTimer = 0f;
        hasReleasedThrow = false;

        //Where the object will leave from and how fast, this is the same as the line the player saw
        swingAnchorLocal = transform.InverseTransformPoint(hand.transform.position);
        swingThrowVelocity = GetThrowVelocity(throwCharge);

        //Wind up away from the throwing direction, no matter how the arm hangs
        Vector3 armVector = hand.transform.position - hand.Arm.transform.position;
        Vector3 right = Vector3.Cross(Vector3.up, GetFlatForward());

        float forwardBefore = Vector3.Dot(armVector, GetFlatForward());
        float forwardAfter = Vector3.Dot(Quaternion.AngleAxis(windupAngle, right) * armVector, GetFlatForward());

        windupSign = forwardAfter < forwardBefore ? 1f : -1f;

        hand.Arm.BeginSwing(swingSpring, swingDamper, swingMaxAngularVelocity);
    }

    void UpdateThrowSwing(float deltaTime)
    {
        HandGrabHandler hand = HeldHand;

        swingTimer += deltaTime;

        float releaseTime = windupTime + swingTime * releasePoint;

        //Wind up slowly, then swing quickly with an accelerating arm
        float angle;

        if(swingTimer < windupTime)
            angle = Mathf.Lerp(0f, windupAngle * windupSign, Mathf.SmoothStep(0f, 1f, swingTimer / windupTime));
        else
        {
            float swingProgress = Mathf.Clamp01((swingTimer - windupTime) / swingTime);

            angle = Mathf.Lerp(windupAngle * windupSign, -followThroughAngle * windupSign, swingProgress * swingProgress);
        }

        hand.Arm.SetSwingAngle(Vector3.Cross(Vector3.up, GetFlatForward()), angle);

        //The object settles onto the launch point while the arm swings past it
        hand.SetAnchor(transform.TransformPoint(swingAnchorLocal), Mathf.SmoothStep(0f, 1f, swingTimer / releaseTime));

        if(!hasReleasedThrow && swingTimer >= releaseTime)
        {
            hasReleasedThrow = true;

            Vector3 spin = Vector3.Cross(Vector3.up, GetFlatForward()) * -swingThrowVelocity.magnitude * 0.5f;

            hand.Throw(swingThrowVelocity, spin);
        }

        if(swingTimer >= windupTime + swingTime + followThroughTime)
        {
            //Relax the arm back to the floppy ragdoll
            hand.Arm.EndSwing();
            ResetGrabState();
        }
    }

    public override void Spawned()
    {
        if(Object.HasInputAuthority)
        {
            Local = this;

            cinemachineVirtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            cinemachineBrain = FindObjectOfType<CinemachineBrain>();

            cinemachineVirtualCamera.m_Follow = transform;
            cinemachineVirtualCamera.m_LookAt = transform;

            //Shows the throw line and power bar to the local player
            gameObject.AddComponent<ThrowAimUI>();

            Utils.DebugLog("Spawned Local Player with input authority");

        }
        else
        {
            Utils.DebugLog("Spawned Remote Player without input authority");
        }

        transform.name = $"P_{Object.Id}";
    }

    public void PlayerLeft(PlayerRef player)
    {
        if(Object.InputAuthority == player)
            Runner.Despawn(Object);
    }
}
