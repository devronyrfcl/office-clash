using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class WorldHealthBarSetup
{
    const string PlayerPrefabPath = "Assets/Prefab/Network/Network Character Prefab.prefab";
    const string SliderPrefabPath = "Assets/Layer Lab/GUI Pro-CasualGame/Prefabs/Prefabs_Component_Sliders/Slider_Basic04_White.prefab";
    const string CanvasName = "Health Bar Canvas";

    [MenuItem("Office Clash/Setup World Health Bar")]
    public static void Setup()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            if (prefabRoot.transform.Find(CanvasName) != null)
            {
                Debug.Log("The player prefab already has a health bar.");
                return;
            }

            GameObject canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(WorldHealthBar));
            canvasObject.transform.SetParent(prefabRoot.transform, false);

            RectTransform canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(300f, 60f);

            //A canvas unit is a pixel, so scale it down to meters: 300 units become 1.2 m
            canvasRect.localScale = Vector3.one * 0.004f;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            GameObject sliderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SliderPrefabPath);
            GameObject sliderObject = (GameObject)PrefabUtility.InstantiatePrefab(sliderPrefab, canvasObject.transform);
            sliderObject.name = "Health Slider";

            RectTransform sliderRect = (RectTransform)sliderObject.transform;
            sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = Vector2.zero;

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            foreach (Graphic graphic in sliderObject.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            SerializedObject bar = new SerializedObject(canvasObject.GetComponent<WorldHealthBar>());
            bar.FindProperty("health").objectReferenceValue = prefabRoot.GetComponent<PlayerHealth>();
            bar.FindProperty("slider").objectReferenceValue = slider;
            bar.FindProperty("fill").objectReferenceValue = slider.fillRect.GetComponent<Image>();
            bar.FindProperty("canvas").objectReferenceValue = canvas;
            bar.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);

            Debug.Log("World health bar added to the player prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
