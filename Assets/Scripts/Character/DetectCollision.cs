using UnityEngine;
using System.Collections;

public class DetectCollision : MonoBehaviour
{
    [Header("Item hits")]
    //Slower hits do nothing, a gentle touch must not hurt
    [SerializeField] float minHitSpeed = 3f;

    //At this impact speed an item does its full base damage
    [SerializeField] float fullDamageSpeed = 10f;

    //Throws at full charge count as a strong throw
    [SerializeField] float fullChargeThreshold = 0.95f;

    NetworkPlayer networkPlayer;
    PlayerHealth playerHealth;
    Rigidbody Hitrigidbody;

    ContactPoint[] contactPoints = new ContactPoint[5];

    void Awake()
    {
        networkPlayer = GetComponentInParent<NetworkPlayer>();
        playerHealth = GetComponentInParent<PlayerHealth>();
        Hitrigidbody = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if(!networkPlayer.HasStateAuthority)
            return;

        if(!collision.collider.CompareTag("CauseDamage"))
            return;

        if(collision.collider.transform.root == networkPlayer.transform)
            return;

        ThrowableItem item = collision.collider.GetComponentInParent<ThrowableItem>();

        if(item != null)
            HandleItemHit(collision, item);
        else
            HandleOtherHit(collision);
    }

    void HandleItemHit(Collision collision, ThrowableItem item)
    {
        if(playerHealth != null && playerHealth.IsEliminated)
            return;

        //Only thrown items hurt, and never their own thrower
        if(!item.IsDangerous)
            return;

        if(item.LastThrower == networkPlayer.Object.InputAuthority)
            return;

        float speed = collision.relativeVelocity.magnitude;

        if(speed < minHitSpeed)
            return;

        ItemData data = item.Data;

        if(data == null)
            return;

        if(playerHealth != null)
        {
            float damage = data.baseDamage * Mathf.Clamp(speed / fullDamageSpeed, 0.2f, 1.25f);

            playerHealth.ApplyDamage(damage, item.LastThrower);
        }

        item.ConsumeHit();

        bool isStrongEnough = speed >= data.knockdownSpeed && (!data.knockdownOnlyAtFullCharge || item.ThrowCharge >= fullChargeThreshold);
        bool knocksDown = data.itemClass == ItemClass.Big || isStrongEnough;

        //Already down or just got up, the hit still hurts but does not knock down again
        if(!knocksDown || !networkPlayer.IsActiveRagdoll || (playerHealth != null && playerHealth.IsInvulnerable))
            return;

        networkPlayer.OnPlayerBodyPartHit();

        Rigidbody itemBody = collision.collider.attachedRigidbody;
        Vector3 pushDirection = itemBody != null ? (Hitrigidbody.position - itemBody.position).normalized : Vector3.zero;

        Vector3 force = (pushDirection + Vector3.up * 0.5f).normalized * Mathf.Clamp(speed * 2f, 5f, 30f);

        Hitrigidbody.AddForce(force, ForceMode.Impulse);
    }

    //Things without item data, for example moving level parts, keep the old impulse based knockdown without damage
    void HandleOtherHit(Collision collision)
    {
        if(!networkPlayer.IsActiveRagdoll)
            return;

        if(playerHealth != null && playerHealth.IsInvulnerable)
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
