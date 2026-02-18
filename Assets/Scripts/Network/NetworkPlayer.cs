using UnityEngine;

public class NetworkPlayer : MonoBehaviour
{
    
    [SerializeField] Rigidbody rigidbody3D;

    [SerializeField] ConfigurableJoint mainJoint;

    Vector2 moveInputVector = Vector2.zero;

    float maxSpeed = 3f;

    bool isGrounded = false;
    bool isJumpButtonPressed = false;

    RaycastHit[] raycastHits = new RaycastHit[10];
    
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
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

        if(inputMagnitude != 0f)
        {
            Quaternion desireDirection = Quaternion.LookRotation(new Vector3(moveInputVector.x, 0f, moveInputVector.y * -1f), transform.up);
            mainJoint.targetRotation = Quaternion.RotateTowards(mainJoint.targetRotation, desireDirection, 300f * Time.fixedDeltaTime);

            Vector3 localVelocityForward = transform.forward * Vector3.Dot(transform.forward, rigidbody3D.linearVelocity);

            float localForwardVelocity = localVelocityForward.magnitude;

            if(localForwardVelocity < maxSpeed)
            {
                rigidbody3D.AddForce(transform.forward * inputMagnitude * 30f);
            }

            if(isJumpButtonPressed && isGrounded)
            {
                rigidbody3D.AddForce(transform.up * 20f, ForceMode.Impulse);
                isJumpButtonPressed = false;
            }

            


        }

    }
}
