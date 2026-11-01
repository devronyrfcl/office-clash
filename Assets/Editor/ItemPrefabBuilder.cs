using System.IO;
using Fusion;
using Fusion.Addons.Physics;
using UnityEditor;
using UnityEngine;

public static class ItemPrefabBuilder
{
    const string ModelFolder = "Assets/SimpleOfficeInteriors/Prefabs";
    const string DataFolder = "Assets/Items/Data";
    const string PrefabFolder = "Assets/Prefab/Items";

    struct ItemDefinition
    {
        public string model;
        public string displayName;
        public ItemClass itemClass;
        public float baseDamage;
        public float knockdownSpeed;
        public bool onlyAtFullCharge;
        public float mass;

        public ItemDefinition(string model, string displayName, ItemClass itemClass, float baseDamage, float knockdownSpeed, bool onlyAtFullCharge, float mass)
        {
            this.model = model;
            this.displayName = displayName;
            this.itemClass = itemClass;
            this.baseDamage = baseDamage;
            this.knockdownSpeed = knockdownSpeed;
            this.onlyAtFullCharge = onlyAtFullCharge;
            this.mass = mass;
        }
    }

    //Damage values are the proposals from the README. Big items stay under HandGrabHandler.maxGrabMass until two-handed grabbing exists
    static readonly ItemDefinition[] items =
    {
        new ItemDefinition("SO_Stapler",     "Stapler",     ItemClass.Small,  10f, 6f, true,  0.5f),
        new ItemDefinition("SO_CoffeeCup_01","Mug",         ItemClass.Small,   8f, 6f, true,  0.4f),
        new ItemDefinition("SO_Book_01",     "Paperweight", ItemClass.Small,  12f, 6f, true,  0.8f),
        new ItemDefinition("SO_Keyboard_01", "Keyboard",    ItemClass.Medium, 15f, 5f, false, 1.5f),
        new ItemDefinition("SO_Monitor_01",  "Monitor",     ItemClass.Medium, 20f, 5f, false, 3f),
        new ItemDefinition("SO_Plant_01",    "Plant",       ItemClass.Medium, 15f, 5f, false, 2.5f),
        new ItemDefinition("SO_Chair01",     "Office Chair",ItemClass.Big,    35f, 0f, false, 9f),
    };

    [MenuItem("Office Clash/Build Item Prefabs")]
    public static void Build()
    {
        EnsureFolder(DataFolder);
        EnsureFolder(PrefabFolder);

        int created = 0;

        foreach (ItemDefinition definition in items)
        {
            string modelPath = $"{ModelFolder}/{definition.model}.prefab";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

            if (model == null)
            {
                Debug.LogWarning($"Item model not found: {modelPath}");
                continue;
            }

            ItemData data = GetOrCreateData(definition);

            if (CreatePrefab(definition, model, data))
                created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Item prefabs built: {created} new. Check the Fusion prefab table picked them up (Tools > Fusion > Network Project Config).");
    }

    static ItemData GetOrCreateData(ItemDefinition definition)
    {
        string path = $"{DataFolder}/{definition.displayName}.asset";

        ItemData data = AssetDatabase.LoadAssetAtPath<ItemData>(path);

        if (data != null)
            return data;

        data = ScriptableObject.CreateInstance<ItemData>();
        data.displayName = definition.displayName;
        data.itemClass = definition.itemClass;
        data.baseDamage = definition.baseDamage;
        data.knockdownSpeed = definition.knockdownSpeed;
        data.knockdownOnlyAtFullCharge = definition.onlyAtFullCharge;
        data.throwSpeedMultiplier = definition.itemClass == ItemClass.Big ? 0.6f : 1f;
        data.spawnWeight = definition.itemClass == ItemClass.Small ? 3f : definition.itemClass == ItemClass.Medium ? 2f : 1f;

        AssetDatabase.CreateAsset(data, path);

        return data;
    }

    static bool CreatePrefab(ItemDefinition definition, GameObject model, ItemData data)
    {
        string prefabPath = $"{PrefabFolder}/Item_{definition.displayName.Replace(" ", "")}.prefab";

        //Never overwrite a prefab that was tuned by hand
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            return false;

        GameObject root = new GameObject($"Item_{definition.displayName}");
        root.tag = "CauseDamage";

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        //The model prefabs have colliders on the visual, add a box from the bounds when they do not
        if (root.GetComponentInChildren<Collider>() == null)
        {
            Bounds bounds = GetRendererBounds(root);

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(bounds.center);
            box.size = bounds.size;
        }

        foreach (Collider itemCollider in root.GetComponentsInChildren<Collider>())
        {
            //Concave mesh colliders cannot be used on dynamic rigidbodies
            if (itemCollider is MeshCollider meshCollider)
                meshCollider.convex = true;

            itemCollider.gameObject.tag = "CauseDamage";
        }

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = definition.mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        root.AddComponent<NetworkObject>();
        root.AddComponent<NetworkRigidbody3D>();

        ThrowableItem throwable = root.AddComponent<ThrowableItem>();

        SerializedObject serializedThrowable = new SerializedObject(throwable);
        serializedThrowable.FindProperty("itemData").objectReferenceValue = data;
        serializedThrowable.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        return true;
    }

    static Bounds GetRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one * 0.3f);

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path).Replace("\\", "/");

        EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
