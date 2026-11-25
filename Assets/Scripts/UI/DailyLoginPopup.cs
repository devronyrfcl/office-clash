using System;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DailyLoginPopup : MonoBehaviour
{
    [Serializable] class PopupStatus { public bool shouldShow, claimed; public string date; public int day, daysInMonth; }
    [Serializable] class DateRequest { public string date; }
    [Serializable] class AcknowledgeResult { public bool ok; }
    [Serializable] class ClaimResult { public bool ok, uncertain, alreadyClaimed; public string reward, message; public int amount; }

    [SerializeField] GameObject popup;
    [SerializeField] TMP_Text information;
    [SerializeField] TMP_Text[] dayLabels;
    [SerializeField] TMP_Text[] rewardLabels;
    [SerializeField] Outline[] dayHighlights;
    [SerializeField] GameObject[] dayCells;
    [SerializeField] Button collectButton;
    [SerializeField] TMP_Text collectButtonText;
    [SerializeField] LobbyPlayerStats playerStats;
    int requestVersion;
    bool showing, busy, claimed, uncertain;
    string shownDate;

    void Awake() { if (popup != null) popup.SetActive(false); }
    public void CheckOnLogin() { RequestStatus(false); }
    public void OpenCalendar() { RequestStatus(true); }

    void RequestStatus(bool force)
    {
        if (!PlayFabClientAPI.IsClientLoggedIn() || showing || busy) return;
        int version = ++requestVersion;
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
        {
            FunctionName = "getDailyLoginPopup", GeneratePlayStreamEvent = false
        }, result =>
        {
            if (!Current(version)) return;
            if (result.Error != null) { Debug.LogWarning("Daily login popup is unavailable."); return; }
            try
            {
                string json = result.FunctionResult as string;
                var status = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PopupStatus>(json);
                if (status == null || status.daysInMonth < 28 || status.daysInMonth > 31 || status.day < 1 || status.day > status.daysInMonth || string.IsNullOrEmpty(status.date))
                    throw new FormatException("Invalid daily login status");
                if (!status.shouldShow && !force) return;
                Show(status);
                if (status.shouldShow) Acknowledge(status.date, version);
            }
            catch (Exception) { Debug.LogWarning("Could not read daily login popup status."); }
        }, error => { if (Current(version)) Debug.LogWarning("Could not load daily login popup: " + error.Error); });
    }

    void Show(PopupStatus status)
    {
        shownDate = status.date;
        claimed = status.claimed;
        busy = false;
        uncertain = false;
        for (int i = 0; i < dayLabels.Length; i++)
        {
            dayLabels[i].text = "DAY " + (i + 1);
            rewardLabels[i].text = i % 3 == 0 ? "100" : (i % 3 == 1 ? "5" : "10");
            dayHighlights[i].enabled = i == status.day - 1;
            dayCells[i].SetActive(i < status.daysInMonth);
        }
        information.text = "DAY " + status.day + " / " + status.daysInMonth;
        UpdateButton();
        showing = true;
        popup.SetActive(true);
        popup.transform.SetAsLastSibling();
    }

    public void Collect()
    {
        if (busy || claimed || uncertain || string.IsNullOrEmpty(shownDate) || !PlayFabClientAPI.IsClientLoggedIn()) return;
        busy = true;
        UpdateButton();
        int version = requestVersion;
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
        {
            FunctionName = "claimDailyLoginReward", FunctionParameter = new DateRequest { date = shownDate }, GeneratePlayStreamEvent = false
        }, result =>
        {
            if (!Current(version)) return;
            try
            {
                string json = result.FunctionResult as string;
                var response = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<ClaimResult>(json);
                if (result.Error != null || response == null) { ClaimUncertain(); return; }
                if (response.ok)
                {
                    claimed = true;
                    information.text = response.alreadyClaimed ? "TODAY'S REWARD COLLECTED" : "COLLECTED: " + response.amount + " " + (response.reward == "GD" ? "GOLD" : response.reward == "DM" ? "DIAMONDS" : "ENERGY");
                    if (playerStats != null) playerStats.Refresh();
                }
                else
                {
                    information.text = string.IsNullOrEmpty(response.message) ? "Could not collect reward." : response.message;
                    if (response.uncertain) { uncertain = true; busy = false; UpdateButton(); return; }
                }
                busy = false;
                UpdateButton();
            }
            catch (Exception) { ClaimUncertain(); }
        }, error => { if (Current(version)) ClaimUncertain(); });
    }

    void ClaimUncertain()
    {
        information.text = "Could not confirm reward. Check your balance or contact support.";
        uncertain = true;
        busy = false;
        UpdateButton();
    }

    void UpdateButton()
    {
        if (collectButton != null) collectButton.interactable = !busy && !claimed && !uncertain;
        if (collectButtonText != null) collectButtonText.text = claimed ? "COLLECTED" : (uncertain ? "CHECK STATUS" : (busy ? "COLLECTING..." : "COLLECT"));
    }

    void Acknowledge(string date, int version)
    {
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
        {
            FunctionName = "ackDailyLoginPopup", FunctionParameter = new DateRequest { date = date }, GeneratePlayStreamEvent = false
        }, result =>
        {
            if (!Current(version)) return;
            try
            {
                string json = result.FunctionResult as string;
                var response = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<AcknowledgeResult>(json);
                if (result.Error != null || response == null || !response.ok) Debug.LogWarning("Daily login display could not be saved; it may appear again next login.");
            }
            catch (Exception) { Debug.LogWarning("Daily login display could not be saved; it may appear again next login."); }
        }, error => { if (Current(version)) Debug.LogWarning("Daily login display could not be saved; it may appear again next login."); });
    }

    public void Close() { if (busy) return; popup.SetActive(false); showing = false; }
    bool Current(int version) => this != null && isActiveAndEnabled && requestVersion == version;
}
