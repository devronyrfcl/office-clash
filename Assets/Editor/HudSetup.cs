using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class HudSetup
{
    const string CanvasName = "HUD Canvas";
    const string SafeAreaName = "Safe Area";

    [MenuItem("Office Clash/Setup HUD Canvas")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();

        Canvas canvas = FindCanvas();

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
        }

        canvas.name = CanvasName;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        //Phones are designed in landscape at 1920x1080, a match of 0.5 keeps the layout on tall and wide screens
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();

        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        //Everything the player touches goes inside the safe area
        Transform safeArea = canvas.transform.Find(SafeAreaName);

        if (safeArea == null)
        {
            GameObject safeAreaObject = new GameObject(SafeAreaName, typeof(RectTransform), typeof(SafeArea));
            safeAreaObject.transform.SetParent(canvas.transform, false);

            RectTransform rect = safeAreaObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        //The project uses the legacy Input Manager, so the standalone input module
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"HUD canvas ready in {scene.name}.");
    }

    //The scene had an empty Canvas from the early prototype, reuse it
    static Canvas FindCanvas()
    {
        GameObject named = GameObject.Find(CanvasName);

        if (named != null)
            return named.GetComponent<Canvas>();

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);

        foreach (Canvas canvas in canvases)
        {
            if (canvas.transform.parent == null && canvas.transform.childCount == 0)
                return canvas;
        }

        return null;
    }
}
