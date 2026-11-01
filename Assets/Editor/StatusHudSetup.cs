using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StatusHudSetup
{
    const string Root = "Assets/Layer Lab/GUI Pro-CasualGame/Prefabs/";
    const string GroupName = "Status HUD";

    [MenuItem("Office Clash/Setup Status HUD")]
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
            Debug.Log("Status HUD already exists.");
            return;
        }

        GameObject group = new GameObject(GroupName, typeof(RectTransform), typeof(HudController));
        group.transform.SetParent(safeArea.transform, false);

        //Behind the touch controls
        group.transform.SetAsFirstSibling();

        RectTransform groupRect = (RectTransform)group.transform;
        groupRect.anchorMin = Vector2.zero;
        groupRect.anchorMax = Vector2.one;
        groupRect.offsetMin = Vector2.zero;
        groupRect.offsetMax = Vector2.zero;

        Transform parent = group.transform;

        //Top left: health
        GameObject healthBar = Create(parent, "Health Bar", "Prefabs_Component_Sliders/Slider_Basic04_Green.prefab", new Vector2(0f, 1f), new Vector2(215f, -60f), 1.3f);
        MakeNonInteractive(healthBar);
        GameObject healthLabel = Create(parent, "Health Text", "Prefabs_Component_Labels/Label_Round01_White.prefab", new Vector2(0f, 1f), new Vector2(215f, -125f), 0.8f);

        //Top center: players left and round time
        GameObject playersLabel = Create(parent, "Players Text", "Prefabs_Component_Labels/Label_Round01_White.prefab", new Vector2(0.5f, 1f), new Vector2(-150f, -55f), 1f);
        GameObject timerLabel = Create(parent, "Timer Text", "Prefabs_Component_Labels/Label_Round01_White.prefab", new Vector2(0.5f, 1f), new Vector2(150f, -55f), 1f);

        //Round banner under the top row
        GameObject banner = Create(parent, "Round Banner", "Prefabs_Component_Labels/Title_Flag01_Blue.prefab", new Vector2(0.5f, 1f), new Vector2(0f, -300f), 0.55f);

        //Bottom center: hand items and the throw charge
        GameObject chargeBar = Create(parent, "Throw Charge Bar", "Prefabs_Component_Sliders/Slider_Basic04_Red.prefab", new Vector2(0.5f, 0f), new Vector2(0f, 200f), 1.8f);
        MakeNonInteractive(chargeBar);

        GameObject leftHand = Create(parent, "Left Hand Item", "Prefabs_Component_Frames/BasicFrame_Circle77_White.prefab", new Vector2(0.5f, 0f), new Vector2(-70f, 310f), 1.2f);
        GameObject rightHand = Create(parent, "Right Hand Item", "Prefabs_Component_Frames/BasicFrame_Circle77_White.prefab", new Vector2(0.5f, 0f), new Vector2(70f, 310f), 1.2f);

        //Middle of the screen: only while knocked down
        GameObject getUp = Create(parent, "Get Up Prompt", "Prefabs_Component_Labels/Label_Round01_White.prefab", new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), 1.2f);
        SetLabel(getUp, "TAP GET UP!");

        SetLabel(healthLabel, "100/100");
        SetLabel(playersLabel, "PLAYERS 0");
        SetLabel(timerLabel, "--:--");

        Image leftIcon = PrepareHandFrame(leftHand);
        Image rightIcon = PrepareHandFrame(rightHand);

        //Connect everything
        SerializedObject hud = new SerializedObject(group.GetComponent<HudController>());
        hud.FindProperty("healthSlider").objectReferenceValue = healthBar.GetComponent<Slider>();
        hud.FindProperty("healthText").objectReferenceValue = healthLabel.GetComponentInChildren<TMP_Text>(true);
        hud.FindProperty("playersText").objectReferenceValue = playersLabel.GetComponentInChildren<TMP_Text>(true);
        hud.FindProperty("timerText").objectReferenceValue = timerLabel.GetComponentInChildren<TMP_Text>(true);
        hud.FindProperty("banner").objectReferenceValue = banner;
        hud.FindProperty("bannerTitle").objectReferenceValue = banner.transform.Find("Title_Text").GetComponent<TMP_Text>();
        hud.FindProperty("bannerSubtitle").objectReferenceValue = banner.transform.Find("SubTitle_Text").GetComponent<TMP_Text>();
        hud.FindProperty("chargeSlider").objectReferenceValue = chargeBar.GetComponent<Slider>();
        hud.FindProperty("getUpPrompt").objectReferenceValue = getUp;

        SerializedProperty handIcons = hud.FindProperty("handIcons");
        handIcons.arraySize = 2;
        handIcons.GetArrayElementAtIndex(0).objectReferenceValue = leftIcon;
        handIcons.GetArrayElementAtIndex(1).objectReferenceValue = rightIcon;

        hud.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("Status HUD created under HUD Canvas/Safe Area.");
    }

    static GameObject Create(Transform parent, string objectName, string prefabFile, Vector2 anchor, Vector2 position, float scale)
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

        //The HUD only shows things, it never takes touches from the controls
        foreach (Graphic graphic in instance.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        return instance;
    }

    static void MakeNonInteractive(GameObject sliderObject)
    {
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
    }

    static void SetLabel(GameObject labelObject, string text)
    {
        labelObject.GetComponentInChildren<TMP_Text>(true).text = text;
    }

    //The circle frame stays visible as an empty slot, its Icon child shows the held item
    static Image PrepareHandFrame(GameObject frame)
    {
        Image icon = frame.transform.Find("Icon").GetComponent<Image>();

        icon.sprite = null;
        icon.enabled = false;
        icon.preserveAspect = true;

        return icon;
    }
}
