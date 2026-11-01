using System.Collections.Generic;
using Fusion;
using UnityEngine;

//Put this on a scene object with a NetworkObject. Only the host spawns items
public class ItemSpawner : NetworkBehaviour
{
    [SerializeField] NetworkObject[] itemPrefabs;

    [Header("Respawn")]
    //An item counts as taken when it is this far from its spawn point
    [SerializeField] float takenDistance = 1.5f;
    [SerializeField] float respawnDelay = 20f;

    [Header("Limits")]
    //No new items are spawned while this many exist, thrown items included
    [SerializeField] int maxActiveItems = 40;

    //Items that fall out of the world are removed
    [SerializeField] float despawnBelowY = -30f;

    class Slot
    {
        public ItemSpawnPoint point;
        public NetworkObject item;
        public float respawnAt = -1f;
    }

    readonly Dictionary<ItemSpawnPoint, Slot> slots = new Dictionary<ItemSpawnPoint, Slot>();

    public override void Spawned()
    {
        if(!HasStateAuthority)
            return;

        //Fill every point once at the start
        foreach (ItemSpawnPoint point in ItemSpawnPoint.All)
            SpawnAt(GetSlot(point));
    }

    public override void FixedUpdateNetwork()
    {
        if(!HasStateAuthority)
            return;

        RemoveLostItems();

        foreach (ItemSpawnPoint point in ItemSpawnPoint.All)
        {
            Slot slot = GetSlot(point);

            if(slot.item != null)
            {
                if(!IsTaken(slot))
                    continue;

                //Forget it, the item now belongs to whoever picked it up and the timer starts
                slot.item = null;
                slot.respawnAt = Time.time + respawnDelay;
                continue;
            }

            if(slot.respawnAt < 0f)
                slot.respawnAt = Time.time + respawnDelay;

            if(Time.time >= slot.respawnAt)
                SpawnAt(slot);
        }
    }

    Slot GetSlot(ItemSpawnPoint point)
    {
        if(!slots.TryGetValue(point, out Slot slot))
        {
            slot = new Slot { point = point };
            slots.Add(point, slot);
        }

        return slot;
    }

    bool IsTaken(Slot slot)
    {
        Vector3 offset = slot.item.transform.position - slot.point.transform.position;

        return offset.sqrMagnitude > takenDistance * takenDistance;
    }

    void SpawnAt(Slot slot)
    {
        //Try again on the next tick when the office is full
        if(ThrowableItem.ActiveCount >= maxActiveItems)
            return;

        NetworkObject prefab = PickPrefab(slot.point);

        if(prefab == null)
            return;

        Transform pointTransform = slot.point.transform;

        slot.item = Runner.Spawn(prefab, pointTransform.position, pointTransform.rotation);
        slot.respawnAt = -1f;
    }

    NetworkObject PickPrefab(ItemSpawnPoint point)
    {
        NetworkObject[] pool = point.AllowedPrefabs != null && point.AllowedPrefabs.Length > 0 ? point.AllowedPrefabs : itemPrefabs;

        if(pool == null || pool.Length == 0)
            return null;

        //Weighted by ItemData.spawnWeight, prefabs without data count as 1
        float totalWeight = 0f;

        for (int i = 0; i < pool.Length; i++)
            totalWeight += GetWeight(pool[i]);

        if(totalWeight <= 0f)
            return null;

        float roll = Random.Range(0f, totalWeight);

        for (int i = 0; i < pool.Length; i++)
        {
            roll -= GetWeight(pool[i]);

            if(roll <= 0f)
                return pool[i];
        }

        return pool[pool.Length - 1];
    }

    static float GetWeight(NetworkObject prefab)
    {
        if(prefab != null && prefab.TryGetComponent(out ThrowableItem item) && item.Data != null)
            return item.Data.spawnWeight;

        return 1f;
    }

    //Items that fell out of the world or were removed by something else
    void RemoveLostItems()
    {
        List<ThrowableItem> items = ThrowableItem.Active;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            ThrowableItem item = items[i];

            if(item != null && item.transform.position.y < despawnBelowY && item.Object != null)
                Runner.Despawn(item.Object);
        }
    }
}
