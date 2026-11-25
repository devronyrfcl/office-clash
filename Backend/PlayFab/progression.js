// PlayFab legacy CloudScript. Merge this file with any existing live handlers before deploying.
// Only getProgression is client-callable. Never trust XP or energy amounts from a client.
var PROGRESSION_ENERGY_MAX = 60;
var PROGRESSION_ENERGY_RECHARGE_MS = 5 * 60 * 1000;

function xpForNextLevel(level) {
    // Level 1 requires 100 XP, level 2 requires 150, level 3 requires 200, etc.
    return 100 + (level - 1) * 50;
}

// For future trusted match-result processing only; this is not a CloudScript handler.
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
    if (!isFinite(value) || value < minimum || Math.floor(value) !== value) throw "Invalid progression data: " + key;
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
    // Calculate recharge from server time without writing on reads. Future server-side
    // spending must first apply this same calculation and persist the new baseline.
    var regenerated = Math.floor((now - updated) / PROGRESSION_ENERGY_RECHARGE_MS);
    var currentEnergy = Math.min(PROGRESSION_ENERGY_MAX, energy + regenerated);
    return JSON.stringify({ level: level, xp: xp, xpTarget: xpForNextLevel(level), energy: currentEnergy, energyMax: PROGRESSION_ENERGY_MAX });
};
