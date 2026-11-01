using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class JoystickSetup
{
    const string PackageFrames = "Assets/Layer Lab/GUI Pro-CasualGame/Prefabs/Prefabs_Component_Frames/";
    const string JoystickName = "Joystick";

    [MenuItem("Office Clash/Setup Joystick")]
    public static void Setup()
    {
        GameObject safeArea = GameObject.Find("HUD Canvas/Safe Area");

        if (safeArea == null)
        {
            Debug.LogError("Run Office Clash > Setup HUD Canvas first.");
            return;
        }

        if (safeArea.transform.Find(JoystickName) != null)
        {
            Debug.Log("Joystick already exists.");
            return;
        }

        //The touch area covers the lower left of the screen. It is invisible but receives touches
        GameObject area = new GameObject(JoystickName, typeof(RectTransform), typeof(Image), typeof(VirtualJoystick));
        area.transform.SetParent(safeArea.transform, false);

        RectTransform areaRect = area.GetComponent<RectTransform>();
        areaRect.anchorMin = new Vector2(0f, 0f);
        areaRect.anchorMax = new Vector2(0.45f, 0.85f);
        areaRect.pivot = Vector2.zero;
        areaRect.offsetMin = Vector2.zero;
        areaRect.offsetMax = Vector2.zero;

        Image touchImage = area.GetComponent<Image>();
        touchImage.color = new Color(1f, 1f, 1f, 0f);
        touchImage.raycastTarget = true;

        //Visuals come from the GUI Pro-CasualGame circle frames, the package prefabs stay untouched
        RectTransform stickBase = CreateCircle(area.transform, PackageFrames + "BasicFrame_Circle77_Dark.prefab", "Stick Base", 3f);
        RectTransform knob = CreateCircle(area.transform, PackageFrames + "BasicFrame_Circle77_White.prefab", "Stick Knob", 1.6f);

        SerializedObject serialized = new SerializedObject(area.GetComponent<VirtualJoystick>());
        serialized.FindProperty("stickBase").objectReferenceValue = stickBase;
        serialized.FindProperty("knob").objectReferenceValue = knob;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("Joystick created under HUD Canvas/Safe Area.");
    }

    static RectTransform CreateCircle(Transform parent, string prefabPath, string objectName, float scale)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = objectName;

        RectTransform rect = (RectTransform)instance.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one * scale;

        //Only the circle, no icon inside, and nothing here may block touches
        foreach (Graphic graphic in instance.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        Transform icon = instance.transform.Find("Icon");

        if (icon != null)
            icon.gameObject.SetActive(false);

        return rect;
    }
}
