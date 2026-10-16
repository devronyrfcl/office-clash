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

    public bool IsActiveRagdoll  => !isKnockedDown;

    [SerializeField] float minRagdollTime = 3f;

    //Set from Update and cleared when the input is sent, so short taps between ticks are not lost
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

    //Knockdown state, decided by the host
    [Networked] public NetworkBool isKnockedDown { get; set; }
    [Networked] public TickTimer getUpTimer { get; set; }

    //Buttons from the last input, used to find which ones were just pressed
    [Networked] NetworkButtons previousButtons { get; set; }

    //Only used by the host
    bool hasReleasedThrow = false;
    float swingTimer = 0f;
    float windupSign = 1f;
    Vector3 swingAnchorLocal = Vector3.zero;
    Vector3 swingThrowVelocity = Vector3.zero;

    float startSlerpPositionSpring = 0.0f;

    
    
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

        isJumpButtonPressed |= Input.GetKey(KeyCode.Space);
        isReviveButtonPressed |= Input.GetKey(KeyCode.R);
        isGrabButtonPressed |= Input.GetKey(KeyCode.G);
        isThrowButtonHeld |= Input.GetKey(KeyCode.F);

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

        //Without input nothing is pressed and held buttons stay as they were
        NetworkButtons buttons = hasInput ? networkInputData.buttons : previousButtons;
        NetworkButtons pressedButtons = buttons.GetPressed(previousButtons);

        previousButtons = buttons;

        if(hasInput)
        {
            float inputMagnitude = networkInputData.movementInput.magnitude;

            if(!isKnockedDown)
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
                if (pressedButtons.IsSet(InputButton.Jump) && isGrounded)
                {
                    rigidbody3D.AddForce(transform.up * 20f, ForceMode.Impulse);
                }
            }
            else
            {
                if(pressedButtons.IsSet(InputButton.Revive) && getUpTimer.ExpiredOrNotRunning(Runner))
                {
                    MakeActiveRagdoll();
                }
                
            }

            

            
        }

        if(Object.HasStateAuthority)
        {
            networkedMovementSpeed = localForwardVelocity * 0.4f;

            UpdateGrab(pressedButtons.IsSet(InputButton.Grab), buttons.IsSet(InputButton.Throw), pressedButtons.IsSet(InputButton.Throw));

            //update the joint rotation based on the animation
            for (int i = 0; i < syncPhysicsObjects.Length; i++)
            {
                syncPhysicsObjects[i].UpdateJointAnimation();
                networkPhysicsSyncedRotations.Set(i, syncPhysicsObjects[i].transform.localRotation);
            }

            if(transform.position.y < -10f)
            {
                Transform spawnPoint = SpawnPoint.PickRandom();

                if(spawnPoint != null)
                    networkRigidbody3D.Teleport(spawnPoint.position, spawnPoint.rotation);
                else
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

        //Also check the keys right now, otherwise a held key looks released when there are more ticks than frames
        networkInputData.buttons.Set(InputButton.Jump, isJumpButtonPressed || Input.GetKey(KeyCode.Space));
        networkInputData.buttons.Set(InputButton.Revive, isReviveButtonPressed || Input.GetKey(KeyCode.R));
        networkInputData.buttons.Set(InputButton.Grab, isGrabButtonPressed || Input.GetKey(KeyCode.G));
        networkInputData.buttons.Set(InputButton.Throw, isThrowButtonHeld || Input.GetKey(KeyCode.F));

        isJumpButtonPressed = false;
        isReviveButtonPressed = false;
        isGrabButtonPressed = false;
        isThrowButtonHeld = false;

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

        getUpTimer = TickTimer.CreateFromSeconds(Runner, minRagdollTime);

        isKnockedDown = true;
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

        getUpTimer = TickTimer.None;

        isKnockedDown = false;
    }

    /*void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("CauseDamage"))
            MakeRegdoll();

        
    }*/

    HandGrabHandler HeldHand => handGrabHandlers[heldHandIndex];

    public bool IsChargingThrow => grabState == GrabState.Charging;

    void UpdateGrab(bool isGrabPressed, bool isThrowHeld, bool isThrowStarted)
    {
        switch (grabState)
        {
            case GrabState.None:
                if(isGrabPressed && !isKnockedDown)
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
