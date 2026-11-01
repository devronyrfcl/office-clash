using TMPro;
using UnityEngine;

//The action button is Jump, and Get Up while the local player is knocked down
public class JumpButtonLabel : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] string jumpText = "JUMP";
    [SerializeField] string getUpText = "GET UP";

    void Update()
    {
        NetworkPlayer local = NetworkPlayer.Local;

        bool isKnockedDown = local != null && local.isKnockedDown;

        string wanted = isKnockedDown ? getUpText : jumpText;

        if (label.text != wanted)
            label.text = wanted;
    }
}
