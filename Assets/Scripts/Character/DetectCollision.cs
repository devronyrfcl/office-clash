using UnityEngine;
using System.Collections;

public class DetectCollision : MonoBehaviour
{
    
    NetworkPlayer networkPlayer;
    Rigidbody Hitrigidbody;

    ContactPoint[] contactPoints = new ContactPoint[5];

    void Awake()
    {
        networkPlayer = GetComponentInParent<NetworkPlayer>();
        Hitrigidbody = GetComponent<Rigidbody>();
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void OnCollisionEnter(Collision collision)
    {
        if(!networkPlayer.HasStateAuthority)
            return;

        if(!networkPlayer.IsActiveRagdoll)
            return;

        if(!collision.collider.CompareTag("CauseDamage"))
            return;

        if(collision.collider.transform.root == networkPlayer.transform)
            return;

        int numberOfContacts = collision.GetContacts(contactPoints);

        for (int i = 0; i< numberOfContacts; i++)
        {
            ContactPoint contactPoint = contactPoints[i];

            Vector3 contactImpulse = contactPoint.impulse / Time.fixedDeltaTime;

            if(contactImpulse.magnitude < 15f)
                continue;

            networkPlayer.OnPlayerBodyPartHit();

            Vector3 forceDirection = (contactImpulse + Vector3.up) * 0.5f;

            forceDirection = Vector3.ClampMagnitude(forceDirection, 30f);

            Debug.DrawRay(Hitrigidbody.position, forceDirection * 40, Color.red, 4f);

            Hitrigidbody.AddForce(forceDirection, ForceMode.Impulse);

            
        }

    }

}
