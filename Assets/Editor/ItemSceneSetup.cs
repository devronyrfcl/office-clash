using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ItemSceneSetup
{
    const string PrefabFolder = "Assets/Prefab/Items";
    const string RootName = "Item System";

    [MenuItem("Office Clash/Setup Item Spawning In Scene")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (GameObject.Find(RootName) != null)
        {
            Debug.Log($"{RootName} already exists in {scene.name}, nothing to do.");
            return;
        }

        GameObject root = new GameObject(RootName);
        root.AddComponent<NetworkObject>();
        ItemSpawner spawner = root.AddComponent<ItemSpawner>();

        //Assign every item prefab to the spawner
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
        NetworkObject[] prefabs = new NetworkObject[guids.Length];

        for (int i = 0; i < guids.Length; i++)
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i])).GetComponent<NetworkObject>();

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        SerializedProperty prefabList = serializedSpawner.FindProperty("itemPrefabs");
        prefabList.arraySize = prefabs.Length;

        for (int i = 0; i < prefabs.Length; i++)
            prefabList.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];

        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

        PlacePoints(root.transform, 8f, 8);
        PlacePoints(root.transform, 14f, 12);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"Item spawning set up: {prefabs.Length} prefabs, {root.transform.childCount} spawn points.");
    }

    //Spawn points on a ring around the origin, spots that are inside geometry are skipped
    static void PlacePoints(Transform parent, float radius, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + (radius > 10f ? 15f : 0f);
            Vector3 position = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;

            //Drop onto the floor
            if (Physics.Raycast(position + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 40f))
                position = hit.point;

            position += Vector3.up * 0.3f;

            if (Physics.CheckSphere(position + Vector3.up * 0.3f, 0.25f))
                continue;

            GameObject point = new GameObject($"Item Spawn Point {parent.childCount + 1}");
            point.transform.SetParent(parent);
            point.transform.position = position;
            point.AddComponent<ItemSpawnPoint>();
        }
    }
}
