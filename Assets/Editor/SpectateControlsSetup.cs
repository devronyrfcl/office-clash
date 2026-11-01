using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SpectateControlsSetup
{
    const string Root = "Assets/Layer Lab/GUI Pro-CasualGame/Prefabs/";
    const string GroupName = "Spectate Controls";

    [MenuItem("Office Clash/Setup Spectate Controls")]
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
            Debug.Log("Spectate controls already exist.");
            return;
        }

        //The group always stays active so its script can show and hide the panel
        GameObject group = new GameObject(GroupName, typeof(RectTransform), typeof(SpectateControls));
        group.transform.SetParent(safeArea.transform, false);

        RectTransform groupRect = (RectTransform)group.transform;
        groupRect.anchorMin = Vector2.zero;
        groupRect.anchorMax = Vector2.one;
        groupRect.offsetMin = Vector2.zero;
        groupRect.offsetMax = Vector2.zero;

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(group.transform, false);

        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject label = Instantiate(panel.transform, "Watching Text", "Prefabs_Component_Labels/Label_Round01_White.prefab", new Vector2(0.5f, 0f), new Vector2(0f, 250f), 1.1f);
        GameObject nextButton = Instantiate(panel.transform, "Next Button", "Prefabs_Component_Buttons/Button01_195_BtnText_Blue.prefab", new Vector2(0.5f, 0f), new Vector2(0f, 130f), 1f);
        nextButton.GetComponentInChildren<TMP_Text>(true).text = "NEXT";
        label.GetComponentInChildren<TMP_Text>(true).text = "WATCHING";

        //The button needs to take touches, the label does not
        foreach (Graphic graphic in label.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        SerializedObject controls = new SerializedObject(group.GetComponent<SpectateControls>());
        controls.FindProperty("panel").objectReferenceValue = panel;
        controls.FindProperty("nextButton").objectReferenceValue = nextButton.GetComponent<Button>();
        controls.FindProperty("watchingText").objectReferenceValue = label.GetComponentInChildren<TMP_Text>(true);

        //Joystick, action buttons and pocket slots are hidden for spectators
        string[] controlNames = { "Joystick", "Action Buttons", "Pocket Slots" };
        SerializedProperty hidden = controls.FindProperty("hideWhileSpectating");
        hidden.arraySize = controlNames.Length;

        for (int i = 0; i < controlNames.Length; i++)
            hidden.GetArrayElementAtIndex(i).objectReferenceValue = safeArea.transform.Find(controlNames[i]).gameObject;

        controls.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("Spectate controls created under HUD Canvas/Safe Area.");
    }

    static GameObject Instantiate(Transform parent, string objectName, string prefabFile, Vector2 anchor, Vector2 position, float scale)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + prefabFile);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = objectName;

        RectTransform rect = (RectTransform)instance.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one * scale;

        return instance;
    }
}
