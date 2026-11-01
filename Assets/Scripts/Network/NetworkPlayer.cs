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

    [Header("Health")]
    //Each press of the get up button shortens the time on the floor
    [SerializeField] float getUpTapTime = 0.3f;
    [SerializeField] float invulnerableAfterGetUp = 1f;
    [SerializeField] int fallDamage = 30;

    PlayerHealth playerHealth;
    PlayerInventory playerInventory;

    public HandGrabHandler[] Hands => handGrabHandlers;

    //Set from Update and cleared when the input is sent, so short taps between ticks are not lost
    bool isJumpButtonPressed = false;
    bool isReviveButtonPressed = false;
    bool isGrabButtonPressed = false;
    bool isThrowButtonHeld = false;
    bool isThrowSecondButtonHeld = false;
    bool isSwapPocket1Pressed = false;
    bool isSwapPocket2Pressed = false;

    RaycastHit[] raycastHits = new RaycastHit[10];

    [SerializeField] private SyncPhysicsObject[] syncPhysicsObjects;

    [SerializeField] private Animator animator;

    [Header("Grab and throw")]
    [SerializeField] private HandGrabHandler[] handGrabHandlers;

    [SerializeField] float minThrowSpeed = 4f;
    [SerializeField] float maxThrowSpeed = 16f;
    [SerializeField] float throwChargeTime = 1.2f;
    [SerializeField] float throwPitch = 30f;

    [Header("Aim")]
    //Without manual aim the throw turns towards the closest player in front of us
    [SerializeField] bool autoAim = true;
    [SerializeField] float autoAimAngle = 40f;
    [SerializeField] float autoAimRange = 18f;
    [SerializeField] float aimTurnSpeed = 450f;

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

    //Flat world direction (x, z) of the current throw, zero while not charging. Set by the host from the aim input or auto-aim
    [Networked] public Vector2 throwAim { get; set; }

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

        playerHealth = GetComponent<PlayerHealth>();
        playerInventory = GetComponent<PlayerInventory>();

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
        isThrowSecondButtonHeld |= Input.GetKey(KeyCode.T);
        isSwapPocket1Pressed |= Input.GetKey(KeyCode.Z);
        isSwapPocket2Pressed |= Input.GetKey(KeyCode.X);

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
            lastAimInput = networkInputData.aimInput;

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
                if(pressedButtons.IsSet(InputButton.Revive))
                    ShortenGetUpTime(getUpTapTime);
                
            }

            

            
        }

        if(Object.HasStateAuthority)
        {
            networkedMovementSpeed = localForwardVelocity * 0.4f;

            //Get up on our own once the time on the floor is over, eliminated players stay down
            if(isKnockedDown && !IsEliminated && getUpTimer.Expired(Runner))
                MakeActiveRagdoll();

            if(!IsEliminated)
                UpdateGrab(buttons, pressedButtons);

            //update the joint rotation based on the animation
            for (int i = 0; i < syncPhysicsObjects.Length; i++)
            {
                syncPhysicsObjects[i].UpdateJointAnimation();
                networkPhysicsSyncedRotations.Set(i, syncPhysicsObjects[i].transform.localRotation);
            }

            if(transform.position.y < -10f)
            {
                //Falling out of the world hurts but never eliminates on its own account
                if(playerHealth != null)
                    playerHealth.ApplyDamage(fallDamage, PlayerRef.None);

                Transform spawnPoint = SpawnPoint.PickRandom();

                if(spawnPoint != null)
                    networkRigidbody3D.Teleport(spawnPoint.position, spawnPoint.rotation);
                else
                    networkRigidbody3D.Teleport(Vector3.zero, Quaternion.identity);

                if(!IsEliminated)
                    MakeActiveRagdoll();
            }
                
        }
    }

    public override void Render()
    {
        animator.SetFloat("movementSpeed", networkedMovementSpeed);

        bool isCarrying = playerInventory != null && playerInventory.HasHandItem && grabState != GrabState.Swinging;

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

        //The on-screen stick wins when it is used, otherwise the keyboard
        networkInputData.movementInput = TouchInput.Move.sqrMagnitude > 0.01f ? TouchInput.Move : moveInputVector;

        networkInputData.aimInput = TouchInput.Aim;

        //The touch action button is Jump, and Get Up while knocked down
        bool touchAction = TouchInput.Consume(InputButton.Jump);

        //Also check the keys right now, otherwise a held key looks released when there are more ticks than frames
        networkInputData.buttons.Set(InputButton.Jump, isJumpButtonPressed || Input.GetKey(KeyCode.Space) || (touchAction && !isKnockedDown));
        networkInputData.buttons.Set(InputButton.Revive, isReviveButtonPressed || Input.GetKey(KeyCode.R) || (touchAction && isKnockedDown) || TouchInput.Consume(InputButton.Revive));
        networkInputData.buttons.Set(InputButton.Grab, isGrabButtonPressed || Input.GetKey(KeyCode.G) || TouchInput.Consume(InputButton.Grab));
        networkInputData.buttons.Set(InputButton.Throw, isThrowButtonHeld || Input.GetKey(KeyCode.F) || TouchInput.Consume(InputButton.Throw));
        networkInputData.buttons.Set(InputButton.ThrowSecond, isThrowSecondButtonHeld || Input.GetKey(KeyCode.T) || TouchInput.Consume(InputButton.ThrowSecond));
        networkInputData.buttons.Set(InputButton.ThrowLeft, TouchInput.Consume(InputButton.ThrowLeft));
        networkInputData.buttons.Set(InputButton.SwapPocket1, isSwapPocket1Pressed || Input.GetKey(KeyCode.Z) || TouchInput.Consume(InputButton.SwapPocket1));
        networkInputData.buttons.Set(InputButton.SwapPocket2, isSwapPocket2Pressed || Input.GetKey(KeyCode.X) || TouchInput.Consume(InputButton.SwapPocket2));

        isJumpButtonPressed = false;
        isReviveButtonPressed = false;
        isGrabButtonPressed = false;
        isThrowButtonHeld = false;
        isThrowSecondButtonHeld = false;
        isSwapPocket1Pressed = false;
        isSwapPocket2Pressed = false;

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

        if(playerHealth != null)
            playerHealth.StartInvulnerability(invulnerableAfterGetUp);
    }

    bool IsEliminated => playerHealth != null && playerHealth.IsEliminated;

    void ShortenGetUpTime(float seconds)
    {
        if(!Object.HasStateAuthority || !isKnockedDown || IsEliminated || !getUpTimer.IsRunning)
            return;

        float remaining = getUpTimer.RemainingTime(Runner) ?? 0f;

        getUpTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0f, remaining - seconds));
    }

    //Called by PlayerHealth on the host when health reaches zero
    public void OnEliminated()
    {
        if(!Object.HasStateAuthority)
            return;

        if(!isKnockedDown)
            MakeRegdoll();

        //Everything we carried is left behind as loot
        if(playerInventory != null)
            playerInventory.DropPockets();

        //An eliminated player never gets up
        getUpTimer = TickTimer.None;
    }

    /*void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("CauseDamage"))
            MakeRegdoll();

        
    }*/

    HandGrabHandler HeldHand => handGrabHandlers[heldHandIndex];

    public bool IsChargingThrow => grabState == GrabState.Charging;

    //Host only, the aim from the last input
    Vector2 lastAimInput = Vector2.zero;

    //The button that started the current charge, the throw ends when it is released
    InputButton chargingButton = InputButton.Throw;

    void UpdateGrab(NetworkButtons buttons, NetworkButtons pressedButtons)
    {
        if(playerInventory == null)
            return;

        playerInventory.Tick();

        bool isGrabPressed = pressedButtons.IsSet(InputButton.Grab);

        switch (grabState)
        {
            case GrabState.None:
                if(isKnockedDown)
                    break;

                if(isGrabPressed)
                    playerInventory.GrabOrDrop();
                else if(pressedButtons.IsSet(InputButton.SwapPocket1))
                    playerInventory.SwapPocket(0);
                else if(pressedButtons.IsSet(InputButton.SwapPocket2))
                    playerInventory.SwapPocket(1);
                //Hand 1 is the right hand, hand 0 the left. These two only throw from their own hand
                else if(pressedButtons.IsSet(InputButton.ThrowSecond))
                    StartCharging(InputButton.ThrowSecond, playerInventory.GetThrowHand(1));
                else if(pressedButtons.IsSet(InputButton.ThrowLeft))
                    StartCharging(InputButton.ThrowLeft, playerInventory.GetThrowHand(0));
                else if(pressedButtons.IsSet(InputButton.Throw))
                {
                    //The main throw button uses the first hand that holds something
                    int hand = playerInventory.GetThrowHand(0);

                    StartCharging(InputButton.Throw, hand >= 0 ? hand : playerInventory.GetThrowHand(1));
                }
                break;

            case GrabState.Charging:
                if(!HeldHand.IsHolding || isGrabPressed)
                {
                    DropHeldObject();
                    break;
                }

                throwCharge = Mathf.Min(1f, throwCharge + Runner.DeltaTime / throwChargeTime);

                UpdateThrowAim();

                if(!buttons.IsSet(chargingButton))
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

    void StartCharging(InputButton button, int hand)
    {
        if(hand < 0)
            return;

        chargingButton = button;
        heldHandIndex = (byte)hand;
        heldObjectId = playerInventory.GetHandItem(hand);
        throwCharge = 0f;
        grabState = GrabState.Charging;
    }

    //Lets go of everything in the hands and gives the arms back to the ragdoll
    void DropHeldObject()
    {
        if(playerInventory != null)
            playerInventory.DropHands();

        ResetGrabState();
    }

    void ResetGrabState()
    {
        grabState = GrabState.None;
        throwCharge = 0f;
        heldObjectId = default;
        throwAim = Vector2.zero;
    }

    Vector3 GetFlatForward()
    {
        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;

        return flatForward.sqrMagnitude > 0.001f ? flatForward.normalized : Vector3.forward;
    }

    //The direction of the throw on the ground, the aim while there is one, otherwise where we look
    Vector3 GetThrowDirection()
    {
        Vector2 aim = throwAim;

        return aim.sqrMagnitude > 0.01f ? new Vector3(aim.x, 0f, aim.y).normalized : GetFlatForward();
    }

    //Host only. Takes the manual aim, or turns towards the closest player in front, and turns the body that way
    void UpdateThrowAim()
    {
        Vector2 aim = Vector2.zero;

        //Dragging away from the throw button aims. The axes are the same as for walking
        if(lastAimInput.sqrMagnitude > 0.25f)
            aim = new Vector2(lastAimInput.x, -lastAimInput.y).normalized;
        else if(autoAim && TryFindAutoAimTarget(out Vector3 targetPosition))
        {
            Vector3 toTarget = targetPosition - transform.position;

            aim = new Vector2(toTarget.x, toTarget.z).normalized;
        }

        throwAim = aim;

        if(aim == Vector2.zero || isKnockedDown)
            return;

        //Face the throw direction so the arm swings the right way
        Quaternion desiredRotation = Quaternion.LookRotation(new Vector3(aim.x, 0f, aim.y), transform.up);

        mainJoint.targetRotation = Quaternion.RotateTowards(mainJoint.targetRotation, desiredRotation, Runner.DeltaTime * aimTurnSpeed);
    }

    bool TryFindAutoAimTarget(out Vector3 position)
    {
        position = Vector3.zero;

        float bestScore = float.MaxValue;
        Vector3 forward = GetFlatForward();

        foreach (PlayerHealth other in PlayerHealth.All)
        {
            if(other == null || other.IsEliminated || other.Object == Object)
                continue;

            Vector3 toOther = other.transform.position - transform.position;
            toOther.y = 0f;

            float distance = toOther.magnitude;

            if(distance < 0.5f || distance > autoAimRange)
                continue;

            float angle = Vector3.Angle(forward, toOther);

            if(angle > autoAimAngle)
                continue;

            //Prefer targets that are straight ahead, then closer ones
            float score = angle / autoAimAngle + distance / autoAimRange * 0.5f;

            if(score < bestScore)
            {
                bestScore = score;
                position = other.transform.position;
            }
        }

        return bestScore < float.MaxValue;
    }

    Vector3 GetThrowVelocity(float charge)
    {
        Vector3 flatForward = GetThrowDirection();
        Vector3 right = Vector3.Cross(Vector3.up, flatForward);

        //Tilt the direction up
        Vector3 direction = Quaternion.AngleAxis(-throwPitch, right) * flatForward;

        float itemMultiplier = 1f;

        //Heavy items fly slower
        if(Runner.TryFindObject(heldObjectId, out NetworkObject heldObject) && heldObject.TryGetComponent(out ThrowableItem item) && item.Data != null)
            itemMultiplier = item.Data.throwSpeedMultiplier;

        return direction * Mathf.Lerp(minThrowSpeed, maxThrowSpeed, charge) * itemMultiplier;
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

            hand.Throw(swingThrowVelocity, spin, throwCharge);
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

            //Lives on its own object so it keeps working after this player is removed
            new GameObject("Spectate Camera").AddComponent<SpectateCamera>();

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
