using UnityEngine;
using System.Collections;
using Fusion;
using Cinemachine;

public class NetworkPlayer : NetworkBehaviour, IPlayerLeft
{

    public static NetworkPlayer Local { get; set; }
    
    [SerializeField] Rigidbody rigidbody3D;

    [SerializeField] ConfigurableJoint mainJoint;

    Vector2 moveInputVector = Vector2.zero;

    public float maxSpeed = 2f;

    bool isGrounded = false;
    bool isJumpButtonPressed = false;

    RaycastHit[] raycastHits = new RaycastHit[10];

    [SerializeField] private SyncPhysicsObject[] syncPhysicsObjects;

    [SerializeField] private Animator animator;

    //Cinemachine
    CinemachineVirtualCamera cinemachineVirtualCamera;

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


    void FixedUpdate()
    {
        isGrounded = false;
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

        float inputMagnitude = moveInputVector.magnitude;

        Vector3 localVelocityForward = transform.forward * Vector3.Dot(transform.forward, rigidbody3D.linearVelocity);

        float localForwardVelocity = localVelocityForward.magnitude;

        if (inputMagnitude != 0f)
        {
            Quaternion desireDirection = Quaternion.LookRotation(new Vector3(moveInputVector.x, 0f, moveInputVector.y * -1f), transform.up);
            mainJoint.targetRotation = Quaternion.RotateTowards(mainJoint.targetRotation, desireDirection, 300f * Time.fixedDeltaTime);

            

            if (localForwardVelocity < maxSpeed)
            {
                rigidbody3D.AddForce(transform.forward * inputMagnitude * 30f);
            }

            if (isJumpButtonPressed && isGrounded)
            {
                rigidbody3D.AddForce(transform.up * 20f, ForceMode.Impulse);
                isJumpButtonPressed = false;
            }

            animator.SetFloat("movementSpeed", localForwardVelocity * 0.9f);

            //update the joint rotation based on the animation
            for (int i = 0; i < syncPhysicsObjects.Length; i++)
            {
                syncPhysicsObjects[i].UpdateJointAnimation();
                Debug.Log("Updating joint animation for " + syncPhysicsObjects[i].gameObject.name);
            }
        }
    }

    public override void Spawned()
    {
        if(Object.HasInputAuthority)
        {
            Local = this;

            cinemachineVirtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            cinemachineVirtualCamera.m_Follow = transform;
            cinemachineVirtualCamera.m_LookAt = transform;

            Ulits.Debug("Spawned Local Player with input authority");

        }
        else
        {
            Ulits.Debug("Spawned Remote Player without input authority");
        }

        transform.name = $"P_{Object.Id}";
    }

    public void PlayerLeft(PlayerRef player)
    {
        
    }
}
