using UnityEngine;
using UnityEngine.UI;

//Health bar above a character. Lives on a world space canvas inside the player prefab,
//follows the player and always faces the camera. The local player sees the HUD bar instead
public class WorldHealthBar : MonoBehaviour
{
    [SerializeField] PlayerHealth health;
    [SerializeField] Slider slider;
    [SerializeField] Image fill;
    [SerializeField] Canvas canvas;

    //Height above the player's root, the ragdoll root is around the hips
    [SerializeField] float height = 2.4f;

    [SerializeField] bool hideForLocalPlayer = true;

    [SerializeField] Color fullColor = new Color(0.25f, 0.9f, 0.3f);
    [SerializeField] Color halfColor = new Color(1f, 0.85f, 0.15f);
    [SerializeField] Color lowColor = new Color(0.95f, 0.2f, 0.15f);

    Camera cachedCamera;

    void LateUpdate()
    {
        bool isSpawned = health != null && health.Object != null && health.Object.IsValid;

        bool isVisible = isSpawned && !health.IsEliminated && !(hideForLocalPlayer && health.Object.HasInputAuthority);

        canvas.enabled = isVisible;

        if (!isVisible)
            return;

        if (cachedCamera == null)
            cachedCamera = Camera.main;

        //Stay upright above the player even when the body is ragdolling, and face the camera
        transform.position = health.transform.position + Vector3.up * height;

        if (cachedCamera != null)
            transform.rotation = cachedCamera.transform.rotation;

        float value = health.MaxHealth > 0 ? (float)health.Health / health.MaxHealth : 0f;

        slider.value = value;

        fill.color = value > 0.5f ? Color.Lerp(halfColor, fullColor, (value - 0.5f) * 2f) : Color.Lerp(lowColor, halfColor, value * 2f);
    }
}
