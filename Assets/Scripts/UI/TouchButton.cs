using UnityEngine;
using UnityEngine.EventSystems;

//Turns a UI button into a game input. Tap is a single press, Hold stays pressed until the finger lifts
//(even when the finger slides off the button, so the throw can be aimed)
public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public enum Mode
    {
        Tap,
        Hold
    }

    [SerializeField] InputButton button;
    [SerializeField] Mode mode = Mode.Tap;

    //For hold buttons: dragging away from the button sets the throw direction
    [SerializeField] bool aimByDrag = false;

    //How far the finger must move from the middle of the button before it aims, in canvas units
    [SerializeField] float aimDeadZone = 70f;

    int activePointerId = int.MinValue;
    bool isPressed = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        //A second finger on the same button is ignored
        if (isPressed)
            return;

        isPressed = true;
        activePointerId = eventData.pointerId;

        if (mode == Mode.Tap)
            TouchInput.Tap(button);
        else
            TouchInput.SetHeld(button, true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPressed || eventData.pointerId != activePointerId)
            return;

        Release();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!aimByDrag || !isPressed || eventData.pointerId != activePointerId)
            return;

        RectTransform rect = (RectTransform)transform;

        //Where the middle of the button is on the screen
        Vector2 center = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, rect.position);

        Canvas canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;

        Vector2 offset = (eventData.position - center) / scale;

        //Staying on the button means no manual aim, the game aims by itself
        TouchInput.Aim = offset.magnitude > aimDeadZone ? offset.normalized : Vector2.zero;
    }

    void OnDisable()
    {
        Release();
    }

    void Release()
    {
        if (!isPressed)
            return;

        isPressed = false;
        activePointerId = int.MinValue;

        if (mode == Mode.Hold)
            TouchInput.SetHeld(button, false);

        if (aimByDrag)
            TouchInput.Aim = Vector2.zero;
    }
}
