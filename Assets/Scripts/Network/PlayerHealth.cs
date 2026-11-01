using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    static readonly List<PlayerHealth> allPlayers = new List<PlayerHealth>();

    public static IReadOnlyList<PlayerHealth> All => allPlayers;

    public static PlayerHealth Local { get; private set; }

    [SerializeField] int maxHealth = 100;

    //How long an eliminated body lies around before it is removed
    [SerializeField] float despawnDelay = 5f;

    public int MaxHealth => maxHealth;

    [Networked] public int Health { get; set; }
    [Networked] public NetworkBool IsEliminated { get; set; }

    //Last player that hurt us, used to credit eliminations
    [Networked] public PlayerRef LastAttacker { get; set; }

    [Networked] TickTimer invulnerableTimer { get; set; }
    [Networked] TickTimer despawnTimer { get; set; }

    NetworkPlayer networkPlayer;

    //Briefly protected from being knocked down again after getting up
    public bool IsInvulnerable => !invulnerableTimer.ExpiredOrNotRunning(Runner);

    void Awake()
    {
        networkPlayer = GetComponent<NetworkPlayer>();
    }

    public override void Spawned()
    {
        allPlayers.Add(this);

        if (Object.HasInputAuthority)
            Local = this;

        if (HasStateAuthority)
            Health = maxHealth;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        allPlayers.Remove(this);

        if (Local == this)
            Local = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (IsEliminated && despawnTimer.Expired(Runner))
        {
            despawnTimer = TickTimer.None;
            Runner.Despawn(Object);
        }
    }

    //Host only. Returns true when the damage was applied
    public bool ApplyDamage(float amount, PlayerRef attacker, bool ignoreRoundState = false)
    {
        if (!HasStateAuthority || IsEliminated)
            return false;

        if (!ignoreRoundState && !RoundManager.CanTakeDamage)
            return false;

        int damage = Mathf.Max(1, Mathf.RoundToInt(amount));

        Health = Mathf.Max(0, Health - damage);

        if (attacker != PlayerRef.None)
            LastAttacker = attacker;

        if (Health == 0)
            Eliminate();

        return true;
    }

    public void Heal(int amount)
    {
        if (!HasStateAuthority || IsEliminated)
            return;

        Health = Mathf.Min(maxHealth, Health + amount);
    }

    public void StartInvulnerability(float seconds)
    {
        if (!HasStateAuthority)
            return;

        invulnerableTimer = TickTimer.CreateFromSeconds(Runner, seconds);
    }

    void Eliminate()
    {
        IsEliminated = true;
        despawnTimer = TickTimer.CreateFromSeconds(Runner, despawnDelay);

        if (networkPlayer != null)
            networkPlayer.OnEliminated();

        Utils.DebugLog($"Player {Object.InputAuthority} eliminated by {LastAttacker}");
    }
}
