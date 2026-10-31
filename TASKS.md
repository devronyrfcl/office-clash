# Office Clash — Task Breakdown

Ordered work plan. Design details live in [README.md](README.md). Phases run in order; a phase starts when the ones it depends on are done. Inside a phase, tasks are numbered in the order to do them, and "Needs" lists hard dependencies.

Legend: `[x]` done, `[ ]` to do.

---

## Phase 0 — Foundation

Goal: one clean scene that runs the networked ragdoll with correct spawning, and a first build on two phones.

**Done**
- [x] Sync knockdown state over the network (`isActiveRagdoll`, `lastTimeBecameRagdoll` as `[Networked]`)
- [x] Switch jump and revive input to `NetworkButtons` + `WasPressed`
- [x] Spawn points in code (`SpawnPoint`, `Spawner`)
- [x] Session handling instead of one hard-coded name
- [x] `Fusion.Addons.Physics` kept in `AssembliesToWeave`
- [x] Remove unused scenes, move `Regdoll Character` to `Assets/Scenes`

**To do**
- [ ] 0.1 Confirm `Assets/Scenes/Regdoll Character.unity` is ticked in Build Settings (path was updated by the move)
- [ ] 0.2 Rename the scene to `Office` (fix the "Regdoll" typo) and keep the GUID by renaming inside Unity
- [ ] 0.3 Place 10+ `SpawnPoint` components spread around the office
- [ ] 0.4 Add an out-of-bounds trigger volume under the map (hook it to damage in 1.x; for now just respawn)
- [ ] 0.5 Remove or repurpose unused assets: team props (`powerupBlock_teamRed`, `swiper_teamBlue`), unused KayKit/GUI Kit folders you won't ship
- [ ] 0.6 Clean repo hygiene: confirm `.gitignore` covers `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated `.csproj`/`.sln`, `.DS_Store`; untrack anything already committed
- [ ] 0.7 Android build settings: IL2CPP, ARM64, min API level, URP mobile quality tier, landscape orientation
- [ ] 0.8 First Android build, 2 phones joining the same session. *Needs 0.1–0.7*

---

## Phase 1 — Networked throwables and item data

Goal: every prop in the office can be grabbed, thrown and is synced. Everything later (damage, inventory) builds on this.

- [ ] 1.1 Create `ItemData` ScriptableObject: class (Small / Medium / Big), base damage, knockdown rule, pocket-allowed flag, hands required, icon, throw speed
- [ ] 1.2 Create `ThrowableItem` component holding an `ItemData` reference and a `[Networked]` last-thrower `PlayerRef`. *Needs 1.1*
- [ ] 1.3 Make prefabs for the first item set (mouse, stapler, mug, keyboard, monitor, plant, office chair). Each gets `NetworkObject`, `NetworkRigidbody3D`, `CauseDamage` tag, `ThrowableItem`. *Needs 1.2*
- [ ] 1.4 Register all prefabs in the Fusion prefab table and make sure they spawn on the host and appear on clients
- [ ] 1.5 Item spawn points + host-side respawn timer so the office never runs dry. *Needs 1.3*
- [ ] 1.6 Physics sleep for idle props and a cap on active props (performance guard). *Needs 1.5*
- [ ] 1.7 Test: throw items between 2 devices, check position sync and that the thrower is recorded. *Needs 1.2–1.5, 0.8*

---

## Phase 2 — Health, damage, knockdown, elimination

Goal: a match can be won. Port the offline health logic and make it host-authoritative.

- [ ] 2.1 Add `[Networked] Health` (100 max) to `NetworkPlayer`; port the useful parts of `TopDownController` health handling
- [ ] 2.2 Damage on hit: in `DetectCollision`, compute damage = item base damage × impact speed factor, applied on the host only. *Needs 1.2, 2.1*
- [ ] 2.3 Knockdown rules per item class (small only at full charge, medium above speed threshold, big always with knockback). *Needs 2.2*
- [ ] 2.4 Credit the attacker: store last damager for kill credit. *Needs 2.2*
- [ ] 2.5 Get-up: auto after ~3 s, tap-to-speed-up, ~1 s invulnerability after getting up. *Needs 2.3*
- [ ] 2.6 Falling out of bounds: heavy damage (tunable, 30) + respawn at a random spawn point, never instant elimination. *Needs 0.4, 2.1*
- [ ] 2.7 Drop hand items when knocked down; keep pocket items. *Needs 2.3 and 3.x for pockets; hands-only version first*
- [ ] 2.8 Elimination at 0 HP: limp ragdoll, then despawn; mark the player eliminated; drop their items as loot. *Needs 2.1*
- [ ] 2.9 Round flow state machine on the host: Waiting → Countdown → Playing → Ended; last player alive wins. *Needs 2.8*
- [ ] 2.10 Spectate camera that cycles through remaining players. *Needs 2.8*
- [ ] 2.11 Test: 2–3 devices, fight to elimination, winner detected, round ends. *Needs 2.1–2.10*

---

## Phase 3 — Hands and pocket inventory

Goal: two hands plus two pocket slots, fully synced.

- [ ] 3.1 Replace `heldObjectId` with `[Networked]` slots: `leftHand`, `rightHand`, `pocket[2]` (all `NetworkId`, host-authoritative)
- [ ] 3.2 Rework `HandGrabHandler` to be per-hand (grab, hold, release for left and right independently). *Needs 3.1*
- [ ] 3.3 Grab rule: nearest item goes to a free hand; both hands full → free pocket; big items need both hands and never go in a pocket. *Needs 3.2, 1.1*
- [ ] 3.4 Throw from the selected hand with charge and arc preview (extend `ThrowAimUI` for two hands). *Needs 3.2*
- [ ] 3.5 Swap pocket → empty hand, and drop-without-throwing. *Needs 3.3*
- [ ] 3.6 Visual attachment of pocket items (hidden or small marker on the body) and held items on both hands. *Needs 3.3*
- [ ] 3.7 Test: all combinations (two small, one big, full pockets, knockdown drop). *Needs 3.1–3.6*

---

## Phase 4 — Mobile controls and HUD

Goal: playable with touch only.

- [ ] 4.1 Set up the Unity Input System with on-screen stick and buttons; feed `NetworkInputData`
- [ ] 4.2 Floating virtual joystick (left side) for movement. *Needs 4.1*
- [ ] 4.3 Right-side buttons: Jump (becomes Get Up when knocked down), Grab/Drop, Throw L, Throw R with hold-to-charge. *Needs 4.1, 3.4*
- [ ] 4.4 Pocket slot icons, tappable to swap. *Needs 3.5*
- [ ] 4.5 Aim: drag off the throw button to aim the arc; light auto-aim toward the nearest player in the throw direction. *Needs 4.3*
- [ ] 4.6 HUD: HP bar, players left, round timer, hand items with charge, get-up prompt. *Needs 2.1, 2.9*
- [ ] 4.7 Health bar above each character
- [ ] 4.8 Mobile camera framing for the touch layout (Cinemachine)
- [ ] 4.9 Test on at least one small and one large phone screen. *Needs 4.1–4.8*

---

## Phase 5 — Menu, lobby, matchmaking

Goal: launch the app, press Play, and end up in a match with other people.

- [ ] 5.1 Create `Main Menu` scene using the GUI Kit (Quick Match, Play with Friends, Customize placeholder, Settings placeholder)
- [ ] 5.2 Quick Match: join any open session with fewer than 10 players, or create one. *Needs 5.1*
- [ ] 5.3 Lobby state: player count "6/10", short countdown, auto-start when full or the timer ends. *Needs 5.2, 2.9*
- [ ] 5.4 Private room by code (create, share, join). *Needs 5.2*
- [ ] 5.5 Scene flow: Menu → Lobby → Office → Results → back to Menu, with Fusion scene loading handled. *Needs 5.3*
- [ ] 5.6 Results screen: placement, eliminations, damage dealt, favorite item. *Needs 2.4, 2.9*
- [ ] 5.7 Play-again path from results. *Needs 5.5, 5.6*
- [ ] 5.8 Handle disconnects and host loss gracefully (return to menu with a message). *Needs 5.5*

---

## Phase 6 — PlayFab accounts and Fusion authentication

Goal: every player is a verified PlayFab account. Needed before economy or stats.

- [ ] 6.1 Create the Office Clash PlayFab title; add PlayFab Unity SDK
- [ ] 6.2 Guest login with `LoginWithCustomID` (device ID). *Needs 6.1*
- [ ] 6.3 Lock client writes in the title API policy (currency, inventory, statistics). *Needs 6.1*
- [ ] 6.4 Set up Photon custom authentication with the PlayFab provider in the Photon dashboard
- [ ] 6.5 Call `GetPhotonAuthenticationToken` after login and pass the token into Fusion; PlayFab ID becomes the Photon `UserId`. *Needs 6.2, 6.4*
- [ ] 6.6 Host reads each player's PlayFab ID on join. *Needs 6.5*
- [ ] 6.7 Test: two accounts join a match with verified identities. *Needs 6.5, 5.2*

---

## Phase 7 — Fun and performance pass

Goal: 10 players on real phones at a stable frame rate. This is the biggest technical risk, so do the stress test as early as the earlier phases allow.

- [ ] 7.1 10-device (or 10-client) stress test; record frame rate, bandwidth, host CPU. *Needs Phases 1–5*
- [ ] 7.2 Profile and fix hot spots; set targets: 30 fps minimum on mid-range Android, 60 fps on recent iPhones. *Needs 7.1*
- [ ] 7.3 Ragdoll LOD: fewer synced joints or simpler colliders for far-away remote players. *Needs 7.2*
- [ ] 7.4 Mobile graphics: URP mobile settings, baked lighting, shadow and texture budgets
- [ ] 7.5 Sprint with stamina bar (port `HandleEnergy`). *Needs 4.3*
- [ ] 7.6 Dive/tackle (decide if it stays in). *Needs 2.2, 4.3*
- [ ] 7.7 Pickups: Coffee (+30 HP), Energy drink, Super Grip, Bubble Wrap, with spawn points and respawn timers. *Needs 2.1, 1.5*
- [ ] 7.8 Shrinking office: after 90 s, sections close and damage players inside until only the centre remains. *Needs 2.9, 2.2*
- [ ] 7.9 Feedback: hit sounds per item class, "oof" voice lines, camera shake, haptics, kill feed, final-elimination slow-mo, paper and confetti particles. *Needs 2.2, 2.4*
- [ ] 7.10 Balance pass: damage numbers, knockdown thresholds, shrink timing, item spawn rates through playtests; move tunables to PlayFab title data. *Needs 7.7, 7.8, 6.1*

---

## Phase 8 — Content

Goal: the game looks and feels like Office Clash instead of a prototype.

- [ ] 8.1 Office worker character models rigged to the same bone layout as the Knight (so `SyncPhysicsObject` still works)
- [ ] 8.2 10 distinct looks per match (shirt colour, tie, hair, glasses). *Needs 8.1*
- [ ] 8.3 Replace the Knight in the player prefab and re-tune ragdoll joints. *Needs 8.1*
- [ ] 8.4 Build the real open-plan office map: cubicles, lots of throwables, 10+ spawn points, item and pickup spawns, kill volume, shrink sections
- [ ] 8.5 Second map: Rooftop with edges to fall off. *Needs 8.4 as a template*
- [ ] 8.6 Optional small maps (server room, break room) for fewer players
- [ ] 8.7 Settings screen: sensitivity, graphics quality, sound. *Needs 5.1*
- [ ] 8.8 Sign in with Apple and Google Play Games; upgrade a guest account by linking. *Needs 6.2*
- [ ] 8.9 Write match results and stats to PlayFab through CloudScript (host calls it at match end); wins, eliminations, damage. *Needs 6.5, 5.6*
- [ ] 8.10 Leaderboards from PlayFab statistics. *Needs 8.9*

---

## Phase 9 — Cosmetics and economy

Goal: the monetization base. Purely visual items, no pay-to-win.

- [ ] 9.1 PlayFab Economy v2 catalog: outfits, ties, hats, glasses, mug skins, throw trails, victory emotes
- [ ] 9.2 Soft currency earned by playing; CloudScript grants match rewards. *Needs 8.9*
- [ ] 9.3 Equip screen and loadout saved to title player data. *Needs 9.1, 8.2*
- [ ] 9.4 Sync the equipped loadout into matches and apply it on every client. *Needs 9.3, 6.6*
- [ ] 9.5 Store UI to buy cosmetics with soft currency. *Needs 9.1, 9.2*
- [ ] 9.6 Unity IAP for premium currency or direct purchases, with receipt redemption through PlayFab (Apple/Google validation). *Needs 9.5*
- [ ] 9.7 Decide the ad network (Unity Ads, AdMob or LevelPlay), then rewarded ads: double match rewards, daily free spin, verified by CloudScript. Light interstitials between matches only. *Needs 9.2*

---

## Phase 10 — Dedicated server and launch prep

Goal: move off client-host and ship to both stores.

- [ ] 10.1 Confirm gameplay code uses `HasStateAuthority` rather than "is a player", so the server switch is mostly config
- [ ] 10.2 Headless Linux Fusion dedicated server build (`GameMode.Server`). *Needs Phases 1–5*
- [ ] 10.3 Host the server on PlayFab Multiplayer Servers; allocate and connect clients; move the match-end CloudScript call to the server. *Needs 10.2, 8.9*
- [ ] 10.4 Reconnect and app-backgrounding handling (rejoin or forfeit). *Needs 5.8*
- [ ] 10.5 iOS build; TestFlight and Play Console closed testing
- [ ] 10.6 Store requirements: Apple Developer ($99/yr) and Google Play ($25) accounts, icons, screenshots, privacy policy, age rating (cartoon violence)
- [ ] 10.7 Compliance: Apple App Tracking Transparency prompt, Google Play Data Safety form, EU ad consent, in-app account deletion. *Needs 9.7, 8.8*
- [ ] 10.8 Analytics and crash reporting
- [ ] 10.9 Soft launch in one region, then global release. *Needs everything above*
