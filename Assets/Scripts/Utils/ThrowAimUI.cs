using UnityEngine;

//Shows the path of the throw and a power bar while the local player charges a throw
public class ThrowAimUI : MonoBehaviour
{
    [SerializeField] float lineWidth = 0.06f;

    [SerializeField] Color weakColor = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] Color mediumColor = new Color(1f, 0.85f, 0.1f);
    [SerializeField] Color strongColor = new Color(1f, 0.2f, 0.15f);

    NetworkPlayer networkPlayer;
    LineRenderer lineRenderer;

    Vector3[] points = new Vector3[80];

    void Awake()
    {
        networkPlayer = GetComponent<NetworkPlayer>();

        //Own child object, a GameObject can only have one LineRenderer and ShootSystem uses the one on the player
        GameObject lineObject = new GameObject("Throw Aim Line");
        lineObject.transform.SetParent(transform, false);

        lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.enabled = false;

        //Sprites/Default uses vertex colors and works in every render pipeline
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    Color GetChargeColor(float charge)
    {
        return charge < 0.5f ? Color.Lerp(weakColor, mediumColor, charge * 2f) : Color.Lerp(mediumColor, strongColor, (charge - 0.5f) * 2f);
    }

    void LateUpdate()
    {
        if(!networkPlayer.IsChargingThrow)
        {
            lineRenderer.enabled = false;
            return;
        }

        int numberOfPoints = networkPlayer.GetThrowPreview(points);

        lineRenderer.enabled = numberOfPoints > 1;
        lineRenderer.positionCount = numberOfPoints;

        for (int i = 0; i < numberOfPoints; i++)
        {
            lineRenderer.SetPosition(i, points[i]);
        }

        Color chargeColor = GetChargeColor(networkPlayer.throwCharge);

        lineRenderer.startColor = chargeColor;
        lineRenderer.endColor = new Color(chargeColor.r, chargeColor.g, chargeColor.b, 0.35f);
    }

    void OnGUI()
    {
        if(!networkPlayer.IsChargingThrow)
            return;

        float width = 260f;
        float height = 22f;

        Rect barRect = new Rect((Screen.width - width) * 0.5f, Screen.height - 80f, width, height);

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(barRect.x - 3f, barRect.y - 3f, barRect.width + 6f, barRect.height + 6f), Texture2D.whiteTexture);

        GUI.color = GetChargeColor(networkPlayer.throwCharge);
        GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * networkPlayer.throwCharge, barRect.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(barRect.x, barRect.y - 24f, barRect.width, 20f), "Throw power - release F to throw");
    }

    void OnDestroy()
    {
        if(lineRenderer != null && lineRenderer.material != null)
            Destroy(lineRenderer.material);
    }
}
