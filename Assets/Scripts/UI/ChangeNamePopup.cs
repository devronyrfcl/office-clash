using System;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;

public class ChangeNamePopup : MonoBehaviour
{
    [Serializable] class RenameRequest { public string newName; public string sessionTicket; }
    [Serializable] class RenameResult { public bool ok; public bool uncertain; public string displayName; public string message; }

    [SerializeField] GameObject popup;
    [SerializeField] TMP_InputField nameInput;
    [SerializeField] TMP_Text information;
    bool busy;

    public void Open()
    {
        if (busy) return;
        nameInput.text = "";
        information.text = "Change your display name for 50 gold.";
        popup.SetActive(true);
        popup.transform.SetAsLastSibling();
    }

    public void Close() { if (!busy) popup.SetActive(false); }

    public void Confirm()
    {
        if (busy) return;
        string newName = nameInput.text.Trim();
        if (newName.Length < 3 || newName.Length > 25)
        {
            information.text = "Enter a name between 3 and 25 characters.";
            return;
        }
        string ticket = PlayFabSettings.staticPlayer.ClientSessionTicket;
        if (!PlayFabClientAPI.IsClientLoggedIn() || string.IsNullOrEmpty(ticket))
        {
            information.text = "Please sign in again.";
            return;
        }

        busy = true;
        information.text = "Changing your name...";
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
        {
            FunctionName = "changeDisplayName",
            FunctionParameter = new RenameRequest { newName = newName, sessionTicket = ticket },
            GeneratePlayStreamEvent = false
        }, result =>
        {
            if (this == null) return;
            busy = false;
            if (result.Error != null)
            {
                Debug.LogWarning("Name change CloudScript: " + result.Error.Error);
                information.text = "Could not confirm the change. Check your name and gold before trying again.";
                RefreshPlayer();
                return;
            }
            try
            {
                string json = result.FunctionResult as string;
                var response = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<RenameResult>(json);
                if (response == null) throw new FormatException("Missing rename response");
                if (!response.ok)
                {
                    information.text = string.IsNullOrEmpty(response.message) ? "Could not change your name." : response.message;
                    if (response.uncertain) RefreshPlayer();
                    return;
                }
                if (string.IsNullOrEmpty(response.displayName)) throw new FormatException("Missing display name");
                var stats = GetComponent<LobbyPlayerStats>();
                if (stats != null) { stats.Refresh(); stats.ApplyDisplayName(response.displayName); }
                var profile = GetComponent<PlayerInfoPopup>();
                if (profile != null) profile.ApplyDisplayName(response.displayName);
                popup.SetActive(false);
            }
            catch (Exception)
            {
                information.text = "Could not confirm the change. Check your name and gold before trying again.";
                RefreshPlayer();
            }
        }, error =>
        {
            if (this == null) return;
            busy = false;
            Debug.LogWarning("Name change request failed: " + error.Error);
            information.text = "Could not confirm the change. Check your name and gold before trying again.";
            RefreshPlayer();
        });
    }

    void RefreshPlayer()
    {
        var stats = GetComponent<LobbyPlayerStats>();
        if (stats != null) stats.Refresh();
    }
}
