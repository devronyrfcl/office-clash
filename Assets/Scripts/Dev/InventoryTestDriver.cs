using System.Collections;
using System.Linq;
using Fusion;
using UnityEngine;

//Temporary play mode test for the hands and pockets, host only. Deleted after the test
public class InventoryTestDriver : MonoBehaviour
{
    PlayerInventory inventory;
    NetworkPlayer player;

    IEnumerator Start()
    {
        while (player == null || inventory == null || ThrowableItem.ActiveCount < 20)
        {
            player = FindFirstObjectByType<NetworkPlayer>();
            inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(1f);

        var smalls = ThrowableItem.Active.Where(i => i != null && i.Data.itemClass == ItemClass.Small).Take(3).ToArray();
        ThrowableItem stapler = smalls[0];
        ThrowableItem mug = smalls[1];
        ThrowableItem paperweight = smalls[2];
        ThrowableItem keyboard = ThrowableItem.Active.FirstOrDefault(i => i != null && i.Data.itemClass == ItemClass.Medium)
            ?? ThrowableItem.Active.First(i => i != null && !smalls.Contains(i) && i.Data.itemClass != ItemClass.Big);
        ThrowableItem chair = ThrowableItem.Active.FirstOrDefault(i => i != null && i.Data.itemClass == ItemClass.Big);
        Log("big item available", chair != null ? "yes" : "no, steps 5 and 8 skipped");

        Log("start", "");

        yield return Bring(stapler);
        Log("1 stapler grabbed (expect 1 hand)", Describe(stapler));

        yield return Bring(mug);
        Log("2 mug grabbed (expect 2 hands)", Describe(mug));

        yield return Bring(paperweight);
        Log("3 paperweight, hands full (expect pocketed)", Describe(paperweight));

        yield return Bring(keyboard);
        Log("4 keyboard, hands full (expect pocketed)", Describe(keyboard));

        if (chair != null) yield return Bring(chair);
        if (chair != null) Log("5 chair, hands and pockets full (expect NOT held, NOT pocketed)", Describe(chair));

        inventory.SwapPocket(0);
        yield return new WaitForFixedUpdate();
        Log("6 swap pocket 0 (expect paperweight in hand, a hand item now pocketed)", $"paperweight[{Describe(paperweight)}] stapler[{Describe(stapler)}] mug[{Describe(mug)}]");

        inventory.DropHands();
        yield return new WaitForFixedUpdate();
        Log("7 dropped hands (expect 0 hands, pockets kept)", $"{Describe(stapler)} | {Describe(mug)} | {Describe(paperweight)} | keyboard[{Describe(keyboard)}]");

        if (chair != null) yield return Bring(chair);
        if (chair != null) Log("8 chair with free hands (expect 2 hand slots, 1 handler holding)", Describe(chair));

        inventory.DropHands();
        inventory.DropPockets();
        yield return new WaitForFixedUpdate();
        Log("9 dropped everything (expect nothing held or pocketed)", $"{Describe(stapler)} | {Describe(mug)} | {Describe(keyboard)} | {(chair != null ? Describe(chair) : "no chair")}");

        // Clear the area so nothing else is in reach of the hands
        foreach (var other in ThrowableItem.Active.Where(i => i != null).ToArray())
            other.GetComponent<Fusion.Addons.Physics.NetworkRigidbody3D>().Teleport(new Vector3(0f, 60f, 0f) + Random.insideUnitSphere * 5f, Quaternion.identity);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        // Throw test: simulate holding the throw button of the hand that holds the item
        var thrownItem = ThrowableItem.Active.First(i => i != null && i.Data.itemClass == ItemClass.Small && !i.IsPocketed && i != stapler);
        yield return Bring(thrownItem);

        int throwHand = player.Hands[0].IsHolding ? 0 : 1;
        string field = throwHand == 0 ? "isThrowButtonHeld" : "isThrowSecondButtonHeld";
        var fieldInfo = typeof(NetworkPlayer).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Log("10 before throw", $"hand={throwHand} button={field} {Describe(thrownItem)} thrower={thrownItem.LastThrower}");

        float held = 0f;
        while (held < 0.6f)
        {
            fieldInfo.SetValue(player, true);
            held += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.25f);
        Log("10 mid swing/after release", $"{Describe(thrownItem)} thrower={thrownItem.LastThrower} charge={thrownItem.ThrowCharge:0.00} dangerous={thrownItem.IsDangerous} speed={thrownItem.GetComponent<Rigidbody>().linearVelocity.magnitude:0.0} grabState={player.grabState}");

        yield return new WaitForSeconds(0.8f);
        Log("11 after throw (expect nothing held, thrower set)", $"{Describe(thrownItem)} thrower={thrownItem.LastThrower} charge={thrownItem.ThrowCharge:0.00} grabState={player.grabState}");

        Log("done", "");
    }

    ThrowableItem Find(string prefabName)
    {
        return ThrowableItem.Active.First(i => i != null && i.name.StartsWith(prefabName));
    }

    IEnumerator Bring(ThrowableItem item)
    {
        Rigidbody body = item.GetComponent<Rigidbody>();

        Vector3 handPosition = player.Hands[0].transform.position;

        //Teleport is the way to move a networked rigidbody, setting the position directly gets overwritten
        item.GetComponent<Fusion.Addons.Physics.NetworkRigidbody3D>().Teleport(handPosition + Vector3.up * 0.05f, Quaternion.identity);
        body.linearVelocity = Vector3.zero;

        Physics.SyncTransforms();

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Collider c = item.GetComponentInChildren<Collider>();
        Log("  pre-grab " + item.name, $"dist h0={Vector3.Distance(item.transform.position, player.Hands[0].transform.position):0.00} h1={Vector3.Distance(item.transform.position, player.Hands[1].transform.position):0.00} mass={body.mass} kinematic={body.isKinematic} colliderEnabled={c.enabled} pocketed={item.IsPocketed} h0holding={player.Hands[0].IsHolding} h1holding={player.Hands[1].IsHolding} found0={FindDist(0)} found1={FindDist(1)}");

        inventory.GrabOrDrop();

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
    }

    string FindDist(int hand)
    {
        float d = player.Hands[hand].FindGrabbable(out Rigidbody found);
        return found != null ? $"{found.name}@{d:0.00}" : "none";
    }

    string Describe(ThrowableItem item)
    {
        NetworkId id = item.Object.Id;

        int handSlots = 0;
        for (int i = 0; i < PlayerInventory.HandCount; i++)
            if (inventory.GetHandItem(i) == id) handSlots++;

        int pocketSlots = 0;
        for (int i = 0; i < PlayerInventory.PocketCount; i++)
            if (inventory.GetPocketItem(i) == id) pocketSlots++;

        int handlersHolding = player.Hands.Count(h => h.IsHolding && h.HeldBody == item.GetComponent<Rigidbody>());

        return $"{item.name.Replace("(Clone)","")}#{System.Array.IndexOf(ThrowableItem.Active.ToArray(), item)}: handSlots={handSlots} handlersHolding={handlersHolding} pocketSlots={pocketSlots} IsPocketed={item.IsPocketed}";
    }

    void Log(string label, string text)
    {
        Debug.Log($"[InvTest] {label}: {text}");
    }
}
