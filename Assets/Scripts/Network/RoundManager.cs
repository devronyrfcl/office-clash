using Fusion;
using UnityEngine;

public enum RoundState : byte
{
    Waiting,
    Countdown,
    Playing,
    Ended
}

//Put this on a scene object with a NetworkObject. The host decides when the round starts and who wins
public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance { get; private set; }

    //Without a round manager in the scene damage always works
    public static bool CanTakeDamage => Instance == null || Instance.State == RoundState.Playing;

    [SerializeField] int minPlayers = 2;
    [SerializeField] float countdownSeconds = 5f;

    //Temporary text until the real HUD exists
    [SerializeField] bool showDebugHud = true;

    [Networked] public RoundState State { get; set; }
    [Networked] public PlayerRef Winner { get; set; }
    [Networked] public int AliveCount { get; set; }
    [Networked] TickTimer countdownTimer { get; set; }

    public override void Spawned()
    {
        Instance = this;

        if (HasStateAuthority)
            State = RoundState.Waiting;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        int playerCount = PlayerHealth.All.Count;

        switch (State)
        {
            case RoundState.Waiting:
                if (playerCount >= minPlayers)
                {
                    countdownTimer = TickTimer.CreateFromSeconds(Runner, countdownSeconds);
                    State = RoundState.Countdown;
                }
                break;

            case RoundState.Countdown:
                if (playerCount < minPlayers)
                {
                    countdownTimer = TickTimer.None;
                    State = RoundState.Waiting;
                }
                else if (countdownTimer.Expired(Runner))
                {
                    countdownTimer = TickTimer.None;
                    State = RoundState.Playing;
                }
                break;

            case RoundState.Playing:
                UpdatePlaying();
                break;
        }
    }

    void UpdatePlaying()
    {
        int alive = 0;
        PlayerHealth lastAlive = null;

        foreach (PlayerHealth player in PlayerHealth.All)
        {
            if (player.IsEliminated)
                continue;

            alive++;
            lastAlive = player;
        }

        AliveCount = alive;

        //Last player standing wins, nobody left means a draw
        if (alive <= 1)
        {
            Winner = lastAlive != null ? lastAlive.Object.InputAuthority : PlayerRef.None;
            State = RoundState.Ended;
        }
    }

    void OnGUI()
    {
        if (!showDebugHud || Object == null || !Object.IsValid)
            return;

        string text = State switch
        {
            RoundState.Waiting => $"Waiting for players ({PlayerHealth.All.Count}/{minPlayers})",
            RoundState.Countdown => $"Starting in {countdownTimer.RemainingTime(Runner) ?? 0f:0.0}",
            RoundState.Playing => $"Players left: {AliveCount}",
            _ => Winner == PlayerRef.None ? "Round over: draw" : $"Round over: winner is player {Winner.PlayerId}",
        };

        PlayerHealth local = PlayerHealth.Local;

        if (local != null)
            text += $"\nHP {local.Health}/{local.MaxHealth}" + (local.IsEliminated ? "  (eliminated, Tab to spectate)" : "");

        GUI.Label(new Rect(10, 10, 600, 80), text);
    }
}
