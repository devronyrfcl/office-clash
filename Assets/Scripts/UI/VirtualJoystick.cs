using UnityEngine;
using UnityEngine.EventSystems;

//Floating stick for the left side of the screen. The stick appears where the thumb lands,
//and its direction goes to TouchInput.Move. Attach to the touch area, a transparent graphic that receives raycasts
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] RectTransform stickBase;
    [SerializeField] RectTransform knob;

    //How far the knob can move from the center, in canvas units
    [SerializeField] float radius = 110f;

    //Where the stick waits when nobody touches it, measured from the bottom left of the touch area
    [SerializeField] Vector2 restPosition = new Vector2(240f, 240f);

    [SerializeField] float idleAlpha = 0.35f;

    RectTransform touchArea;
    CanvasGroup canvasGroup;

    int activePointerId = int.MinValue;
    bool isActive = false;

    void Awake()
    {
        touchArea = (RectTransform)transform;

        //The touch area must have its pivot in the bottom left, so local points are measured from there
        touchArea.pivot = Vector2.zero;

        canvasGroup = stickBase.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = stickBase.gameObject.AddComponent<CanvasGroup>();

        //The visuals never take touches away from the area
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        ShowAtRest();
    }

    void OnDisable()
    {
        Release();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        //A second finger on the area is ignored
        if (isActive)
            return;

        isActive = true;
        activePointerId = eventData.pointerId;

        if (!ScreenToArea(eventData, out Vector2 localPoint))
            return;

        stickBase.anchoredPosition = localPoint;
        knob.anchoredPosition = localPoint;

        canvasGroup.alpha = 1f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isActive || eventData.pointerId != activePointerId)
            return;

        if (!ScreenToArea(eventData, out Vector2 localPoint))
            return;

        Vector2 offset = Vector2.ClampMagnitude(localPoint - stickBase.anchoredPosition, radius);

        knob.anchoredPosition = stickBase.anchoredPosition + offset;

        TouchInput.Move = offset / radius;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isActive || eventData.pointerId != activePointerId)
            return;

        Release();
    }

    void Release()
    {
        isActive = false;
        activePointerId = int.MinValue;

        TouchInput.Move = Vector2.zero;

        if (stickBase != null && knob != null)
            ShowAtRest();
    }

    void ShowAtRest()
    {
        stickBase.anchoredPosition = restPosition;
        knob.anchoredPosition = restPosition;

        canvasGroup.alpha = idleAlpha;
    }

    bool ScreenToArea(PointerEventData eventData, out Vector2 localPoint)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(touchArea, eventData.position, eventData.pressEventCamera, out localPoint);
    }
}
