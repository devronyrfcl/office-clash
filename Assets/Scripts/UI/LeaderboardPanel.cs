using System.Collections.Generic;
using System.Globalization;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardPanel : MonoBehaviour
{
    [Header("PlayFab")]
    [SerializeField] string statisticName = "Wins";
    [SerializeField, Range(1, 100)] int maximumResults = 100;
    [Header("Screens")]
    [SerializeField] GameObject rankingScreen;
    [SerializeField] GameObject lobbyScreen;
    [SerializeField] Transform rowContainer;
    [SerializeField] GameObject[] rowTemplates;
    [SerializeField] TMP_Text statusText;
    [SerializeField] TMP_Text resetText;
    [Header("Your ranking")]
    [SerializeField] TMP_Text myRank;
    [SerializeField] TMP_Text myName;
    [SerializeField] TMP_Text myWins;
    [SerializeField] UnityEngine.UI.Image myAvatar;
    [SerializeField] GameObject myAvatarContainer;
    [SerializeField] ScrollRect scrollRect;
    [Header("Leaderboard tabs")]
    [SerializeField] GameObject globalFocus;
    [SerializeField] GameObject countryFocus;

    readonly List<GameObject> rows = new List<GameObject>();
    int requestVersion;

    void Awake()
    {
        foreach (var template in rowTemplates) template.SetActive(false);
        ClearMyRank();
    }

    public void Open()
    {
        lobbyScreen.SetActive(false);
        rankingScreen.SetActive(true);
        rankingScreen.transform.SetAsLastSibling();
        ShowGlobal();
    }

    public void Close()
    {
        requestVersion++;
        rankingScreen.SetActive(false);
        lobbyScreen.SetActive(true);
    }

    public void Refresh() { ShowGlobal(); }

    public void ShowGlobal()
    {
        int version = ++requestVersion;
        ClearRows();
        ClearMyRank();
        if (globalFocus != null) globalFocus.SetActive(true);
        if (countryFocus != null) countryFocus.SetActive(false);
        resetText.text = "ALL-TIME WINS";
        statusText.text = "LOADING LEADERBOARD...";
        if (!PlayFabClientAPI.IsClientLoggedIn())
        {
            statusText.text = "SIGN IN TO VIEW THE LEADERBOARD";
            return;
        }

        LoadLeaderboard(version, statisticName);
    }

    public void ShowCountry()
    {
        int version = ++requestVersion;
        ClearRows();
        ClearMyRank();
        if (globalFocus != null) globalFocus.SetActive(false);
        if (countryFocus != null) countryFocus.SetActive(true);
        resetText.text = "COUNTRY WINS";
        if (!PlayFabClientAPI.IsClientLoggedIn())
        {
            statusText.text = "SIGN IN TO VIEW THE LEADERBOARD";
            return;
        }
        statusText.text = "FINDING YOUR COUNTRY...";
        PlayFabClientAPI.GetPlayerProfile(new GetPlayerProfileRequest
        {
            ProfileConstraints = new PlayerProfileViewConstraints { ShowLocations = true }
        }, result =>
        {
            if (!Current(version)) return;
            var locations = result.PlayerProfile?.Locations;
            string country = null;
            if (locations != null)
                foreach (var location in locations)
                {
                    if (location == null || !location.CountryCode.HasValue) continue;
                    string code = location.CountryCode.Value.ToString();
                    if (country != null && country != code)
                    {
                        statusText.text = "MULTIPLE COUNTRIES FOUND. COUNTRY RANKING UNAVAILABLE.";
                        return;
                    }
                    country = code;
                }
            if (country == null || country.Length != 2)
            {
                statusText.text = "COUNTRY NOT AVAILABLE YET";
                return;
            }
            resetText.text = country + " / ALL-TIME WINS";
            statusText.text = "LOADING COUNTRY LEADERBOARD...";
            LoadLeaderboard(version, statisticName + "_" + country);
        }, error =>
        {
            if (!Current(version)) return;
            statusText.text = "COULD NOT FIND YOUR COUNTRY";
            Debug.LogWarning("Player country: " + error.ErrorMessage);
        });
    }

    void LoadLeaderboard(int version, string leaderboardName)
    {
        PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest(), account =>
        {
            if (!Current(version)) return;
            var info = account.AccountInfo;
            string name = info?.TitleInfo?.DisplayName;
            if (string.IsNullOrWhiteSpace(name)) name = info?.Username;
            if (!string.IsNullOrWhiteSpace(name)) myName.text = name;
        }, error => { if (Current(version)) Debug.LogWarning("Player name: " + error.ErrorMessage); });

        PlayFabClientAPI.GetLeaderboard(new GetLeaderboardRequest
        {
            StatisticName = leaderboardName,
            StartPosition = 0,
            MaxResultsCount = maximumResults
        }, result =>
        {
            if (!Current(version)) return;
            if (result.Leaderboard == null || result.Leaderboard.Count == 0)
            {
                statusText.text = "NO RANKED PLAYERS YET";
                return;
            }
            statusText.text = "";
            for (int i = 0; i < result.Leaderboard.Count; i++) AddRow(result.Leaderboard[i]);
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }, error => { if (Current(version)) { statusText.text = "COULD NOT LOAD LEADERBOARD. PLEASE TRY AGAIN."; Debug.LogWarning("Leaderboard: " + error.ErrorMessage); } });

        // Around-player returns a default position even without a statistic, so check first.
        PlayFabClientAPI.GetPlayerStatistics(new GetPlayerStatisticsRequest { StatisticNames = new List<string> { leaderboardName } }, result =>
        {
            if (!Current(version)) return;
            var ownStat = result.Statistics?.Find(s => s.StatisticName == leaderboardName);
            if (ownStat == null)
            {
                myRank.text = "--";
                myWins.text = "0";
                return;
            }
            myWins.text = ownStat.Value.ToString("N0", CultureInfo.InvariantCulture);
            PlayFabClientAPI.GetLeaderboardAroundPlayer(new GetLeaderboardAroundPlayerRequest { StatisticName = leaderboardName, MaxResultsCount = 1 }, around =>
            {
                if (!Current(version) || around.Leaderboard == null) return;
                var own = around.Leaderboard.Find(entry => entry.PlayFabId == PlayFabSettings.staticPlayer.PlayFabId);
                if (own == null) return;
                myRank.text = "#" + (own.Position + 1).ToString("N0", CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(own.DisplayName)) myName.text = own.DisplayName;
                myWins.text = own.StatValue.ToString("N0", CultureInfo.InvariantCulture);
            }, error => { if (Current(version)) Debug.LogWarning("Player ranking: " + error.ErrorMessage); });
        }, error => { if (Current(version)) Debug.LogWarning("Player statistics: " + error.ErrorMessage); });
    }

    bool Current(int version) => this != null && rankingScreen.activeInHierarchy && requestVersion == version;

    void AddRow(PlayerLeaderboardEntry entry)
    {
        int template = Mathf.Clamp(entry.Position, 0, 3);
        var row = Instantiate(rowTemplates[template], rowContainer);
        rows.Add(row);
        row.SetActive(true);
        var rank = row.transform.Find("Text_4");
        if (rank != null) rank.GetComponent<TMP_Text>().text = (entry.Position + 1).ToString("N0", CultureInfo.InvariantCulture);
        row.transform.Find("Text_NickName").GetComponent<TMP_Text>().text = DisplayName(entry);
        row.transform.Find("Text_Score").GetComponent<TMP_Text>().text = entry.StatValue.ToString("N0", CultureInfo.InvariantCulture);
        Hide(row.transform.Find("Icon_Country"));
        Hide(row.transform.Find("Character_Bg"));
        Hide(row.transform.Find("Character_BackGround_Blue"));
    }

    static void Hide(Transform item) { if (item != null) item.gameObject.SetActive(false); }
    static string DisplayName(PlayerLeaderboardEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.DisplayName)) return entry.DisplayName;
        if (!string.IsNullOrEmpty(entry.PlayFabId)) return "PLAYER " + entry.PlayFabId;
        return "PLAYER";
    }
    void ClearRows() { foreach (var row in rows) if (row != null) { row.SetActive(false); Destroy(row); } rows.Clear(); }
    void ClearMyRank()
    {
        myRank.text = "--";
        var stats = lobbyScreen != null ? lobbyScreen.GetComponent<LobbyPlayerStats>() : null;
        myName.text = stats != null && !string.IsNullOrWhiteSpace(stats.PlayerName) && stats.PlayerName != "--"
            ? stats.PlayerName : "YOU";
        myWins.text = "--";
        var picker = lobbyScreen != null ? lobbyScreen.GetComponent<ProfileAvatarPicker>() : null;
        var avatar = picker != null ? picker.CurrentAvatar : null;
        if (myAvatarContainer != null) myAvatarContainer.SetActive(avatar != null);
        if (myAvatar != null && avatar != null) { myAvatar.sprite = avatar; myAvatar.color = Color.white; myAvatar.preserveAspect = true; }
    }
}
