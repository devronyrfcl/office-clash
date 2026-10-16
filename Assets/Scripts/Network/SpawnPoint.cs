using UnityEngine;
using System.Collections.Generic;

//Place these in a scene where players can spawn
public class SpawnPoint : MonoBehaviour
{
    static readonly List<SpawnPoint> spawnPoints = new List<SpawnPoint>();

    public static int Count => spawnPoints.Count;

    void OnEnable()
    {
        spawnPoints.Add(this);
    }

    void OnDisable()
    {
        spawnPoints.Remove(this);
    }

    public static Transform Get(int index)
    {
        if(spawnPoints.Count == 0)
            return null;

        return spawnPoints[Mathf.Abs(index) % spawnPoints.Count].transform;
    }

    public static Transform PickRandom()
    {
        return Get(Random.Range(0, spawnPoints.Count));
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward);
    }
}
