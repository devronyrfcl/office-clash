using TMPro;
using UnityEngine;
using UnityEngine.UI;

//Shown while the local player is eliminated: a Next button and the name of who is being watched.
//The game controls are hidden at the same time, there is nothing to control any more
public class SpectateControls : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Button nextButton;
    [SerializeField] TMP_Text watchingText;

    //Touch controls that make no sense for a spectator
    [SerializeField] GameObject[] hideWhileSpectating;

    bool wasSpectating = false;

    void Awake()
    {
        nextButton.onClick.AddListener(SpectateCamera.RequestNext);

        panel.SetActive(false);
    }

    void Update()
    {
        bool isSpectating = SpectateCamera.IsSpectating;

        if (isSpectating != wasSpectating)
        {
            wasSpectating = isSpectating;

            panel.SetActive(isSpectating);

            foreach (GameObject control in hideWhileSpectating)
                control.SetActive(!isSpectating);
        }

        if (!isSpectating)
            return;

        PlayerHealth target = SpectateCamera.Target;

        string text = target != null && target.Object != null && target.Object.IsValid ? $"WATCHING PLAYER {target.Object.InputAuthority.PlayerId}" : "NOBODY TO WATCH";

        if (watchingText.text != text)
            watchingText.text = text;
    }
}
