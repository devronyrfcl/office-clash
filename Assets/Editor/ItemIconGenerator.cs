using System.IO;
using UnityEditor;
using UnityEngine;

//Renders a small picture of every item prefab and assigns it as the icon of its ItemData
public static class ItemIconGenerator
{
    const string PrefabFolder = "Assets/Prefab/Items";
    const string IconFolder = "Assets/Items/Icons";
    const int Size = 192;

    [MenuItem("Office Clash/Generate Item Icons")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(IconFolder))
            AssetDatabase.CreateFolder("Assets/Items", "Icons");

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });

        int created = 0;

        foreach (string guid in guids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            ThrowableItem throwable = prefab.GetComponent<ThrowableItem>();

            if (throwable == null || throwable.Data == null)
                continue;

            string iconPath = $"{IconFolder}/{throwable.Data.displayName.Replace(" ", "")}.png";

            Texture2D texture = Render(prefab);

            if (texture == null)
                continue;

            File.WriteAllBytes(iconPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(iconPath);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            throwable.Data.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            EditorUtility.SetDirty(throwable.Data);

            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Item icons generated: {created}");
    }

    static Texture2D Render(GameObject itemPrefab)
    {
        //The first child is the model of the item, the root only holds networking components
        if (itemPrefab.transform.childCount == 0)
            return null;

        PreviewRenderUtility preview = new PreviewRenderUtility();

        try
        {
            GameObject model = Object.Instantiate(itemPrefab.transform.GetChild(0).gameObject);
            model.transform.position = Vector3.zero;
            model.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

            preview.AddSingleGO(model);

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
                return null;

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Camera camera = preview.camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;

            //Look at the item from slightly above and the side, far enough to fit it
            float distance = bounds.extents.magnitude / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            Vector3 direction = new Vector3(0.45f, 0.5f, -1f).normalized;

            camera.transform.position = bounds.center + direction * distance;
            camera.transform.LookAt(bounds.center);

            preview.lights[0].intensity = 1.1f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            preview.lights[1].intensity = 0.7f;
            preview.ambientColor = new Color(0.55f, 0.55f, 0.6f);

            preview.BeginStaticPreview(new Rect(0f, 0f, Size, Size));
            camera.Render();

            return preview.EndStaticPreview();
        }
        finally
        {
            preview.Cleanup();
        }
    }
}
