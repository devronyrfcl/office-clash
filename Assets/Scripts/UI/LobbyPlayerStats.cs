using System;
using System.Globalization;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Displays server-owned progression and legacy currency balances for the signed-in player.
public class LobbyPlayerStats : MonoBehaviour
{
    [Serializable] class Progression
    {
        public int level, xp, xpTarget, energy, energyMax;
    }

    [Header("Player information")]
    [SerializeField] TMP_Text levelText;
    [SerializeField] TMP_Text secondaryLevelText;
    [SerializeField] TMP_Text xpText;
    [SerializeField] Slider xpBar;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text secondaryNameText;
    [Header("Resources")]
    [SerializeField] TMP_Text energyText;
    [SerializeField] TMP_Text goldText;
    [SerializeField] TMP_Text diamondText;
    [Header("Legacy currency codes")]
    [SerializeField] string goldCurrencyCode = "GD";
    [SerializeField] string diamondCurrencyCode = "DM";
    int requestVersion;
    bool reportedError;
    bool progressionLoading;

    public string PlayerName => nameText != null ? nameText.text : "--";
    public string PlayerLevel => levelText != null ? levelText.text : "--";
    public string PlayerXp => xpText != null ? xpText.text : "-- / --";
    public float PlayerXpProgress => xpBar != null ? xpBar.normalizedValue : 0f;
    public string GoldBalance => goldText != null ? goldText.text : "--";
    public string DiamondBalance => diamondText != null ? diamondText.text : "--";
    public string EnergyBalance => energyText != null ? energyText.text : "-- / --";

    public void ApplyDisplayName(string displayName)
    {
        if (nameText != null) nameText.text = displayName;
        if (secondaryNameText != null) secondaryNameText.text = displayName;
    }

    void Awake()
    {
        xpBar.interactable = false;
    }

    void OnEnable() { Refresh(); InvokeRepeating(nameof(RefreshProgression), 30f, 30f); }
    void OnDisable() { CancelInvoke(nameof(RefreshProgression)); requestVersion++; progressionLoading = false; }

    public void Refresh()
    {
        int version = ++requestVersion;
        reportedError = false;
        progressionLoading = false;
        levelText.text = secondaryLevelText.text = "--";
        xpText.text = energyText.text = "-- / --";
        goldText.text = diamondText.text = "--";
        nameText.text = secondaryNameText.text = "--";
        xpBar.value = xpBar.minValue;

        if (!PlayFabClientAPI.IsClientLoggedIn())
        {
            Debug.LogWarning("Player stats require a PlayFab login.");
            return;
        }

        PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest(), result =>
        {
            if (!Current(version)) return;
            var info = result.AccountInfo;
            string displayName = info?.TitleInfo?.DisplayName;
            if (string.IsNullOrEmpty(displayName)) displayName = info?.Username;
            if (!string.IsNullOrEmpty(displayName)) nameText.text = secondaryNameText.text = displayName;
        }, error => LogFailure(version, "account", error));

        RefreshProgression();

        PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(), result =>
        {
            if (!Current(version)) return;
            if (result.VirtualCurrency != null && result.VirtualCurrency.TryGetValue(goldCurrencyCode, out int gold))
                goldText.text = Format(gold);
            else Debug.LogWarning("Legacy gold currency code not found: " + goldCurrencyCode);
            if (result.VirtualCurrency != null && result.VirtualCurrency.TryGetValue(diamondCurrencyCode, out int diamonds))
                diamondText.text = Format(diamonds);
            else Debug.LogWarning("Legacy diamond currency code not found: " + diamondCurrencyCode);
        }, error => LogFailure(version, "inventory", error));
    }

    bool Current(int version) => this != null && isActiveAndEnabled && version == requestVersion;
    static string Format(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    void RefreshProgression()
    {
        if (!PlayFabClientAPI.IsClientLoggedIn() || progressionLoading) return;
        int version = requestVersion;
        progressionLoading = true;
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest { FunctionName = "getProgression" }, result =>
        {
            if (!Current(version)) return;
            progressionLoading = false;
            if (result.Error != null) { ReportFailure(version, "progression", result.Error.Message); return; }
            try
            {
                var json = result.FunctionResult as string;
                var data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Progression>(json);
                if (data == null || data.level < 1 || data.xp < 0 || data.xpTarget < 1 || data.energy < 0 || data.energyMax < 1)
                    throw new FormatException("Invalid progression response");
                levelText.text = secondaryLevelText.text = Format(data.level);
                xpText.text = Format(data.xp) + " / " + Format(data.xpTarget);
                energyText.text = Format(data.energy) + " / " + Format(data.energyMax);
                xpBar.value = Mathf.Lerp(xpBar.minValue, xpBar.maxValue, Mathf.Clamp01((float)data.xp / data.xpTarget));
            }
            catch (Exception exception) { ReportFailure(version, "progression", exception.Message); }
        }, error => { if (Current(version)) progressionLoading = false; LogFailure(version, "progression", error); });
    }
    void LogFailure(int version, string section, PlayFabError error)
    {
        ReportFailure(version, section, error.ErrorMessage);
    }
    void ReportFailure(int version, string section, string message)
    {
        if (!Current(version)) return;
        Debug.LogWarning("Could not load player " + section + ": " + message);
        if (reportedError) return;
        reportedError = true;
        var lobby = FindObjectOfType<Lobby>();
        if (lobby != null) lobby.ShowStatsError();
    }
}
