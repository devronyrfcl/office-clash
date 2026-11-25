using System;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;

public class MissionsPanel : MonoBehaviour
{
    [Serializable] class DailyStatus
    {
        public int checkIn;
        public bool matchTracking;
        public int matches;
        public int wins;
        public int resetSeconds;
    }
    [SerializeField] GameObject missionsScreen;
    [SerializeField] GameObject lobbyScreen;
    [SerializeField] LobbyPlayerStats playerStats;
    [SerializeField] GameObject dailyRows;
    [SerializeField] GameObject dailyFocus;
    [SerializeField] GameObject campaignFocus;
    [SerializeField] GameObject clanFocus;
    [SerializeField] GameObject challengeFocus;
    [SerializeField] TMP_Text heading;
    [SerializeField] TMP_Text message;
    [SerializeField] TMP_Text goldText;
    [SerializeField] TMP_Text diamondText;
    [SerializeField] TMP_Text energyText;
    [Header("Daily progress")]
    [SerializeField] TMP_Text checkInProgress;
    [SerializeField] TMP_Text matchProgress;
    [SerializeField] TMP_Text winProgress;
    [SerializeField] GameObject resetDisplay;
    [SerializeField] TMP_Text resetTime;

    bool dailyActive;
    int requestVersion;
    float resetAt;
    bool hasResetTime;

    public void Open()
    {
        goldText.text = playerStats.GoldBalance;
        diamondText.text = playerStats.DiamondBalance;
        energyText.text = playerStats.EnergyBalance;
        lobbyScreen.SetActive(false);
        missionsScreen.SetActive(true);
        missionsScreen.transform.SetAsLastSibling();
        ShowDaily();
    }

    public void Close()
    {
        dailyActive = false;
        requestVersion++;
        missionsScreen.SetActive(false);
        lobbyScreen.SetActive(true);
    }

    public void ShowDaily()
    {
        SetFocus(dailyFocus);
        dailyActive = true;
        dailyRows.SetActive(true);
        heading.text = "DAILY MISSIONS";
        RefreshDaily();
    }

    public void ShowCampaign()
    {
        dailyActive = false;
        requestVersion++;
        resetDisplay.SetActive(false);
        SetFocus(campaignFocus);
        dailyRows.SetActive(false);
        heading.text = "CAMPAIGN LOCKED";
        message.text = "Campaign missions will be available later.";
    }

    public void ShowClan()
    {
        dailyActive = false;
        requestVersion++;
        resetDisplay.SetActive(false);
        SetFocus(clanFocus);
        dailyRows.SetActive(false);
        heading.text = "CLAN LOCKED";
        message.text = "Clan missions will be available later.";
    }

    public void ShowChallenges()
    {
        dailyActive = false;
        requestVersion++;
        resetDisplay.SetActive(false);
        SetFocus(challengeFocus);
        dailyRows.SetActive(false);
        heading.text = "CHALLENGES";
        message.text = "No challenges available yet.";
    }

    void SetFocus(GameObject selected)
    {
        dailyFocus.SetActive(selected == dailyFocus);
        campaignFocus.SetActive(selected == campaignFocus);
        clanFocus.SetActive(selected == clanFocus);
        challengeFocus.SetActive(selected == challengeFocus);
    }

    void RefreshDaily()
    {
        int version = ++requestVersion;
        hasResetTime = false;
        resetDisplay.SetActive(false);
        checkInProgress.text = matchProgress.text = winProgress.text = "Loading...";
        message.text = "Loading daily missions...";
        if (!PlayFabClientAPI.IsClientLoggedIn())
        {
            ShowError("Sign in to view daily missions.");
            return;
        }
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest { FunctionName = "getDailyMissions" }, result =>
        {
            if (!Current(version)) return;
            if (result.Error != null) { ShowError("Could not load daily missions. Please try again."); return; }
            try
            {
                string json = result.FunctionResult as string;
                var data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<DailyStatus>(json);
                if (data == null || data.checkIn != 1 || data.resetSeconds < 1 || data.resetSeconds > 86400 || data.matches < 0 || data.wins < 0)
                    throw new FormatException("Invalid daily mission status");
                checkInProgress.text = "Signed in today: 1 / 1";
                matchProgress.text = data.matchTracking ? "Matches: " + Mathf.Min(data.matches, 1) + " / 1" : "Match tracking not available yet";
                winProgress.text = data.matchTracking ? "Wins: " + Mathf.Min(data.wins, 1) + " / 1" : "Match tracking not available yet";
                message.text = "Reward claims are being set up.";
                resetAt = Time.realtimeSinceStartup + data.resetSeconds;
                hasResetTime = true;
                resetDisplay.SetActive(true);
                UpdateCountdown();
            }
            catch (Exception)
            {
                ShowError("Could not read daily missions. Please try again.");
            }
        }, error =>
        {
            if (Current(version)) ShowError("Could not load daily missions. Please try again.");
        });
    }

    void Update()
    {
        if (!dailyActive || !hasResetTime) return;
        if (Time.realtimeSinceStartup >= resetAt)
        {
            hasResetTime = false;
            RefreshDaily();
            return;
        }
        UpdateCountdown();
    }

    void UpdateCountdown()
    {
        int seconds = Mathf.Max(0, Mathf.CeilToInt(resetAt - Time.realtimeSinceStartup));
        resetTime.text = string.Format("{0:00}h {1:00}m", seconds / 3600, seconds % 3600 / 60);
    }

    void ShowError(string text)
    {
        checkInProgress.text = matchProgress.text = winProgress.text = "-- / --";
        message.text = text;
    }

    bool Current(int version) => this != null && isActiveAndEnabled && dailyActive && version == requestVersion;
}
