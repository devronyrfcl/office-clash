using PlayFab;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInfoPopup : MonoBehaviour
{
    [SerializeField] GameObject popup;
    [SerializeField] LobbyPlayerStats playerStats;
    [SerializeField] TMP_Text playerName;
    [SerializeField] TMP_Text playerId;
    [SerializeField] TMP_Text playerLevel;
    [SerializeField] TMP_Text playerXp;
    [SerializeField] Slider xpBar;
    [SerializeField] TMP_Text[] matchStatValues;

    public void Open()
    {
        playerName.text = playerStats.PlayerName;
        playerId.text = string.IsNullOrEmpty(PlayFabSettings.staticPlayer.PlayFabId)
            ? "--" : "#" + PlayFabSettings.staticPlayer.PlayFabId;
        playerLevel.text = playerStats.PlayerLevel;
        playerXp.text = playerStats.PlayerXp;
        xpBar.normalizedValue = playerStats.PlayerXpProgress;
        // Temporary zeroes until match-stat tracking is connected.
        foreach (TMP_Text text in matchStatValues)
            if (text != null) text.text = "0";
        popup.SetActive(true);
        popup.transform.SetAsLastSibling();
    }

    public void Close() { popup.SetActive(false); }
    public void ApplyDisplayName(string displayName) { playerName.text = displayName; }
}
