// Office Clash - PlayFab legacy CloudScript (merge any other existing handlers before deploying).
// Set DiscordWebhookUrl in PlayFab INTERNAL Title Data; never put webhook secrets in client code.

function discordNotify(message) {
    var result = server.GetTitleInternalData({ Keys: ["DiscordWebhookUrl"] });
    var url = result.Data && result.Data.DiscordWebhookUrl;
    if (!url) {
        log.error("DiscordWebhookUrl is missing from Internal Title Data.");
        return;
    }
    http.request(url, "post", JSON.stringify({ content: message }), "application/json", {});
}

function playerDisplayName() {
    var result = server.GetPlayerProfile({ PlayFabId: currentPlayerId });
    return result.PlayerProfile && result.PlayerProfile.DisplayName || "Unknown Player";
}

handlers.newUserRegistered = function (args) {
    discordNotify("Hello, " + playerDisplayName() + ". Welcome to Office Clash. Player ID is: " + currentPlayerId);
};

handlers.newUserWelcome = function (args) {
    discordNotify("Player Logged In: " + playerDisplayName());
};

handlers.validateScore = function (args) {
    var score = args && Number(args.score);
    if (!isFinite(score)) return { valid: false, message: "Invalid score." };
    if (score > 100) discordNotify("Player " + currentPlayerId + " submitted a score above 100.");
    return { valid: score <= 100 };
};

handlers.playerLoggedIn = function (args) {
    discordNotify("Player Logged In: " + currentPlayerId);
};

// Progression and energy defaults. XP awards and energy spending must be performed
// by trusted server-side match logic, not by passing amounts from the game client.
var PROGRESSION_ENERGY_MAX = 60;
var PROGRESSION_ENERGY_RECHARGE_MS = 5 * 60 * 1000;

function xpForNextLevel(level) {
    return 100 + (level - 1) * 50;
}

// Helper for future trusted server-side match rewards. It is not a public handler.
function applyExperience(progress, amount) {
    if (amount < 0 || Math.floor(amount) !== amount) throw "Invalid XP amount";
    progress.xp += amount;
    while (progress.xp >= xpForNextLevel(progress.level)) {
        progress.xp -= xpForNextLevel(progress.level);
        progress.level++;
    }
    return progress;
}

function readInteger(data, key, fallback, minimum) {
    if (!data || !data[key] || data[key].Value === undefined) return fallback;
    var raw = data[key].Value;
    if (!/^\d+$/.test(raw)) throw "Invalid progression data: " + key;
    var value = Number(raw);
    if (!isFinite(value) || value < minimum || Math.floor(value) !== value)
        throw "Invalid progression data: " + key;
    return value;
}

handlers.getProgression = function () {
    if (!currentPlayerId) throw "Player must be signed in";
    var keys = ["Level", "XP", "Energy", "EnergyUpdatedAt"];
    var data = server.GetUserReadOnlyData({ PlayFabId: currentPlayerId, Keys: keys }).Data || {};
    var now = Date.now();
    var isNew = !data.Level && !data.XP && !data.Energy && !data.EnergyUpdatedAt;
    if (!isNew && (!data.Level || !data.XP || !data.Energy || !data.EnergyUpdatedAt))
        throw "Incomplete progression data; contact support";

    var level = readInteger(data, "Level", 1, 1);
    var xp = readInteger(data, "XP", 0, 0);
    var energy = readInteger(data, "Energy", PROGRESSION_ENERGY_MAX, 0);
    var updated = readInteger(data, "EnergyUpdatedAt", now, 0);
    if (updated > now) throw "Invalid energy timestamp";

    if (isNew) {
        server.UpdateUserReadOnlyData({ PlayFabId: currentPlayerId, Data: {
            Level: String(level), XP: String(xp), Energy: String(energy), EnergyUpdatedAt: String(updated)
        }});
    }

    // Recharge is calculated from server time; future spending must persist a new baseline.
    var regenerated = Math.floor((now - updated) / PROGRESSION_ENERGY_RECHARGE_MS);
    var currentEnergy = Math.min(PROGRESSION_ENERGY_MAX, energy + regenerated);
    return JSON.stringify({
        level: level, xp: xp, xpTarget: xpForNextLevel(level),
        energy: currentEnergy, energyMax: PROGRESSION_ENERGY_MAX
    });
};

