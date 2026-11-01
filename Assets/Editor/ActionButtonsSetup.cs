using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ActionButtonsSetup
{
    const string PackageButtons = "Assets/Layer Lab/GUI Pro-CasualGame/Prefabs/Prefabs_Component_Buttons/";
    const string GroupName = "Action Buttons";

    [MenuItem("Office Clash/Setup Action Buttons")]
    public static void Setup()
    {
        GameObject safeArea = GameObject.Find("HUD Canvas/Safe Area");

        if (safeArea == null)
        {
            Debug.LogError("Run Office Clash > Setup HUD Canvas first.");
            return;
        }

        if (safeArea.transform.Find(GroupName) != null)
        {
            Debug.Log("Action buttons already exist.");
            return;
        }

        GameObject group = new GameObject(GroupName, typeof(RectTransform));
        group.transform.SetParent(safeArea.transform, false);

        RectTransform groupRect = (RectTransform)group.transform;
        groupRect.anchorMin = Vector2.zero;
        groupRect.anchorMax = Vector2.one;
        groupRect.offsetMin = Vector2.zero;
        groupRect.offsetMax = Vector2.zero;

        //Positions are measured from the bottom right corner of the safe area, in 1920x1080 canvas units
        GameObject jump = CreateButton(group.transform, "Jump Button", "Button_Circle147_White.prefab", "JUMP", new Vector2(-210f, 210f), 1.3f, InputButton.Jump, TouchButton.Mode.Tap);
        jump.AddComponent<JumpButtonLabel>();
        SerializedObject jumpLabel = new SerializedObject(jump.GetComponent<JumpButtonLabel>());
        jumpLabel.FindProperty("label").objectReferenceValue = jump.GetComponentInChildren<TMP_Text>(true);
        jumpLabel.ApplyModifiedPropertiesWithoutUndo();

        CreateButton(group.transform, "Grab Button", "Button_Circle147_Gray.prefab", "GRAB", new Vector2(-210f, 470f), 1.1f, InputButton.Grab, TouchButton.Mode.Tap);
        CreateButton(group.transform, "Throw Left Button", "Button_Circle128_Dark.prefab", "L", new Vector2(-470f, 360f), 1.2f, InputButton.ThrowLeft, TouchButton.Mode.Hold);
        CreateButton(group.transform, "Throw Right Button", "Button_Circle128_Dark.prefab", "R", new Vector2(-480f, 150f), 1.2f, InputButton.ThrowSecond, TouchButton.Mode.Hold);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("Action buttons created under HUD Canvas/Safe Area.");
    }

    static TMP_Text CreateLabel(Transform parent)
    {
        GameObject reference = AssetDatabase.LoadAssetAtPath<GameObject>(PackageButtons + "Button_Circle147_Navy.prefab");
        TMP_Text referenceText = reference.GetComponentInChildren<TMP_Text>(true);

        GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)labelObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.font = referenceText.font;
        text.fontSharedMaterial = referenceText.fontSharedMaterial;
        text.fontSize = 44f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        return text;
    }

    static GameObject CreateButton(Transform parent, string objectName, string prefabFile, string label, Vector2 position, float scale, InputButton input, TouchButton.Mode mode)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackageButtons + prefabFile);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = objectName;

        RectTransform rect = (RectTransform)instance.transform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one * scale;

        //Text only, the package icon is not used here
        Transform icon = instance.transform.Find("Icon");

        if (icon != null)
            icon.gameObject.SetActive(false);

        TMP_Text text = instance.GetComponentInChildren<TMP_Text>(true);

        //Some circle buttons (the 128 size) have no text, make one that looks like the others
        if (text == null)
            text = CreateLabel(instance.transform);

        text.text = label;

        TouchButton touchButton = instance.AddComponent<TouchButton>();

        SerializedObject serialized = new SerializedObject(touchButton);
        serialized.FindProperty("button").enumValueIndex = (int)input;
        serialized.FindProperty("mode").enumValueIndex = (int)mode;

        //Throw buttons aim by dragging away from the button
        serialized.FindProperty("aimByDrag").boolValue = mode == TouchButton.Mode.Hold;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return instance;
    }
}
