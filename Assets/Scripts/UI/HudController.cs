using TMPro;
using UnityEngine;
using UnityEngine.UI;

//Fills the status HUD for the local player: health, players left, round time, banner, throw charge, hand items and the get up prompt
public class HudController : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] Slider healthSlider;
    [SerializeField] TMP_Text healthText;

    [Header("Round")]
    [SerializeField] TMP_Text playersText;
    [SerializeField] TMP_Text timerText;
    [SerializeField] GameObject banner;
    [SerializeField] TMP_Text bannerTitle;
    [SerializeField] TMP_Text bannerSubtitle;

    [Header("Throwing")]
    [SerializeField] Slider chargeSlider;
    [SerializeField] Image[] handIcons;
    [SerializeField] GameObject getUpPrompt;

    //How long "FIGHT!" stays after the round starts
    [SerializeField] float fightBannerSeconds = 1.5f;

    void Update()
    {
        NetworkPlayer local = NetworkPlayer.Local;
        PlayerHealth health = PlayerHealth.Local;
        RoundManager round = RoundManager.Instance;

        UpdateHealth(health);
        UpdateRound(round, health);
        UpdateThrowing(local, health);
    }

    void UpdateHealth(PlayerHealth health)
    {
        if (health == null)
            return;

        healthSlider.value = health.MaxHealth > 0 ? (float)health.Health / health.MaxHealth : 0f;
        SetText(healthText, $"{health.Health}/{health.MaxHealth}");
    }

    void UpdateRound(RoundManager round, PlayerHealth health)
    {
        if (round == null || !round.Object.IsValid)
        {
            SetText(playersText, $"PLAYERS {PlayerHealth.All.Count}");
            SetText(timerText, "--:--");
            banner.SetActive(false);
            return;
        }

        bool isLocalEliminated = health != null && health.IsEliminated;

        switch (round.State)
        {
            case RoundState.Waiting:
                SetText(playersText, $"PLAYERS {PlayerHealth.All.Count}");
                SetText(timerText, "--:--");
                ShowBanner("WAITING", $"{PlayerHealth.All.Count}/{round.MinPlayers} players");
                break;

            case RoundState.Countdown:
                SetText(playersText, $"PLAYERS {PlayerHealth.All.Count}");
                SetText(timerText, "--:--");
                ShowBanner("GET READY", Mathf.CeilToInt(round.CountdownRemaining).ToString());
                break;

            case RoundState.Playing:
                SetText(playersText, $"LEFT {round.AliveCount}");
                SetText(timerText, FormatTime(round.PlayingElapsed));

                if (isLocalEliminated)
                    ShowBanner("ELIMINATED", "You are watching");
                else if (round.PlayingElapsed < fightBannerSeconds)
                    ShowBanner("FIGHT!", "");
                else
                    banner.SetActive(false);
                break;

            case RoundState.Ended:
                SetText(playersText, $"LEFT {round.AliveCount}");
                SetText(timerText, FormatTime(round.PlayingElapsed));

                bool localWon = health != null && round.Winner != Fusion.PlayerRef.None && round.Winner == health.Object.InputAuthority;

                if (round.Winner == Fusion.PlayerRef.None)
                    ShowBanner("DRAW", "Nobody is left");
                else if (localWon)
                    ShowBanner("YOU WIN!", "Last one standing");
                else
                    ShowBanner("ROUND OVER", $"Player {round.Winner.PlayerId} wins");
                break;
        }
    }

    void UpdateThrowing(NetworkPlayer local, PlayerHealth health)
    {
        bool hasPlayer = local != null && local.Object.IsValid;

        bool isCharging = hasPlayer && local.IsChargingThrow;

        chargeSlider.gameObject.SetActive(isCharging);

        if (isCharging)
            chargeSlider.value = local.throwCharge;

        bool isEliminated = health != null && health.IsEliminated;

        getUpPrompt.SetActive(hasPlayer && local.isKnockedDown && !isEliminated);

        PlayerInventory inventory = hasPlayer ? local.GetComponent<PlayerInventory>() : null;

        for (int i = 0; i < handIcons.Length; i++)
        {
            Sprite sprite = inventory != null ? ItemIcons.Get(local.Runner, inventory.GetHandItem(i)) : null;

            handIcons[i].sprite = sprite;
            handIcons[i].enabled = sprite != null;
        }
    }

    void ShowBanner(string title, string subtitle)
    {
        banner.SetActive(true);

        SetText(bannerTitle, title);
        SetText(bannerSubtitle, subtitle);
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text.text != value)
            text.text = value;
    }

    static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);

        return $"{total / 60}:{total % 60:00}";
    }
}
