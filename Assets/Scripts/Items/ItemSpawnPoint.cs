using UnityEngine;
using System.Collections.Generic;

//Place these in a scene where items can appear, the ItemSpawner fills them
public class ItemSpawnPoint : MonoBehaviour
{
    static readonly List<ItemSpawnPoint> itemSpawnPoints = new List<ItemSpawnPoint>();

    public static IReadOnlyList<ItemSpawnPoint> All => itemSpawnPoints;

    //Leave empty to allow every prefab of the spawner, otherwise only these can appear here
    [SerializeField] Fusion.NetworkObject[] allowedPrefabs;

    public Fusion.NetworkObject[] AllowedPrefabs => allowedPrefabs;

    void OnEnable()
    {
        itemSpawnPoints.Add(this);
    }

    void OnDisable()
    {
        itemSpawnPoints.Remove(this);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.15f, new Vector3(0.4f, 0.3f, 0.4f));
    }
}