// Rename via the authenticated player's session and charge legacy gold.
// These legacy operations are separate; refund is attempted when the rename fails.
handlers.changeDisplayName = function (args) {
    var newName = args && typeof args.newName === "string" ? args.newName.trim() : "";
    var ticket = args && args.sessionTicket;
    if (!currentPlayerId || !ticket) return JSON.stringify({ ok: false, message: "Please sign in again." });
    if (newName.length < 3 || newName.length > 25)
        return JSON.stringify({ ok: false, message: "Enter a name between 3 and 25 characters." });

    var auth;
    try { auth = server.AuthenticateSessionTicket({ SessionTicket: ticket }); }
    catch (e) { return JSON.stringify({ ok: false, message: "Your session has expired. Please sign in again." }); }
    if (!auth || auth.IsSessionTicketExpired || !auth.UserInfo || auth.UserInfo.PlayFabId !== currentPlayerId)
        return JSON.stringify({ ok: false, message: "Your session has expired. Please sign in again." });
    var existingName = auth.UserInfo.TitleInfo && auth.UserInfo.TitleInfo.DisplayName;
    if (existingName && existingName === newName)
        return JSON.stringify({ ok: false, message: "That is already your display name." });

    var inventory = server.GetUserInventory({ PlayFabId: currentPlayerId });
    if (!inventory.VirtualCurrency || typeof inventory.VirtualCurrency.GD !== "number")
        return JSON.stringify({ ok: false, message: "Gold currency is not configured. Please contact support." });
    if (inventory.VirtualCurrency.GD < 50)
        return JSON.stringify({ ok: false, message: "You need 50 gold to change your name." });

    var debit;
    try { debit = server.SubtractUserVirtualCurrency({ PlayFabId: currentPlayerId, VirtualCurrency: "GD", Amount: 50 }); }
    catch (e) {
        // A timeout may occur after a remote operation was applied. Do not retry automatically.
        return JSON.stringify({ ok: false, uncertain: true, message: "Could not confirm the gold charge. Check your balance before trying again." });
    }
    if (!debit || typeof debit.Balance !== "number")
        return JSON.stringify({ ok: false, uncertain: true, message: "Could not confirm the gold charge. Check your balance before trying again." });

    var response = null;
    if (debit.Balance >= 0) {
        try {
            // No request/response logging: the header contains the player's session ticket.
            var raw = http.request("https://" + script.titleId + ".playfabapi.com/Client/UpdateUserTitleDisplayName", "post",
                JSON.stringify({ DisplayName: newName }), "application/json", { "X-Authorization": ticket }, false);
            response = JSON.parse(raw);
            if (response && response.code === 200 && response.data && response.data.DisplayName === newName)
                return JSON.stringify({ ok: true, displayName: response.data.DisplayName });
        } catch (e) { /* The rename may have succeeded even if its response was lost. */ }
    }

    try {
        server.AddUserVirtualCurrency({ PlayFabId: currentPlayerId, VirtualCurrency: "GD", Amount: 50 });
    } catch (e) {
        return JSON.stringify({ ok: false, uncertain: true, message: "Could not complete or refund this change. Please contact support before retrying." });
    }
    if (debit.Balance < 0) return JSON.stringify({ ok: false, message: "You need 50 gold to change your name." });
    // Refresh the account and balance after this response: an HTTP timeout can leave the name outcome uncertain.
    return JSON.stringify({ ok: false, uncertain: true, message: "The change could not be confirmed. Your gold was refunded; check your name before trying again." });
};

// Daily mission status is read-only. Match counters must only be updated by
// trusted match-result processing; the client cannot advance these objectives.
handlers.getDailyMissions = function () {
    if (!currentPlayerId) throw "Player must be signed in";
    var now = Date.now();
    var dayMs = 24 * 60 * 60 * 1000;
    var config = server.GetTitleData({ Keys: ["MissionMatchTrackingEnabled"] });
    var matchTracking = !!(config.Data && config.Data.MissionMatchTrackingEnabled === "true");
    var matches = 0, wins = 0;
    if (matchTracking) {
        var stats = server.GetPlayerStatistics({ PlayFabId: currentPlayerId, StatisticNames: ["DailyMatchesPlayed", "DailyWins"] });
        var values = stats.Statistics || [];
        for (var i = 0; i < values.length; i++) {
            if (values[i].StatisticName === "DailyMatchesPlayed") matches = Math.max(0, values[i].Value);
            if (values[i].StatisticName === "DailyWins") wins = Math.max(0, values[i].Value);
        }
    }
    return JSON.stringify({
        checkIn: 1,
        matchTracking: matchTracking,
        matches: matches,
        wins: wins,
        resetSeconds: Math.ceil((Math.floor(now / dayMs) * dayMs + dayMs - now) / 1000)
    });
};

// The popup is acknowledged separately after the client displays it. No rewards
// are granted by either handler; reward transactions need their own secure flow.
function dailyPopupState() {
    if (!currentPlayerId) throw "Player must be signed in";
    var now = new Date();
    var today = now.toISOString().slice(0, 10);
    var result = server.GetUserInternalData({ PlayFabId: currentPlayerId, Keys: ["LoginCalendar", "DailyRewardClaimDate"] });
    var record = result.Data && result.Data.LoginCalendar;
    var state = record && record.Value ? JSON.parse(record.Value) : { lastShown: "" };
    if (!state || typeof state.lastShown !== "string")
        throw "Invalid login calendar data";
    var claimed = !!(result.Data && result.Data.DailyRewardClaimDate && result.Data.DailyRewardClaimDate.Value === today);
    return { today: today, state: state, day: now.getUTCDate(), daysInMonth: new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 0)).getUTCDate(), claimed: claimed };
}

