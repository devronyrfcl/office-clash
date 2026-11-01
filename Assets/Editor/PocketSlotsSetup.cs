using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PocketSlotsSetup
{
    const string SlotPrefab = "Assets/Layer Lab/GUI Pro-CasualGame/Prefabs/Prefabs_Component_Buttons/Button_Square03_White.prefab";
    const string GroupName = "Pocket Slots";

    [MenuItem("Office Clash/Setup Pocket Slots")]
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
            Debug.Log("Pocket slots already exist.");
            return;
        }

        GameObject group = new GameObject(GroupName, typeof(RectTransform));
        group.transform.SetParent(safeArea.transform, false);

        RectTransform groupRect = (RectTransform)group.transform;
        groupRect.anchorMin = Vector2.zero;
        groupRect.anchorMax = Vector2.one;
        groupRect.offsetMin = Vector2.zero;
        groupRect.offsetMax = Vector2.zero;

        //Top right corner, one slot next to the other
        CreateSlot(group.transform, "Pocket Slot 1", new Vector2(-240f, -110f), 0, InputButton.SwapPocket1);
        CreateSlot(group.transform, "Pocket Slot 2", new Vector2(-90f, -110f), 1, InputButton.SwapPocket2);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("Pocket slots created under HUD Canvas/Safe Area.");
    }

    static void CreateSlot(Transform parent, string objectName, Vector2 position, int pocketIndex, InputButton input)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefab);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = objectName;

        RectTransform rect = (RectTransform)instance.transform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;

        //The package icon child shows the item, empty slots show only the frame
        Image icon = instance.transform.Find("Icon").GetComponent<Image>();
        icon.sprite = null;
        icon.enabled = false;
        icon.raycastTarget = false;

        TouchButton touchButton = instance.AddComponent<TouchButton>();
        SerializedObject serializedButton = new SerializedObject(touchButton);
        serializedButton.FindProperty("button").enumValueIndex = (int)input;
        serializedButton.FindProperty("mode").enumValueIndex = (int)TouchButton.Mode.Tap;
        serializedButton.ApplyModifiedPropertiesWithoutUndo();

        PocketSlotUI slot = instance.AddComponent<PocketSlotUI>();
        SerializedObject serializedSlot = new SerializedObject(slot);
        serializedSlot.FindProperty("pocketIndex").intValue = pocketIndex;
        serializedSlot.FindProperty("icon").objectReferenceValue = icon;
        serializedSlot.ApplyModifiedPropertiesWithoutUndo();
    }
}
