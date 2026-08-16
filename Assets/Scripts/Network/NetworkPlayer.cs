using UnityEngine;
using System.Collections;
using Fusion;
using Cinemachine;
using Fusion.Addons.Physics;

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
    bool isJumpButtonPressed = false;

    RaycastHit[] raycastHits = new RaycastHit[10];

    [SerializeField] private SyncPhysicsObject[] syncPhysicsObjects;

    [SerializeField] private Animator animator;

    //Cinemachine
    CinemachineVirtualCamera cinemachineVirtualCamera;

    CinemachineBrain cinemachineBrain;

    //Networked array to store the rotations of the sync physics objects
    [Networked, Capacity(10)] public NetworkArray<Quaternion> networkPhysicsSyncedRotations { get; }

    [Networked] public float networkedMovementSpeed { get; set; }

    float startSlerpPositionSpring;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        //seach for SyncPhysicsObjects in children and add them to the list
        if(syncPhysicsObjects.Length == 0)
            syncPhysicsObjects = GetComponentsInChildren<SyncPhysicsObject>();



    }

    // Update is called once per frame
    void Update()
    {
        //move input
        moveInputVector.x = Input.GetAxis("Horizontal");
        moveInputVector.y = Input.GetAxis("Vertical");

        if(Input.GetKeyDown(KeyCode.Space))
                isJumpButtonPressed = true;
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
        
        if(GetInput(out NetworkInputData networkInputData))
        {
            float inputMagnitude = networkInputData.movementInput.magnitude;

            

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

        if(Object.HasStateAuthority)
        {
            networkedMovementSpeed = localForwardVelocity * 0.4f;

            //update the joint rotation based on the animation
            for (int i = 0; i < syncPhysicsObjects.Length; i++)
            {
                syncPhysicsObjects[i].UpdateJointAnimation();
                networkPhysicsSyncedRotations.Set(i, syncPhysicsObjects[i].transform.localRotation);
            }

            if(transform.position.y < -10f)
                networkRigidbody3D.Teleport(Vector3.zero, Quaternion.identity);
        }
    }

    public override void Render()
    {
        animator.SetFloat("movementSpeed", networkedMovementSpeed);

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

        isJumpButtonPressed = false;

        return networkInputData;
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