handlers.getDailyLoginPopup = function () {
    var current = dailyPopupState();
    var alreadyShown = current.state.lastShown === current.today;
    return JSON.stringify({
        shouldShow: !alreadyShown,
        date: current.today,
        day: current.day,
        daysInMonth: current.daysInMonth,
        claimed: current.claimed
    });
};

handlers.ackDailyLoginPopup = function (args) {
    var current = dailyPopupState();
    if (!args || args.date !== current.today) return JSON.stringify({ ok: false });
    if (current.state.lastShown !== current.today) {
        current.state.lastShown = current.today;
        server.UpdateUserInternalData({ PlayFabId: currentPlayerId, Data: { LoginCalendar: JSON.stringify(current.state) } });
    }
    return JSON.stringify({ ok: true });
};

// Prototype legacy claim: one server-created group is an at-most-once lock per
// player and UTC day. A failure after the lock requires manual reconciliation;
// never automatically retry an uncertain grant. Client Shared Group APIs must
// be denied in the title API policy so players cannot pre-create these locks.
handlers.claimDailyLoginReward = function (args) {
    var current = dailyPopupState();
    if (!args || args.date !== current.today)
        return JSON.stringify({ ok: false, message: "The day changed. Open the calendar again." });
    if (current.claimed) return JSON.stringify({ ok: true, alreadyClaimed: true, message: "Today's reward was already collected." });

    var type = (current.day - 1) % 3;
    var currency = type === 0 ? "GD" : (type === 1 ? "DM" : null);
    var amount = type === 0 ? 100 : (type === 1 ? 5 : 10);
    var energyNow = 0;
    if (currency) {
        var inventory = server.GetUserInventory({ PlayFabId: currentPlayerId });
        if (!inventory.VirtualCurrency || typeof inventory.VirtualCurrency[currency] !== "number")
            return JSON.stringify({ ok: false, message: "Reward currency is not configured. Please contact support." });
        if (inventory.VirtualCurrency[currency] > 2147483647 - amount)
            return JSON.stringify({ ok: false, message: "Your balance is too high to collect this reward." });
    } else {
        var energyData = server.GetUserReadOnlyData({ PlayFabId: currentPlayerId, Keys: ["Energy", "EnergyUpdatedAt"] }).Data || {};
        if (!energyData.Energy || !energyData.EnergyUpdatedAt)
            return JSON.stringify({ ok: false, message: "Energy is not ready yet. Return to the lobby and try again." });
        var now = Date.now();
        var energy = readInteger(energyData, "Energy", 0, 0);
        var updated = readInteger(energyData, "EnergyUpdatedAt", now, 0);
        if (updated > now) return JSON.stringify({ ok: false, message: "Energy data is unavailable. Please contact support." });
        energyNow = Math.min(PROGRESSION_ENERGY_MAX, energy + Math.floor((now - updated) / PROGRESSION_ENERGY_RECHARGE_MS));
        if (energyNow + amount > PROGRESSION_ENERGY_MAX)
            return JSON.stringify({ ok: false, message: "You need 10 free energy capacity to collect this reward." });
    }

    var lockId = "OCDaily" + currentPlayerId + current.today.replace(/-/g, "");
    try { server.CreateSharedGroup({ SharedGroupId: lockId }); }
    catch (e) {
        return JSON.stringify({ ok: false, uncertain: true, message: "This claim may already be processing. Check your balance or contact support before trying again." });
    }
    try {
        if (currency) {
            server.AddUserVirtualCurrency({ PlayFabId: currentPlayerId, VirtualCurrency: currency, Amount: amount });
        } else {
            var remaining = (now - updated) % PROGRESSION_ENERGY_RECHARGE_MS;
            var newEnergy = energyNow + amount;
            server.UpdateUserReadOnlyData({ PlayFabId: currentPlayerId, Data: {
                Energy: String(newEnergy),
                EnergyUpdatedAt: String(newEnergy >= PROGRESSION_ENERGY_MAX ? now : now - remaining)
            }});
        }
        server.UpdateUserInternalData({ PlayFabId: currentPlayerId, Data: { DailyRewardClaimDate: current.today } });
        return JSON.stringify({ ok: true, reward: currency || "ENERGY", amount: amount, message: "Reward collected!" });
    } catch (e) {
        return JSON.stringify({ ok: false, uncertain: true, message: "The reward could not be confirmed. Check your balance or contact support before retrying." });
    }
};
