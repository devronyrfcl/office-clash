using Fusion;
using UnityEngine;

//Two hands and two pockets, all decided by the host. Throwing itself stays in NetworkPlayer
public class PlayerInventory : NetworkBehaviour
{
    public const int HandCount = 2;
    public const int PocketCount = 2;

    //A big item occupies both hand slots with the same id
    [Networked, Capacity(HandCount)] NetworkArray<NetworkId> handItems { get; }
    [Networked, Capacity(PocketCount)] NetworkArray<NetworkId> pocketItems { get; }

    NetworkPlayer networkPlayer;

    //Host only, the hand that picked something up last is the one that drops first
    int lastGrabbedHand = 0;

    HandGrabHandler[] Hands => networkPlayer.Hands;

    void Awake()
    {
        networkPlayer = GetComponent<NetworkPlayer>();
    }

    public NetworkId GetHandItem(int hand) => handItems.Get(hand);
    public NetworkId GetPocketItem(int pocket) => pocketItems.Get(pocket);

    public bool HasHandItem
    {
        get
        {
            for (int i = 0; i < HandCount; i++)
            {
                if (handItems.Get(i).IsValid)
                    return true;
            }

            return false;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        //Pocketed items are hidden, never leave them hidden forever
        if (hasState && HasStateAuthority)
            DropPockets();
    }

    //Clears slots whose item is no longer held, for example thrown, or pulled away from the hand
    public void Tick()
    {
        for (int hand = 0; hand < HandCount; hand++)
        {
            NetworkId id = handItems.Get(hand);

            if (!id.IsValid || Hands[hand].IsHolding)
                continue;

            //Only valid as the second hand of a big item that the other hand holds
            int other = 1 - hand;

            if (Hands[other].IsHolding && handItems.Get(other) == id)
                continue;

            handItems.Set(hand, default);
        }
    }

    //Which hand throws when the given hand's button is pressed, -1 when that hand holds nothing
    public int GetThrowHand(int requestedHand)
    {
        if (Hands[requestedHand].IsHolding)
            return requestedHand;

        int other = 1 - requestedHand;
        NetworkId id = handItems.Get(requestedHand);

        //Big item held by the other hand but occupying this one too
        if (id.IsValid && Hands[other].IsHolding && handItems.Get(other) == id)
            return other;

        return -1;
    }

    //Grab key: pick up what is in reach, drop what we hold when there is nothing to pick up.
    //Something in reach that cannot be carried does nothing, so a full inventory never drops an item by accident
    public void GrabOrDrop()
    {
        if (TryGrab() == GrabResult.NothingInReach)
            DropLast();
    }

    enum GrabResult
    {
        Grabbed,
        NothingInReach,
        CannotCarry
    }

    bool IsHandFree(int hand) => !Hands[hand].IsHolding && !handItems.Get(hand).IsValid;

    int FindFreePocket()
    {
        for (int i = 0; i < PocketCount; i++)
        {
            if (!pocketItems.Get(i).IsValid)
                return i;
        }

        return -1;
    }

    GrabResult TryGrab()
    {
        //The hand that is closest to an object
        int bestHand = -1;
        float bestDistance = float.MaxValue;
        Rigidbody bestBody = null;

        for (int i = 0; i < HandCount; i++)
        {
            float distance = Hands[i].FindGrabbable(out Rigidbody body);

            if (body != null && distance < bestDistance)
            {
                bestHand = i;
                bestDistance = distance;
                bestBody = body;
            }
        }

        if (bestBody == null)
            return GrabResult.NothingInReach;

        NetworkObject networkObject = bestBody.GetComponentInParent<NetworkObject>();
        ThrowableItem item = networkObject.GetComponent<ThrowableItem>();

        int handsRequired = item != null && item.Data != null ? item.Data.HandsRequired : 1;
        bool canPocket = item != null && item.Data != null && item.Data.CanGoInPocket;

        int other = 1 - bestHand;

        if (handsRequired == 2)
        {
            if (!IsHandFree(bestHand) || !IsHandFree(other))
                return GrabResult.CannotCarry;

            HoldInHand(bestHand, bestBody, networkObject.Id);
            handItems.Set(other, networkObject.Id);

            return GrabResult.Grabbed;
        }

        int freeHand = IsHandFree(bestHand) ? bestHand : IsHandFree(other) ? other : -1;

        if (freeHand >= 0)
        {
            HoldInHand(freeHand, bestBody, networkObject.Id);
            return GrabResult.Grabbed;
        }

        //Both hands are full, the pockets are the only place left
        int pocket = FindFreePocket();

        if (canPocket && pocket >= 0)
        {
            item.Pocket(transform);
            pocketItems.Set(pocket, networkObject.Id);
            return GrabResult.Grabbed;
        }

        return GrabResult.CannotCarry;
    }

    void HoldInHand(int hand, Rigidbody body, NetworkId id)
    {
        Hands[hand].Grab(body);
        handItems.Set(hand, id);
        lastGrabbedHand = hand;
    }

    //Lets go of the item we picked up last, a big item frees both hands
    void DropLast()
    {
        int hand = Hands[lastGrabbedHand].IsHolding ? lastGrabbedHand : Hands[1 - lastGrabbedHand].IsHolding ? 1 - lastGrabbedHand : -1;

        if (hand < 0)
            return;

        ReleaseHand(hand);
    }

    void ReleaseHand(int hand)
    {
        NetworkId id = handItems.Get(hand);

        Hands[hand].Release();
        Hands[hand].Arm.EndSwing();
        handItems.Set(hand, default);

        int other = 1 - hand;

        if (id.IsValid && handItems.Get(other) == id)
            handItems.Set(other, default);
    }

    //Knocked down or eliminated, everything in the hands falls. Pockets stay
    public void DropHands()
    {
        for (int hand = 0; hand < HandCount; hand++)
            ReleaseHand(hand);
    }

    //Eliminated, the pockets spill out as loot
    public void DropPockets()
    {
        for (int i = 0; i < PocketCount; i++)
        {
            NetworkId id = pocketItems.Get(i);

            if (!id.IsValid)
                continue;

            pocketItems.Set(i, default);

            if (Runner.TryFindObject(id, out NetworkObject networkObject) && networkObject.TryGetComponent(out ThrowableItem item))
            {
                Vector3 offset = Random.insideUnitSphere * 0.5f;
                offset.y = Mathf.Abs(offset.y) + 0.6f;

                item.Unpocket(transform.position + offset);
            }
        }
    }

    //Moves a pocket item into a hand, or stows a hand item into an empty pocket
    public void SwapPocket(int pocket)
    {
        NetworkId pocketId = pocketItems.Get(pocket);

        if (pocketId.IsValid)
            PocketToHand(pocket, pocketId);
        else
            HandToPocket(pocket);
    }

    void PocketToHand(int pocket, NetworkId pocketId)
    {
        if (!Runner.TryFindObject(pocketId, out NetworkObject networkObject) || !networkObject.TryGetComponent(out ThrowableItem item))
        {
            pocketItems.Set(pocket, default);
            return;
        }

        int freeHand = IsHandFree(0) ? 0 : IsHandFree(1) ? 1 : -1;

        //Both hands are busy, swap with a hand item that fits a pocket
        int swapHand = -1;

        if (freeHand < 0)
        {
            for (int hand = 0; hand < HandCount; hand++)
            {
                if (Hands[hand].IsHolding && TryGetHandItem(hand, out ThrowableItem heldItem) && heldItem.Data.CanGoInPocket)
                {
                    swapHand = hand;
                    break;
                }
            }

            if (swapHand < 0)
                return;

            freeHand = swapHand;
        }

        if (swapHand >= 0)
        {
            TryGetHandItem(swapHand, out ThrowableItem heldItem);

            NetworkId heldId = handItems.Get(swapHand);

            Hands[swapHand].Release();
            handItems.Set(swapHand, default);

            heldItem.Pocket(transform);
            pocketItems.Set(pocket, heldId);
        }
        else
        {
            pocketItems.Set(pocket, default);
        }

        item.Unpocket(Hands[freeHand].transform.position);

        HoldInHand(freeHand, item.GetComponent<Rigidbody>(), pocketId);
    }

    void HandToPocket(int pocket)
    {
        int hand = Hands[lastGrabbedHand].IsHolding ? lastGrabbedHand : Hands[1 - lastGrabbedHand].IsHolding ? 1 - lastGrabbedHand : -1;

        if (hand < 0 || !TryGetHandItem(hand, out ThrowableItem item) || !item.Data.CanGoInPocket)
            return;

        NetworkId id = handItems.Get(hand);

        Hands[hand].Release();
        handItems.Set(hand, default);

        item.Pocket(transform);
        pocketItems.Set(pocket, id);
    }

    bool TryGetHandItem(int hand, out ThrowableItem item)
    {
        item = null;

        NetworkId id = handItems.Get(hand);

        return id.IsValid && Runner.TryFindObject(id, out NetworkObject networkObject) && networkObject.TryGetComponent(out item) && item.Data != null;
    }
}
