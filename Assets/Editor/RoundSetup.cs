using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoundSetup
{
    const string PlayerPrefabPath = "Assets/Prefab/Network/Network Character Prefab.prefab";
    const string RoundManagerName = "Round Manager";

    [MenuItem("Office Clash/Setup Health And Round")]
    public static void Setup()
    {
        AddHealthToPlayerPrefab();
        AddRoundManagerToScene();
    }

    static void AddHealthToPlayerPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        bool changed = false;

        if (prefabRoot.GetComponent<PlayerHealth>() == null)
        {
            prefabRoot.AddComponent<PlayerHealth>();
            changed = true;
        }

        if (prefabRoot.GetComponent<PlayerInventory>() == null)
        {
            prefabRoot.AddComponent<PlayerInventory>();
            changed = true;
        }

        if (changed)
        {
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            Debug.Log("Added PlayerHealth / PlayerInventory to the player prefab.");
        }
        else
        {
            Debug.Log("Player prefab already has PlayerHealth and PlayerInventory.");
        }

        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }

    static void AddRoundManagerToScene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (GameObject.Find(RoundManagerName) != null)
        {
            Debug.Log($"{RoundManagerName} already exists in {scene.name}.");
            return;
        }

        GameObject root = new GameObject(RoundManagerName);
        root.AddComponent<NetworkObject>();
        root.AddComponent<RoundManager>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"Added {RoundManagerName} to {scene.name}.");
    }
}
