using UnityEngine;

//Keeps its children inside the screen area that is not covered by notches, rounded corners or the home bar
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    RectTransform rectTransform;

    Rect appliedSafeArea;
    Vector2Int appliedScreenSize;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        Apply();
    }

    void Update()
    {
        //The safe area changes when the phone is rotated
        if (Screen.safeArea != appliedSafeArea || Screen.width != appliedScreenSize.x || Screen.height != appliedScreenSize.y)
            Apply();
    }

    void Apply()
    {
        appliedSafeArea = Screen.safeArea;
        appliedScreenSize = new Vector2Int(Screen.width, Screen.height);

        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Vector2 anchorMin = appliedSafeArea.position;
        Vector2 anchorMax = appliedSafeArea.position + appliedSafeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
