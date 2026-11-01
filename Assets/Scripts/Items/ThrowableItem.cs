using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public class ThrowableItem : NetworkBehaviour
{
    static readonly List<ThrowableItem> activeItems = new List<ThrowableItem>();

    //Every item that currently exists, used to cap the number of physics props
    public static List<ThrowableItem> Active => activeItems;

    public static int ActiveCount => activeItems.Count;

    [SerializeField] ItemData itemData;

    //Idle props go to sleep sooner than the Unity default, so they cost nothing until touched
    [SerializeField] float sleepThreshold = 0.1f;

    public ItemData Data => itemData;

    //Who threw it last, so hits and eliminations can be credited. Default means nobody
    [Networked] public PlayerRef LastThrower { get; set; }

    [Networked] public float ThrowCharge { get; set; }

    //After a throw the item can hurt for a short time, a lying item is harmless
    [SerializeField] float damageWindow = 3f;

    [Networked] TickTimer damageTimer { get; set; }

    //Only meaningful on the host, which is the only one that applies damage
    public bool IsDangerous => HasStateAuthority && LastThrower != PlayerRef.None && !damageTimer.ExpiredOrNotRunning(Runner);

    //One throw hurts one player once
    public void ConsumeHit()
    {
        if (!HasStateAuthority)
            return;

        damageTimer = TickTimer.None;
    }

    //In a pocket the item is hidden and has no physics
    [Networked] public NetworkBool IsPocketed { get; set; }

    Transform pocketOwner;
    Rigidbody body;
    NetworkRigidbody3D networkBody;
    Collider[] colliders;
    Renderer[] renderers;
    bool appliedPocketed = false;

    public override void Spawned()
    {
        activeItems.Add(this);

        colliders = GetComponentsInChildren<Collider>(true);
        renderers = GetComponentsInChildren<Renderer>(true);
        body = GetComponent<Rigidbody>();
        networkBody = GetComponent<NetworkRigidbody3D>();

        if(body != null)
            body.sleepThreshold = sleepThreshold;

        SetPocketedVisuals(IsPocketed);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !IsPocketed)
            return;

        //The owner is gone, the item comes back where it was
        if (pocketOwner == null)
        {
            Unpocket(transform.position);
            return;
        }

        //Networked rigidbodies are moved with Teleport, setting the position directly does not stick
        networkBody.Teleport(pocketOwner.position + Vector3.up, transform.rotation);
    }

    public override void Render()
    {
        if (IsPocketed != appliedPocketed)
            SetPocketedVisuals(IsPocketed);
    }

    void SetPocketedVisuals(bool pocketed)
    {
        appliedPocketed = pocketed;

        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = !pocketed;

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = !pocketed;
    }

    //Host only. Puts the item away, it follows the owner while hidden
    public void Pocket(Transform owner)
    {
        if (!HasStateAuthority)
            return;

        ClearThrower();

        pocketOwner = owner;
        IsPocketed = true;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;

        SetPocketedVisuals(true);
    }

    //Host only. Brings the item back into the world
    public void Unpocket(Vector3 position)
    {
        if (!HasStateAuthority)
            return;

        pocketOwner = null;
        IsPocketed = false;

        body.isKinematic = false;
        networkBody.Teleport(position, transform.rotation);
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.WakeUp();

        SetPocketedVisuals(false);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        activeItems.Remove(this);
    }

    public void OnThrown(PlayerRef thrower, float charge)
    {
        if (!HasStateAuthority)
            return;

        LastThrower = thrower;
        ThrowCharge = charge;
        damageTimer = TickTimer.CreateFromSeconds(Runner, damageWindow);
    }

    public void ClearThrower()
    {
        if (!HasStateAuthority)
            return;

        LastThrower = PlayerRef.None;
        ThrowCharge = 0f;
        damageTimer = TickTimer.None;
    }
}
