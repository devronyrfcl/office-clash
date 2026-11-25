using UnityEngine;

// Keeps a UI document drifting around its original position without falling.
[RequireComponent(typeof(RectTransform))]
public class FloatingDocument : MonoBehaviour
{
    [SerializeField] Vector2 distance = new Vector2(18f, 12f);
    [SerializeField] float cycleSeconds = 5f;

    RectTransform rect;
    Vector2 origin;
    float phase;
    float elapsed;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        origin = rect.anchoredPosition;
        phase = transform.GetSiblingIndex() * 1.37f;
        cycleSeconds += transform.GetSiblingIndex() * 0.35f;
    }

    void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float angle = elapsed * (2f * Mathf.PI / Mathf.Max(0.1f, cycleSeconds));
        rect.anchoredPosition = origin + new Vector2(
            Mathf.Sin(angle + phase) - Mathf.Sin(phase),
            Mathf.Sin(angle * 0.8f + phase) - Mathf.Sin(phase)) * distance;
    }

    void OnDisable()
    {
        if (rect != null) rect.anchoredPosition = origin;
        elapsed = 0f;
    }
}
